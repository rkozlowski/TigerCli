# Using TigerCli with AI coding agents

This is the compact instruction set for AI coding agents generating or modifying TigerCli applications. It is not the full guide. Prefer TigerCli framework features over local hacks, and inspect the guides/API before inventing a pattern.

Full human documentation lives in [docs/guides/](guides/).

## What TigerCli Is

TigerCli is an opinionated .NET CLI framework for script-safe command apps that can become semi-interactive when a human needs help.

Use it for command-oriented tools, especially CRUD-style apps. Do not treat TigerCli as a neutral parser, a full-screen TUI framework, or a generic rich-console widget library.

For public examples, start with [Getting Started / ROI Cities](getting-started.md) for the command-app shape and [Folder Copy](examples/folder-copy.md) for real operations with folder prompts, activities, progress, cancellation, and non-interactive execution. Use `CommandParserTest` only when you need broad dogfooding coverage such as dependent providers or localization.

## Core Rules

- Use async command handler classes for normal TigerCli applications.
- Keep `Program.cs` thin and build the [TigerCliApp](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliApp.html) through a reusable app factory.
- Derive settings classes from [TigerCliSettings](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliSettings.html).
- Declare inputs with [TigerCliArgumentAttribute](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliArgumentAttribute.html) and [TigerCliOptionAttribute](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliOptionAttribute.html).
- Use generated help, version, `--non-interactive`, and `--culture` behavior. Do not reimplement them.
- Use enum-backed exit codes when possible.
- Use `TigerCliAppTestHost` for app-level tests.
- Search current docs/source before writing examples for less common APIs.

```csharp
public static class MyApp
{
    public static TigerCliApp Create()
    {
        return TigerCliApp.CreateBuilder()
            // App identity/description come from the project file (<AssemblyName>, <Description>);
            // UseAssemblyMetadata imports them. Prefer this to SetApplicationName(...) boilerplate.
            .UseAssemblyMetadata(typeof(MyApp).Assembly)
            .AddCommandGroup("widgets", group => group
                .AddCommand<ListWidgetsCommand>("list", "List widgets.")
                .AddCommand<ShowWidgetCommand>("show", "Show a widget."))
            .Build();
    }
}
```

`Program.cs`:

```csharp
return await MyApp.Create().RunAsync(args);
```

## Command Registration

- Command names are single tokens.
- Multi-token command paths must be represented with `AddCommandGroup(...)`.
- Do not register flattened paths such as `AddCommand("connections edit", ...)`.
- Use generated help instead of custom help commands.
- Use `UseCommandMenu(...)` only when the app needs an opt-in command picker; the selected command still runs through parse, bind, prompt, validate, execute.
- Delegate commands (`AddDefaultCommand(...)` and non-generic `AddCommand(...)`) are only for tiny
  utilities, demos, smoke tests, and simple automation. Do not use them when typed settings,
  arguments/options, prompts, providers, validation, selectors, command groups, localization, or
  reusable command behavior are needed.
- A minimal app may combine delegate commands with `UseTigerCliExitKindExitCodes()` to expose the
  declared `TigerCliExitKind` values directly. Reusable command libraries may return
  `TigerCliExitKind` and let the consuming app map it with `ExitKind(...)`.
- Return routine `NotFound`, `AlreadyExists`, `Conflict`, and `NotSupported` outcomes normally. Do
  not use exceptions for normal command flow; reserve `TigerCliCommandException` for exceptional
  failures or adaptation boundaries.

```csharp
builder.AddCommandGroup("connections", group => group
    .AddCommand<ListConnectionsCommand>("list")
    .AddCommand<ShowConnectionCommand>("show")
    .AddCommand<AddConnectionCommand>("add")
    .AddCommand<EditConnectionCommand>("edit")
    .AddCommand<DeleteConnectionCommand>("delete"));
```

## Reusable App Contributions

- Use `ITigerCliAppContribution` when a reusable TigerCli-based library needs one app-wide CLI
  configuration option shared across its commands.
- The host must opt in with `TigerCliAppBuilder.AddContribution(...)`; do not register library
  contributions implicitly.
- In 0.9.1, use only `builder.GlobalOptions.AddOptionalString(...)`: one long name, no alias,
  optional string value, CLI-only, and never promptable or provider-backed.
- Place contributed global options after the command path and positional arguments, with other
  options. Their app-wide meaning does not change command selection or TigerCli's command-line shape.
- Apply the parsed `string?` to library-owned options or services in the callback. Return
  `TigerCliValidationResult.Error(...)` for library-owned validation failures.
- Localize a contributed global-option description with the optional `descriptionResourceKey`.
  TigerCli resolves it through the host's `UseAppResources(...)` manager at help-render time;
  option names and `valueName` placeholders remain literal.
- Do not use static value stores, inspect raw arguments in handlers, or model ordinary command
  inputs as global contributions.
- TigerCli currently has no shell-completion entry point or completion-provider adapter. Normal command
  providers run after contributed global-option callbacks and may observe contribution-owned state;
  help invokes neither callbacks, providers, nor handlers. Keep provider I/O cancellable and
  preferably idempotent, and do not assume this ordering for a future completion feature.

See [App contributions and global options](guides/app-contributions.md) for the ownership boundary,
lifecycle, constraints, and host composition example.

## CRUD Shape

TigerCli is strongly suited to CRUD-style command apps:

- `list`: render many records with [CliList&lt;T&gt;](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Rendering.CliList-1.html).
- `show`: render one record with [CliDetails](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Rendering.CliDetails.html).
- `add`: bind supplied values, then use parser-driven prompts/providers for missing values.
- `edit`: use `.AsEdit(...)` so TigerCli loads existing values, applies command-line overrides, prompts editable fields with defaults, and validates providers.
- `delete`: use a bounded confirmation prompt, then report the result with [TigerConsole](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Terminal.TigerConsole.html).

Do not hand-render normal list/details output, and do not model edit as "add with every option optional." See [CRUD command apps](guides/crud-commands.md).

## Prompting Rules

- Use attributes and parser-driven prompts for missing arguments/options.
- Use providers for selectable values.
- Use `Required = true` for values automation must supply explicitly and humans may be prompted for.
- Use `Promptable = [TigerCliPromptable](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliPromptable.html).Normal` / `First` / `Last` to opt into prompting or control prompt order.
- Use `Promptable = TigerCliPromptable.No` only when the value must never be prompted, such as automation-only flags, unsafe values, or secrets that must come from configuration/secure channels.
- Do not set `TigerCliPromptable.No` just to avoid learning the prompt system.
- Do not manually prompt inside command handlers for values TigerCli can bind or prompt before the handler runs.
- Respect `--non-interactive`; parser-driven prompts and prompt providers are skipped by the framework.
- For one existing input file, put `[TigerCliFileOpen(Filter = "*.json")]` on the string option. For one output file, use `[TigerCliFileSave(DefaultExtension = ".json", DefaultFileName = "project.json", Overwrite = TigerCliFileOverwrite.Prompt, OverwriteWhenOption = nameof(Force))]`; pair the override with a `bool`/`bool?` `--force` option. File prompts require a supplied path under `--non-interactive`, and save never creates the parent directory. See the [File Copy sample](examples/file-copy.md) for a focused end-to-end app.

```csharp
public sealed class RunSettings : TigerCliSettings
{
    [TigerCliOption("-c|--connection",
        Required = true,
        Promptable = TigerCliPromptable.Normal,
        Provider = "connections",
        Description = "Connection to use.")]
    public string Connection { get; set; } = string.Empty;
}
```

Use direct [TigerTui](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Tui.TigerTui.html) prompts only for bounded command behavior after execution starts, such as delete confirmation. Do not use direct prompts to reimplement argument/option binding.

## Providers

- Use providers for dynamic choices.
- Register providers at the scope that needs them: app, command group, or command.
- Prefer `AddAsync(...)` / `AddAsyncProvider(...)` for I/O-backed or slow providers.
- Observe [TigerCliProviderContext](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliProviderContext.html).CancellationToken for cooperative cancellation.
- Use `OptionItem<T>` when the display label differs from the stable bound key.
- Configure slow-provider loading text through provider options when useful.
- Providers return choices. Providers do not render UI.
- TigerCli owns the interactive loading message and spinner for slow provider-backed prompts.
- Use provider `configure` options such as `EmptyMessage(...)` for app-specific required no-choice failures; do not throw just to customize an empty provider result.
- Use `AutoSelectSingleChoice = true` on the argument/option, not on the provider, only when skipping confirmation for exactly one selectable provider outcome is acceptable.

```csharp
builder.ConfigureProviders(providers =>
{
    providers.AddAsync(
        "connections",
        async ctx => await store.GetConnectionNamesAsync(ctx.CancellationToken),
        configure: options => options.LoadingMessage("Loading connections..."));
});
```

## Arguments, Options, And Selectors

Command shape:

```text
app <command-path> <positional-arguments> [options]
```

- Positional arguments are required and come before options.
- Options are unordered after positionals.
- A default/root command has an empty command path, so its shape is
  `app <positional-arguments> [options]`; with no declared positionals its options area starts at
  the first token (`app --theme light`).
- Framework-owned execution options such as `--non-interactive`, `--culture`, `--theme`, and
  `--color` are app-wide in meaning but still belong after the command path and required
  positionals. Contributed global options follow the same rule.
- Root `--help`, `--help-errors`, `--help-env`, and opt-in `--version` / `--version-full` are informational forms,
  not permission to put execution options before a command. Use `app command --help` for command
  help; `app --help command` is invalid. They share the options area with execution options, so
  `app --help --help-env --theme light` is valid.
- Do not define app options that conflict with framework-owned options.
- `--help`, `--help-errors`, and `--help-env` are composable. Normal help hints only at sections not
  included in the current request; command forms still follow normal option placement.
- Register help-only environment metadata with `AddEnvironmentVariable(name, description)` on the
  app, group, command, or contribution builder. Contributions may supply an optional
  `descriptionResourceKey`, resolved through the host's app resources at help-render time.
  Registration does not make TigerCli read or apply the variable.
- Labels are display-only. Keys and command-line values should remain stable and language-neutral.
- A selector is the input that carries the object key. The command (`add`, `show`, `edit`, or
  `delete`) decides the operation; the selector identifies the keyed object.
- Use the same selector/key shape for the same object across CRUD commands. `add` does not give the
  selector a different meaning, and selector does not mean "existing object only."
- Selectors are usually positional arguments because object identity should remain stable and
  readable across operations. A selector may be a natural key, primary key, or parent/context key.
- Selector is not a synonym for required. Values that describe, configure, or change an object
  remain options even when `Required = true`.
- Preserve selectors in generated commands and agent tool descriptions: they make command intent
  and object identity explicit.

### Selectors example

The same project key is visible in every operation:

```text
project add    <connection> <project> --schema dbo
project show   <connection> <project>
project edit   <connection> <project> --schema sales
project delete <connection> <project>
```

`<connection>` and `<project>` are selectors in all four commands; together they form the object
key. `--schema` is object data, not a selector, even when schema is required.

## Edit Commands

Use existing edit-command support instead of creating ad-hoc edit flows.

```csharp
builder.AddCommandGroup("connections", group => group
    .AddCommand<EditConnectionCommand>("edit", command => command
        .AsEdit<ConnectionSettings>(async settings =>
        {
            var profile = await store.FindAsync(settings.Profile);
            return profile is null
                ? TigerCliEditLoad<ConnectionSettings>.NotFound()
                : TigerCliEditLoad<ConnectionSettings>.Found(new ConnectionSettings
                {
                    Server = profile.Server,
                    Database = profile.Database
                });
        })));
```

Rely on TigerCli's prompt/default/provider model for edit scenarios. Command-line values win; unspecified editable fields are seeded from the loaded object and may be prompted with those values as defaults. Use `EditProvider` when a shared add/edit settings type needs edit-only selectable values.

## Progress And Activity

- Use async providers for slow choice loading.
- Use `TigerTui.RunActivityAsync(...)` for real long-running operations that need activity/progress UI.
- Use existing spinner/activity primitives; configure them through TigerCli APIs.
- Do not create ad-hoc console spinners, background writer loops, or local progress renderers.

Provider loading is not an activity dialog. Let provider-backed prompts use TigerCli's built-in loading spinner/message.

TigerCli activity APIs are [TigerCliInteractionMode](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.TigerCliInteractionMode.html)-aware. Do not write separate "direct" and "activity" execution paths just to support `--non-interactive`. Use one `RunActivityAsync` path and let TigerCli render it interactively or run it headlessly.

Bad pattern:

```csharp
if (settings.InteractionMode == TigerCliInteractionMode.NonInteractive)
    await RunDirectAsync(...);
else
    await RunWithActivityAsync(...);
```

Preferred pattern:

```csharp
await RunWithActivityAsync(...);
```

`RunWithActivityAsync` should use `TigerTui.RunActivityAsync`. Simple static-message overloads use that message as one script-friendly status line under `--non-interactive`. For richer activity dialogs, use an `ActivityDialogSpec`, and where useful call `SetNonInteractiveMessage(...)` when script output should differ from the visible activity message.

See [Folder Copy](examples/folder-copy.md) for a complete in-repo sample of this pattern.

Trust TigerCli's mode-aware primitives:

- Prompts, selects, and command menus are blocked or skipped in non-interactive mode.
- Activities run headlessly in non-interactive mode.
- Progress updates are safe and still validated.
- No TUI is rendered and no keyboard input is read for headless activities.
- One execution path avoids drift and preserves framework cancellation, timeout, and result semantics.

Mode changes presentation and prompting policy. It should not fork the business operation.

Branch on interaction mode only when the command truly needs different semantics, such as refusing a required question in non-interactive mode or choosing a different confirmation policy. Do not branch merely to suppress activity UI.

## Message Box Dialogs

Use `TigerTui.MessageBoxAsync(...)`, `TigerTui.WarningAsync(...)`, and `TigerTui.ErrorAsync(...)` for bounded informational dialogs. Pick the method that matches the message's severity — TigerCli resolves the correct dialog surface from the active theme.

```csharp
// Informational — normal dialog surface
await TigerTui.MessageBoxAsync("Settings saved.");

// Warning — yellow/orange surface
await TigerTui.WarningAsync("This action cannot be undone.");

// Error — red surface
await TigerTui.ErrorAsync("Connection failed. Check your credentials.");
```

- Do not hand-roll warning/error message boxes with custom colors or local dialog code.
- Do not pass `[MessageBoxKind](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.MessageBoxKind.html).Warning`/`Error` directly when the convenience method is cleaner.
- Use [MessageBoxButtons](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.MessageBoxButtons.html) to control the button set; the default is a single `[ OK ]` button.
- The dialog surface is theme-driven. Do not override colors locally.

## Output Rules

- Use `TigerConsole.MarkupLine(...)` and `TigerConsole.MarkupErrorLine(...)`, not raw `Console.WriteLine(...)`.
- Use `settings.T(...)`, `settings.F(...)`, and `settings.E(...)` as settings localization helpers for command output.
- Use `settings.E(...)` for localized markup output with dynamic formatted values; it escapes formatted arguments.
- Use `CliMarkupParser.Escape(...)` for raw markup escaping when no localization lookup is needed.
- Use semantic markup/styles such as `[Success]`, `[Error]`, `[Muted]`, `[Key]`, `[Value]`, `[Path]`, `[Link]`, and [ThemeStyle](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.ThemeStyle.html) roles instead of raw colors/decorations unless there is a real reason.
- Use `CliList<T>` for `list`, `CliDetails` for `show`/details, `CliTable` for lower-level tables, and `CliGrid` or a `CliRenderableComponent` only when custom layout is needed.
- Do not hand-format tables, lists, or details line by line when structured output helpers fit.

```csharp
var details = new CliDetails()
    .AddTitle(settings.T("Connection"))
    .AddKey(settings.T("Name:"), profile.Name)
    .Add(settings.T("Server:"), profile.Server)
    .AddOptionalPath(settings.T("Config:"), profile.ConfigPath);

TigerConsole.Render(details);
```

## Testing Model

- Use `TigerCliAppTestHost`.
- Assert `ExitCode`, `StdOut`, and `StdErr`.
- Queue parser-driven prompt answers with `WithTextInput(...)`, `WithSelectIndex(...)`, `WithConfirm(...)`, and `WithMultiSelectIndexes(...)`.
- Do not use real console input or output in app tests.

## Common Mistakes To Avoid

- Generating sync command handlers or blocking I/O where async provider/activity APIs fit.
- Using `Console.WriteLine(...)` directly in app code.
- Manually parsing `--help`, `--help-errors`, `--help-env`, `--version`, `--version-full`,
  `--non-interactive`, `--theme`, `--color` / `--no-color`, or `--culture`.
- Hand-writing help output.
- Setting `TigerCliPromptable.No` just to suppress prompts.
- Manually prompting inside handlers for values that parser-driven prompting can handle.
- Building local select menus instead of provider-backed prompts.
- Reimplementing provider-backed selection, provider validation, or provider ordering.
- Adding local spinners/loading messages around providers.
- Creating background console writer loops for progress/activity.
- Branching on `settings.InteractionMode` to bypass `TigerTui.RunActivityAsync` with a separate direct execution path.
- Using hard-coded ANSI colors or raw color tags when semantic styles fit.
- Hand-rolling warning/error message boxes with local colors or custom dialog code instead of `TigerTui.WarningAsync(...)`/`TigerTui.ErrorAsync(...)`.
- Hand-rendering tables, lists, or details with string padding and `MarkupLine` loops.
- Registering multi-token names with `AddCommand(...)` instead of `AddCommandGroup(...)`.
- Accepting localized enum/provider labels as stable command-line input.
- Treating TigerCli as a full-screen TUI framework.

## More Documentation

- [Command apps](guides/command-apps.md)
- [Folder Copy sample](examples/folder-copy.md)
- [CRUD command apps](guides/crud-commands.md)
- [Arguments and options](guides/arguments-and-options.md)
- [Prompting and providers](guides/prompting-and-providers.md)
- [Structured output](guides/structured-output.md)
- [Semi-interactive prompts](guides/semi-interactive-prompts.md)
- [Themes and styles](guides/themes-and-styles.md)
- [Exit codes](guides/exit-codes.md)
- [Localization](guides/localization.md)
- [App testing](guides/app-testing.md)
- [API map](reference/api-map.md)
