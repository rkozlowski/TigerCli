# App Contributions and Global Options

Reusable TigerCli-based libraries can contribute small pieces of app-wide configuration without
making TigerCli depend on the library's domain. The host app remains in control: it explicitly opts
in by registering an `ITigerCliAppContribution` with `TigerCliAppBuilder.AddContribution(...)`.

TigerCli contributions support optional string global options and help-only environment-variable
metadata. Global options are intended for app-wide execution configuration shared by multiple
commands, not for normal command input UX. Environment-variable registrations only advertise
variables that the reusable library already recognizes; TigerCli does not read or apply them.

## Ownership Boundary

The responsibilities are intentionally split:

- TigerCli registers contributed metadata, validates names, parses values, renders help, rejects
  repeated occurrences, and invokes the contribution before command binding.
- The reusable library owns the option name, description, semantics, validation, and the
  library-owned options or service that receives the value.
- The host app chooses whether to register the contribution and connects the same library-owned
  state to its command factories or services.

TigerCli does not need knowledge of the consuming library or its domain.

## Contribute Environment-Variable Help

A reusable library can add app-wide metadata to `--help-env` from the same contribution:

```csharp
public void Configure(TigerCliAppContributionBuilder builder)
{
    builder.AddEnvironmentVariable(
        "ACME_CONFIG",
        "Selects the Acme configuration file used by the library.");
}
```

This registration is documentation only. It does not call `Environment.GetEnvironmentVariable`,
define precedence, mutate contribution state, add an option, or participate in prompting. The
library remains responsible for reading and interpreting `ACME_CONFIG` in its own code.

The host app can register its own app-wide metadata with
`TigerCliAppBuilder.AddEnvironmentVariable(name, description)`. Command groups and commands use the
same method on `TigerCliCommandGroupBuilder` and `TigerCliCommandBuilder`; command `--help-env`
inherits app-wide and ancestor-group variables before adding command-local variables.

Names and descriptions must be non-empty, names cannot contain whitespace, and duplicate names in
one effective help scope are rejected deterministically. Framework-owned names such as
`TIGERCLI_THEME` and `NO_COLOR` cannot be registered again.

## Define a Contribution

Keep the resolved value in library-owned state. This avoids static storage and lets host command
factories receive the same options object explicitly:

```csharp
using ItTiger.TigerCli.Commands;

public sealed class AcmeCliOptions
{
    public string? ConfigurationFile { get; internal set; }
}

public sealed class AcmeCliContribution : ITigerCliAppContribution
{
    public AcmeCliOptions Options { get; } = new();

    public void Configure(TigerCliAppContributionBuilder builder)
    {
        builder.GlobalOptions.AddOptionalString(
            name: "--acme-config",
            valueName: "path",
            description: "Use an Acme configuration file.",
            apply: (context, value) =>
            {
                if (value is { Length: 0 })
                    return TigerCliValidationResult.Error("The Acme configuration path must not be empty.");

                Options.ConfigurationFile = value;
                return TigerCliValidationResult.Success();
            });
    }
}
```

The callback receives `null` when the option is absent. It also receives a
`TigerCliGlobalOptionContext` containing the resolved culture and interaction mode. Return
`TigerCliValidationResult.Error(...)` to stop the run cleanly before command settings are bound or
the handler is created.

## Opt In from a Host App

The host creates the contribution once, registers it, and supplies its state to commands through
the host's normal composition pattern:

```csharp
var acme = new AcmeCliContribution();

return TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .AddContribution(acme)
    .AddCommand("run", () => new RunCommand(acme.Options), "Runs the operation.")
    .Build();
```

The option is app-wide in meaning, but syntactically it is still an option. It follows TigerCli's
normal command-line shape:

```text
app <command-path> <positional-arguments> [options]
```

Write contributed global options after the command path and positional arguments, with the other
options:

```text
my-app run --acme-config settings.json
```

Contributed global options do not participate in or change command selection.

Framework-owned execution options follow the same rule. App-wide meaning does not make either kind
of option valid before a command path or before its required positionals. Root informational forms
such as `my-app --help` are separate requests, not a general pre-command option area.

`--acme-config=settings.json` is also accepted. When a value itself begins with `-`, use the
equals form so it cannot be mistaken for another option.

## Parsing and Apply Lifecycle

TigerCli configures all registered contributions during `Build()`. This is when invalid, duplicate,
reserved, and reliably detectable command-option collisions fail.

For a command run, TigerCli then:

1. recognizes an exact root informational form, when present;
2. resolves the command path from the leading command tokens;
3. extracts framework execution options and contributed global options from the command's options
   area, after its positionals;
4. resolves the interaction mode and any command-menu selection;
5. invokes each contributed option callback once, using `null` for an absent option;
6. binds, prompts for, and validates command settings; and
7. executes the handler.

Help renders contribution metadata but does not invoke apply callbacks. Contributed global options
never enter the prompt model, so interactive and `--non-interactive` runs resolve them identically.

Supplying a contributed global option without a value is an argument error. Supplying the same
option more than once is also an error; TigerCli does not choose a last value.

## Global Option Constraints

The contribution surface is intentionally narrow:

- optional strings only;
- one canonical long name beginning with `--`;
- no short names or aliases;
- CLI-only and never required;
- no prompts, providers, selectors, or file/folder pickers;
- no environment-variable lookup from global-option registration; and
- no generic `Add<T>()` registration.

Contributed globals are app-wide execution configuration. Continue to use `TigerCliOption` on
`TigerCliSettings` for command-owned inputs that participate in binding, prompting, providers, or
command-specific validation.
