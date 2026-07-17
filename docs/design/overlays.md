# Overlay Design

## Purpose

This page explains the role of overlays in TigerCli rendering. It is not a full API reference.

## Core Idea

Overlays are post-layout adornments. They are rendered after a `CliGrid` has been measured and do not participate in layout.

## Why This Exists

Some terminal UI details need to sit on top of measured content without changing the layout model. Examples include scrollbars and horizontal scroll indicators.

Keeping overlays narrow prevents a second rendering system from growing beside `CliGrid`.

## Design Rules

- Overlays are one-dimensional: vertical or horizontal.
- Overlays start at a grid cell.
- Overlays render after measurement.
- Overlays do not affect sizing, wrapping, or alignment.
- Overlays may overwrite rendered cells.
- Overlay styling may be uniform or per-character (see below). It never affects layout.
- Overlap is intentionally constrained for deterministic output.

## Styling

Every overlay carries a base `CliCharStyle` (`CliOverlay.Style`). There are two ways to produce content:

- **Plain (uniform) — `CliOverlayRenderer`.** Returns `char[]`. Every character is drawn with the
  overlay's base style. This is the original path; scrollbars, scroll indicators, dynamic text, and
  single-colour progress bars all use it and are unchanged.
- **Styled (per-character) — `CliStyledOverlayRenderer`.** Returns `CliOverlayGlyph[]`, where each glyph
  is a character plus an optional `CliCharStyle`. A glyph whose style is `null` falls back to the
  overlay's base style, so a renderer only specifies a style where it differs. This lets a single
  overlay mix styles — for example a two- or three-colour progress bar.

Both paths are applied through a single internal styled pipeline: a plain renderer is adapted into glyphs
that carry no per-character style, so its output resolves to the base style and renders exactly as
before. Overlays still own no theme: callers resolve [ThemeStyle](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.ThemeStyle.html) to `CliCharStyle` before constructing
the overlay (the base style and any per-glyph styles are already resolved).

### Multi-style progress bars

`CliOverlayRenderers.ProgressBar` has two forms. The original `char`-based overload is a single-style bar
(filled/track glyphs share the overlay base style) and is unchanged. A styled overload takes
`CliOverlayGlyph` segments — `done`, `track`, and an optional `completed` — each a glyph plus an optional
pre-resolved `CliCharStyle`:

- **Two-style:** `done` and `track` with distinct styles.
- **Three-style / completed:** when the fraction reaches **exactly 100%** and `completed` is supplied, the
  whole filled interior is drawn with `completed` instead of `done` (a "completed state"). Below 100%
  `completed` is never used — including the case where rounding alone fills the interior — so the trigger
  is the value reaching 1.0, not the rendered fill count.

End caps (`leftCap`/`rightCap`) behave as in the single-style bar: they occupy the outer cell(s), are
dropped when the strip is too short to also hold one interior cell, and carry no per-glyph style (so they
use the overlay base style). Segment styles are pre-resolved `CliCharStyle` values; the renderer stays
theme-agnostic.

## Boundary

Use grid structure for real content. Use overlays only for visual adornment that depends on measured state.

Hint text that is part of the user-visible content should be a normal grid row, not an overlay.

## Related Docs

- [Structured output](../guides/structured-output.md)
- [Semi-interactive TUI design](semi-interactive-tui.md)
