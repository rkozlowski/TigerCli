# Repository Guidelines

## Project Structure & Module Organization

TigerCli is a C#/.NET 10 solution centered on `ItTiger.TigerCli/`, the main library. Core namespaces map to directories: `Commands/` for CLI parsing and handlers, `Rendering/` for grids, tables, frames, and buffers, `Tui/` for interactive controls and themes, `Terminal/` for `TigerConsole` and render sinks, `Markup/` for styled text parsing, `Primitives/` for value types, and `Enums/` for shared options. Tests live in `ItTiger.TigerCli.Tests/`. Public sample apps live in `RoiCities.Basic/`, `RoiCities.Extended/`, and `FolderCopy/`. Project docs are in `docs/`; public APIs should be documented in XML comments and surfaced through DocFX plus `docs/reference/api-map.md`.

## Build, Test, and Development Commands

- `dotnet build TigerCli.sln` builds the full solution.
- `dotnet build ItTiger.TigerCli/ItTiger.TigerCli.csproj` builds only the library.
- `dotnet test TigerCli.sln` runs all solution tests.
- `dotnet test ItTiger.TigerCli.Tests/ItTiger.TigerCli.Tests.csproj` runs the xUnit v3 suite.
- `dotnet test ItTiger.TigerCli.Tests/ --filter "FullyQualifiedName~MethodName"` runs targeted tests.
- `dotnet run --project RoiCities.Basic -- --help` and `dotnet run --project FolderCopy -- --help` run the public sample apps.

## DocFX / API Docs

Run DocFX when XML comments, DocFX configuration, generated API reference content, or `docs/reference/api-map.md` may be affected:

```text
dotnet docfx docs/api-docfx/docfx.json
```

In this environment, do not run `dotnet docfx` in a restricted sandbox when Roslyn/MSBuild named-pipe access is blocked. Known failure modes include `UnauthorizedAccessException` involving Roslyn/MSBuild named pipes, DocFX appearing to hang, and stale `dotnet`/DocFX/build-host processes remaining after interruption.

If DocFX hangs or fails this way, stop the command, terminate only stale child processes you started, optionally run `dotnet build-server shutdown`, and do not leave orphaned `dotnet`/DocFX processes. Rerun DocFX only in the normal/non-sandbox command environment. If normal execution is unavailable, skip DocFX validation and report that limitation clearly.

`docs/reference/api-map.md` is generated from DocFX metadata. Do not hand-edit it. Correct flow:

```text
dotnet docfx docs/api-docfx/docfx.json
dotnet run --project internal/DocSamples -- api-map
dotnet run --project internal/DocSamples -- api-map check
```

When a task changes XML comments, DocFX config, or API-map generation, the final response should state whether DocFX was run, whether it needed the normal/non-sandbox environment, the DocFX warning/error count, whether the API map was regenerated and checked, and whether any stale processes were cleaned up.

## Coding Style & Naming Conventions

Use C# with nullable references and implicit usings enabled. Follow the existing brace style and four-space indentation. Public types use `PascalCase`; locals, parameters, and fields use `camelCase`. Keep APIs that accept grid coordinates in `(column, row)` order. Prefer the established rendering path, `CliGrid -> Measure() -> Render() -> ICliRenderSink`, rather than adding parallel layout systems.

TigerCli supports non-interactive single-command execution and semi-interactive inline prompts. It does not provide persistent full-interactive session navigation; applications must not rely on `FullInteractive` as an implemented mode.

Styles cascade from general to specific: grid default, table default, axis style, row/column style, then per-cell style. Later entries win, with `CliStylePrecedence` deciding row-versus-column precedence at intersections. Pass nested grids directly to `CliGrid.Set(column, row, subgrid)`; subgrid cells have no `Style`, and scrolling is configured with `CliScrollMode`. Overlays are post-layout one-dimensional strips added with `CliGrid.AddOverlay()`; they overwrite measured cells and do not participate in measurement.

## Testing Guidelines

Tests use xUnit v3 and generally extend `TestBase`. Prefer snapshot assertions via `AssertSnapshot(...)`, which render through `TigerConsole.RenderToLines` or `RenderGridToLines` for stable output without ANSI codes. Name focused behavior tests descriptively, for example `Table_HiddenHeader_Vertical_NoOuter_NoRecords`; legacy numbered tests exist but should not be expanded unless matching nearby coverage.

Before adding or changing tests, consult `docs/contributing/unit-testing.md`, which documents the TigerCli testing strategy. Use non-interactive rendering tests for pure layout/rendering behavior, and use `TestShell` / `TestTerminal` for semi-interactive TUI behavior. For semi-interactive tests, use `WaitForInputDrainedAsync` for non-terminating key batches, and await the modal task for terminating keys such as Enter/Escape. Keep manual/smoke testing for actual console visual feel, resize behavior, and terminal-specific quirks.

## Commit & Pull Request Guidelines

Recent commit history uses short, imperative summaries such as `Scrolling fix.` and `Added padding`. Keep commits focused and mention the affected feature or behavior. Pull requests should include a concise description, relevant test results, linked issues when available, and screenshots or terminal output for visible rendering or TUI changes.

## Agent-Specific Instructions

Any change that adds, removes, or modifies public or protected API must add or update XML documentation comments for every affected public or protected type and member. Public package projects must remain CS1591-clean. If the public API shape or docs change, regenerate/check DocFX metadata and `docs/reference/api-map.md` through the documented DocFX flow. Fix rendering issues at their source in measurement, style cascade, or render buffering rather than masking symptoms at call sites.

Aim for a clean build with 0 errors and 0 warnings, and for all unit tests to pass. Do not suppress warnings or skip failing tests unless explicitly justified in the final report.

Start documentation work with `docs/README.md`. Important references include `docs/reference/api-map.md` for the generated public type map, `docs/api-docfx/README.md` for API-reference generation, `docs/guides/cli-table.md` and `docs/guides/command-apps.md` for public usage, and the design documents under `docs/design/` for overlays and semi-interactive TUI behavior.

## Review / Commit Policy

The human reviews all changes in GitHub Desktop before committing.

Agents must keep diffs small and focused.

Agents must not commit, push, tag, or create branches unless explicitly instructed.

After making changes, agents should summarize:
- files changed
- why each file changed
- build/test results
- any risky or uncertain changes

## ANSI / ESC characters in source files

Do not insert literal ESC control characters into source files, tests, snapshots, or documentation.

When C# code needs to refer to the ANSI escape character, always use an explicit C# escape sequence:

```csharp
private const string Esc = "\u001b";
```

Use that constant in assertions and expected strings:

```csharp
output.ShouldContain($"{Esc}[31m");
output.ShouldContain($"{Esc}]8;;https://example.com{Esc}\\");
```

Do not use invisible literal ESC characters, empty placeholder strings, copy-pasted terminal control characters, or editor-rendered escape glyphs. They are fragile and may not survive editing, diffing, or agent tool calls reliably.

For documentation, prefer visible escaped forms such as:

```text
\u001b[31m
ESC ] 8 ; ; URI ESC \
```

instead of embedding raw control characters.

If an ESC assertion reports an impossible match, inspect the failure before changing production code. An xUnit message showing an empty `Found: ""` or a match at position zero can mean the assertion needle was stripped or mangled. Inspect the source bytes when necessary; `"\u001b"` is encoded as `22 5C 75 30 30 31 62 22`, while a literal ESC contains byte `1B` and an empty string contains adjacent `22 22`. Repair the source using visible ASCII characters, never by retyping a control character. If runtime behavior contradicts the file on disk, rule out stale `bin`/`obj` output before changing code.
