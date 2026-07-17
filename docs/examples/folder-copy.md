# Folder Copy Sample

[`FolderCopy`](../../FolderCopy/) is the TigerCli "real operation" sample. It is not the Getting Started app; use [ROI Cities](../getting-started.md) first for the command-app shape, `list`/`show`, selectors, providers, command menus, structured output, exit codes, and app-boundary tests.

Folder Copy shows how TigerCli supports work that actually does something: folder picker prompts, long-running scanning and copying, progress rows, cooperative cancellation, and strict non-interactive behavior.

![The Folder Copy activity dialog copying six files, with Dash-style progress bars for the current file, files copied, and total size](folder-copy/folder-copy-activity.webp)

The animation is generated, not recorded: the sample's real activity dialog runs on a scripted `TestShell` with a manual clock against a deterministic copy storyboard, and every rendered frame is assembled into the loop (`dotnet run --project internal/DocSamples -- folder-copy`, the same model as the [spinner](spinners/spinners.md) and [progress-bar](progress-bars/progress-bars.md) showcases).

## App Shape

`Program.cs` stays thin:

```csharp
using FolderCopy;

return await FolderCopyApp.Create().RunAsync(args);
```

All app wiring lives in [`FolderCopyApp.Create`](../../FolderCopy/FolderCopyApp.cs). The app has one default command and no command menu:

```csharp
TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(FolderCopyApp).Assembly, enableVersion: false)
    .SetDefaultCommand<FolderCopyCommand>();
```

Its identity and description live in the project file — `<AssemblyName>folder-copy</AssemblyName>` (which is also the executable name) and `<Description>` — and are imported with `UseAssemblyMetadata(...)`, the preferred pattern for a normal executable app (see [command apps → app metadata](../guides/command-apps.md#app-metadata)). `enableVersion: false` reads the metadata without turning on `--version`, since this sample has no version. That shape is useful for operation tools where the app's main behavior is obvious from the executable name:

```bash
folder-copy --source C:\Input --destination C:\Output
folder-copy -s C:\Input -d C:\Output --non-interactive
```

## Folder Options

The command surface is intentionally small: source and destination are required options decorated with `[TigerCliFolderSelect]`:

```csharp
[TigerCliOption("-s|--source", Required = true, Description = "Source folder to copy from.")]
[TigerCliFolderSelect]
public string? Source { get; set; }

[TigerCliOption("-d|--destination", Required = true, Description = "Destination folder to copy into.")]
[TigerCliFolderSelect]
public string? Destination { get; set; }
```

They are options rather than positional arguments because the folder picker is an option-level prompt. The trade-off is a named CLI (`--source`, `--destination`) instead of bare paths, but the result is clear prompting behavior:

- Supplied paths bind normally; no picker is shown.
- Missing values in semi-interactive mode open the inline folder picker.
- Missing values under `--non-interactive` fail as required options before the command body runs.

Tests can make the picker deterministic by passing an `IFolderBrowser` to the app factory.

## Activity Phases

The command does not branch into separate interactive and non-interactive copy bodies. It uses TigerCli's activity APIs for both modes.

First, it scans the source folder inside a simple `RunActivityAsync` phase:

```csharp
var scanResult = await TigerTui.RunActivityAsync(
    settings.T("Scanning source folder..."),
    (_, ct) => FolderCopyPlanner.PlanAsync(source, ct));
```

That keeps a slow filesystem walk cancellable and visible instead of blocking silently before the copy begins. Under `--non-interactive`, the same simple message prints once before the scan body runs.

The copy itself runs inside a richer activity dialog built with `ActivityDialogSpec`. The dialog reports:

- current file
- current-file progress
- files copied progress
- bytes copied progress
- elapsed time and ETA

In semi-interactive mode, TigerCli renders the live dialog and progress bars. Under `--non-interactive`, the same operation runs headlessly: no dialog, no keyboard input, and no prompt, but cancellation and progress validation still use the same activity machinery. The spec's explicit non-interactive message gives scripts a single status line in place of the richer dialog.

A mid-copy frame of the dialog (all three progress bars use [ProgressBarStyle](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.ProgressBarStyle.html).Dash) is committed as a drift-checked artifact pair: [`folder-copy.html`](folder-copy.html) also carries the generated help screen, and [`png/folder-copy-activity.png`](png/folder-copy-activity.png) is its PNG companion. Both regenerate with `dotnet run --project internal/DocSamples` and are guarded by the normal artifact drift test.

## Copy Planner

[`FolderCopyPlanner`](../../FolderCopy/FolderCopyPlanner.cs) is deliberately TigerCli-free. It plans and executes the filesystem work without knowing about command handlers, prompts, markup, or activity dialogs. That keeps the operation testable directly with temporary folders.

The copy behavior is intentionally simple:

- recursively enumerate files under the source root
- preserve relative paths
- create destination directories as needed
- overwrite existing files
- report progress while copying
- observe cancellation before and during file I/O

It is not a robocopy-style tool: no retries, ACL preservation, symlink policy, exclude rules, checksum verification, mirroring, or delete behavior.

## Tests

[`FolderCopy.Tests`](../../FolderCopy.Tests/) covers the sample at two levels:

- planner tests exercise planning, overwrite behavior, progress, and cancellation against temporary folders without TigerCli involved
- app-boundary tests run the real `FolderCopyApp.Create()` through `TigerCliAppTestHost`, including non-interactive required-option failures and headless copy execution
- folder-picker tests use `TestShell` plus a deterministic `IFolderBrowser` for semi-interactive missing-value resolution

Use Folder Copy when you need a public example of real operations, folder selection, `RunActivityAsync`, progress bars, cancellation-aware work, non-interactive long-running execution, or TigerCli-free business logic.
