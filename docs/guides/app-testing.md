# App Testing

TigerCli apps can be tested at the same boundary where users run them: the command-line application boundary. You test the app *as an app* — not just individual command handlers in isolation.

Use `ItTiger.TigerCli.Testing.TigerCliAppTestHost` to run a real `TigerCliApp` in a test without touching the real console. The host supplies command-line arguments, queues prompt answers, captures stdout and stderr separately, and returns the app exit code.

This is especially useful for TigerCli's main use case: script-safe CLI apps that become semi-interactive when a human needs help, and stay strict when a script or agent runs them.

## Overview

A `TigerCliAppTestHost` run drives the *whole* app pipeline: parse, bind, validate, apply prompt policy, resolve providers, dispatch the command (or open the command menu), render output, and map the result to an exit code. Because it is the real pipeline, one host can cover:

- Command dispatch and command-line arguments/options
- Generated help (`--help`) and error help (`--help-errors`)
- Localized help and localized errors (via `--culture`)
- stdout for command output, stderr for framework and app errors — kept separate
- Parser-driven prompts (text, select, confirm, multi-select)
- Provider-backed select prompts
- Command-menu navigation, when the app enables a menu
- `--non-interactive` behavior (strict, automation-safe)
- Prompt-timeout cancellation and its exit-code mapping
- Typed exit-code policies
- Structured output written through `TigerConsole`

The real terminal is not used for input. Prompt answers are queued through the host, and the result exposes `ExitCode`, `StdOut`, and `StdErr`.

## Recommended App Shape

Put TigerCli app construction behind a factory method, then use that *same* factory from `Program.cs` and from tests.

```csharp
using ItTiger.TigerCli.Commands;

public static class MyApp
{
    public static TigerCliApp Create()
    {
        return TigerCliApp.CreateBuilder()
            .UseAssemblyMetadata(typeof(MyApp).Assembly)
            .SetDefaultCommand<RunCommand>()
            .AddCommandGroup("prompt", group => group
                .AddCommand<PromptCommand>("smoke", "Runs the prompt smoke test."))
            .Build();
    }
}
```

`Program.cs` stays small:

```csharp
return await MyApp.Create().RunAsync(args);
```

Tests call `MyApp.Create()` too:

```csharp
var result = await TigerCliAppTestHost.For(MyApp.Create()).WithArgs("--help").RunAsync();
```

Using one factory for production and tests is the key move. It keeps command registration and metadata, prompt providers, localization/resources, prompt policy, command-menu policy, and exit-code mapping identical between the app your users run and the app your tests exercise. There is no separate "test app" to drift out of sync.

## Basic Test

```csharp
using ItTiger.TigerCli.Testing;

public sealed class MyAppTests
{
    [Fact]
    public async Task DefaultCommand_Polish_WritesLocalizedGreeting()
    {
        var result = await TigerCliAppTestHost
            .For(MyApp.Create())
            .WithArgs("--culture", "pl-PL")
            .RunAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Witaj", result.StdOut);
        Assert.Empty(result.StdErr);
    }
}
```

The test runs the real app pipeline: parse, bind, validate, prompt policy, command dispatch, output, and exit-code mapping.

## Capturing Stdout and Stderr

`TigerCliAppRunResult` is the record returned by `RunAsync()`:

```csharp
public sealed record TigerCliAppRunResult(
    int ExitCode,
    string StdOut,
    string StdErr);
```

TigerCli keeps normal output and errors on separate streams:

- `TigerConsole.MarkupLine(...)` writes to `StdOut`.
- `TigerConsole.MarkupErrorLine(...)` writes to `StdErr`.
- Framework parse, validation, interaction, and unhandled-exception errors write to `StdErr`.
- Help and `--help-errors` write to `StdOut`.

The host pins a deterministic no-color mode, so captured output never depends on the host terminal's ANSI capability. Assert on the visible text.

```csharp
Assert.Equal(0, result.ExitCode);
Assert.Contains("Usage:", result.StdOut);
Assert.Empty(result.StdErr);
```

For an error case, the same run gives you the exit code *and* the stderr text:

```csharp
Assert.NotEqual(0, result.ExitCode);
Assert.Empty(result.StdOut);
Assert.Contains("Missing required option", result.StdErr);
```

Prefer durable assertions such as `Assert.Contains(...)` for help and formatted output. Full exact snapshots are usually too brittle for application tests unless the formatting itself is the behavior under test. For structured tables/lists/details, assert on the values that carry meaning rather than pinning the whole frame — see [structured output](structured-output.md).

## Testing Help and Localized Help

Pass `--help` just as a user would. If the app supports `--culture`, include it in `WithArgs(...)`.

```csharp
var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("--help", "--culture", "pl-PL")
    .RunAsync();

Assert.Equal(0, result.ExitCode);
Assert.Contains("Użycie:", result.StdOut);   // framework-owned text
Assert.Contains("Opcje:", result.StdOut);    // framework-owned text
Assert.Contains("Aplikacja testowa", result.StdOut); // app-owned metadata
Assert.Empty(result.StdErr);
```

This verifies both framework-owned text and app-owned metadata together when your app registers resources with `UseAppResources(...)`. `--help-errors` works the same way and lists your typed exit codes with their localized descriptions.

## Testing Prompt Flows

Queue prompt answers in the same order TigerCli asks the prompts.

```csharp
var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("prompt", "smoke")
    .WithTextInput("Riley")
    .WithSelectIndex(1)
    .WithConfirm(true)
    .WithMultiSelectIndexes(0, 2)
    .RunAsync();

Assert.Equal(0, result.ExitCode);
Assert.Contains("Riley", result.StdOut);
Assert.Empty(result.StdErr);
```

Prompt answer methods map to the parser-driven prompt types:

- `WithTextInput("value")` — enters text and presses Enter. Answers a `string` prompt (`TigerTui.InputAsync`).
- `WithSelectIndex(1)` — moves to item index `1` and presses Enter. Answers an enum or provider-backed select.
- `WithConfirm(true)` — accepts the default *Yes*; `WithConfirm(false)` chooses *No*. Answers a `bool?` prompt (`TigerTui.ConfirmAsync`).
- `WithMultiSelectIndexes(0, 2)` — toggles those indexes and presses Enter. Answers a `[Flags]` enum or provider-backed multi-select. Indexes are normalized (sorted, de-duplicated), so `WithMultiSelectIndexes(2, 0, 2)` and `WithMultiSelectIndexes(0, 2)` behave identically.

Answers are consumed positionally, in the order the prompts appear. Queue them to match the prompt order your command produces.

## Testing Provider-Backed Prompts

Provider-backed prompts render as select (or multi-select) prompts, so answer them by index just like an enum prompt. The selected *value* is the provider key, which stays language-neutral even when the displayed label is localized.

```csharp
var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("provider", "smoke", "--culture", "pl-PL")
    .WithSelectIndex(1)   // choose the second connection
    .WithSelectIndex(0)   // choose the first project
    .RunAsync();

Assert.Equal(0, result.ExitCode);
Assert.Contains("połączenie=demo", result.StdOut);   // key, not the localized label
Assert.Contains("projekt=sandbox", result.StdOut);
Assert.Empty(result.StdErr);
```

Assert on the stable, language-neutral keys (here `demo` and `sandbox`), not the localized labels. This keeps provider tests robust across cultures. Dependent providers work naturally: the second provider callback sees the first selection, so a later `WithSelectIndex(...)` chooses from choices that depend on the earlier answer.

Provider validation still runs on values supplied non-interactively. If a provider-backed option, an argument with an explicit `Provider`, or a multi-select value receives a command-line value that is not a valid key or label, validation fails through the normal pipeline — the host captures that failure on `StdErr` with the mapped `ValidationError` exit code, and the handler does not run. Members that opt out with `ValidateAgainstProvider = false` (or `AllowCustomValues` for multi-select) pass the supplied value through instead.

## Testing the Command Menu

When an app opts into a command menu with `UseCommandMenu(...)`, the host drives menu navigation with the same `WithSelectIndex(...)` answers used for prompts. The menu only *selects* a command; the chosen command then runs through the normal parse/bind/prompt/execute pipeline.

```csharp
var app = TigerCliApp.CreateBuilder()
    .SetApplicationName("menu-test")
    .UseCommandMenu(CommandMenuMode.Enabled)
    .AddCommand<AlphaCommand>("alpha")
    .AddCommand<BravoCommand>("bravo")
    .Build();

var result = await TigerCliAppTestHost
    .For(app)
    .WithSelectIndex(1)   // pick the second listed command
    .RunAsync();

Assert.Equal(0, result.ExitCode);
Assert.Contains("ran=bravo", result.StdOut);
```

Nested groups are navigable the same way — one `WithSelectIndex(...)` per level:

```csharp
var result = await TigerCliAppTestHost
    .For(app)
    .WithSelectIndex(0)   // enter the group
    .WithSelectIndex(0)   // pick the command inside it
    .RunAsync();
```

A selection can flow straight into prompts: queue the menu selection first, then the answers the selected command needs.

```csharp
var result = await TigerCliAppTestHost
    .For(app)
    .WithSelectIndex(0)          // menu picks "greet"
    .WithTextInput("riley")      // greet then prompts for --name
    .RunAsync();

Assert.Contains("hello=riley", result.StdOut);
```

Menu eligibility (which commands are listed) is best asserted behaviorally: run a selection index and assert *which* command ran. In non-interactive mode the menu is interaction and does not open — see below.

## Testing Non-Interactive Behavior

`--non-interactive` is framework-owned. Non-interactive disables *interaction*, not *execution*: supplied values are still parsed, bound, validated, and provider-validated, but the framework never prompts, never opens a menu, and never waits for a key.

Queued prompt answers do not make missing input valid — a missing promptable value fails instead of being silently answered:

```csharp
var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("prompt", "smoke", "--non-interactive")
    .WithTextInput("unused")
    .WithSelectIndex(2)
    .RunAsync();

Assert.NotEqual(0, result.ExitCode);
Assert.Contains("Missing required", result.StdErr);
Assert.DoesNotContain("unused", result.StdOut);
```

The command menu is also interaction, so a non-interactive run of a menu app fails cleanly rather than opening a picker. Map the interaction-not-allowed kind to assert the exact code:

```csharp
var app = TigerCliApp.CreateBuilder()
    .SetApplicationName("menu-test")
    .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.InteractiveNotAllowed, 46)
    .UseCommandMenu(CommandMenuMode.Enabled)
    .AddCommand<AlphaCommand>("alpha")
    .Build();

var result = await TigerCliAppTestHost
    .For(app)
    .WithArgs("--non-interactive")
    .RunAsync();

Assert.Equal(46, result.ExitCode);
Assert.Contains("interactive", result.StdErr);
```

Use these patterns to prove automation-safe behavior: missing governed input fails, and interactive surfaces are refused, instead of a script or CI job hanging on a prompt.

## Testing Cancellation

TigerCli models cancellation as a normal control-flow outcome that maps to `TigerCliExitKind.Cancelled`. The host can drive the prompt-timeout path directly: `WithPromptTimeout(...)` bounds how long a prompt flow waits before it cancels.

```csharp
var app = App(builder => builder
    .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 46));

var result = await TigerCliAppTestHost
    .For(app)
    .WithPromptTimeout(TimeSpan.FromMilliseconds(20))
    .RunAsync();

Assert.Equal(46, result.ExitCode);
Assert.Contains("Cancelled.", result.StdErr);
```

This lets you prove that a prompt flow fails fast — and with your mapped exit code — instead of waiting indefinitely when no answer is queued.

Escape-driven cancellation (a user pressing `Esc` to back out of a menu or prompt) is a keystroke rather than a host answer method, so it is exercised at the lower `TestShell` level by enqueuing `ConsoleKey.Escape`. Reach for `TestShell` directly when a test needs that keystroke-level behavior; keep app-boundary tests focused on the outcomes the host exposes (exit code, stdout, stderr).

## Testing Typed Exit Codes

Exit-code policy is part of the app, so it is exercised by the host like everything else. Configure typed codes on the builder and assert the mapped value on the result. Because the factory is shared, the codes you assert in tests are exactly the codes users and scripts observe.

```csharp
var app = TigerCliApp.CreateBuilder()
    .SetApplicationName("host-test")
    .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
    .SetDefaultCommand<RequiredNameCommand>()
    .Build();

var result = await TigerCliAppTestHost
    .For(app)
    .WithArgs("--non-interactive")   // required --name missing => validation error
    .RunAsync();

Assert.Equal(45, result.ExitCode);
Assert.Contains("Missing required option: --name", result.StdErr);
```

A command that returns a raw integer, throws, or reports a typed failure all flow through the same mapping. See [exit codes](exit-codes.md) for the full model, and `--help-errors` to verify the codes and their (localizable) descriptions are rendered.

## Testing Activities and Headless Behavior

An activity (`TigerTui.RunActivityAsync`) is *work-with-presentation*: in interactive modes it renders a dialog/spinner, and in non-interactive mode the same work runs **headlessly** — no dialog, no keyboard wait — while still returning a normal `Completed`/`Failed` result.

At the app-test boundary, the host verifies the *observable* side of that contract: run an activity-backed command under `--non-interactive` and assert the command completed, produced its stdout, and mapped to the expected exit code. The optional one-line `ActivityDialogSpec.NonInteractiveMessage` prints once to stdout in non-interactive mode, so it is assertable too.

```csharp
var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("import", "--non-interactive")
    .RunAsync();

Assert.Equal(0, result.ExitCode);
Assert.Contains("Imported", result.StdOut);   // the activity body still ran
```

This is the payoff of writing one activity-based execution path: the same code runs under test whether it would render or run headlessly. The activity's *rendering* — spinner frames, stop buttons, progress rows, dialog layout — is presentation, and is covered more precisely by lower-level TUI tests using `TestShell`/`TestTerminal`. Keep app-boundary activity tests focused on the outcome (result, output, exit code); see [activity and progress design](../design/activity-progress.md) and [interaction modes](interaction-modes.md#activities-progress-and-spinners) for the full model.

For a public app-boundary example, see [Folder Copy](../examples/folder-copy.md): its tests run the real copy operation under `--non-interactive`, assert required-option failures, and keep the TigerCli-free planner covered directly with temporary folders.

## Culture, Viewport, and Prompt Timeout

The host exposes the environment knobs that affect a real run:

- `WithArgs("--culture", "pl-PL")` selects a supported culture, so help, errors, and localized labels resolve for that culture. The host reads `--culture` from the args and configures the run's culture to match.
- `WithViewport(width, height)` sets the terminal dimensions the prompt/menu layout measures against. Most app tests do not need this; set it when rendering depends on width (for example, verifying a wide menu still lists an item).
- `WithPromptTimeout(TimeSpan)` bounds how long a prompt flow waits before cancelling (see [Testing Cancellation](#testing-cancellation)).

```csharp
var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("provider", "smoke")
    .WithViewport(100, 30)
    .WithSelectIndex(0)
    .WithSelectIndex(1)
    .RunAsync();
```

## Testing Guidance

The host is intentionally small and focused on the app boundary. A few practices keep these tests reliable and durable:

- **Use one host per run.** `TigerCliAppTestHost` is single-use; create a fresh host (from the shared factory) for each run. Reusing one throws.
- **Keep app-boundary tests focused.** Assert on what the boundary exposes — `ExitCode`, `StdOut`, `StdErr`. Drop to `TestShell`/`TestTerminal` for keystroke-level behavior (Escape, per-key navigation) and for detailed control/render assertions.
- **Prefer durable assertions over brittle full snapshots.** Use `Assert.Contains(...)` on the values that carry meaning. Reserve exact snapshots for tests where the formatting itself is the behavior under test.
- **Queue prompt answers in expected order.** Answers are consumed positionally as prompts appear. For provider-backed prompts, assert on the language-neutral keys, not localized labels.
- **Use command/menu tests where behavior matters.** Assert menu behavior by which command a selection index runs, rather than pinning menu layout.
- **Isolate console output if you parallelize.** The host redirects the process-global `Console.Out`/`Console.Error` for the duration of a run and restores them afterward. If your suite runs host tests concurrently in the same process, put them in a non-parallel xUnit collection (or disable parallelization for that assembly) so captured output does not interleave.

## Full Example

This compact example follows the same factory-and-host pattern used by the public [ROI Cities](../../RoiCities.Tests/) and [Folder Copy](../../FolderCopy.Tests/) samples. The broader dogfooding version lives in [CommandParserTestAppTests.cs](../../CommandParserTest.Tests/CommandParserTestAppTests.cs), with its app factory in [CommandParserTestApp.cs](../../CommandParserTest/CommandParserTestApp.cs).

```csharp
using ItTiger.TigerCli.Testing;

public sealed class MyAppTests
{
    [Fact]
    public async Task Help_Polish_WritesLocalizedFrameworkAndAppText()
    {
        var result = await RunAsync("--help", "--culture", "pl-PL");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Użycie:", result.StdOut);
        Assert.Contains("Opcje:", result.StdOut);
        Assert.Contains("Aplikacja testowa", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task PromptSmoke_AnswersMissingValues()
    {
        var result = await TigerCliAppTestHost
            .For(MyApp.Create())
            .WithArgs("prompt", "smoke")
            .WithTextInput("Riley")
            .WithSelectIndex(1)
            .WithMultiSelectIndexes(0, 2)
            .RunAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("name=Riley", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task ProviderSmoke_SelectsLanguageNeutralKeys()
    {
        var result = await TigerCliAppTestHost
            .For(MyApp.Create())
            .WithArgs("provider", "smoke", "--culture", "pl-PL")
            .WithSelectIndex(1)
            .WithSelectIndex(0)
            .RunAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("połączenie=demo", result.StdOut);
        Assert.Contains("projekt=sandbox", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task PromptSmoke_NonInteractive_FailsWithoutPrompting()
    {
        var result = await TigerCliAppTestHost
            .For(MyApp.Create())
            .WithArgs("prompt", "smoke", "--non-interactive")
            .WithTextInput("unused")
            .RunAsync();

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Missing required", result.StdErr);
        Assert.DoesNotContain("unused", result.StdOut);
    }

    private static Task<TigerCliAppRunResult> RunAsync(params string[] args)
    {
        return TigerCliAppTestHost
            .For(MyApp.Create())
            .WithArgs(args)
            .RunAsync();
    }
}
```

## Capturing Styled Output As HTML

By default the host captures plain text (it pins `--no-color`). For documentation artifacts or
styled-output assertions, opt in to HTML capture:

```csharp
var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("--help")
    .WithHtmlCapture()
    .RunAsync();

// result.StdOutHtml / result.StdErrHtml — deterministic HtmlSink fragments, no ANSI.
```

Capture semantics:

- `StdOutHtml` / `StdErrHtml` are `null` unless `WithHtmlCapture` was called; plain runs are unchanged.
- With capture on, TigerCli-rendered output goes to the HTML capture **instead of** `StdOut`/`StdErr`.
  Use separate runs when a test needs plain-text assertions.
- Unstyled text is captured without machine-dependent console colours; line endings are `\n`.
- `HtmlSinkOptions` controls hyperlink mode, layout width (`SoftMaxWidth`), and wrapping
  (`WrapInPre`, default `true`). See [HtmlSink](../reference/html-sink.md).
- Interactive prompt UI renders through the shell, not the output streams, so it does not appear in
  captured HTML.

## Related Docs

- Understand modes with [interaction modes](interaction-modes.md).
- Build prompt flows with [semi-interactive prompts](semi-interactive-prompts.md).
- Configure and assert exit codes with [exit codes](exit-codes.md).
- Assert structured output with [structured output](structured-output.md).
- Build command handlers with [command apps](command-apps.md).
- See public app-boundary tests in [`RoiCities.Tests`](../../RoiCities.Tests/) and [`FolderCopy.Tests`](../../FolderCopy.Tests/).
- Use [`CommandParserTest.Tests`](../../CommandParserTest.Tests/CommandParserTestAppTests.cs) for broad dogfooding coverage of the [`CommandParserTest`](../../CommandParserTest/) sample.
