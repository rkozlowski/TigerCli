# Prompting and Providers

TigerCli apps are script-safe first. A command should run from supplied arguments and options in automation, but it can help a human fill missing values when semi-interactive prompting is allowed.

## Overview

TigerCli uses one command-processing model:

```text
parse provided values
bind settings
prompt missing values only when allowed
validate
execute handler
```

Prompting is framework-owned. Command handlers should not each reinvent `--non-interactive` checks, prompt ordering, provider calls, timeout behavior, or validation fallbacks. The framework decides whether a missing value can be prompted before the handler runs.

This keeps the same command useful in two contexts:

```bash
roi-cities show Galway --non-interactive
roi-cities show
```

The first form is fully explicit and script-safe. The second form may prompt a human for the missing city when the app runs in semi-interactive mode.

`--non-interactive` always wins for prompting. When it is present, TigerCli does not render parser-driven prompts and does not call providers to build prompt choices. Providers may still be called to validate supplied values — see [Provider Validation Of Supplied Values](#provider-validation-of-supplied-values).

## Prompting Order

TigerCli prompts missing values in command-meaning order, not in provider registration order:

1. Missing positional arguments, by argument index
2. Missing required options
3. Optional promptable options

This order matters for dependent choices. A project provider can rely on `ConnectionName` already being available when `ProjectName` is prompted.

```text
connection -> project -> optional settings
```

For example, `provider smoke` can ask for a connection first, then load projects for the selected connection.

## Prompt Modes

Prompt mode controls whether TigerCli may ask for missing values for a command.
`Promptable` controls whether a specific argument or option participates in prompting.

```csharp
using ItTiger.TigerCli.Enums;

var app = TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .SetDefaultPromptMode(TigerCliPromptMode.RequiredOnly)
    .AddCommandGroup("connections", group =>
    {
        group.SetPromptMode(TigerCliPromptMode.Yes);
        group.AddCommand<ProviderSmokeCommand>(
            "add",
            command => command.SetPromptMode(TigerCliPromptMode.RequiredOnly));
    })
    .Build();
```

Prompt modes:

- `TigerCliPromptMode.No` does not prompt missing values by default.
- `TigerCliPromptMode.RequiredOnly` prompts missing required values by default. This is the framework default.
- `TigerCliPromptMode.Yes` may also prompt optional values when TigerCli can produce a prompt.

Prompt mode inherits from framework default to app, group, and command:

- The framework default is `RequiredOnly`.
- `SetDefaultPromptMode(...)` sets the app default.
- `group.SetPromptMode(...)` overrides the app default for child commands.
- `command.SetPromptMode(...)` overrides the group and app defaults for that command registration.
- `SetCommandPromptMode<THandler>(...)` remains available as a type-level command override.

Prompt mode is still gated by interaction mode. `--non-interactive` forces non-interactive execution for the current run, so promptability and prompt mode cannot make TigerCli prompt.

## Promptable Metadata

`Promptable` is available on both arguments and options. It uses `TigerCliPromptable`, so promptability and prompt order are one concept:

- `TigerCliPromptable.No` never prompts.
- `TigerCliPromptable.First` prompts early when prompting applies.
- `TigerCliPromptable.Normal` uses normal prompt order when prompting applies.
- `TigerCliPromptable.Last` prompts late when prompting applies.
- Omitted `Promptable` uses the effective prompt mode in normal prompt order.

```csharp
public sealed class ProjectSettings : TigerCliSettings
{
    [TigerCliArgument(0, Name = "connection", Promptable = TigerCliPromptable.Normal)]
    public string ConnectionName { get; set; } = string.Empty;

    [TigerCliArgument(1, Name = "project", Promptable = TigerCliPromptable.No)]
    public string ProjectName { get; set; } = string.Empty;

    [TigerCliOption("--schema", Promptable = TigerCliPromptable.Normal, Description = "Schema name.")]
    public string Schema { get; set; } = "dbo";

    [TigerCliOption("--token", Required = true, Promptable = TigerCliPromptable.No)]
    public string Token { get; set; } = string.Empty;
}
```

How to read it:

- `First`, `Normal`, and `Last` allow that member to prompt when interaction is allowed.
- `Promptable = TigerCliPromptable.No` prevents prompting for that member.
- `Promptable` does not make an option required.
- `--non-interactive` blocks prompting even when `Promptable` is `First`, `Normal`, or `Last`.

Use `Promptable = TigerCliPromptable.No` for values that should always be explicit, such as automation-only flags, unsafe values, or credentials that should come from configuration or a secure channel.

Options can also gate prompting on another option:

```csharp
[TigerCliOption("--username",
    Promptable = TigerCliPromptable.Normal,
    RequiredWhenOption = "--authentication",
    RequiredWhenValueIn = new[] { "SqlPassword", "EntraPassword" },
    PromptWhenOption = "--authentication",
    PromptWhenValueIn = new[] { "SqlPassword", "EntraPassword" })]
public string? Username { get; set; }
```

TigerCli resolves simple option dependencies before evaluating dependent prompt conditions. If a controlling option is populated from argv, a default initializer, or an earlier prompt, dependent `PromptWhen...` and `RequiredWhen...` checks use that resolved value. Prompt mode, `Promptable`, provider availability, and `--non-interactive` still apply.

Conditional values can use single values, inclusion lists, or exclusion lists:

- `RequiredWhenValue` / `PromptWhenValue` match one controlling value.
- `RequiredWhenValueIn` / `PromptWhenValueIn` match when the controlling value is one of the listed values.
- `RequiredWhenValueNotIn` / `PromptWhenValueNotIn` match when the controlling option has a value and it is not one of the listed values.

Within each family, configured single, `In`, and `NotIn` values are ORed. Null or empty `In`/`NotIn` arrays are ignored. Value comparison uses the same rules as single-value conditions: enum names and bool values are parsed, strings compare ordinal, and other values compare their string form ordinal-ignore-case.

Cycles between option prompt dependencies are unsupported and fail with a framework validation error.

## Required And Promptable

`Required` and `Promptable` answer two different questions, and the common pattern for a value that a human should be asked for — but that automation must supply explicitly — sets both:

```csharp
[TigerCliOption("-c|--connection",
    Required = true,
    Promptable = TigerCliPromptable.Normal,
    Provider = "connections")]
public string Connection { get; set; } = string.Empty;
```

- **`Required`** controls validation and the non-interactive failure. A missing required value fails before the handler runs.
- **`Promptable`** controls whether the framework *may* ask for the missing value interactively.

Expected behavior:

```text
semi-interactive + missing value  -> prompt
non-interactive  + missing value  -> required-option failure
with Provider                     -> provider-backed select prompt
```

So the same option is script-safe (fails clearly under `--non-interactive`) and friendly in a semi-interactive session (prompts, and with a `Provider` the prompt is a select over app-owned choices). For a *missing* value, the provider is only consulted when a prompt actually happens — the missing-value failure under `--non-interactive` never invokes it. A *supplied* value is a different matter: it is validated against the provider's choices in both modes ([Provider Validation Of Supplied Values](#provider-validation-of-supplied-values)).

## Automatic Prompts

When no provider is configured, TigerCli can prompt some missing values from the settings property type.

| Property shape | Parser-driven prompt |
|---|---|
| `string` / `string?` | Text input |
| `string` / `string?` with `[TigerCliFolderSelect]` | Folder picker |
| `int` / `int?` | Text input with integer validation |
| `bool?` | Confirm |
| enum | Select |
| `[Flags]` enum | Multi-select |

Optional nullable selects (`enum?` and provider-backed `string?`) also offer a no-selection row — see [No-Selection For Optional Nullable Prompts](#no-selection-for-optional-nullable-prompts).

Parser-driven select and multi-select prompts use the same dialogs as the direct `TigerTui` prompts; rendered storyboards of those dialogs are committed at [`docs/examples/tui-storyboards.html`](../examples/tui-storyboards.html).

Example:

```csharp
using ItTiger.Core;
using ItTiger.TigerCli.Commands;

[TigerText("Prompt smoke modes")]
public enum PromptSmokeMode
{
    [TigerText("Fast")]
    Fast,

    [TigerText("Normal")]
    Normal,

    [TigerText("Careful")]
    Careful
}

[Flags]
[TigerText("Prompt smoke features")]
public enum PromptSmokeFeatures
{
    [TigerText("None")]
    None = 0,

    [TigerText("Logging")]
    Logging = 1,

    [TigerText("Metrics")]
    Metrics = 2,

    [TigerText("Trace")]
    Trace = 4
}

public sealed class PromptSmokeSettings : TigerCliSettings
{
    [TigerCliArgument(0, Name = "name", Description = "Name to process.")]
    public string Name { get; set; } = string.Empty;

    [TigerCliOption("--mode", Required = true, Description = "Mode to use.")]
    public PromptSmokeMode Mode { get; set; }

    [TigerCliOption("--features", Promptable = TigerCliPromptable.Normal, Description = "Optional features.")]
    public PromptSmokeFeatures Features { get; set; }
}
```

Enum and flags prompt labels can be localized with `TigerTextAttribute` and app resources. Command-line parsing still uses enum member names, not localized labels.

For secret string input, set `Secret = true` on the option:

```csharp
[TigerCliOption("--password",
    Promptable = TigerCliPromptable.Normal,
    Secret = true,
    AllowCommandLineValue = false,
    RequiredWhenOption = "--authentication",
    RequiredWhenValue = "SqlPassword",
    PromptWhenOption = "--authentication",
    PromptWhenValue = "SqlPassword",
    MinLength = 1)]
public string? Password { get; set; }
```

`Secret = true` uses masked text input for automatic parser-driven prompts. `AllowCommandLineValue = false` rejects argv values before the handler runs; use both for password-style values because argv can leak through process lists, shell history, logs, and automation output. Prompt-only secrets are documented under `Prompted values:` rather than `Options:` and do not show a `--password <value>` command-line form. Validation hints never include the typed secret value.

For direct TUI workflows outside parser-driven prompting, `TigerTui.SecretInputAsync(...)` is also available; see [semi-interactive prompts](semi-interactive-prompts.md).

### Folder Selection

Add `[TigerCliFolderSelect]` next to `[TigerCliOption]` on a `string` / `string?` property so a missing value is prompted with the inline folder picker instead of a text prompt. The selected folder path becomes the option value.

```csharp
public sealed class CopySettings : TigerCliSettings
{
    [TigerCliOption("-d|--destination", Description = "Destination folder.")]
    [TigerCliFolderSelect]
    public string? DestinationFolder { get; set; }
}
```

Behavior:

- A value supplied on the command line binds normally; the picker is never shown.
- When the value is missing and prompting is allowed in semi-interactive mode, the folder picker runs. Enter confirms the highlighted folder; Escape cancels (the same cancel/timeout/token behavior as other prompts).
- In non-interactive mode the picker does not run; a missing required value fails like any other required option.
- The property's current/default value seeds the picker's initial folder (e.g. a default of `D:\Media\Movies` starts in `D:\Media` with `Movies` highlighted).
- Only `string` / `string?` properties are supported. Applying the marker to any other property type throws `InvalidOperationException` when the command's options are built.

The picker browses the real filesystem by default. Apps (and tests) can override the filesystem source with `TigerCliAppBuilder.UseFolderBrowser(IFolderBrowser)`. The control behavior is documented under the folder-select section of [semi-interactive prompts](semi-interactive-prompts.md).

See [Folder Copy](../examples/folder-copy.md) for the public sample that uses required `--source` and `--destination` folder-select options, prompts for missing folders in semi-interactive mode, and fails cleanly under `--non-interactive`.

### Multi-Select (Select Zero Or Many)

Two shapes cover "pick zero or more values".

**Flags enums — no extra attribute.** A `[Flags]` enum (or `enum?`) option is already a multi-select:

```csharp
[Flags]
public enum FileMode { None = 0, Read = 1, Write = 2, Execute = 4 }

[TigerCliOption("--mode", Promptable = TigerCliPromptable.Normal, Description = "File mode.")]
public FileMode? Mode { get; set; }
```

- **Interactive**: a checklist of the single-bit members; the current value is preselected; the combined flags value is bound.
- **Non-interactive**: `--mode Read,Write` (comma-separated names), `--mode 5` (decimal), or `--mode 0x6` (hex) all bind the combined value.
- Labels use the standard enum-text conventions (`TigerText` / resources).

**Dynamic lists — `[TigerCliMultiSelect]`.** For provider-backed lists whose choices are not known at compile time, add `[TigerCliMultiSelect]` next to `[TigerCliOption]` on a `List<T>` / `T[]` property (`T` is `string`, `int`, `short`, `long`, or `Guid`):

```csharp
public sealed class TagSettings : TigerCliSettings
{
    [TigerCliOption("--tags", Provider = "tags", Promptable = TigerCliPromptable.Normal, Description = "Tags.")]
    [TigerCliMultiSelect]
    public string[]? Tags { get; set; }
}

// provider registration (app / group / command level)
.ConfigureProviders(providers =>
    providers.Add("tags", _ => new List<string> { "red", "green", "blue" }));
```

Behavior:

- **Interactive**: the inline multi-select checklist (`InlineMultiSelect`) is shown — Space toggles, Enter confirms, Escape cancels. The property's current value seeds the preselection (useful in edit commands). Selected values are bound preserving choice order.
- **Non-interactive**: the value is a comma-separated list and/or a repeated option — `--tags red,blue` or `--tags red --tags blue`. Each token is matched against the provider's choices by key or label (case-insensitive); duplicates collapse; unknown tokens are rejected with a clear error unless `AllowCustomValues = true` (string collections only).
- **Empty selection** is allowed by default; set `AllowEmpty = false` to require at least one value.

For key/label choices (display a friendly label, bind a typed key) the provider returns `OptionItem<TKey>` and the property is `List<TKey>` / `TKey[]`:

```csharp
[TigerCliOption("--language-options", Provider = "langopts", Description = "Language options.")]
[TigerCliMultiSelect]
public long[]? LanguageOptions { get; set; }

.ConfigureProviders(providers => providers.Add<long>("langopts", _ =>
[
    new OptionItem<long>(0x4, "Use DateOnly (0x0004)"),
    new OptionItem<long>(0x2, "Use TimeOnly (0x0002)")
]));
```

The command receives the selected keys (here `long` bit values) and combines them however it needs — for example OR-ing them into a single flag mask — without any bespoke multi-select or comma-parsing plumbing:

```csharp
long combined = 0;
foreach (var value in settings.LanguageOptions ?? [])
    combined |= value;
```

## No-Selection For Optional Nullable Prompts

Select-style prompts (enum selects and provider-backed selects) offer a synthetic **no-selection** row as their first choice when, and only when, the field is **optional and nullable**. Selecting it binds `null`.

The no-selection row is added when all of these hold:

- the prompt is select-style — a nullable enum select or a provider-backed select (not a text input, confirm, or `[Flags]` multi-select);
- the target property can hold null — `Nullable<T>` (for example `enum?`) or a nullable reference type (for example `string?`);
- the field is **not required** under the current effective rules — neither `Required = true` nor required via `RequiredWhen` / `RequiredWhenValue` / `RequiredWhenValueIn` / `RequiredWhenValueNotIn` for the current settings.

It is **not** offered when the property is non-nullable, when the field is required (including conditionally required for the current values), or for `[Flags]` multi-select prompts.

```csharp
// Optional + nullable → the prompt shows a "(None)" row first; choosing it binds null.
[TigerCliOption("--encrypt", Promptable = TigerCliPromptable.Normal, Description = "Encryption mode.")]
public EncryptOption? Encrypt { get; set; }

// Required (or non-nullable) → no "(None)" row; a real value must be chosen.
[TigerCliOption("--encrypt", Required = true, Promptable = TigerCliPromptable.Normal)]
public EncryptOption? Encrypt { get; set; }
```

Behavior details:

- **Display.** The no-selection row renders through the standard grid null-display value, currently `[Muted](None)[/]` (resource key `Tui_Select_NoSelection`). The stored value is a real `null` sentinel, so a provider may still expose a genuine choice whose key/label is `"None"` without colliding with the synthetic row.
- **Preselect.** When the current/default value is `null` (add mode with no initializer, or an existing `null` value in edit mode), the no-selection row is preselected. When the current/default value matches a real choice, that choice is preselected instead.
- **Cancel is not no-selection.** Pressing Escape (or hitting the prompt timeout / token cancellation) cancels the prompt and is reported as a prompt-cancellation error — it does **not** bind `null`. Only choosing the no-selection row binds `null`.
- **Provider validation.** A `null` (no-selection) value is skipped by provider validation, exactly like an absent value; non-null values are still validated strictly against the provider's current choices.
- **Not stale-value injection.** A stale (no-longer-offered) current value is never added as a choice. For an optional field the no-selection row remains available and is preselected; the stale value is not shown.
- **No provider choices.** For optional nullable provider-backed prompts, `(None)` is still a selectable outcome even when the provider returns no real choices.

## Providers

Providers are named dynamic value sources with two consumers:

- **Prompting.** A missing value is selected from app-owned choices instead of typed as
  free text (interactive modes only).
- **Validation of supplied values.** A value supplied on the command line (or carried by
  an existing/default value) is checked against the provider's current choices, and a
  matching value is re-bound to the canonical provider key. See
  [Provider validation of supplied values](#provider-validation-of-supplied-values).

Use providers in two steps:

1. Register a named provider on the app, command group, or command.
2. Reference the provider from an option or argument with `Provider = "..."`.

Providers can be registered at app, command group, or command level:

```csharp
using ItTiger.TigerCli.Commands;

return TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .ConfigureProviders(providers =>
    {
        providers.Add("connection", ctx =>
            connectionService.GetConnectionNamesAsync(ctx.CancellationToken));
    })
    .AddCommandGroup("connections", group =>
    {
        group.SetDescription("Manage saved SQL Server connections");

        group.AddProvider("project", ctx =>
            projectService.GetProjectNamesAsync(ctx.CancellationToken));

        group.AddCommand<ProviderSmokeCommand>("add", command =>
        {
            command.AddProvider("schema", ctx => ["dbo", "audit"]);
        });
    })
    .Build();
```

Provider inheritance is app, then group, then command. More specific providers
override less specific providers with the same key.

### Provider Scope

Scope decides **which commands can see a provider**, and it is the most common source of confusion:

```text
App-level providers     apply to default/top-level commands (and are inherited by groups/commands).
Group-level providers   apply only inside that command group.
Command-level providers  apply only to that command.
```

A provider registered inside a command group does **not** leak to top-level commands. Register at the scope where every command that needs the provider can see it.

The TigerQuery lesson: if both the **default command** and a top-level **`run`** command use `-c|--connection`, the `connections` provider belongs at **app scope**, because both commands resolve providers from app → (group) → command. Registering it inside the `connections` command group would make it available to `connections add` / `connections edit`, but **not** to the default or `run` command. A `connections` group may still have its own providers; those simply do not apply to top-level commands.

```csharp
return TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(SqlCmdApp).Assembly)
    // App scope: visible to the default command AND the top-level `run` command.
    .ConfigureProviders(providers =>
        providers.Add("connections", ctx => store.GetConnectionNamesAsync(ctx.CancellationToken)))
    .SetDefaultCommand(() => new QueryCommand(store)) // uses -c|--connection
    .AddCommand("run", () => new RunCommand(store))    // also uses -c|--connection
    .AddCommandGroup("connections", group =>
    {
        // Group scope: visible only to `connections ...` child commands.
        group.AddCommand("add", () => new AddConnectionCommand(store));
    })
    .Build();
```

Settings:

```csharp
public sealed class ProviderSmokeSettings : TigerCliSettings
{
    [TigerCliArgument(
        0,
        Name = "connection",
        Provider = "connection",
        Description = "Connection to use.")]
    public string ConnectionName { get; set; } = string.Empty;

    [TigerCliArgument(
        1,
        Name = "project",
        Provider = "project",
        Description = "Project to use.")]
    public string ProjectName { get; set; } = string.Empty;

    [TigerCliOption("--schema", Provider = "schema", Description = "Schema to use.")]
    public string Schema { get; set; } = "dbo";
}
```

`Provider` is metadata, not display text. If it is omitted, prompt lookup still
falls back to existing conventions such as argument names and option aliases.

Providers receive:

- `TigerCliProviderContext`, including `Culture` and `CancellationToken`
- resolved option values through `TryGetValue<T>(...)` / `GetOptionValue<T>(...)`
- optionally, the partially bound settings object when using typed overloads such as `Add<TSettings, TValue>(...)`

String providers are the common case. TigerCli maps each string to a choice whose value and label are the same string.

Use rich `OptionItem<T>` providers when display labels differ from bound values, or when the provider returns non-string values. Each `OptionItem<T>` has a key and a label. TigerCli renders the label, but binds the selected key to the settings property.

### Auto-Selecting A Single Provider Choice

Provider-backed arguments and options can opt into skipping the select UI when there is exactly one selectable outcome:

```csharp
[TigerCliOption("-c|--connection",
    Required = true,
    Promptable = TigerCliPromptable.Normal,
    Provider = "connections",
    AutoSelectSingleChoice = true)]
public string Connection { get; set; } = string.Empty;
```

The default is `false`, so provider-backed prompts normally still ask the user to confirm a single choice. `AutoSelectSingleChoice` is member metadata because it controls whether that bound argument or option may skip user confirmation; it is not provider-owned.

TigerCli counts selectable outcomes after applying optional no-selection behavior:

- required + one provider choice auto-selects that choice when enabled;
- optional nullable + zero provider choices has one selectable outcome, `(None)`, so it auto-selects `null` when enabled;
- optional nullable + one provider choice still has two selectable outcomes, `(None)` and the real choice, so TigerCli shows the prompt even when auto-select is enabled;
- multiple selectable outcomes always show the prompt.

`AutoSelectSingleChoice` does not change non-interactive behavior. A missing required value still fails under `--non-interactive` without calling its prompt provider.

Provider callbacks can be synchronous or async. `AddProvider(...)` accepts either shape and stays
fully source-compatible. For slow or I/O-backed providers (a network call, a media-card scan) prefer
the `AddAsyncProvider(...)` registration — it is the same async overload under a clearer name and
signals intent at the call site:

```csharp
group.AddAsyncProvider("project", async ctx =>
    await projectService.GetProjectNamesAsync(ctx.CancellationToken));
```

The app-level equivalent on `ConfigureProviders(...)` is `AddAsync(...)`. Sync providers registered
through `AddProvider(...)` keep working exactly as before.

**Cancellation is cooperative.** Every provider receives a `TigerCliProviderContext` whose
`CancellationToken` is the effective run/prompt token threaded from the pipeline. Observing it is
optional: a provider that ignores the token behaves as it always has. A provider that *does* observe
it — and throws `OperationCanceledException` when the token is cancelled — is treated as a
cancellation rather than a provider failure. A prompt-backed provider folds onto the standard
prompt-cancellation outcome; a validation-time provider propagates the cancellation. An
`OperationCanceledException` thrown for an unrelated reason (the token was not cancelled) is still
reported as a genuine provider error.

**Slow providers show a generic loading message.** When a provider-backed prompt is resolving its
choices interactively and the provider takes longer than a short threshold (~150&#160;ms), the
framework shows a generic, localized loading message with a spinner (e.g. `Loading options…`) until the
choices are ready, then continues to the normal select prompt. A provider that returns quickly never
shows it. This is entirely framework-level: providers only return choices and **never render UI
themselves**. The loading UI is interactive-only — non-interactive resolution (CLI value validation)
runs the provider directly with no UI. Pressing Escape (or a caller/system cancellation) while the
loading message is shown cancels the prompt cooperatively through the provider's
`CancellationToken`, mapping to the same prompt-cancellation outcome as any other dismissal; a provider
that fails while loading still surfaces as a provider failure. Prefer `AddAsyncProvider(...)` for
slow/I-O-backed providers so the work is genuinely asynchronous while the loading UI animates.

The generic `Loading options…` message is the default. A registration can override it through the
trailing `configure` options callback — either a literal string or an app resource key resolved for the
active run culture (the same literal-or-key shape as descriptions). This only changes the *text* the
loading UI shows; providers still never render UI, and the message is ignored in non-interactive mode.

The same `configure` callback can set a custom empty message for required provider-backed prompts whose
provider returns zero real choices and has no selectable outcome. The message can be literal or resolved
from the app `ResourceManager`; missing resource keys fall back to the supplied literal fallback, and raw
resource keys are never shown.

```csharp
command.AddAsyncProvider<MediaSettings, string>(
    "destinations",
    async (settings, ctx) => await mediaService.ScanDestinationsAsync(ctx.CancellationToken),
    configure: options => options
        .LoadingMessage("Scanning media roots…")
        .EmptyMessage("No media roots were found."));

// Or localized through the app ResourceManager registered via UseAppResources:
command.AddAsyncProvider<MediaSettings, string>(
    "groups",
    async (settings, ctx) => await mediaService.LoadGroupsAsync(ctx.CancellationToken),
    configure: options => options
        .LoadingMessageResource("Provider_Loading_Groups",
            fallback: "Loading destination groups…")
        .EmptyMessageResource("Provider_Empty_Groups",
            fallback: "No destination groups are configured."));
```

Custom empty messages are used only for the empty provider result, not provider exceptions or
cancellation. Optional nullable provider-backed prompts with zero real choices still have `(None)` as a
selectable outcome, so they do not use the empty message.

Provider registrations can include `dependsOn` ordering hints. These hints do not make values required; they only prefer calling the provider after those options have been bound or prompted when that is possible.

```csharp
group.AddProvider(
    "database",
    async ctx =>
    {
        ctx.TryGetValue<string>("--server", out var server);
        ctx.TryGetValue<AuthenticationType>("--authentication", out var authentication);

        return await connectionService.GetDatabaseNamesAsync(
            server,
            authentication,
            ctx.CancellationToken);
    },
    dependsOn: ["--server", "--authentication", "--username", "--password"]);
```

Options can also declare prompt/provider ordering dependencies directly with `DependsOnOption` or `DependsOnOptions`. Use this when an option's provider depends on other option values, but the dependency is not already expressed by `RequiredWhenOption` or `PromptWhenOption`.

```csharp
public enum AuthenticationType
{
    Integrated,
    SqlPassword,
    EntraPassword
}

[TigerCliOption("--server", Promptable = TigerCliPromptable.Normal)]
public string? Server { get; set; }

[TigerCliOption("--authentication", Promptable = TigerCliPromptable.Normal)]
public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

[TigerCliOption("--username",
    Promptable = TigerCliPromptable.Normal,
    RequiredWhenOption = "--authentication",
    RequiredWhenValueIn = new[] { "SqlPassword", "EntraPassword" },
    PromptWhenOption = "--authentication",
    PromptWhenValueIn = new[] { "SqlPassword", "EntraPassword" })]
public string? Username { get; set; }

[TigerCliOption("--encrypt")]
public bool Encrypt { get; set; }

[TigerCliOption("--trust-server-certificate")]
public bool TrustServerCertificate { get; set; }

[TigerCliOption("--database",
    Provider = "database",
    Promptable = TigerCliPromptable.Last,
    DependsOnOptions = new[]
    {
        "--server",
        "--authentication",
        "--encrypt",
        "--trust-server-certificate"
    })]
public string? Database { get; set; }
```

`RequiredWhenOption` and `PromptWhenOption` automatically imply ordering dependencies, so `Username` does not need to repeat `DependsOnOption = "--authentication"`. `DependsOnOption` and `DependsOnOptions` are ordering dependencies only: they do not make a value required, do not make a value promptable, and do not validate either option by themselves.

For custom labels or settings-dependent choices:

```csharp
using ItTiger.TigerCli.Primitives;

group.AddProvider<ProjectSettings, string>("project", async (settings, ctx) =>
{
    var projects = await LoadProjectsAsync(settings.ConnectionName, ctx.CancellationToken);
    return projects.Select(p => new OptionItem<string>(p.Id, p.DisplayName)).ToArray();
});
```

`ConfigurePrompts<TSettings>(...)` remains available for compatibility with
older app-level prompt-provider configuration. Prefer `ConfigureProviders(...)`,
`group.AddProvider(...)`, or `command.AddProvider(...)` for new code.

## Provider Behavior And Failures

Current provider behavior:

- For **prompting**, providers are called only for missing values that are eligible for prompting, and never in non-interactive mode.
- For **validation of supplied values**, providers are also called in non-interactive mode — see [Provider Validation Of Supplied Values](#provider-validation-of-supplied-values).
- `Promptable = TigerCliPromptable.No` prevents prompt-time provider calls for that member.
- `PromptWhen...` false prevents prompt-time provider calls for that option after its controlling option is resolved.
- Provider registration `dependsOn` and option `DependsOnOption(s)` affect prompt order only; they do not require missing values.
- A required missing value with no provider choices fails validation.
- An optional nullable provider-backed value with no provider choices offers `(None)`; optional non-nullable provider-backed values with no choices are skipped and keep their existing/default value.
- `AutoSelectSingleChoice = true` skips the select UI only when there is exactly one selectable outcome after `(None)` is considered.
- A provider registration can customize the required no-choices failure through `EmptyMessage(...)` or `EmptyMessageResource(...)`.
- Duplicate provider keys fail validation.
- A provider exception maps through framework unhandled-exception handling.
- The selected key is bound to the setting, not the display label.

For a required option:

```csharp
[TigerCliOption(
    "--connection",
    Required = true,
    Provider = "connection",
    Description = "Connection")]
public string ConnectionName { get; set; } = string.Empty;
```

If its provider returns no choices, TigerCli fails before the handler runs. For an optional nullable provider-backed option, no real choices still leaves `(None)` as the selectable outcome; for an optional non-nullable promptable option, no choices means "nothing to ask" and the prompt is skipped.

Provider keys must be unique for a single prompt. This is what lets tests and commands rely on stable bound values even when labels are localized or reworded.

### Provider Validation Of Supplied Values

A provider does not only drive the select prompt — its choices are **authoritative for supplied values**. When a validated member carries a value (from the command line, or from an existing/default value), TigerCli checks it against the provider's current choices after prompting and required-field validation. A value that matches no choice fails with a localized error (`Invalid value for <city>: Atlantis is not an available choice.`) and the `ValidationError` exit kind, before the handler runs. A matching value is re-bound to the **canonical provider key** (so `galway` binds as `Galway`).

Which members are validated:

- **Options** with a configured provider are validated by default (editable options — options are editable by default).
- **Arguments** with an explicit `Provider = "..."` (or `EditProvider` in edit mode) are validated by default. An argument whose name merely happens to match a registered provider key is *not* validated — implicit name matching drives prompting only.
- **Multi-select options** validate each supplied token — see [Multi-Select](#multi-select-select-zero-or-many).
- **Edit selectors are the exception**: in edit mode, the positional selector that identifies the record is resolved by the edit loader, not by provider validation. `Cannot find 'x' to edit.` remains the not-found outcome.

Validation runs in both interaction modes — a script passing `--non-interactive` gets the same rejection as an interactive session. Values chosen from a provider select during prompting are not re-validated (they are valid by construction).

Opting out — when the provider offers *suggestions* but custom values are acceptable, disable validation per member:

```csharp
[TigerCliArgument(0, Name = "tag", Provider = "recent-tags",
    ValidateAgainstProvider = false, Description = "Tag (any value accepted).")]
public string Tag { get; set; } = string.Empty;
```

For multi-select string collections the equivalent switch is `AllowCustomValues = true` on `[TigerCliMultiSelect]`.

### Matching Supplied Values To Choices

When a value is supplied on the command line for a provider-backed option or argument, TigerCli matches it against the provider's current choices — during provider validation and multi-select resolution. Matching only decides which choice the supplied value corresponds to; the **bound value is always the provider's canonical key**, never the raw input. Set the strategy per member with `ValueMatching`:

```csharp
[TigerCliOption("--config", Provider = "config-files",
    ValueMatching = TigerCliValueMatchPreset.FileSystemPath,
    Description = "Configuration file.")]
public string ConfigPath { get; set; } = default!;
```

| Preset | Matching |
|---|---|
| `Default` | Case-insensitive matching of string keys and labels; non-string keys keep type-safe equality. |
| `Exact` | Case-sensitive string matching. |
| `IgnoreCase` | Explicit case-insensitive string matching — documents intent; same string behavior as `Default`. |
| `FileSystemPath` | Path-aware matching. On Windows, matching is case-insensitive and treats `/` and `\`, trailing separators, and drive-root spelling (`K:` ⇔ `K:\`) as insignificant. On non-Windows, matching is case-sensitive with an insignificant trailing `/`. Drive-relative and rooted-without-drive paths are never widened to absolute. |

`ValueMatching` defaults to `Default`, so keys and labels already match case-insensitively without configuration. Reach for `Exact` when key case is significant, and `FileSystemPath` when a provider's keys are paths.

## Localization And Providers

Providers can use `ctx.Culture` to return localized labels without changing process-wide culture.

```csharp
providers.Add<string>("connection", ctx =>
[
    new OptionItem<string>("local", AppStrings.Get("Connection_Local", ctx.Culture)),
    new OptionItem<string>("demo", AppStrings.Get("Connection_Demo", ctx.Culture))
]);
```

Keep keys stable and language-neutral:

```text
key:   demo
label: Demo connection / Połączenie demo
```

Labels are display text only. The selected key is what gets bound to settings. Command-line parsing does not accept localized labels unless your application explicitly implements that behavior somewhere else.

See [localization](localization.md) for app resources, `--culture`, and enum text.

## Testing Prompt Flows

Use `TigerCliAppTestHost` to test prompt flows without the real console.

```csharp
using ItTiger.TigerCli.Testing;
using RoiCities.Extended;

var result = await TigerCliAppTestHost
    .For(RoiCitiesApp.Create())
    .WithArgs("show")
    .WithSelectIndex(4) // Galway in the ROI Cities store
    .RunAsync();

Assert.Equal(0, result.ExitCode);
Assert.Contains("Galway", result.StdOut);
Assert.Contains("Corrib", result.StdOut);
Assert.Empty(result.StdErr);
```

Prompt answer helpers:

- `WithTextInput("value")` answers text prompts.
- `WithSelectIndex(1)` answers select prompts by zero-based row index.
- `WithMultiSelectIndexes(0, 2)` answers flags/multi-select prompts.
- `WithConfirm(true)` or `WithConfirm(false)` answers confirm prompts.

Answers are consumed in prompt order. For provider-backed prompts, answer by index; labels may be localized, but indexes follow the rendered choices.

See [app testing](app-testing.md) for full harness behavior, timeout configuration, viewport configuration, and current limitations.

## Common Mistakes

- Do not manually check `--non-interactive` in handlers. TigerCli handles interaction policy before execution.
- Do not prompt directly inside handlers for required values when parser-driven prompting or providers can handle them.
- Do not use localized labels as stable keys. Keep keys language-neutral and localize labels.
- Do not assume providers build prompt choices in non-interactive mode — prompting never happens there. Supplied values are still provider-validated in both modes.
- Do not make provider order depend on unrelated optional prompts. Design dependencies around positional arguments and required values that run earlier in the prompt order.
- Do not expect provider registration order to control positional prompt order. Positional prompts use argument indexes.
- Do not rely on command-line users typing provider labels. Labels match under the default value-matching preset, but they may be localized or reworded — scripts should pass the stable provider keys.

## Related Docs

- Build the app shape with [command apps](command-apps.md).
- Bind settings with [arguments and options](arguments-and-options.md).
- Understand interaction policy in [interaction modes](interaction-modes.md).
- Test prompts with [app testing](app-testing.md).
- Localize labels with [localization](localization.md).
- Call direct prompt helpers with [semi-interactive prompts](semi-interactive-prompts.md).
- Read the detailed policy design in [command processing prompting](../design/command-processing-prompting.md).
- See provider-backed selectors in [ROI Cities](../getting-started.md).
- See folder picker prompting in [Folder Copy](../examples/folder-copy.md).
- Use [`CommandParserTest`](../../CommandParserTest/) for broad dogfooding coverage, dependent providers, localization, and the `features` multi-select.
