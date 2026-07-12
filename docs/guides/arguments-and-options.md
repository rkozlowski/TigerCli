# Arguments and Options

TigerCli commands use a command-line shape that stays readable as apps grow:

```text
app <command-path> <positional-arguments> [options]
```

Command paths choose what to run. Positional arguments provide the required command input, usually identity or context. Options modify that command with settings, switches, and additional values.

Example:

```bash
my-tool projects sp-add local Billing --schema dbo
```

```text
my-tool          app
projects sp-add  command path
local            positional argument 0
Billing          positional argument 1
--schema dbo     option
```

For the full app shape, see [command apps](command-apps.md).

## Arguments, Options, And Selectors

Before the detailed syntax, three terms this documentation uses throughout:

- **Arguments** are positional inputs. They are bound by position, after the command path.
- **Options** are named inputs and switches (`--schema dbo`, `--dry-run`). They are bound by name and may appear in any order.
- A **selector** is an input that identifies the object key a command works with — usually the natural key / primary key of the domain object, including any parent/context key that participates in that identity.

A selector answers "which object?". That object may be the new object being created by `add`, the existing object used by `show`, `edit`, or `delete`, or a parent/context object under which the command operates. Selector means object identity/key — it does not mean "existing object only".

Selector is a role, not a separate syntax. Selectors are usually positional arguments, because they are the identity a command cannot run without. Values that describe, configure, or change the object are usually options — **even when they are required**.

A CRUD-style example, where the selector/key is the same `(connection-name, project-name)` pair across every verb:

```text
project add    <connection> <project> --schema dbo
project show   <connection> <project>
project edit   <connection> <project> --schema sales
project delete <connection> <project>
```

```text
<connection>  selector / parent-context key   part of the project's identity
<project>     selector / project key          which project
--schema      object data                     what the object holds — not a selector
```

`--schema` may well be required, but it stays an option: it is data the object *holds*, not part of *which object* the command works on.

> **Selector means object identity/key. Required does not mean selector. Selector usually means positional.**

This split keeps command lines readable and stable: the identity reads the same way across `add`, `show`, `edit`, and `delete`, while values can be added, reordered, or omitted without changing the command shape. It also drives edit commands, where the selectors identify the object to load and options carry the changes — see [CRUD command apps](crud-commands.md) and [edit commands](command-apps.md#edit-commands).

## Positional Arguments

Positional arguments are declared on a settings class with `[TigerCliArgument]`.

```csharp
using ItTiger.TigerCli.Commands;

public sealed class ProjectsSpAddSettings : TigerCliSettings
{
    [TigerCliArgument(0, Name = "connection", Description = "Connection name.")]
    public string ConnectionName { get; set; } = string.Empty;

    [TigerCliArgument(1, Name = "project", Description = "Project name.")]
    public string ProjectName { get; set; } = string.Empty;

    [TigerCliOption("--schema", Description = "Schema name.")]
    public string Schema { get; set; } = "dbo";
}
```

Positionals are bound by zero-based index, not by property declaration order. `Name` controls the display name in generated help; if omitted, TigerCli derives one from the property name.

Rules:

- Positionals come after the resolved command path.
- Positionals come before options.
- Positionals are always required.
- Missing positionals fail in non-interactive mode.
- Missing positionals may be prompted in semi-interactive mode when prompting policy allows it.

Valid:

```bash
my-tool projects sp-add local Billing --schema sales
```

Invalid, because `Billing` appears after options have started:

```bash
my-tool projects sp-add local --schema sales Billing
```

For deeper command-path and positional ordering rules, see [command processing positionals](../design/command-processing-positionals.md).

## Options

Options are declared with `[TigerCliOption]`.

```csharp
public sealed class EchoSettings : TigerCliSettings
{
    [TigerCliOption("-m|--message",
        Required = true,
        Description = "Message to echo.",
        DescriptionResourceKey = "Opt_Echo_Message_Description")]
    public string Message { get; set; } = string.Empty;

    [TigerCliOption("-n|--name", Description = "Name to include.")]
    public string Name { get; set; } = "World";
}
```

The option template is a `|`-separated list of aliases.

```csharp
[TigerCliOption("-n|--name")]  // short + long
[TigerCliOption("--schema")]   // long only
[TigerCliOption("-cs|--connection-string")] // multi-letter short alias
```

Options may be supplied after positionals and may appear in any order relative to other options:

```bash
my-tool echo --message "hello" --name Riley
my-tool echo --name Riley --message "hello"
```

`Required = true` means the user must provide that option explicitly. A property initializer does not satisfy a required option.

Property initializers define defaults for optional options:

```csharp
[TigerCliOption("--schema", Description = "Schema name.")]
public string Schema { get; set; } = "dbo";
```

`Description` is rendered in generated help and prompt labels. `DescriptionResourceKey` lets the text be resolved from app resources when the app calls `UseAppResources(...)`; missing resource keys fall back to `Description`.

Use `ValueName` when the generated help placeholder needs a clearer name:

```csharp
[TigerCliOption("--path", ValueName = "file-path", Description = "Path to write.")]
public string? Path { get; set; }
```

Alias names are validated when metadata is built. Short aliases start with `-` and contain one to eight letters. Long aliases start with `--`, begin with a letter, and may contain letters, digits, `_`, and `-`.

## Option Value Syntax

TigerCli supports these option forms.

| Form | Example | Notes |
|---|---|---|
| Long option with separate value | `--name Riley` | For value-taking options. |
| Long option with inline value | `--name=Riley` | Also used by `--culture=pl-PL`. |
| Short option with separate value | `-n Riley` | Short aliases may be one or more letters. |
| Short option with inline value | `-n=Riley` | Same binding as `-n Riley`. |
| Presence switch | `--upper` | For non-nullable `bool` properties. |
| Nullable bool value | `--enabled true` | `bool?` consumes `true` or `false`; `--enabled=true` also works. |

There is no short-option bundling. `-abc` is treated as one option alias named `abc`, not as `-a -b -c`.

For scalar options, if the same option is provided more than once, the last value wins.

```bash
tool --name first --name second
```

`Name` binds to `second`.

### Repeated Values

Repeated scalar options are supported for `string[]` and `List<string>`.

```csharp
[TigerCliOption("-v|--var", ValueName = "value", Description = "Variable value.")]
public string[] Variables { get; set; } = [];
```

```bash
tool --var one --var two
tool -v one -v=two
```

TigerCli binds all supplied values in command-line order.

### Key-Value Options

Key-value options are supported with `List<KeyValuePair<string, string>>`.

```csharp
[TigerCliOption("-v|--var", ValueName = "name=value", Description = "Variable value.")]
public List<KeyValuePair<string, string>> Variables { get; set; } = [];
```

Supported forms:

```bash
tool --var env=dev
tool --var=env=dev
tool --var env dev
```

All three bind a key of `env` and a value of `dev`.

## Required, Optional, And Defaulted Values

Positional arguments are always required. If a command declares `[TigerCliArgument(0)]`, that value must exist before the handler runs.

Options are optional unless marked `Required = true`. Marking an option required does not make it positional — a required value that is not a [selector](#arguments-options-and-selectors) should stay an option.

```csharp
[TigerCliOption("--mode", Required = true, Description = "Mode to use.")]
public PromptSmokeMode Mode { get; set; }
```

Optional options keep their property initializer when not provided:

```csharp
[TigerCliOption("--schema", Description = "Schema name.")]
public string Schema { get; set; } = "dbo";
```

### Conditional Required Options

Use `RequiredWhenOption` and `RequiredWhenValue` when an option is required only for a simple option state.

```csharp
public enum AuthenticationType
{
    Integrated,
    SqlPassword,
    EntraPassword
}

[TigerCliOption("--authentication", Description = "Authentication mode.")]
public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

[TigerCliOption("--username",
    Description = "SQL login username.",
    RequiredWhenOption = "--authentication",
    RequiredWhenValue = "SqlPassword",
    PromptWhenOption = "--authentication",
    PromptWhenValue = "SqlPassword",
    MinLength = 1)]
public string? Username { get; set; }
```

Use `RequiredWhenValueIn` for multiple accepted controlling values and `RequiredWhenValueNotIn` for every value except the listed values:

```csharp
[TigerCliOption("--username",
    Description = "SQL login username.",
    RequiredWhenOption = "--authentication",
    RequiredWhenValueIn = new[] { "SqlPassword", "EntraPassword" },
    PromptWhenOption = "--authentication",
    PromptWhenValueIn = new[] { "SqlPassword", "EntraPassword" },
    MinLength = 1)]
public string? Username { get; set; }
```

The condition references another option by alias or property name. Values are compared against the final bound option value. Enum values match by enum member name, bool values match `true`/`false`, strings use ordinal comparison, and other values compare their string form ordinal-ignore-case. `RequiredWhenValue`, `RequiredWhenValueIn`, and `RequiredWhenValueNotIn` are ORed within the required family. Null or empty `In`/`NotIn` arrays are ignored.

`RequiredWhen...` adds a required case; it does not weaken `Required = true`.

### Conditional Prompting

Use `PromptWhenOption` and `PromptWhenValue` to skip prompting unless a simple option condition is active.

```csharp
[TigerCliOption("--password",
    Description = "SQL login password.",
    Promptable = TigerCliPromptable.Normal,
    RequiredWhenOption = "--authentication",
    RequiredWhenValue = "SqlPassword",
    PromptWhenOption = "--authentication",
    PromptWhenValue = "SqlPassword",
    MinLength = 1)]
public string? Password { get; set; }
```

Use `PromptWhenValueIn` and `PromptWhenValueNotIn` the same way for prompt gating. `PromptWhenValue`, `PromptWhenValueIn`, and `PromptWhenValueNotIn` are ORed within the prompt family.

TigerCli resolves simple option dependencies before evaluating dependent prompt conditions. If `--authentication` is itself prompted and the selected value is `SqlPassword`, dependent values such as `--username` and `--password` are then re-evaluated and prompted when eligible. If the final controlling value is `Integrated`, those dependent prompts are skipped. `RequiredWhenOption` and `PromptWhenOption` automatically imply ordering dependencies; you do not need to repeat them with `DependsOnOption`.

If the prompt condition is false, TigerCli does not prompt for that option or call its provider. If it is true, normal interaction policy still applies: `--non-interactive` prevents prompting, `Promptable = TigerCliPromptable.No` prevents prompting, and the effective prompt mode still controls omitted `Promptable`.

Use `DependsOnOption` or `DependsOnOptions` for ordering dependencies that are not already expressed by `RequiredWhen...` or `PromptWhen...`, especially provider-backed options:

```csharp
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

`DependsOnOption` and `DependsOnOptions` only affect prompt/provider ordering. They do not make the dependency required, do not make it promptable, and do not validate either option by themselves.

Cycles between option prompt dependencies are unsupported and fail with a framework validation error.

### Secret And Prompt-Only Options

Use `Secret = true` for automatic string prompts that must mask rendered input. For passwords and similar secrets, also use `AllowCommandLineValue = false` so argv values are rejected before the handler receives them.

```csharp
[TigerCliOption("--password",
    Description = "SQL login password.",
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

Command-line passwords are intentionally rejected because argv can leak through process lists, shell history, logs, and automation output. Help output documents prompt-only secrets under `Prompted values:` instead of `Options:`, using the prompted value name rather than the command-line token:

```text
Prompted values:
  password
      SQL login password.
      Secret value; prompted when required.
      Cannot be supplied on the command line.
```

`Secret` does not by itself reject command-line values. Use both `Secret = true` and `AllowCommandLineValue = false` for password-style inputs.

Generated help shows defaults only when TigerCli has a useful value to show:

- Required options do not show defaults.
- Secret options do not show defaults.
- Non-nullable `bool` switches do not show `Default: False`.
- Non-empty string defaults may be shown.
- Enum defaults may be shown.
- Collection defaults are not shown.
- Defaults that are forbidden by `ForbiddenValues` are not shown.

## Promptability

Both `[TigerCliArgument]` and `[TigerCliOption]` support `Promptable`. Promptability and prompt order are one concept:

- `TigerCliPromptable.No` never prompts.
- `TigerCliPromptable.First` prompts early when prompting applies.
- `TigerCliPromptable.Normal` uses normal prompt order when prompting applies.
- `TigerCliPromptable.Last` prompts late when prompting applies.
- Omitting `Promptable` uses the effective prompt mode in normal prompt order.

```csharp
[TigerCliArgument(0, Name = "connection", Promptable = TigerCliPromptable.Normal)]
public string ConnectionName { get; set; } = string.Empty;

[TigerCliOption("--language", Promptable = TigerCliPromptable.Normal, Description = "Language.")]
public string? Language { get; set; }

[TigerCliOption("--dry-run", Promptable = TigerCliPromptable.No, Description = "Preview changes.")]
public bool DryRun { get; set; }
```

The effective prompt mode can be configured at the framework default, app, command group, or command level:

- `TigerCliPromptMode.No` does not prompt missing values by default.
- `TigerCliPromptMode.RequiredOnly` prompts missing required values by default. This is the framework default.
- `TigerCliPromptMode.Yes` may also prompt optional values when TigerCli can produce a prompt.

Inheritance is app default, then command group, then command. More specific configuration wins.

`First`, `Normal`, and `Last` opt a member into prompting when interaction is allowed; the value only controls relative order. `Last` does not force a prompt if prompt mode, `PromptWhen`, or `--non-interactive` prevents it.

Promptability never overrides `--non-interactive`. In non-interactive mode, TigerCli does not render prompts and does not call providers to build prompt choices. Providers can still be called to validate supplied values — see [provider validation of supplied values](prompting-and-providers.md#provider-validation-of-supplied-values).

Provider-backed arguments and options also support `AutoSelectSingleChoice = true`. This leaves the default confirmation behavior unchanged, but lets that member skip the select prompt when its provider produces exactly one selectable outcome after optional `(None)` handling is applied. For optional nullable provider-backed options, `(None)` counts as a selectable outcome, so one real provider choice plus `(None)` still shows the prompt.

For promptable boolean choices where “missing” and `false` are different states, avoid non-nullable `bool`; use `bool?` or an enum. A non-nullable bool switch is `false` when absent and cannot express “not supplied.”

For deeper prompting behavior, see [prompting and providers](prompting-and-providers.md) and [command processing prompting](../design/command-processing-prompting.md).

## Binding And Validation Timing

TigerCli handles command input in this order:

```text
parse command path
parse provided positional arguments and options
reject argv values for prompt-only options
bind provided values
prompt missing values when allowed
run framework validation
run settings validation
execute handler
```

Framework validation covers parser errors, missing required arguments, missing required options, invalid scalar conversions, forbidden option values, and framework-owned constraints. It also validates provider-backed values: a supplied value for a provider-backed option, or for an argument with an explicit `Provider`, must match one of the provider's choices unless the member sets `ValidateAgainstProvider = false` — see [provider validation of supplied values](prompting-and-providers.md#provider-validation-of-supplied-values).

For `int` arguments and scalar options, `MinValue` / `MaxValue` add integer bounds. `MinValueProvider` / `MaxValueProvider` resolve bounds from the existing named provider registry; each provider must return exactly one int-compatible key. When prompting is enabled, missing required `int` arguments are prompted with text input and invalid integers or out-of-range values are shown as inline validation hints.

Use `ForbiddenValues` to reject sentinel enum values and keep them out of help:

```csharp
public enum Mode
{
    Unspecified,
    Simple,
    Advanced
}

[TigerCliOption("--mode",
    Required = true,
    ForbiddenValues = new object[] { Mode.Unspecified },
    Description = "Mode to use.")]
public Mode Mode { get; set; } = Mode.Unspecified;
```

Use `[TigerCliExactlyOneOf]` for mutually exclusive option groups:

```csharp
[TigerCliExactlyOneOf(nameof(FilePath), nameof(Query),
    Description = "Provide either a file or a query, but not both.")]
public sealed class RunSettings : TigerCliSettings
{
    [TigerCliOption("-f|--file", Description = "Path to the SQL script file.")]
    public string? FilePath { get; set; }

    [TigerCliOption("-q|--query", ValueName = "sql", Description = "Inline SQL query.")]
    public string? Query { get; set; }
}
```

Custom validation belongs in `TigerCliSettings.Validate()`:

```csharp
public override TigerCliValidationResult Validate()
{
    return Schema.Length <= 128
        ? TigerCliValidationResult.Success()
        : TigerCliValidationResult.Error("Schema name is too long.");
}
```

Prompting fills missing values before validation. It does not replace validation.

## Enums And Flags

Enum options parse from enum member names, case-insensitively:

```csharp
public enum PromptSmokeMode
{
    Fast,
    Normal,
    Careful
}

[TigerCliOption("--mode", Required = true, Description = "Mode to use.")]
public PromptSmokeMode Mode { get; set; }
```

```bash
tool --mode Normal
tool --mode normal
```

Localized enum labels are display-only. They do not become accepted command-line values.

Use `TigerTextAttribute` to localize enum labels and descriptions used by prompts and exit-code help:

```csharp
using ItTiger.Core;

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
```

For `[Flags]` enums, parser-driven prompts show selectable non-zero single-bit values. Composite members such as `All = Read | Write | Execute` are not shown as separate prompt rows.

Command-line parsing still uses enum member names. See [localization](localization.md) for enum text localization and [prompting and providers](prompting-and-providers.md) for prompt behavior.

## Framework-Owned Options

TigerCli owns several framework options. They are removed before command settings binding and should not be defined as app options.

| Option | Purpose |
|---|---|
| `-h`, `--help` | Show root or command help. |
| `--version` | Show the short application version when the app calls `SetVersion(...)` or `UseAssemblyMetadata(...)` with version output enabled. |
| `--version-full` | Show the full/product application version when version output is enabled. |
| `--help-errors` | Show documented exit-code help when an exit-code enum is configured. |
| `--non-interactive` | Disable prompting for the current run. |
| `--culture <culture>` | Select the active framework UI culture for the current run. `--culture=pl-PL` also works. |

`--culture` is available only for cultures the app supports through `SetSupportedCultures(...)` and `SetDefaultCulture(...)`. See [localization](localization.md).

Exit-code help is documented in [exit codes](exit-codes.md).

## Common Mistakes

- Do not put options before required positionals. TigerCli expects `<command-path> <positionals> [options]`.
- Do not turn a required value into a positional argument just because it is required. Positionals are for [selectors](#arguments-options-and-selectors); required values that describe, configure, or change the object belong in options with `Required = true`.
- Do not rely on localized enum labels for CLI parsing. Use enum member names on the command line.
- Do not manually check `--non-interactive` in handlers. TigerCli owns interaction policy before handler execution.
- Use `TigerConsole.MarkupLine(...)` or `TigerConsole.MarkupErrorLine(...)` for app output instead of `Console.WriteLine(...)`, so tests and terminal rendering stay on the TigerCli path.
- Do not define framework-owned options such as `--help`, `--version`, `--version-full`, `--non-interactive`, or `--culture` on settings classes.
- Do not use short-option bundles such as `-abc` unless you have defined `-abc` as a single alias.

## More Details

- Build the full app shape with [command apps](command-apps.md).
- Understand positional ordering in [command processing positionals](../design/command-processing-positionals.md).
- Understand prompt policy in [command processing prompting](../design/command-processing-prompting.md).
- Add provider-backed choices with [prompting and providers](prompting-and-providers.md).
- Localize metadata and enum text with [localization](localization.md).
- Configure typed results with [exit codes](exit-codes.md).
