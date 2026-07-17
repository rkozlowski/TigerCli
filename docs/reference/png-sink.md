# PngSink

`ItTiger.TigerCli.PngSink` is an optional package for rendering TigerCli output to deterministic PNG
images. It is separate from `ItTiger.TigerCli`; SkiaSharp and its native assets are referenced only by
the PNG sink package.

`PngSink` implements `ICliRenderSink`, so it consumes the same resolved `CliTextSegment` stream as
`AnsiSink` and `HtmlSink`. It does not introduce a parallel rendering model or a separate color
system. [CliColor](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliColor.html) values are mapped through `CliColorPalette`, including the 0-255 terminal palette.

## Intended Use

PNG output is for documentation and Markdown-embeddable visual artifacts. `HtmlSink` remains the
better choice for diffable text artifacts, unit-test-style snapshots, and generated examples where a
reviewer needs to see precise textual changes.

The documentation pipeline uses this package through `internal/DocSamples`, which generates the
committed HTML + PNG artifact pairs under `docs/examples/` (see
[`docs/design/doc-artifacts.md`](../design/doc-artifacts.md)).

## Output Lifecycle

`PngSink` is buffered. `Write` records styled text into terminal cells, `NewLine` advances the cursor,
and `Flush` is safe to call repeatedly. `Flush` does not encode or finalize the PNG.

Use `Save(Stream)`, `ToBytes()`, or the `PngRenderer` helpers after rendering is complete:

```csharp
var bytes = PngRenderer.RenderGridToBytes(
    grid,
    new PngSinkOptions { Columns = 80, Rows = 24 });
```

## Fonts

The package embeds static TTF files:

- terminal text: Cascadia Mono regular/bold/italic/bold-italic
- title/chrome text: Cascadia Mono regular/bold/italic/bold-italic
- symbol fallback: Noto Sans Symbols 2 regular

Fonts are loaded from package resources. Terminal cells and title/chrome text use Cascadia Mono first;
if Cascadia Mono lacks a terminal-cell glyph needed by generated documentation, the sink uses bundled
Noto Sans and then Noto Sans Symbols 2 as explicit, pinned fallbacks. The sink does not silently fall
back to OS-installed fonts. Missing fonts and glyphs that are absent from all bundled families fail
loudly. Variable fonts are not used in v1.

## Window Chrome

[PngWindowChrome](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.PngSink.PngWindowChrome.html).FrameAndTitle draws a one-pixel frame, title bar, title text, right-side window
symbols, and a small title-bar icon. The default assets are bundled under `Assets/`:
`tc_term_ico.png` for the left icon and `tc_window_symbols.png` for the right-side symbols. Chrome
assets are drawn at their native pixel size without scaling. The default title text size is 16pt and
the default title-bar background is `CliColor.Gray15` (`#262626`).

Use `PngSinkOptions.TitleBarIcon` and `PngSinkOptions.TitleBarSymbols` to customize or disable them:

```csharp
new PngSinkOptions
{
    Columns = 80,
    Rows = 24,
    Chrome = PngWindowChrome.FrameAndTitle,
    Title = "Example",
    TitleBarIcon = PngTitleBarIcon.Default,
    TitleBarSymbols = PngTitleBarSymbols.Default
};
```

Custom assets can come from a file or copied bytes:

```csharp
TitleBarIcon = PngTitleBarIcon.FromFile("custom-icon.png");
TitleBarIcon = PngTitleBarIcon.FromBytes(iconBytes);
TitleBarSymbols = PngTitleBarSymbols.FromFile("custom-symbols.png");
TitleBarSymbols = PngTitleBarSymbols.FromBytes(symbolBytes);
```

Disable rendering with `PngTitleBarIcon.None` or `PngTitleBarSymbols.None`. These options are ignored
when `Chrome` is `None`, and they do not affect terminal-cell content rendering.
