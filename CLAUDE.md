# CLAUDE.md

@AGENTS.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository. It supplements the shared instructions in `AGENTS.md` (imported above) with Claude Code-specific detail.

## Repository Layout

```
TigerCli-dev/
├── ItTiger.TigerCli/           ← main library
├── ItTiger.TigerCli.Tests/     ← xUnit v3 test suite
├── RoiCities.Basic/            ← getting-started sample app (list/show, providers)
├── RoiCities.Extended/         ← extended sample app (command menu, CliList/CliDetails)
└── FolderCopy/                 ← real-operation sample app (folder picker, RunActivityAsync)
```

Target framework: **net10.0**. Nullable and implicit usings are enabled. See `AGENTS.md` for build/test commands, including the class-filter form (`--filter "ClassName~TigerCliTableRenderingTest"`).

## Architecture Overview

TigerCli is a custom CLI rendering and interaction framework for script-safe command apps and structured terminal output. Its current supported app flow is single-command execution, optionally guided by menus and prompts:

| Mode | Input | Rendering | State |
|---|---|---|---|
| Non-Interactive | CLI args only | Direct stdout | Stateless |
| Semi-Interactive | CLI args + inline prompts | Minimal | Minimal |

TigerCli does not provide persistent full-interactive session navigation, and applications must not rely on `FullInteractive` as an implemented mode.

### Core Rendering Pipeline

```
Data → CliTable.ToGrid() → CliGrid → Measure() → Render() → ICliRenderSink
```

Every renderable becomes a `CliGrid`. The `Measure()` pass is the critical step — it resolves all column widths, row heights, wrapping, alignment, and truncation. Rendering after measurement is nearly trivial.

**Key types:**
- `CliGrid` (`Rendering/CliGrid.cs`, partial) — central layout engine; cells addressed by `(column, row)`
- `CliTable : CliRenderableComponent` — structured tabular data; converts to `CliGrid` via `ToGrid()`
- `MeasuredCell` — output of the measure pass; holds final styled lines ready to render
- `TigerConsole` (static, `Terminal/TigerConsole.cs`) — render entry point (`RenderGrid`, `RenderToLines`, `Markup`)
- `ICliRenderSink` — output target abstraction: `ConsoleSink`, `StringLinesSink`, `TextWriterSink`, `TextSegmentLinesSink`

### Namespace Map

| Namespace | Purpose |
|---|---|
| `ItTiger.TigerCli.Enums` | All enumerations |
| `ItTiger.TigerCli.Primitives` | Value types, styles, cell primitives |
| `ItTiger.TigerCli.Rendering` | `CliGrid`, `CliTable`, `CliFrameArea`, `CliRenderBuffer` |
| `ItTiger.TigerCli.Terminal` | `TigerConsole`, sink implementations |
| `ItTiger.TigerCli.Markup` | `CliMarkupParser` (TigerCli bracket markup such as `[color]text[/]`) |
| `ItTiger.TigerCli.Commands` | CLI parsing, settings, command handlers |
| `ItTiger.TigerCli.Tui.*` | TUI abstractions, themes, controls, windowing |

### Command System

```csharp
public sealed class MySettings : TigerCliSettings
{
    [TigerCliOption("-c|--connection", Description = "Connection string.")]
    public string ConnectionString { get; set; } = default!;
}

public sealed class MyCommand : TigerCliAsyncCommandHandler<MySettings>
{
    public override async Task<int> ExecuteAsync(MySettings settings) { ... }
}

var app = TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyCommand).Assembly)  // app name/metadata from the project file
    .SetDefaultCommand<MyCommand>()
    .Build();
return await app.RunAsync(args);
```

Key attributes: `[TigerCliOption]`, `[TigerCliArgument]`, `[TigerCliExactlyOneOf]` (class-level mutual exclusion). Commands have parameterless constructors — no DI.

### TUI System

- `ICliAppShell` — shell contract (viewport, theme, modal dialogs)
- `InlineShell` — singleton shell for semi-interactive mode; polls keyboard in a loop
- `TigerTui` — high-level entry point (`SelectAsync<T>`, string-based select)
- `InlineSelect` — keyboard-navigable list control (Up/Down/PgUp/PgDn/Home/End)
- `InlineDialog` — wraps a control with a frame; Enter = Ok, Escape = Cancel
- `ITheme` / `ThemeBase` / `DarkTheme` — style resolution by `ThemeStyle` enum

### Overlays

Post-layout one-dimensional strips (scrollbars, indicators) that overwrite measured cells after the layout pass. They do not participate in measurement. Added via `CliGrid.AddOverlay()`.

## Critical Conventions

**Column before Row** — all APIs that accept both axis coordinates use `(column, row)` order, matching `(x, y)`. Applies to `CliGrid.Set`, `CliFrameArea` methods, `CliFrameLine`, `CliScrollableCell`, `CliRenderBuffer.Set`. Never deviate.

**Style cascade** — styles merge from most-general to most-specific: grid default → table default → axis style → row/column style → per-cell style. Later entries win. `CliStylePrecedence` controls whether row or column styles win at the intersection — use `RowOverColumn` for vertical tables, `ColumnOverRow` for horizontal tables.

**Keep public API docs current** — whenever a public type, member, signature, or default value is added, removed, or renamed in the main library, update the corresponding XML documentation comments as part of the same change. If the public API shape or docs change, regenerate/check DocFX metadata and `docs/reference/api-map.md` through the documented DocFX flow.

**Fix issues, don't mask them** — a workaround that hides a symptom without addressing the root cause is not a fix. Before proposing a change, identify where the problem actually originates. A correct fix in the right place is always preferred over a defensive patch at the point of symptom.

## Testing Pattern

Tests extend `TestBase` and use snapshot assertions:

```csharp
AssertSnapshot(table, "╔═════╗", "║ One ║", "╚═════╝");
AssertSnapshot(grid, "expected", "lines");
```

`TigerConsole.RenderGridToLines` strips ANSI color codes — use it for all test assertions. Test files: `TigerCliTableRenderingTest.cs`, `TigerCliGridRenderingTest.cs`, `WrappingAndTruncationTests.cs`, `GridTest1–4.cs`, `TigerCliExactlyOneOfTests.cs`.

See `AGENTS.md` for the testing strategy doc pointer and `TestShell`/`TestTerminal` semi-interactive testing conventions.

## Key Documentation Files

Start with `docs/README.md`:

- `docs/design/context.md` — architectural philosophy and design decisions
- `docs/reference/api-map.md` — compact public type map generated from DocFX metadata
- `docs/api-docfx/README.md` — local generated API reference workflow
- `docs/guides/cli-table.md` — CliTable usage patterns
- `docs/guides/command-apps.md` — command parser API
- `docs/design/overlays.md` — overlay model specification
- `docs/design/semi-interactive-tui.md` — semi-interactive controls specification with mockups
- `docs/reference/help-rendering-trust-model.md` — help text rendering and trust model

Review/commit policy is in `AGENTS.md` — it applies here unchanged.

## ANSI / ESC characters in source files

See `AGENTS.md` for the base policy: never insert literal ESC control characters into source files, tests, snapshots, or documentation; always refer to the ANSI escape character via the explicit C# escape-sequence constant shown there, and use visible escaped forms in documentation rather than raw control characters.

The playbook below is Claude Code-specific: concrete diagnostic steps and PowerShell one-liners for when an ESC-related test assertion behaves impossibly in this environment.

### When ESC handling goes wrong (debugging playbook)

If a test that checks for ESC behaves impossibly — e.g. an exact `Equal` on the rendered string
passes (proving there is no ESC) but `DoesNotContain(escVar, output)` still fails — the ESC token
itself is the problem, not the code under test. What worked:

- **Read the symptom.** An xUnit failure of the form `Sub-string found … ↓ (pos 0) … Found: ""`
  means the needle is an empty/mangled string (an empty string is "found" at position 0 of
  everything). That is the signature of a botched ESC literal, not a real match.
- **Verify the actual bytes** rather than trusting the rendered view (editors and tool output hide
  `0x1B`). Dump them:

  ```powershell
  $line = (Get-Content $path)[<zero-based-line>]
  ([System.Text.Encoding]::UTF8.GetBytes($line) | ForEach-Object { $_.ToString('X2') }) -join ' '
  ```

  A correct `"\u001b"` shows `... 22 5C 75 30 30 31 62 22 ...`; a raw literal shows a bare `1B`
  between the quotes; a stripped placeholder shows `22 22` (empty `""`).
- **Prefer a char-code over any literal/escape when tooling keeps mangling it.** Constructing the
  value from its code point is pure ASCII source with nothing to mangle, and is the most robust check:

  ```csharp
  Assert.False(html.Contains((char)0x1B)); // asserts "no ESC / no ANSI"
  ```

- **Fix existing bad bytes with PowerShell, building the replacement from char codes** (single-quoted
  PowerShell strings keep `\` literal) — do not retype the ESC by hand:

  ```powershell
  $bs = [char]0x5C; $escLiteral = $bs + 'u001b'   # the six chars: \ u 0 0 1 b
  $newLine = '    private const string Esc = "' + $escLiteral + '";'
  ```

- **Rule out a stale build.** When runtime behavior contradicts the on-disk source, delete `bin`/`obj`
  for the affected project and rebuild before drawing conclusions.
