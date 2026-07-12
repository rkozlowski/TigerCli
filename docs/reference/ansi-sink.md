# AnsiSink

`AnsiSink` is an `ICliRenderSink` that writes styled TigerCli output as **ANSI SGR escape
sequences** to a `TextWriter`. It is the one public sink in the framework; the others
(`ConsoleSink`, `TextWriterSink`, `StringLinesSink`, `TextSegmentLinesSink`) are internal.

Throughout this document `ESC` denotes the ASCII escape character `0x1B` (C# `"\u001b"`), which
introduces every CSI sequence.

```
namespace ItTiger.TigerCli.Terminal;

public sealed class AnsiSink : ICliRenderSink
{
    public AnsiSink(TextWriter writer, bool emitHyperlinks = false, bool emitTerminalControls = true);
}
```

## What it is for

`AnsiSink` renders the full ANSI **0–255** `CliColor` palette faithfully, so it is the right sink
for emitting colored output to ANSI-capable terminals, files, pipes, golden-file tests, generated
documentation, and examples. When terminal controls are enabled, it also emits framework-owned
window title updates as OSC 0 sequences through the same writer path.

### Automatic selection

The default console output paths (`TigerConsole.RenderGrid(grid)`, `Markup`, `MarkupError`) select
`AnsiSink` automatically based on `TigerConsole.ColorMode`: under `Ansi256` always, and under `Auto`
only when `TerminalCapabilities` detects safe 256-colour support for the stream (otherwise
`ConsoleSink`). The inline semi-interactive TUI (`ConsoleTerminal` / `InlineShell`) resolves its
sink through the same policy, so live dialogs, menus, and status rows render 256-colour theme roles
faithfully whenever ANSI output is active. See [color-mode.md](color-mode.md) for the policy, the
`--color` option, and environment-variable precedence.

## Difference from ConsoleSink

| | `ConsoleSink` | `AnsiSink` |
|---|---|---|
| Output target | `System.Console` (sets `Console.Foreground/BackgroundColor`) | a `TextWriter` (writes escape sequences) |
| `CliColor` 0–15 | rendered as the matching `ConsoleColor` | classic 16-color SGR (`30`–`37`/`90`–`97`, bg `40`–`47`/`100`–`107`) |
| `CliColor` 16–255 | **degraded** to the nearest standard `ConsoleColor` (via `CliColorPalette` / `CliColorMapper`) | **faithful** `ESC[38;5;<n>m` / `ESC[48;5;<n>m` |
| `null` channel | leaves the current console color as-is | resolves to the ANSI **default** (`39` fg / `49` bg) |

## Color mapping rules

### 0–15 — classic 16-color SGR (remapped)

`CliColor` values 0–15 follow **`ConsoleColor` order**, which is *not* ANSI palette order (ANSI
index `1` is red, but `CliColor.DarkBlue` is `1`). Emitting `ESC[38;5;<index>m` for these would
render the wrong color, so 0–15 are remapped explicitly to classic 16-color SGR codes. Background
codes are the foreground code `+ 10`.

| CliColor | Value | Foreground | Background |
|---|---|---|---|
| Black | 0 | 30 | 40 |
| DarkBlue | 1 | 34 | 44 |
| DarkGreen | 2 | 32 | 42 |
| DarkCyan | 3 | 36 | 46 |
| DarkRed | 4 | 31 | 41 |
| DarkMagenta | 5 | 35 | 45 |
| DarkYellow | 6 | 33 | 43 |
| Gray | 7 | 37 | 47 |
| DarkGray | 8 | 90 | 100 |
| Blue | 9 | 94 | 104 |
| Green | 10 | 92 | 102 |
| Cyan | 11 | 96 | 106 |
| Red | 12 | 91 | 101 |
| Magenta | 13 | 95 | 105 |
| Yellow | 14 | 93 | 103 |
| White | 15 | 97 | 107 |

### 16–255 — faithful 256-color SGR

A `CliColor` value 16–255 equals its ANSI palette index, so it is emitted directly:

- foreground: `ESC[38;5;<n>m`
- background: `ESC[48;5;<n>m`

where `n == (int)CliColor`. For example `CliColor.OceanBlue` (24) → `ESC[38;5;24m`, and
`CliColor.Gray85` (253) → `ESC[38;5;253m`.

## Text decorations

Beyond colour, a `CliCharStyle` carries `CliTextDecoration` flags (`Bold`, `Italic`, `Underline`).
`AnsiSink` diffs them against the previously emitted style and emits the matching SGR attribute codes:

| Decoration | On | Off |
|---|---|---|
| `Bold` | `1` | `22` |
| `Italic` | `3` | `23` |
| `Underline` | `4` | `24` |

Added flags emit their on-code; flags that drop out of the effective style emit their off-code. The
decoration codes are coalesced with any colour change into a single sequence (decoration codes first),
e.g. `[Bold Yellow]` → `ESC[1;93m`. A full reset (`ESC[0m`) — emitted before a newline and on flush
when a style is active — clears decorations as well as colours.

Decorations are emitted only when ANSI output is active. `ConsoleSink` (the no-ANSI path) does not
render them. TigerCli controls the emitted ANSI sequence, not the final font rendering.

`[Bold]` / `CliTextDecoration.Bold` emits ANSI SGR 1. Some terminals treat SGR 1 as an
"intense text" request rather than guaranteed font weight. Depending on terminal settings, it may
render as a bold font, brighter colors, both, or neither. Windows Terminal exposes this through the
`Intense text style` setting. Italic and underline are also terminal/font dependent. Do not rely on
bold alone to carry critical meaning; combine important emphasis with semantic styles or text where
appropriate.

## Hyperlinks (OSC 8)

When constructed with `emitHyperlinks: true`, `AnsiSink` turns a text run carrying a resolved
`CliCharStyle.HyperlinkTarget` into a clickable **OSC 8 hyperlink**:

```
ESC]8;;<uri>ESC\   <visible text>   ESC]8;;ESC\
```

The visible text is always written verbatim, so links stay **visible and copyable** even on terminals
that ignore OSC 8. Behaviour:

- **Diffing** — the target is diffed like the SGR attributes: a change closes the currently open link
  (`ESC]8;;ESC\`) and opens the new one. Contiguous segments with the *same* target stay a single
  continuous link (no churn); a transition to no target closes the link.
- **Sanitization** — control characters (C0 including `ESC`, `DEL`, and C1) are stripped from the
  target before it is written, so a malformed value cannot terminate or break out of the sequence.
  Only the emitted target is affected; the visible text is never changed.
- **No bleed** — any open link is closed before each newline, reset, and flush. If the run continues
  on the next line, the link re-opens from the carried style.

`emitHyperlinks` defaults to `false`, so a directly-constructed `AnsiSink` (and helpers such as
`MarkupToAnsi` / `RenderGridToAnsi`) emit no hyperlink sequences unless opted in. On the default
console paths, `ConsoleSinkFactory` sets the flag from `TigerConsole.HyperlinkMode` (`CliHyperlinkMode`):
`Always` (on whenever the ANSI sink is used), `Never` (off), and `Auto` (default — on only when ANSI
was capability-detected for the stream, not when forced/uncertain). Targets come from `[Link]…[/]`
markup or from structured `CliDetails.AddLink` / `CliList.AddLinkColumn` values.

## Window titles (OSC 0)

`SetWindowTitle(title)` emits:

```text
ESC]0;<title>BEL
```

The title is plain terminal text, not TigerCli markup. `AnsiSgr.SetWindowTitle` strips control
characters from the title before emission, including `ESC`, `BEL`, C0 controls, `DEL`, and C1
controls. Calling `SetWindowTitle` resets any active SGR or hyperlink state before writing the OSC
sequence so title updates cannot be nested inside styled output.

`emitTerminalControls` defaults to `true` for directly constructed sinks. The default console sink
factory disables terminal controls for redirected output so pipes and files do not receive OSC title
sequences.

## Null / default behavior

`AnsiSink` is deterministic rather than "leave as-is":

- A `null` foreground means the ANSI default foreground (`39`).
- A `null` background means the ANSI default background (`49`).
- A transition from a colored foreground to `null` emits `ESC[39m`; from a colored background to
  `null` emits `ESC[49m`.

This differs intentionally from `ConsoleSink`, whose `null` means "do not touch the current console
color." For text streams, deterministic default-channel behavior is more predictable.

## Style diffing, reset, and newlines

- **Diffing** — an SGR sequence is emitted only when the foreground, background, or decorations
  actually change. Adjacent segments with the same style produce no redundant escape.
- **Coalescing** — when several attributes change for a segment, they are combined into a single
  sequence, e.g. `ESC[38;5;24;48;5;221m`, or `ESC[1;93m` for `[Bold Yellow]` (decoration codes first).
- **Newline** — if a style is active, `ESC[0m` is emitted **before** the line break so a colored
  background does not bleed past the end of the line; tracked state returns to the default.
- **Flush** — if a style is active, `ESC[0m` is emitted, then the underlying `TextWriter` is
  flushed. Completely plain output emits no reset.
- **Reset** — emits `ESC[0m` only when a style is active and clears tracked state.

`SoftMaxWidth`, `SoftMaxHeight`, `MaxWidth`, and `MaxHeight` are all `null` (the sink targets an
arbitrary writer, not a sized terminal).

## Examples

Render a grid to an ANSI-capable writer:

```csharp
using var writer = new StringWriter();
var sink = new AnsiSink(writer);
TigerConsole.RenderGrid(table.ToGrid(), sink);
string ansi = writer.ToString();
```

Or use the convenience helpers on `TigerConsole`:

```csharp
// Markup → ANSI string (semantic tokens resolved through CurrentTheme unless a theme is passed).
string hello = TigerConsole.MarkupToAnsi("[OceanBlue]Hello[/]");
// => ESC[38;5;24mHello ESC[0m

// Whole grid → ANSI string.
string grid = TigerConsole.RenderGridToAnsi(table.ToGrid());
```

`MarkupToAnsi` uses a plain base style, so the result contains escape sequences only for the colors
introduced by the markup. Semantic tokens such as `[Accent]` resolve through the active theme, so
the same markup renders different colors under different themes.

## See also

- [API map](api-map.md) — compact index for `AnsiSink`, `TigerConsole.MarkupToAnsi`, and `TigerConsole.RenderGridToAnsi`
- [DocFX API generation](../api-docfx/README.md) — local generated API reference workflow
- `CliColorPalette` / `CliColorMapper` — RGB source of truth and the ConsoleColor degradation used by `ConsoleSink`
