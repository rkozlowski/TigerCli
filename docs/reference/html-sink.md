# HtmlSink

`HtmlSink` is an `ICliRenderSink` that renders TigerCli text segments to **deterministic HTML**. It
is a public sink (like `AnsiSink`) and is **opt-in**: no TigerCli app emits HTML unless it explicitly
asks for it. It is not a browser UI framework — the output is plain `<pre>` + `<span>` (and optionally
`<a>`) markup.

```csharp
namespace ItTiger.TigerCli.Terminal;

public sealed class HtmlSink : ICliRenderSink
{
    public HtmlSink(TextWriter writer, HtmlSinkOptions? options = null);
}
```

## What it is for

- **Snapshot-style tests** of rendered output — internal TigerCli tests and external app/project tests.
- **Documentation examples** generated from real TigerCli rendering (so docs cannot drift from the
  actual layout/styles).

The goal is stable, diffable HTML, not interactivity.

Full app runs (generated help, framework errors) can be captured as HTML through
`TigerCliAppTestHost.WithHtmlCapture` — see [app testing](../guides/app-testing.md).

## Quick start

```csharp
using ItTiger.TigerCli.Terminal;

// From a renderable (CliTable, CliList result, CliDetails, …):
string html = TigerConsole.RenderToHtml(details);

// From markup:
string fragment = TigerConsole.MarkupToHtml("[Heading]Devices[/]");

// Directly, for full control:
var writer = new StringWriter();
var sink = new HtmlSink(writer, new HtmlSinkOptions { HyperlinkMode = HtmlHyperlinkMode.Anchor });
TigerConsole.RenderGrid(grid, sink);
string output = writer.ToString();
```

### Helpers on `TigerConsole`

| Helper | Renders |
|---|---|
| `MarkupToHtml(string markup, HtmlSinkOptions? options = null, ITheme? theme = null)` | TigerCli bracket markup |
| `RenderGridToHtml(CliGrid grid, HtmlSinkOptions? options = null)` | a measured/!measured `CliGrid` |
| `RenderToHtml(CliRenderableComponent component, HtmlSinkOptions? options = null)` | any renderable (`CliTable`, `CliList` result, `CliDetails`, …) |

These mirror the existing `MarkupToAnsi` / `RenderGridToAnsi` helpers.

## Output shape

By default the whole render is wrapped in `<pre class="tigercli">…</pre>` so the browser preserves
whitespace and line breaks. Styled runs become `<span>` elements:

```html
<pre class="tigercli"><span class="tc-bold" style="color:#FF0000">Error</span>
<span style="color:#808080">camera-main</span>
<span class="tc-link">https://example.com</span></pre>
```

An unstyled run is emitted as plain (escaped) text with no wrapping element.

## `HtmlSinkOptions`

```csharp
public sealed class HtmlSinkOptions
{
    public bool WrapInPre { get; init; } = true;
    public HtmlHyperlinkMode HyperlinkMode { get; init; } = HtmlHyperlinkMode.Text;
    public int? SoftMaxWidth { get; init; } = null;
}
```

- **`WrapInPre`** (default `true`) — wrap the output in `<pre class="tigercli">…</pre>`. When `false`,
  only the inner HTML is emitted (for embedding inside a caller-supplied container).
- **`HyperlinkMode`** (default [HtmlHyperlinkMode](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.HtmlHyperlinkMode.html).Text) — how hyperlink targets render (below).
- **`SoftMaxWidth`** (default `null`) — optional emulated terminal width, in text columns (below).
  Must be positive when set.

The defaults are conservative and documentation-friendly.

## Layout width — emulating a terminal

HTML output has no terminal, so by default the sink reports no soft width and nothing wraps or
truncates because of the sink. That is wrong for documentation of width-dependent behavior —
wrapping, truncation, and responsive tables are core TigerCli features. Setting
`SoftMaxWidth` makes the sink report an emulated terminal width to the measure pass:

```csharp
// Renders exactly as it would in an 80-column terminal.
string html = TigerConsole.RenderToHtml(table, new HtmlSinkOptions { SoftMaxWidth = 80 });
```

Standard measure-pass rules apply:

- A grid's **own** `SoftMaxWidth` takes precedence over the sink's, like with every other sink.
- An **already-measured** grid is rendered as measured; the option only affects the measure pass.
  This is deliberate: a grid measured by a `TestShell` at its viewport width renders to HTML with
  that layout intact.
- Segment-level helpers (`MarkupToHtml`) write segments directly with no measure pass, so the
  option has no effect there.

## Styles

`HtmlSink` renders the resolved `CliCharStyle` it receives:

| Style | HTML |
|---|---|
| Bold / italic / underline | CSS classes `tc-bold`, `tc-italic`, `tc-underline` (fixed order) |
| Hyperlink target present | CSS class `tc-link` (in addition to any anchor) |
| Foreground colour | inline `style="color:#RRGGBB"` |
| Background colour | inline `style="background-color:#RRGGBB"` (after `color`, joined by `; `) |

**Why colours are inline and decorations are classes.** Text decorations and the link role are a
small, stable, documentable class set, so they use CSS classes (the preference is "classes where
possible"). Colours come from the full ANSI **0–255** palette, which has no stable CSS class names, so
they use a deterministic inline `#RRGGBB` hex derived from `CliColorPalette.GetRgb` — self-contained
output that needs no external stylesheet. You may style `.tigercli`, `.tc-bold`, `.tc-italic`,
`.tc-underline`, and `.tc-link` in your own CSS.

## Links — visible/copyable first

Like `AnsiSink`, the **visible link text is always emitted unchanged**, regardless of mode;
`HyperlinkMode` only decides whether an anchor is added.

`HtmlHyperlinkMode.Text` (default) — render link text as a styled span; never emit `<a>`:

```html
<span class="tc-link">https://example.com</span>
```

`HtmlHyperlinkMode.Anchor` — when a non-empty, **safe** `CliCharStyle.HyperlinkTarget` is present,
wrap the visible text in an anchor:

```html
<a class="tc-link" href="https://example.com">https://example.com</a>
```

```csharp
var options = new HtmlSinkOptions
{
    WrapInPre = true,
    HyperlinkMode = HtmlHyperlinkMode.Anchor
};
```

An empty/whitespace target, or an **unsafe** one, falls back to the `Text` rendering (a styled span,
no anchor). Unsafe means:

- contains control characters (stripped before use), or
- uses a dangerous scheme: `javascript:`, `vbscript:`, or `data:`.

Targets are never invented; only an explicit `HyperlinkTarget` produces an anchor. `CliDetails.AddLink`
and `CliList.AddLinkColumn` set that target, so they render as anchors in `Anchor` mode.

## Escaping & safety

- **Text content** is HTML-escaped: `&` → `&amp;`, `<` → `&lt;`, `>` → `&gt;`.
- **Attribute values** (the anchor `href`) are additionally escaped for `"` and `'`, after control
  characters are stripped, so a malformed target cannot break out of the attribute.
- Whitespace and line breaks are preserved (via `<pre>`; line breaks are written as `\n`).
- Output is deterministic and contains **no ANSI escape sequences**.

## Limitations

- **Semantic token names are not reconstructed.** Once `[Heading]` / `[Key]` / `[Path]` / `[Link]`
  have been resolved to a concrete `CliCharStyle`, `HtmlSink` renders that style (decoration classes +
  colour hex + `tc-link`), not the original token name. `HtmlSink` does not emit `tc-heading`,
  `tc-key`, or `tc-path` because the render model does not carry those semantic roles. The link role
  survives because it is carried on the style as `HyperlinkTarget`.
- The 256-colour palette maps to inline hex, not to per-colour CSS classes.

## Difference from the other sinks

| | `ConsoleSink` | `AnsiSink` | `HtmlSink` |
|---|---|---|---|
| Output target | `System.Console` | a `TextWriter` (ANSI) | a `TextWriter` (HTML) |
| Visibility | internal | public | public |
| Colour fidelity | 0–15 (degrades 16–255) | faithful 0–255 | faithful 0–255 (inline hex) |
| Links | visible text only | visible text + optional OSC 8 | visible text + optional `<a>` |
| Width source | safe terminal width | unbounded | unbounded, or opt-in `SoftMaxWidth` |
