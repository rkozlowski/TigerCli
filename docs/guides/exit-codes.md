# Exit Codes

TigerCli treats process exit codes as part of the application contract. A command-line tool should make failure categories stable enough for scripts, CI jobs, and other callers to depend on them.

## Overview

The preferred TigerCli model is:

- command handlers return typed enum values instead of magic integers
- framework failures map through an app-owned exit-code policy
- documented enum values produce generated `--help-errors` output
- the final process exit code is still the enum value's underlying integer

Raw integer handlers are still supported for simple tools and migration paths, but enum-backed exit codes are the preferred Tiger style.

## Basic Enum-Backed Exit Codes

Define an enum that represents the app contract:

```csharp
public enum MyExitCode
{
    Ok = 0,
    InvalidArguments = 20,
    UnhandledException = 40
}
```

Return that enum from a typed [TigerCliAsyncCommandHandler&lt;TSettings, TExitCode&gt;](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliAsyncCommandHandler-2.html):

```csharp
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

public sealed class FailSettings : TigerCliSettings
{
}

public sealed class FailCommand
    : TigerCliAsyncCommandHandler<FailSettings, MyExitCode>
{
    public override Task<MyExitCode> ExecuteAsync(FailSettings settings)
    {
        TigerConsole.MarkupErrorLine("Intentional typed failure.");
        return Task.FromResult(MyExitCode.InvalidArguments);
    }
}
```

TigerCli converts the enum value to its underlying integer at the process boundary.

## Configuring Exit-Code Policy

Command return values cover command-owned outcomes. TigerCli also needs to map framework-owned outcomes such as parse failures, validation failures, help, and unhandled exceptions.

TigerCli models those framework outcomes as a **layered exit model** so the common path stays terse and precision is available when you need it:

```text
TigerCliExitOutcome        (Success, Error)
  TigerCliExitCategory     (Success, Usage, Validation, Execution, Unexpected, Cancelled)
    TigerCliExitKind       (the specific reason: InvalidArguments, ValidationError, …)
```

The model's [TigerCliExitOutcome](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliExitOutcome.html), [TigerCliExitCategory](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliExitCategory.html), and [TigerCliExitKind](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliExitKind.html) enums distinguish the outcome, broad category, and specific framework reason.

The outcome/category/kind roll-up is fixed by the framework. You configure the exit codes those layers produce.

### Start with the outcome baseline

Every policy begins with a mandatory baseline: one code for **all success** outcomes and one for **all error** outcomes. This is the whole configuration most apps need:

```csharp
var app = TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .UseExitCodes<MyExitCode>(MyExitCode.Ok, MyExitCode.Error)
    .Build();
```

That means:

```text
all Success outcomes -> MyExitCode.Ok
all Error outcomes   -> MyExitCode.Error
```

`UseExitCodes(...)` returns the app builder, so the exit-code configuration stays part of the normal builder chain.

### Add precision only where needed

Refine the baseline with three optional, layered overrides, all as ordinary builder methods. Each beats the layer below it — **kind → range → category → outcome baseline** — regardless of the order you configure them:

```csharp
.UseExitCodes<MyExitCode>(MyExitCode.Ok, MyExitCode.Error)
    // one code for a whole family of failures
    .ExitCategory(TigerCliExitCategory.Usage, MyExitCode.UsageError)
    // a compact band of kinds -> consecutive codes
    .ExitRange(
        TigerCliExitKind.InvalidArguments,
        TigerCliExitKind.NoCommand,
        MyExitCode.InvalidArguments)
    // one precise kind
    .ExitKind(TigerCliExitKind.NoCommand, MyExitCode.NoCommand)
    .SetDefaultCommand<GreetCommand>()
    .Build();
```

`ExitCategory(category, code)` maps every kind in that category. `ExitKind(kind, code)` maps one specific kind. `ExitRange(start, end, firstCode)` maps the inclusive band of kinds whose declared value is in `[start, end]` to consecutive codes:

```text
Range(InvalidArguments, NoCommand, MyExitCode.InvalidArguments)

InvalidArguments        -> MyExitCode.InvalidArguments
MissingRequiredArgument -> MyExitCode.InvalidArguments + 1
ValidationError         -> MyExitCode.InvalidArguments + 2
InteractiveNotAllowed   -> MyExitCode.InvalidArguments + 3
NoCommand               -> MyExitCode.InvalidArguments + 4
```

A range is bounded strictly by its explicit `start` and `end`, so kinds added to `TigerCliExitKind` later can never silently enter an existing range. Range offsets are arithmetic on the underlying enum value and need not be defined members. An inverted range (`start` after `end`) is rejected.

The `TigerCliExitKind` values are contract — `Range(...)` depends on their declared order, which is locked by tests. Use the generated DocFX API reference for the enum value table; [api-map.md](../reference/api-map.md) links the type to its generated page.

There is a distinct `MissingRequiredArgument` kind for missing positional arguments (category `Usage`). Missing required options are framework validation failures and map through `ValidationError` (category `Validation`).

### Raw integer policy

The same layered API is available for raw integers:

```csharp
var app = TigerCliApp.CreateBuilder()
    .UseExitCodes(0, 1)
    .ExitCategory(TigerCliExitCategory.Usage, 2)
    .ExitKind(TigerCliExitKind.NoCommand, 3)
    .Build();
```

If you never call `UseExitCodes(...)`, the framework default baseline is `Success = 0`, `Error = -1`.

## Framework Exit Kinds vs App Exit Codes

TigerCli separates framework reasons from application codes.

| Layer | Example | Meaning |
|---|---|---|
| `TigerCliExitKind` | `ValidationError` | TigerCli's category for what happened |
| App enum member | `MyExitCode.InvalidArguments` | The app contract value you expose |
| Process exit code | `20` | The integer returned to the OS |

Runtime flow:

```text
framework result -> TigerCliExitKind -> configured policy -> app enum value -> int
```

This keeps TigerCli internals from leaking into your public contract while still giving the framework a stable way to map its own failures.

## Prompt Cancellation

When a user dismisses an interactive prompt — pressing `Escape`, letting a prompt time out, or triggering token/system cancellation (Ctrl-C) — *after* a command has been selected, that is a **normal flow**, not a validation or usage error. TigerCli models it explicitly:

- Kind: `TigerCliExitKind.Cancelled`
- Category: `TigerCliExitCategory.Cancelled`
- Outcome: `Error` (by default)

The console prints a gentle, muted one-line notice (localized `Cancelled.`) to stderr — no `Error:` prefix and no error styling. Cancellation is never classified as `Usage` or `Validation`.

Cancelling the **command menu itself** before any command is selected stays quiet: it exits through the success baseline and prints nothing.

By default, with only the outcome baseline configured, `Cancelled` maps to the **error** baseline code. Apps that care can override it at the category or kind layer:

```csharp
.UseExitCodes<MyExitCode>(MyExitCode.Ok, MyExitCode.Error)
    // give cancellation its own dedicated code…
    .ExitCategory(TigerCliExitCategory.Cancelled, MyExitCode.Cancelled)
    // …or make Escape neutral by mapping it onto success
    .ExitCategory(TigerCliExitCategory.Cancelled, MyExitCode.Ok)
```

A missing required value in non-interactive mode, or a value that fails validation, is **not** cancellation — those still report through `MissingRequiredArgument`/`ValidationError` with a real error message.

## Command-Specific vs App-Wide Codes

Most apps should use one application-wide exit-code enum and one policy:

```csharp
.UseExitCodes<MyExitCode>(MyExitCode.Ok, MyExitCode.UnhandledException)
```

Commands can also return a command-specific enum:

```csharp
public sealed class BuildCommand
    : TigerCliAsyncCommandHandler<BuildSettings, BuildExitCode>
{
    public override Task<BuildExitCode> ExecuteAsync(BuildSettings settings)
    {
        return Task.FromResult(BuildExitCode.ProjectFailed);
    }
}
```

For command-level `--help-errors`, a named command's handler enum takes precedence over the app-wide enum. If the command uses raw `int` or has no specific enum, TigerCli falls back to the app-wide enum configured through `UseExitCodes<TExitCode>(...)`.

TigerCli does not merge command-specific and global enum docs. If a command-specific enum exists, it owns that command's exit-code help.

## Generated --help-errors

When TigerCli has a documented enum source, normal help includes a short hint:

```text
For a list of exit codes, use --help-errors.
```

`--help-errors` prints generated exit-code help:

```text
Exit codes:
  My tool exit codes

  0   Ok
      Operation completed successfully.

  20  InvalidArguments
      Invalid command-line arguments.

  40  UnhandledException
      Unhandled exception was caught by TigerCli.
```

`--help --help-errors` prints normal help first, then the exit-code help, and returns the configured `HelpShown` code.

If no documented enum is configured, `--help-errors` prints a clear framework message instead of inventing documentation.

## Localized Exit-Code Text

Use `TigerTextAttribute` from `ItTiger.Core` for app-owned exit-code labels and descriptions:

```csharp
using ItTiger.Core;

[TigerText("My tool exit codes")]
public enum MyExitCode
{
    [TigerText("OK",
        Description = "Operation completed successfully.")]
    Ok = 0,

    [TigerText("Invalid arguments",
        Description = "Invalid command-line arguments.")]
    InvalidArguments = 20
}
```

In the simple form, `Text` and `Description` are source-text resource keys and fallbacks. If you register app resources with `UseAppResources(...)`, TigerCli resolves those strings for the active culture.

Explicit keys are available when you need stable identifier-style resource keys:

```csharp
[TigerText(
    "Invalid arguments",
    ResourceKey = "Exit_InvalidArguments_Label",
    Description = "Invalid command-line arguments.",
    DescriptionResourceKey = "Exit_InvalidArguments_Description")]
InvalidArguments = 20
```

`DescriptionAttribute` remains supported as literal fallback documentation. It is not a localization mechanism. `DisplayAttribute` is also supported as a standard .NET fallback.

See [localization](localization.md) for the full enum text model, source-text localization, explicit keys, and culture configuration.

## Raw Integer Handlers

Raw integer handlers are supported:

```csharp
public sealed class RawCommand
    : TigerCliAsyncCommandHandler<RawSettings>
{
    public override Task<int> ExecuteAsync(RawSettings settings)
    {
        return Task.FromResult(settings.Code);
    }
}
```

Use this shape for very small tools, migration scenarios, or an intentional escape hatch. It is not the preferred model for larger apps because the meaning of the returned number is not visible in the type.

A raw command can still run inside an app with a global enum-backed policy, but the raw handler itself does not provide command-specific enum documentation for `--help-errors`.

## Error Output And Exceptions

Framework errors write to stderr through `TigerConsole.MarkupErrorLine(...)` with the localized framework error prefix.

Unhandled command exceptions map through the `UnhandledException` kind (category `Unexpected`):

```csharp
.UseExitCodes<MyExitCode>(MyExitCode.Ok, MyExitCode.Error)
    .ExitKind(TigerCliExitKind.UnhandledException, MyExitCode.UnhandledException)
```

TigerCli unwraps reflection and single-inner aggregate wrapper exceptions so stderr shows the meaningful command exception message rather than a generic invocation wrapper. Dynamic exception text is escaped before being written as markup.

## Testing Exit Codes

Use `TigerCliAppTestHost` to test real app behavior:

```csharp
using ItTiger.TigerCli.Testing;

var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("fail")
    .RunAsync();

Assert.Equal((int)MyExitCode.InvalidArguments, result.ExitCode);
Assert.Contains("Intentional typed failure.", result.StdErr);
```

Framework failures can be tested the same way:

```csharp
var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("--unknown")
    .RunAsync();

Assert.Equal((int)MyExitCode.InvalidArguments, result.ExitCode);
Assert.Contains("Error:", result.StdErr);
```

See [app testing](app-testing.md) for stdout/stderr capture, prompt answers, cultures, and viewport configuration.

## Common Mistakes

- Do not return random magic integers from every command.
- Do not document exit codes manually when `--help-errors` can generate them from the enum.
- Do not treat `TigerCliExitKind` and app enum members as the same thing.
- Do not over-map: the outcome baseline already covers every success and error kind. Add `Category`/`Range`/`Kind` only where you need more precision.
- Do not use `DescriptionAttribute` as a localized text mechanism.
- Do not rely on raw integer handlers unless that escape hatch is intentional.
- Do not assume missing required options use `MissingRequiredArgument`; they currently map through the `Validation` category.
- Do not renumber or reorder `TigerCliExitKind`; `Range(...)` depends on the declared values.

## Related Docs

- Build typed command handlers with [command apps](command-apps.md).
- Localize enum labels and descriptions with [localization](localization.md).
- Test exit-code behavior with [app testing](app-testing.md).
- Review the public API in [api-map.md](../reference/api-map.md) and the generated DocFX API reference.
