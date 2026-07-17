# Rendered Examples

This directory contains generated rendering artifacts plus short handwritten notes for public sample
apps, such as [Folder Copy](folder-copy.md).

The generated files are real TigerCli rendering captured through `HtmlSink` and `PngSink` (see
[doc-artifacts.md](../design/doc-artifacts.md)). Curated artifacts are generated as
**HTML + PNG pairs**:

- The `.html` pages are the inspectable, diffable truth. View them locally in a browser, or via a
  docs site (GitHub shows `.html` files as source).
- The `png/*.png` images are the embeddable visual companions — they render inline in
  GitHub/Gitea markdown. The default canvas is 120 columns; height derives from the rendered
  content.
- Every PNG has a `png/*.png.txt` sidecar recording the generation environment (canonical OS,
  actual generation OS, .NET/SkiaSharp versions, fonts, columns/rows, chrome, title) and the
  visible text of the image, so image changes stay reviewable in diffs.

For example, the select-prompt storyboard frame ([HTML page](tui-storyboards.html)):

![Select prompt — initial state](png/tui-select-initial.png)

Published visual references:

- [CliColor ANSI 0–255 palette](https://rkozlowski.github.io/TigerCli/reference/cli-color.html)
- [Dark theme](https://rkozlowski.github.io/TigerCli/examples/theme-dark.html)
- [Light theme](https://rkozlowski.github.io/TigerCli/examples/theme-light.html)
- [Tiger Blue theme](https://rkozlowski.github.io/TigerCli/examples/theme-tiger-blue.html)

Do not edit generated artifact files by hand. Regenerate them with:

```bash
dotnet run --project internal/DocSamples
```

The **canonical PNG generation OS is Windows 11** — glyph rasterization is platform-backed, so
PNG bytes are not identical across operating systems. Regenerate committed PNGs on Windows 11.

A drift test in `ItTiger.TigerCli.Tests` (`DocExamplesDriftTests`) fails when these files differ
from what the generator produces, so rendering changes must regenerate the artifacts in the same
change. HTML/CSS compare exactly (newline-normalized); sidecars compare exactly apart from the
volatile `generated-os:`/`dotnet:` lines; PNG bytes are compared only on Windows 11 and are
existence-checked elsewhere.
