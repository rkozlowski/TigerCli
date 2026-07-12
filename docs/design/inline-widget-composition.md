# Inline Widget Composition Design

TigerCli inline dialogs host one top-level control. Richer dialogs are built by making that control composite, not by making grids interactive or by stacking unrelated dialogs.

## Current Model

- `InlineDialog` hosts exactly one `InlineControlBase`.
- A control exposes one or more top-level widget areas through `GetWidgets()`.
- `InlineWidget` is the reusable embeddable unit for rendering and key handling inside a control.
- `InlineMultiControl` is the composite base for controls made from multiple widgets.
- `InlineFolderSelect` is a composite control with an editable path input, a folder list, and a button row.

## Boundaries

- `CliGrid` renders and measures; it never processes keys.
- Controls and widgets own behavior, focus, and key handling.
- The dialog-owned hint/status row stays with `InlineDialog`; there is no status-bar widget.
- Composite controls coordinate domain state across widgets. Individual widgets stay reusable and context-light.
- Standalone controls should prefer composition over inheritance where widget reuse is useful.

## Focus and Active Point

The active widget determines the parent grid's `ActivePoint`. That one pointer drives:

- cursor placement
- active scrollable cell
- scrollbar and indicator source
- focused widget rendering

Inactive scrollable widgets preserve their offsets but do not chase selection or show active indicators.

See also:

- [Semi-interactive TUI design](semi-interactive-tui.md)
- [CliGrid measurement ownership](cli-grid-measurement-ownership.md)
- [API map](../reference/api-map.md)
