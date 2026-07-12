# Command Apps

TigerCli command apps are command-line applications built around a small, predictable shape:

```text
app <command-path> <positional-arguments> [options]
```

The framework owns parsing, binding, help, framework errors, interaction policy, localization, and command dispatch. Your app owns command registration, settings metadata, command behavior, and application output.

## Overview

A TigerCli app is built from:

- `TigerCliAppBuilder` for application setup
- settings classes that derive from `TigerCliSettings`
- async command handlers
- `[TigerCliArgument]` and `[TigerCliOption]` metadata
- generated help and other framework-owned behavior

The usual flow is:

```text
Resolve command path -> bind arguments and options -> prompt when allowed -> validate -> execute
```

That keeps automation safe while still allowing semi-interactive commands to help a human fill missing values.

## Recommended App Factory Pattern

Put app construction behind a factory method and use that same factory from `Program.cs` and tests.

```csharp
using ItTiger.TigerCli.Commands;

public static class MyApp
{
    public static TigerCliApp Create()
    {
        return TigerCliApp.CreateBuilder()
            .UseAssemblyMetadata(typeof(MyApp).Assembly)
            .SetDefaultCommand<DefaultCommand>()
            .AddCommand<EchoCommand>("echo", "Echoes a message.")
            .AddCommandGroup("projects", group => group
                .AddCommand<ProjectsSpAddCommand>("sp-add", "Adds a stored procedure to a project."))
            .Build();
    }
}
```

The app's name, description, and product metadata come from the project file (`<AssemblyName>`, `<Description>`, …) and are imported with `UseAssemblyMetadata(...)` — the preferred pattern for a normal executable app. See [App Metadata](#app-metadata); prefer it to setting the name in code, which duplicates the project file and can drift from the real executable name.

`Program.cs` stays small:

```csharp
return await MyApp.Create().RunAsync(args);
```

This pattern matters because `TigerCliAppTestHost` can run the real app pipeline from tests. Command registration, localization, prompts, and exit-code policy stay identical between production and test runs.

## App Metadata

For a normal executable app, application identity and product metadata belong in the **project file**, not in builder calls. `<AssemblyName>` controls the produced executable name and is the natural source for the TigerCli command/application name, so define the metadata once in the CLI `.csproj` (or a shared props file) and import it with `UseAssemblyMetadata(...)`. That keeps a single source of truth and avoids duplication:

```csharp
return TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(TigerWrapApp).Assembly)
    .SetDefaultCommand<WrapCommand>()
    .Build();
```

```xml
<PropertyGroup>
  <AssemblyName>tiger-wrap</AssemblyName>
  <Product>TigerWrap</Product>
  <Description>A wrapping tool.</Description>
  <Version>1.2.3</Version>
  <Copyright>Copyright (c) IT Tiger</Copyright>
  <RepositoryUrl>https://github.com/rkozlowski/TigerWrap/</RepositoryUrl>
  <PackageProjectUrl>https://www.ittiger.net/projects/tigerwrap/</PackageProjectUrl>
</PropertyGroup>
```

`UseAssemblyMetadata()` reads app metadata from an assembly and enables `--version` by default. Use `UseAssemblyMetadata(enableVersion: false)` to read metadata without enabling the framework-owned version option. `UseAssemblyMetadata(assembly, ...)` reads from a provided `Assembly`; `UseAssemblyMetadata<TMarker>(...)` reads from `typeof(TMarker).Assembly`. If the app factory type is a static class, use the explicit assembly overload — static classes cannot be used as generic type arguments.

### Version Output

Supplying a version (through `UseAssemblyMetadata(...)` or an explicit `SetVersion(...)`) opts the app into the framework-owned global version options:

```bash
tiger-wrap --version
```

```text
TigerWrap version 1.2.3
```

`Version` is the short user-facing version. `ProductVersion` is the full/product/informational version. TigerCli uses the label `product version` for the full value because it matches Windows application and file details terminology.

```text
--version       -> TigerWrap version 0.5.0
--version-full  -> TigerWrap product version 0.5.0+20260614.165940
```

If the two differ, TigerCli prints both lines with the short version first:

```text
TigerWrap version 0.5.0
TigerWrap product version 0.5.0+20260614.165940
```

### Assembly Metadata Read By TigerCli

TigerCli reads these assembly attributes:

| Metadata | Source |
|---|---|
| Application name | assembly name, then normal fallback |
| Display name | `AssemblyProductAttribute`, then `AssemblyTitleAttribute`, then the application name |
| Description | `AssemblyDescriptionAttribute` |
| Version | simple version derived from `AssemblyInformationalVersionAttribute` when possible, then assembly name version, then `unknown` |
| ProductVersion | `AssemblyInformationalVersionAttribute` as-is, then `Version` |
| Copyright | `AssemblyCopyrightAttribute` |

Assembly link metadata is read from `AssemblyMetadataAttribute` keys. `Website` maps to the localized `Website` label. `Documentation`, `ProjectUrl`, and `PackageProjectUrl` map to `Documentation`. `Repository`, `RepositoryUrl`, `SourceCode`, and `SourceCodeUrl` map to `Source code`.

### Overriding Assembly Metadata

The manual builder setters — `SetApplicationName(...)`, `SetDisplayName(...)`, `AddDescription(...)`, `SetVersion(...)`, `AddCopyright(...)`, and the standard link helpers — are **overrides**, not the default app-authoring pattern. Assembly metadata acts as defaults, and an explicit builder call wins when present:

```csharp
TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(TmfCliApp).Assembly)
    .SetDisplayName("Tiger Media Flow")
    .SetVersion("0.1.0-preview")
    .AddDocumentation("https://www.ittiger.net/projects/tiger-media-flow/");
```

Reach for a manual setter when the CLI value must **intentionally differ** from the assembly/package metadata, or in tests and small synthetic examples. For instance, if two sibling projects ship distinct assemblies but deliberately share one command name, the name cannot come from `<AssemblyName>` (which is unique per project), so `SetApplicationName(...)` is the correct override. Using a manual setter where the value already matches the project file is duplication — and `SetApplicationName(...)` in particular can silently drift from the real executable name.

For shared values across projects, keep common SDK/MSBuild properties in a shared props file such as `Version.props` or `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Version>0.5.0</Version>
    <AssemblyVersion>0.5.0.0</AssemblyVersion>
    <FileVersion>0.5.0.0</FileVersion>
    <InformationalVersion>$(Version)+$(UtcTimestamp)</InformationalVersion>

    <Company>IT Tiger</Company>
    <Copyright>Copyright (c) IT Tiger</Copyright>
    <Authors>IT Tiger</Authors>
    <RepositoryUrl>https://github.com/rkozlowski/TigerMediaFlow</RepositoryUrl>
    <PackageProjectUrl>https://www.ittiger.net/projects/tiger-media-flow/</PackageProjectUrl>
  </PropertyGroup>
</Project>
```

Keep project-specific values in the CLI `.csproj`:

```xml
<PropertyGroup>
  <AssemblyName>tiger-tmf</AssemblyName>
  <Title>Tiger Media Flow</Title>
  <Product>Tiger Media Flow</Product>
  <Description>Tiger Media Flow - trust-first media ingestion.</Description>
</PropertyGroup>
```

Package/project properties are not always available at runtime unless they are emitted as assembly metadata. For runtime links, add explicit assembly metadata attributes:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute">
    <_Parameter1>Documentation</_Parameter1>
    <_Parameter2>$(PackageProjectUrl)</_Parameter2>
  </AssemblyAttribute>

  <AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute">
    <_Parameter1>RepositoryUrl</_Parameter1>
    <_Parameter2>$(RepositoryUrl)</_Parameter2>
  </AssemblyAttribute>
</ItemGroup>
```

With that setup, app startup can usually avoid duplicate metadata:

```csharp
return TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(TmfCliApp).Assembly)
    .AddCommandGroup(...)
    .Build();
```

Runtime assembly APIs do not expose every original MSBuild package property. For SDK-style projects, TigerCli derives the short version from the informational version prefix before `+` when available. For example, `InformationalVersion` `0.5.0+20260614.165940` gives `--version` `0.5.0` and `--version-full` `0.5.0+20260614.165940`.

`AddCopyright(...)` and application links are rendered as a simple help footer. `AddLink(label, url)` stores a custom link label and visible/copyable URL. The convenience methods add standard framework labels:

| Method | Help label |
|---|---|
| `AddWebsite(url)` | `Website` |
| `AddRepository(url)` | `Source code` |
| `AddDocumentation(url)` | `Documentation` |

Those convenience labels are localized with the active TigerCli culture, including standard links populated from assembly metadata. Custom labels passed to `AddLink(...)`, URLs, display names, explicit versions, copyright text, and assembly-provided values are app-owned text and are preserved as provided.

## Terminal Window Title

By default, TigerCli sets the terminal window title at app run start. The title is resolved from the display name, then the application name, then `app`:

```csharp
// Display name "Tiger Media Flow" comes from the project's <Product>; see App Metadata above.
var app = TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(TmfApp).Assembly)
    .Build();
```

For that app, the base terminal title is:

```text
Tiger Media Flow
```

Title writes go through the active render sink. ANSI terminals receive an OSC window-title sequence from `AnsiSink`; non-terminal or plain sinks do not emit terminal escape sequences. `HtmlSink` and the line-capture sinks store the latest title for tests and diagnostics.

Disable all framework title writes with:

```csharp
TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(TmfApp).Assembly)
    .DisableTerminalTitleManagement();
```

TigerCli does not try to query and restore an unknown previous terminal title; that is not portable across terminals.

## Minimal Default Command

A command starts with a settings class. The class derives from `TigerCliSettings`; properties become command input when they are decorated with TigerCli attributes.

```csharp
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

public enum ToolExitCode
{
    Ok = 0,
    InvalidArguments = 2
}

public sealed class GreetSettings : TigerCliSettings
{
    [TigerCliOption("-n|--name", Description = "Name to greet.")]
    public string Name { get; set; } = "World";
}

public sealed class GreetCommand
    : TigerCliAsyncCommandHandler<GreetSettings, ToolExitCode>
{
    public override Task<ToolExitCode> ExecuteAsync(GreetSettings settings)
    {
        TigerConsole.MarkupLine(settings.E("Hello, [White]{0}[/]!", settings.Name));
        return Task.FromResult(ToolExitCode.Ok);
    }
}
```

Then register it as the default command:

```csharp
var app = TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(GreetApp).Assembly)
    .SetDefaultCommand<GreetCommand>()
    .Build();
```

The command can run without a command path (the executable name — `greet` — comes from `<AssemblyName>`):

```bash
greet --name Alice
greet --help
```

For a public default-command operation sample, see [Folder Copy](../examples/folder-copy.md). For the primary named-command app shape (`list`/`show`, providers, command menu, and typed exit codes), start with [ROI Cities](../getting-started.md).

## Adding Commands

Register named commands with `AddCommand`. A top-level command name is a single token. Multi-token command paths are produced only by command groups, which own their child commands — see [Command Groups](#command-groups).

```csharp
return TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .SetDefaultCommand<DefaultCommand>()
    .AddCommand<EchoCommand>("echo", "Echoes a message.")
    .AddCommandGroup("projects", group => group
        .AddCommand<ProjectsSpAddCommand>("sp-add", "Adds a stored procedure to a project."))
    .Build();
```

The group above owns the command path `projects sp-add`. TigerCli resolves the first non-option tokens after the app name as the command path; when paths share a prefix, the longest matching path wins.

```bash
my-tool echo --message "hello"
my-tool projects sp-add local Billing --schema dbo
```

A multi-token `name` passed to `AddCommand` (at app or group scope) throws — commands live in one clear tree, and every path prefix is an explicit group. After the command path is resolved, remaining non-option tokens are positional arguments for that command.

Commands can also configure terminal-title metadata through the command builder:

```csharp
return TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(TmfApp).Assembly)
    .AddCommand<ScanCommand>("scan", command => command.AppendTitle("Scanning"))
    .AddCommand<ShortScanCommand>("quick-scan", command => command.SetTitle("TMF Scan"))
    .Build();
```

`AppendTitle("Scanning")` produces:

```text
Tiger Media Flow - Scanning
```

`SetTitle("TMF Scan")` replaces the app title for that command:

```text
TMF Scan
```

`AppendTitle` and `SetTitle` are mutually exclusive on the same command registration. Title strings are plain terminal text, not TigerCli markup.

## Command Construction And Dependencies

By default every command is created with its **parameterless constructor** — there is no DI container. For commands that need injected dependencies (a store, a service, options), register them with a **factory** instead. Factory overloads exist at all three levels and share the same lifetime semantics: the factory is invoked once, only when that command actually runs.

| Registration | Parameterless | Factory |
|---|---|---|
| Default / top-level command | `SetDefaultCommand<T>()` | `SetDefaultCommand(() => new T(dep))` |
| Top-level named command | `AddCommand<T>("run")` | `AddCommand("run", () => new T(dep))` |
| Group child command | `group.AddCommand<T>("add")` | `group.AddCommand("add", () => new T(dep))` |

```csharp
var store = new ConnectionStore(...);

return TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(SqlCmdApp).Assembly)
    // Default command receives the store through its constructor.
    .SetDefaultCommand(() => new QueryCommand(store))
    // Top-level named command, same store.
    .AddCommand("run", () => new RunCommand(store))
    .AddCommandGroup("connections", group =>
    {
        // Group children take dependencies through the group factory overload.
        group.AddCommand("add", () => new AddConnectionCommand(store));
        group.AddCommand("edit", () => new EditConnectionCommand(store));
    })
    .Build();
```

What this means for composition and testing:

- **No service container.** Composition is plain factory closures. The app builder never resolves types from a container; it either calls `new T()` or your factory.
- **Testability.** A test can build the real app object (see [Testing Command Apps](#testing-command-apps)) and pass a fake/in-memory dependency through the same factory the production `Program.cs` uses.
- **Lifetime.** The factory runs per execution of that command, after parsing/help resolution — so requesting `--help` never constructs the handler or its dependencies.

Recommended patterns:

- **Prefer the factory overload** at the matching scope whenever a command needs constructor dependencies — default/top-level commands and group children all support it.
- For a **default or top-level command** with dependencies, use `SetDefaultCommand(() => ...)` / `AddCommand(name, () => ...)`.
- For **group commands**, use `group.AddCommand(name, () => ...)`; reusable command libraries expose a static helper that takes a `TigerCliCommandGroupBuilder` and wires its commands through factories (see [Reusable Command Libraries](#reusable-command-libraries)).
- An **ambient/internal app-composition seam** (static or internal app state read inside a parameterless handler) is only a fallback for code that cannot use a factory. Prefer an explicit factory; it keeps the dependency visible at registration and avoids hidden global state.

## Command Groups

A command group is a command-path prefix that owns a set of child commands. Groups are generic: TigerCli attaches no meaning to a group beyond "these commands share a path prefix and a help entry." A command registered on a group behaves exactly like a normal command whose path is the group name followed by the command name — parsing, binding, prompting, validation, and execution all stay on the same pipeline.

```csharp
builder.AddCommandGroup("widgets", group =>
{
    group.SetDescription("Manage widgets.");
    group.AddCommand<ListWidgetsCommand>("list", "List widgets.");
    group.AddCommand<AddWidgetCommand>("add", "Add a widget.");
});
```

This registers the command paths `widgets list` and `widgets add`:

```bash
my-tool widgets list
my-tool widgets add my-widget --color blue
```

The group name is chosen by the consuming app. The same commands can be mounted under any prefix the app prefers.

### Nested Groups

A group can own subgroups as well as commands. Call `AddCommandGroup` on a `TigerCliCommandGroupBuilder` to nest a subgroup; its name is relative to the parent, and the resolved path is the parent's path followed by the subgroup name. Nesting is a help/menu and configuration convenience — commands still resolve, bind, prompt, validate, and execute on the same flat, path-based pipeline.

```csharp
builder.AddCommandGroup("projects", projects =>
{
    projects.SetDescription("Manage projects.");
    projects.AddCommand<ListProjectsCommand>("list", "List projects.");
    projects.AddCommand<ShowProjectCommand>("show", "Show a project.");

    projects.AddCommandGroup("sp", sp =>
    {
        sp.SetDescription("Manage stored procedures.");
        sp.AddCommand<AddSpCommand>("add", "Add a stored procedure.");
        sp.AddCommand<RemoveSpCommand>("remove", "Remove a stored procedure.");
    });
});
```

This registers the paths `projects list`, `projects show`, `projects sp add`, and `projects sp remove`:

```bash
my-tool projects list
my-tool projects sp add my-proc
```

Prefer nesting over multi-token group names: a group name such as `"projects sp"` places the description on a fake flat entry and shows two unrelated top-level groups, whereas the subgroup owns its own description and appears under its real parent. Group-level prompt mode, providers, and command-menu opinion cascade to descendants, with the nearest group winning; a provider defined on `projects` is available to a command under `projects sp` unless the subgroup redefines the same key.

Top-level help lists only top-level groups. Each group's own help lists that group's immediate child commands and immediate subgroups (by their relative name); deeper commands are represented by the subgroup entry, and `my-tool projects sp --help` lists the subgroup's own children. The command menu mirrors this: selecting a group opens its submenu, and selecting a subgroup opens a deeper submenu.

### Reusable Command Libraries

A reusable library can define a group's commands once and let any app mount them with a single call. The library exposes a static helper that takes a `TigerCliCommandGroupBuilder` and adds commands to it; the app picks the group name and supplies any services or options.

```csharp
// In the reusable library:
public static class WidgetCommands
{
    public static void Configure(
        TigerCliCommandGroupBuilder group,
        Action<WidgetCommandOptions>? configure = null)
    {
        var options = new WidgetCommandOptions();
        configure?.Invoke(options);

        group.AddCommand("list", () => new ListWidgetsCommand(options.WidgetService));
        group.AddCommand("add", () => new AddWidgetCommand(options.WidgetService));
    }
}

// In the consuming app:
builder.AddCommandGroup("widgets", group =>
{
    group.SetDescription("Manage widgets.");
    WidgetCommands.Configure(group, options => options.WidgetService = widgetService);
});
```

### Command Factories

The factory overload `AddCommand<TCommand>(string name, Func<TCommand> factory)` lets a command receive constructor dependencies. This is how reusable libraries pass services or options to their commands without requiring the consuming app to wire up a DI container. The non-factory overload `AddCommand<TCommand>(string name)` keeps the parameterless-constructor model. The same factory overloads also exist at app scope (`TigerCliAppBuilder.SetDefaultCommand(Func<T>)` and `AddCommand(string, Func<T>)`), so default and top-level commands can take dependencies too — see [Command Construction And Dependencies](#command-construction-and-dependencies).

### Help Behavior

Top-level help lists immediate entries only — single-token ungrouped commands and groups. A group shows as a single entry with its description; its child commands appear under the group's own help, not in the top-level list.

```text
Commands:
  widgets    Manage widgets.
```

The group's own help lists the group's immediate child commands:

```bash
my-tool widgets --help
```

```text
my-tool widgets
  Manage widgets.

Usage:
  my-tool widgets <command> [options]

Commands:
  list    List widgets.
  add     Add a widget.
```

Leaf command help works as usual:

```bash
my-tool widgets add --help
```

## Command Aliases

Aliases are short, root-level entry points into the existing command tree. They make common
workflows easier without changing command ownership: the real command stays where it is, and the
alias is just an alternate way to reach it.

```csharp
builder
    .AddCommandGroup("card", group => group
        .SetDescription("Manage cards.")
        .AddCommand<IngestCommand>("ingest", "Ingest a card."))
    .AddCommandAlias("import", "card ingest", alias =>
        alias.SetDescription("Import files from a registered card or source."));
```

With this, `my-tool import …` runs exactly the same as `my-tool card ingest …`.

Key rules:

- **Entry point only.** The alias owns no handler, settings, or providers. The target command owns
  parsing, prompting, providers, validation, prompt-mode inheritance, and execution — the alias falls
  straight through to the target's normal pipeline.
- **Commands resolve first.** Aliases are consulted only when no command path matches, and at
  `Build()` an alias path may not collide with any command path, group path, other alias, or the
  named command-menu command. The target must name an existing command (not a group). Violations
  throw.
- **Single token, root level.** The alias name is a single token (e.g. `import`, `register-card`)
  that must not start with `-`. Multi-token alias *paths* are not supported yet; the target path may
  be multi-token (`card ingest`).

### Presentation

An alias may own its own presentation, independent of the target:

- `SetDescription(...)` — shown in help and the menu; falls back to the target's description.
- `HideFromHelp()` — omits the alias from generated help (it still resolves and can still appear in
  the menu).
- `CommandMenu(CommandMenuMode mode)` — the alias's own menu eligibility (see below).

Top-level help lists aliases in their own **Aliases** section, each with a muted marker naming the
target so ownership is never confused:

```text
Aliases:
  import         Import files from a registered card or source.  → card ingest
  register-card  Register a card.                                → card register
```

`my-tool import --help` shows the alias identity and an `Alias for: card ingest` note, but the
arguments and options come from the target command:

```text
my-tool import
  Import files from a registered card or source.

Usage:
  my-tool import <card> [options]

  Alias for: card ingest
```

### Aliases And The Command Menu

An alias has its own menu eligibility chain — **app level → alias** — and does **not** inherit the
target's eligibility. This is deliberate: it lets you hide a command from the menu while still
offering a friendly alias for it.

```csharp
builder
    .UseCommandMenu(CommandMenuMode.Enabled)
    // Hidden from the menu as a command…
    .AddCommand<IngestCommand>("ingest", configure: c => c.CommandMenu(CommandMenuMode.Disabled))
    // …but reachable through a visible alias, which runs the same command.
    .AddCommandAlias("import", "ingest");
```

Selecting an alias in the menu runs its target through the normal prompt/validate/execute pipeline,
exactly like selecting the command directly.

## Arguments And Options

Settings classes describe command input with `[TigerCliArgument]` and `[TigerCliOption]`.

Positional arguments come after the command path and before options. Options come after positionals and may appear in any order relative to other options.

```csharp
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

This supports command lines like:

```bash
my-tool projects sp-add local Billing --schema dbo
```

For option aliases, value syntax, required/default behavior, repeated values, key-value options, and validation timing, see [arguments and options](arguments-and-options.md).

## Generated Help

TigerCli generates help from the builder and settings metadata.

For an app with command paths and arguments, help includes:

- usage
- registered commands
- positional arguments
- command options
- framework options
- an exit-code hint when documented exit codes are configured

Example shape:

```text
Usage:
  my-tool projects sp-add <connection> <project> [options]

Arguments:
  <connection>
      Connection name.

Options:
  --schema <value>
      Schema name.
  -h, --help
      Show help.

For a list of exit codes, use --help-errors.
```

App-provided descriptions may contain TigerCli markup. Dynamic values are escaped before rendering. For the full escaping and trust model, see [help rendering trust model](../reference/help-rendering-trust-model.md).

## Async Command Handlers Only

TigerCli supports async command handlers.

Use one of these shapes:

```csharp
public sealed class RawCommand : TigerCliAsyncCommandHandler<RawSettings>
{
    public override Task<int> ExecuteAsync(RawSettings settings)
    {
        return Task.FromResult(0);
    }
}
```

```csharp
public sealed class TypedCommand
    : TigerCliAsyncCommandHandler<TypedSettings, ToolExitCode>
{
    public override Task<ToolExitCode> ExecuteAsync(TypedSettings settings)
    {
        return Task.FromResult(ToolExitCode.Ok);
    }
}
```

Synchronous command logic can return `Task.FromResult(...)`. Keeping all command handlers async keeps execution, prompting, and app testing on one model.

## Exit Codes

Small tools can return raw integer exit codes. Larger tools should usually define one application-wide enum and return typed values from commands.

```csharp
using ItTiger.Core;

[TigerText("Tool exit codes")]
public enum ToolExitCode
{
    [TigerText("OK", Description = "Operation completed successfully.")]
    Ok = 0,

    [TigerText("Invalid arguments", Description = "Invalid command-line arguments.")]
    InvalidArguments = 2,

    [TigerText("Unhandled exception", Description = "Unhandled exception.")]
    UnhandledException = 10
}
```

Configure framework-owned failures through the builder:

```csharp
var app = TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .UseExitCodes<ToolExitCode>(ToolExitCode.Ok, ToolExitCode.UnhandledException)
        .ExitKind(TigerCliExitKind.InvalidArguments, ToolExitCode.InvalidArguments)
    .Build();
```

See [exit codes](exit-codes.md) for the full policy model and `--help-errors` behavior.

## Localization In Command Apps

English-only apps can omit localization setup. Apps that support more cultures should configure them explicitly.

```csharp
return TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .SetDefaultCulture("en-US")
    .SetSupportedCultures("en-US", "pl-PL")
    .UseAppResources(AppStrings.ResourceManager)
    // A localized description is an override of the assembly description: the resource key wins
    // for the active culture, and the English text is the fallback.
    .AddDescription("Runs project maintenance tasks.", resourceKey: "App_Description")
    .AddCommand<EchoCommand>("echo", "Echoes a message.",
        descriptionResourceKey: "Cmd_Echo_Description")
    .Build();
```

Settings expose the active run culture and app-resource helpers:

- `settings.T(...)` for source-text lookup using app resources and the active run culture
- `settings.F(...)` for localized formatted plain text
- `settings.E(...)` for localized markup output with escaped formatted arguments

Use `TigerTextAttribute` for localized enum labels and descriptions, including exit-code enums and enum prompt choices.

Do not duplicate the localization rules in command docs. See [localization](localization.md) for culture selection, app resources, `--culture`, metadata localization, `settings.T/F/E`, and enum text.

## Interaction Modes And Prompting

TigerCli is script-safe first. Missing required input fails in non-interactive mode and may be prompted only when semi-interactive policy allows it.

```csharp
using ItTiger.TigerCli.Enums;

var app = TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .SetInteractionMode(TigerCliInteractionMode.SemiInteractive)
    .SetDefaultPromptMode(TigerCliPromptMode.RequiredOnly)
    .Build();
```

A member can opt in or out:

```csharp
[TigerCliOption("--language", Promptable = TigerCliPromptable.Normal, Description = "Language.")]
public string? Language { get; set; }

[TigerCliOption("--dry-run", Promptable = TigerCliPromptable.No, Description = "Preview changes.")]
public bool DryRun { get; set; }
```

Provider-backed prompts let a string setting be selected from app-owned choices. Register named providers at the app, group, or command scope, then reference the provider from the settings metadata:

```csharp
using ItTiger.TigerCli.Primitives;

builder.ConfigureProviders(providers =>
{
    providers.Add<string>("connections", ctx =>
    [
        new OptionItem<string>("local", "Local"),
        new OptionItem<string>("demo", "Demo")
    ]);
});
```

Provider keys should be stable command values. Labels are display text and may be localized.
`ConfigurePrompts<TSettings>(...)` remains available for older property-targeted registrations, but prefer `ConfigureProviders(...)`, `AddProvider(...)`, and `AddAsyncProvider(...)` for new code.

#### Value matching

When a value is supplied non-interactively (or an existing/default value is validated), TigerCli matches it against the provider's choices by key **or** label. The `ValueMatching` preset on `[TigerCliOption]` / `[TigerCliArgument]` controls the comparison, and applies identically to single-select options, provider-backed arguments, and `[TigerCliMultiSelect]` collections:

| Preset | Behavior |
|---|---|
| `Default` (unset) | string keys/labels match **case-insensitively**; no path normalization |
| `Exact` | case-sensitive string matching |
| `IgnoreCase` | explicit case-insensitive matching (same as `Default` for strings) |
| `FileSystemPath` | path-aware matching for path-like keys (see below) |

In every case the **bound value is the provider's canonical key**, not the raw user input — so a provider key `K:\` supplied as `k:` still binds `K:\`. This lets provider keys stay canonical app values.

`FileSystemPath` is for path-like provider keys. On Windows it is case-insensitive and treats slash direction (`/` vs `\`), trailing separators, and bare-drive spelling as insignificant, so a provider key `K:\` matches `K:`, `k:`, `K:\`, and `k:\`, and `K:\Xxx\` matches `k:/xxx`, `K:\Xxx`, etc. It deliberately does **not** widen ambiguous forms: a drive-relative `K:xxx` never matches the absolute `K:\xxx`, and a rooted-without-drive `\` never matches a drive root like `C:\` (TigerCli must not guess the current drive). On Linux/macOS the comparison is conservative (case-sensitive, trailing `/` insignificant, no Windows drive rules).

```csharp
[TigerCliOption("--media-root", Provider = "media-roots",
    ValueMatching = TigerCliValueMatchPreset.FileSystemPath)]
public string MediaRoot { get; set; } = "";
```

Prefer a preset over a bespoke per-option normalizer attribute — value matching is a single, centralized policy shared by all provider-backed value paths.

For the full prompting rules, see [interaction modes](interaction-modes.md) and [prompting and providers](prompting-and-providers.md).

## Command Menu

The command menu is the TigerCli alternative to shell tab completion: an opt-in, discoverable picker that lists eligible commands, lets the user choose one, and then runs it through the **normal** parse/bind/prompt/validate/execute pipeline. It is a UX layer *above* prompting, not a separate execution system:

- **commands** — the user knows what to type.
- **prompting** — the user knows the command; TigerCli asks for missing values.
- **command menu** — the user knows the app; TigerCli helps choose the command.

The menu is disabled by default. Enable it with `UseCommandMenu`:

```csharp
using ItTiger.TigerCli.Enums;

// Menu is the default/root command — runs when no command is given.
builder.UseCommandMenu(CommandMenuMode.Enabled);

// Or a named root-level command that opens the menu (e.g. `my-tool menu`).
builder.UseCommandMenu(CommandMenuMode.Enabled, commandName: "menu");
```

This API registers either a default menu **or** a named menu, never both. The default form (`commandName: null`) conflicts with a configured default command — `Build()` throws. When `mode` is `Disabled`, no menu command is registered (but the app-level mode is still recorded for eligibility).

### Eligibility

A command's menu eligibility is resolved from its **app → group → command** `CommandMenuMode` chain:

> A command is eligible when the chain contains **at least one `Enabled`** and **no `Disabled`**. `Inherit` means "no local opinion".

`Disabled` anywhere wins, so it is how you hide a command (or a whole group) from the menu. Two-level outcomes:

| App | Command | Result |
|-----|---------|--------|
| `Enabled` | `Inherit` | eligible |
| `Enabled` | `Disabled` | hidden |
| `Inherit` | `Enabled` | eligible |
| `Inherit` | `Inherit` | hidden |
| `Disabled` | _any_ | hidden |

Set the opinion per node:

```csharp
builder.UseCommandMenu(CommandMenuMode.Enabled);                 // app: opt every command in

builder.AddCommand<DeployCommand>("deploy",
    configure: c => c.CommandMenu(CommandMenuMode.Disabled));     // hide one command

builder.AddCommandGroup("admin", group => group
    .CommandMenu(CommandMenuMode.Enabled)                        // opt the group's children in
    .AddCommand<ResetCommand>("reset"));
```

Using `CommandMenuMode.Inherit` at the app level turns the menu surface on while leaving every command opted out by default — each command or group then opts in explicitly with `CommandMenu(CommandMenuMode.Enabled)`.

### Behavior

- Command groups appear as nested submenus: selecting a group opens its eligible children; Escape returns to the parent menu, and Escape at the top backs out without running anything.
- Rows are laid out in aligned columns — a left-aligned name/key, a left-aligned description, and a right-aligned muted marker (`→ target path` for an alias, `›` for a group, blank for a normal command). The menu builds each row as structured cells and renders them through the reusable multi-column select (`TigerTui.MultiColumnSelectIndexAsync`, backed by `InlineMultiColumnSelect` and the `SelectColumn`/`SelectRow`/`SelectCell` model), not as preformatted strings; the marker column reserves its own width so it never disturbs description alignment, and long descriptions truncate to the frame.
- Command names and descriptions (including localized `descriptionResourceKey` values) are reused from the existing command metadata.
- Help, version, and the menu command itself are never listed in the menu (and the menu command is hidden from help).
- Selecting a command resumes the normal pipeline, so any missing arguments/options are prompted exactly as if the command had been typed.
- The menu requires a semi-interactive session; running it with `--non-interactive` fails cleanly.
- An empty eligible set shows a clear "No commands are available." message.

## Edit Commands

An *edit command* loads an existing object, applies command-line overrides, prompts for the remaining editable fields with the existing values as defaults, and lets your handler save the result. Its positional arguments are [selectors](arguments-and-options.md#arguments-options-and-selectors) — they identify the object to load, they are not edited. It reuses the same parsing, prompting, and provider machinery as an add command — you opt in with `AsEdit` and supply a loader.

```csharp
public sealed class ConnectionSettings : TigerCliSettings
{
    // Selector: identifies the profile. Arguments are not editable by default.
    [TigerCliArgument(0, Name = "profile", Description = "Profile name.")]
    public string Profile { get; set; } = default!;

    // Editable options (the default for options).
    [TigerCliOption("-s|--server", Promptable = TigerCliPromptable.Normal, Description = "SQL Server host.")]
    public string Server { get; set; } = default!;

    // Provider-backed and provider-validated by default.
    [TigerCliOption("-d|--database", Provider = "databases",
        Promptable = TigerCliPromptable.Normal, Description = "Database name.")]
    public string Database { get; set; } = default!;
}

builder.AddCommandGroup("connection", group => group
    .AddCommand<EditConnectionCommand>("edit", b => b
    .AsEdit<ConnectionSettings>(async settings =>
    {
        // settings.Profile is already bound from the command line.
        var profile = await store.FindAsync(settings.Profile);
        return profile is null
            ? TigerCliEditLoad<ConnectionSettings>.NotFound()
            : TigerCliEditLoad<ConnectionSettings>.Found(new ConnectionSettings
              {
                  Server   = profile.Server,
                  Database = profile.Database,
              });
    })));
```

```text
connection edit reporting --server sql-new
```

- `reporting` selects the profile (not editable, never overwritten). When supplied on the command line it is bound and not prompted.
- **Selector arguments are resolved before the loader.** Every positional argument is treated as a selector the loader needs, so a missing one is resolved or rejected *before* `AsEdit` runs — the loader is never called with a missing/empty selector. If the argument can be prompted in the current interaction mode (semi-interactive and promptable), it is prompted and the bound value is passed to the loader; otherwise (non-interactive, or `Promptable = TigerCliPromptable.No`) it fails with the normal missing-argument error and the loader is not called. `Editable = false` only means "not edited after load" — it does not let a missing selector reach the loader, and missing selectors are never seeded from the existing object (it has not been loaded yet).
- `--server sql-new` overrides the existing server and is not prompted.
- `--database` and any other editable field are seeded from the existing profile; in semi-interactive mode they are prompted with the existing value preselected, and accepting the default keeps it.
- The command-line value always wins. Non-interactive partial edits keep unspecified fields: `connection edit reporting --server sql-new --non-interactive`.
- `NotFound()` reports a framework error and the handler does not run. Saving the edited object stays in your `ExecuteAsync`.

Behavior rules:

- **Editability.** Options are editable by default; arguments are not (they are usually selectors). Set `Editable = false` on an option, or `Editable = true` on an argument, to change this. `Editable = false` means "not edited after load" — the field is not prompted, overwritten, or provider-validated as an editable field after the existing object loads. It does not stop a missing promptable positional selector from being resolved before the loader. For arguments, editability only affects edit mode; for options, `Editable = false` currently also skips provider validation in add/normal commands.
- **Provider validation.** When a provider is configured for an editable field, the effective value is validated against the provider's current choices in both add and edit modes. A command-line, existing, or default value that is not an available choice fails. A stale existing value is not injected into the choices: in semi-interactive mode the user must pick a valid replacement; in non-interactive mode the edit fails. Set `ValidateAgainstProvider = false` to opt out for a field. Provider validation is not edit-specific — in normal commands, arguments with an explicit `Provider` and provider-backed options validate supplied values the same way ([provider validation of supplied values](prompting-and-providers.md#provider-validation-of-supplied-values)); the edit-mode *selector* is the exception and defers to the loader.
- **Edit-only provider (`EditProvider`).** `Provider` is the normal provider and applies in every mode. `EditProvider` is used **only in edit mode**, where (when set) it overrides `Provider`; in add/normal commands it is ignored. This is for settings types **shared** by an add and an edit command, where add needs a new value but edit needs a selector over existing values. For example, `[TigerCliArgument(0, Name = "name", EditProvider = "connections")]` types a new name in `connection add` but renders a provider-backed selector of existing connections in `connection edit`. Empty/whitespace `EditProvider` is treated as not configured (falls back to `Provider`). There is no implicit group-token convention — the edit provider is always explicit. `EditProvider` is available on both `[TigerCliArgument]` and `[TigerCliOption]`.
- **Manual edit.** A command without `AsEdit` behaves exactly as before. For standard CRUD edit flows, prefer `AsEdit`; direct `TigerTui` prompts belong only in custom workflows that cannot fit the parser/provider/edit pipeline.

### Secret Fields In Edit Commands

Secret options (`Secret = true`) work in edit mode the same way other editable options do, with masking applied to the prompt:

- **Secret options can be editable** and are prompted like any other editable field.
- **Secret options are seeded in edit mode.** The loader returns the existing (decrypted) value, and the merge seeds it into settings for fields not supplied on the command line — exactly like non-secret options.
- **The secret prompt preselects the seeded value and renders it masked.** The plaintext is never written to the terminal; pressing **Enter** keeps the current seeded value.
- **`AllowCommandLineValue = false` still rejects argv secrets** in edit mode. The value can only arrive through the seeded existing object or the masked prompt, never through `--password ...` on the command line.

Seeding the effective secret matters beyond the prompt: a **dependent provider may need it**. A SQL Server `databases` provider, for example, depends on the current server, auth mode, user, and password to connect and list databases. In edit mode the seeded password is present in settings, so the provider can run without forcing the user to re-enter it.

```csharp
public sealed class ConnectionSettings : TigerCliSettings
{
    [TigerCliArgument(0, Name = "profile", EditProvider = "connections", Description = "Profile.")]
    public string Profile { get; set; } = default!;

    [TigerCliOption("--server", Promptable = TigerCliPromptable.Normal, Description = "SQL Server host.")]
    public string Server { get; set; } = default!;

    // Secret, never accepted from argv; seeded in edit mode and preselected (masked) at the prompt.
    [TigerCliOption("--password", Secret = true, AllowCommandLineValue = false,
        Promptable = TigerCliPromptable.Normal, Description = "Password.")]
    public string Password { get; set; } = default!;

    // The database provider depends on the effective server/password, both available after seeding.
    [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.Last,
        DependsOnOptions = new[] { "--server", "--password" }, Description = "Database.")]
    public string Database { get; set; } = default!;
}
```

## Testing Command Apps

Use `TigerCliAppTestHost` to test the same app object that `Program.cs` runs.

```csharp
using ItTiger.TigerCli.Testing;

public sealed class MyAppTests
{
    [Fact]
    public async Task Echo_WritesMessage()
    {
        var result = await TigerCliAppTestHost
            .For(MyApp.Create())
            .WithArgs("echo", "--message", "hello")
            .RunAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.StdOut);
        Assert.Empty(result.StdErr);
    }
}
```

The harness captures stdout, stderr, prompt answers, and the final exit code without using the real console. See [app testing](app-testing.md) for prompt flows, localized output, `--non-interactive`, and test limitations.

## What This Guide Does Not Cover

This guide is the starting point for building command apps. It intentionally does not cover every command-processing detail.

- Colour mode and the `--color` / `--no-color` framework option: [colour mode](../reference/color-mode.md)
- Prompting and providers in depth: [prompting and providers](prompting-and-providers.md) and [command processing prompting](../design/command-processing-prompting.md)
- Structured terminal output and tables: [structured output](structured-output.md) and [CliTable](cli-table.md)
- Argument and option binding: [arguments and options](arguments-and-options.md)
- Public API overview: [API map](../reference/api-map.md) and [DocFX generation](../api-docfx/README.md)
- Help escaping and trusted text: [help rendering trust model](../reference/help-rendering-trust-model.md)
- Design rationale for command paths and positionals: [command processing positionals](../design/command-processing-positionals.md)
- Primary public command-app sample: [ROI Cities](../getting-started.md)
- Default-command real-operation sample: [Folder Copy](../examples/folder-copy.md)
- Broad dogfooding sample for larger feature coverage, dependent providers, and localization: [`CommandParserTest/CommandParserTestApp.cs`](../../CommandParserTest/CommandParserTestApp.cs)
