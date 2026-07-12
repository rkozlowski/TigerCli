# Bundled Fonts

TigerCli.PngSink bundles static font files for deterministic documentation image generation.

## Terminal Font

Terminal output uses Cascadia Mono static TTF files from the Cascadia Code stable release:

- Release page: https://github.com/microsoft/cascadia-code/releases/tag/v2407.24
- Version/tag: `v2407.24` (`Cascadia Code 2407.24`)
- Downloaded ZIP: `CascadiaCode-2407.24.zip`
- Extracted from: `ttf/static/`

Files included:

- `CascadiaMono-Regular.ttf`
- `CascadiaMono-Bold.ttf`
- `CascadiaMono-Italic.ttf`
- `CascadiaMono-BoldItalic.ttf`

The package intentionally does not include Cascadia Code ligature fonts, Cascadia Mono PL/Powerline
fonts, Cascadia Mono NF/Nerd Font fonts, or variable fonts.

License: SIL Open Font License 1.1. See `CASCADIA-LICENSE.txt`.

Cascadia Mono is the primary terminal font. Bundled Noto Sans and Noto Sans Symbols 2 are also used as
explicit pinned fallbacks for terminal glyphs that Cascadia Mono does not contain; no OS font fallback
is used.

## Title Font

Title/window UI text uses Noto Sans static TTF files from the Noto fonts repository:

- Source repository: https://github.com/notofonts/noto-fonts
- Source path: `hinted/ttf/NotoSans/`
- Version/source: repository `main` branch at the time the assets were added

Files included:

- `NotoSans-Regular.ttf`
- `NotoSans-Bold.ttf`
- `NotoSans-Italic.ttf`
- `NotoSans-BoldItalic.ttf`
- `NotoSansSymbols2-Regular.ttf`

License: SIL Open Font License 1.1. See `OFL.txt`.

Variable fonts are intentionally not bundled in v1. Static TTFs keep rendering setup explicit and
avoid variable-axis ambiguity in generated documentation artifacts.
