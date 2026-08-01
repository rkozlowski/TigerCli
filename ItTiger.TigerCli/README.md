# ItTiger.TigerCli

TigerCli is an opinionated .NET CLI/TUI app framework for command-driven tools. It is intended for
developers building utilities, administration tools, developer tooling, and automation that should
work well both at a terminal and in scripts.

It combines command-line execution with semi-interactive prompts, command menus, structured output,
activity and progress UI, validation, app-provided choices, themes, and a predictable exit-code
policy. The same command model can prompt a person when values are missing or fail clearly under
`--non-interactive` when automation must not wait for input.

## Installation

TigerCli currently targets .NET 10.

```bash
dotnet add package ItTiger.TigerCli --version 0.9.0
```

## Minimal app

Delegate commands are the smallest way to create a working TigerCli app:

```csharp
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

return await TigerCliApp.CreateBuilder()
    .AddDefaultCommand(static () =>
    {
        TigerConsole.MarkupLine("[Key]Hello[/], [Accent]TigerCli[/]!");
        return TigerCliExitKind.Success;
    })
    .Build()
    .RunAsync(args);
```

This gives the tool TigerCli's normal command pipeline, framework options, help behavior, rendering,
and exit handling without requiring settings or handler classes.

## Full command model

Delegate commands are intended for tiny tools, demos, smoke apps, and simple automation. For a
larger application, the promoted model separates typed settings, a command handler, and builder
registration:

```csharp
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

return await TigerCliApp.CreateBuilder()
    .SetDefaultCommand<GreetCommand>()
    .Build()
    .RunAsync(args);

public sealed class GreetSettings : TigerCliSettings
{
    [TigerCliOption("--name", Required = true, Description = "Name to greet.")]
    public string Name { get; set; } = string.Empty;
}

public sealed class GreetCommand : TigerCliAsyncCommandHandler<GreetSettings>
{
    public override Task<int> ExecuteAsync(GreetSettings settings)
    {
        TigerConsole.MarkupLine(settings.E("[Key]Hello[/], {0}!", settings.Name));
        return Task.FromResult(0);
    }
}
```

The full model is where typed options and arguments, prompts, providers, validation, selectors,
command groups, localization, reusable handlers, and application-specific exit codes belong.

## What TigerCli provides

- Command applications with default commands, named commands, command groups, and optional command menus
- Prompted options with text, confirm, select, and multi-select controls
- Application providers for dynamic, validated choices
- Folder and file prompts through `TigerCliFolderSelect`, `TigerCliFileOpen`, and `TigerCliFileSave`
- Structured terminal output with `CliList`, `CliDetails`, `CliTable`, grids, markup, and semantic styles
- Activity dialogs, spinners, cancellation, and progress bars
- Built-in themes plus console, HTML, and test-friendly rendering sinks
- Automation-safe `--non-interactive` behavior with no surprise prompts
- Configurable exit-code policy and portable `TigerCliExitKind` outcomes
- App-level test hosting and deterministic TUI test infrastructure

## Related packages

- [ItTiger.TigerCli](https://www.nuget.org/packages/ItTiger.TigerCli/) — this package: command apps,
  prompting, TUI controls, rendering, exit policy, and testing support.
- [ItTiger.Core](https://www.nuget.org/packages/ItTiger.Core/) — shared core helpers used by TigerCli;
  it is installed transitively with this package.
- [ItTiger.TigerCli.PngSink](https://www.nuget.org/packages/ItTiger.TigerCli.PngSink/) — optional
  deterministic PNG rendering for documentation artifacts, examples, and visual review.

## Links

- [Project page](https://www.ittiger.net/projects/tigercli/)
- [Documentation](https://github.com/rkozlowski/TigerCli/blob/main/docs/README.md)
- [API reference](https://rkozlowski.github.io/TigerCli/index.html)
- [Examples and getting started](https://github.com/rkozlowski/TigerCli/blob/main/docs/getting-started.md)
- [GitHub repository](https://github.com/rkozlowski/TigerCli)
- [MIT license](https://github.com/rkozlowski/TigerCli/blob/main/LICENSE)

An open-source project by [IT Tiger](https://www.ittiger.net/).
