# Structured Output

TigerCli output is not just `Console.WriteLine(...)`. It provides a small rendering model for command-line tools that need predictable terminal output, testable layouts, and the same markup and escaping rules across normal output, help, errors, and semi-interactive controls.

## Overview

TigerCli structured output is a small stack. Pick the highest layer that fits:

```text
CliList / CliDetails -> normal CRUD output
CliTable             -> custom tabular output
CliGrid              -> low-level building block
```

- `CliList` and `CliDetails` are specialized builders over `CliTable` with simplified APIs — the default choice for CRUD-style `list` and `show`/details output.
- `CliTable` is the medium-level structured table API. Use it directly when `CliList` or `CliDetails` are not suitable — custom or richer tabular layouts.
- `CliGrid` is the low-level layout and rendering class. App commands generally should not create it directly; it exists for building higher-level renderables, controls, widgets, sinks, and framework features.
- `TigerConsole` writes markup output and renders all of them; `CliRenderableComponent` is the app-facing base for reusable custom structured output.

The usual rendering path is:

```text
data -> CliList / CliDetails / CliTable -> CliGrid -> measure -> render -> console, TextWriter, or lines
```

The same ideas support generated help, command output, and the inline TUI controls: content is represented structurally, measured, then rendered through TigerCli sinks.

## TigerConsole Basics

Use `TigerConsole` for app output.

```csharp
using ItTiger.TigerCli.Terminal;

TigerConsole.MarkupLine("[Success]Completed.[/]");
TigerConsole.MarkupErrorLine("[Error]Failed.[/]");
```

Use stdout for normal command output:

```csharp
TigerConsole.MarkupLine("Project created.");
```

Use stderr for framework or app errors:

```csharp
TigerConsole.MarkupErrorLine("[Error]Project was not found.[/]");
```

In TigerCli command apps, direct `Console.WriteLine(...)` should usually be avoided. `TigerConsole` keeps markup parsing, stdout/stderr behavior, app tests, and rendering conventions on the same path.

## Output Ownership

Once a TigerCli command writes user-visible output through `TigerConsole` or a TigerCli renderable,
that stream should be treated as TigerCli-owned for the rest of the rendering flow. Use
`TigerConsole.MarkupLine(...)`, `TigerConsole.MarkupErrorLine(...)`, and structured renderables for
normal command output.

Avoid interleaving raw `Console.Write*`, Spectre.Console writes, or another renderer on the same
stdout/stderr stream in the middle of TigerCli output. TigerCli can only guarantee ordering, style
reset behavior, stdout/stderr routing, prompt rendering, app-test capture, and generated artifacts when
the output goes through TigerCli-owned paths.

This is not a ban on integration. Low-level sink code, isolated console interop, and intentionally
separate output phases can still use other APIs. The rule is about stream ownership: do not mix
independent renderers in one TigerCli-owned output flow and expect TigerCli to measure, capture, style,
or restore what it did not render.

## Markup And Escaping

Markup in TigerCli output is trusted text. Dynamic values are not trusted and must be escaped before interpolation.

```csharp
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;

var name = args[0];
TigerConsole.MarkupLine($"Hello, [White]{CliMarkupParser.Escape(name)}[/]!");
```

Use `CliMarkupParser.Escape(...)` for raw markup escaping when no localization lookup is needed, or when escaping an individual value directly.

Inside command handlers that produce localized output from settings, the `TigerCliSettings` helpers are usually cleaner:

```csharp
TigerConsole.MarkupLine(settings.E("Hello, [White]{0}[/]!", name));
```

`settings.E(...)` looks up the format string through the active settings resources/culture and escapes each formatted argument before inserting it into markup.

Generated help follows the same trust model: framework text and trusted app metadata may contain markup, while dynamic values are escaped. See [help rendering trust model](../reference/help-rendering-trust-model.md) for the detailed rules.

## Semantic Theme Markup

Besides raw colors (`[Red]`, `[on Blue]`, `[Red on Blue]`), markup supports a **curated subset of theme-defined semantic styles**. These resolve through the active theme (`TigerConsole.CurrentTheme`), so the same string renders differently under different themes and application/localized strings can use theme roles instead of hard-coded colors.

```csharp
TigerConsole.MarkupLine("[Accent]Name[/]: value");
TigerConsole.MarkupLine("[Success]Connection saved.[/]");
TigerConsole.MarkupErrorLine("[Alert]Error![/] Something went wrong.");
TigerConsole.MarkupLine("[Panel]Connection details[/]");
TigerConsole.MarkupLine("[Dialog]Choose an option[/]");
```

Semantic markup is a curated subset of the theme's styles — not every [ThemeStyle](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.ThemeStyle.html) value is exposed, and table-only ink/surface tokens are intentionally hidden. Apps can also register **custom semantic styles** (e.g. `[ConnectionName]`, `[EnvironmentProd]`) that resolve through the active theme alongside these built-ins — see [themes, styles & colors](themes-and-styles.md).

| Token | Channel | Resolves from |
|-------|---------|---------------|
| `Accent` | foreground-only | `ThemeStyle.Accent` |
| `Muted` | foreground-only | `ThemeStyle.MutedText` |
| `Success` | foreground-only | `ThemeStyle.Success` |
| `Warning` | foreground-only | `ThemeStyle.Warning` |
| `Error` | foreground-only | `ThemeStyle.Error` |
| `Heading` | foreground-only (carries the style's decorations, e.g. bold) | `ThemeStyle.Heading` |
| `Key` | foreground-only | `ThemeStyle.Key` |
| `Value` | foreground-only | `ThemeStyle.Value` |
| `Path` | foreground-only | `ThemeStyle.Path` |
| `Link` | foreground-only (carries the style's decorations, e.g. underline) | `ThemeStyle.Link` |
| `Selected` | foreground + background | `ThemeStyle.Selected` |
| `Alert` | foreground + background | `ThemeStyle.Alert` |
| `Panel` | foreground + background | `ThemeStyle.PanelSurface` (foreground falls back to the theme's text ink) |
| `Dialog` | foreground + background | `ThemeStyle.DialogSurface` (falls back to `PanelSurface`) |

A token's name is framework-known but its visual meaning stays theme/developer-configurable — `[Key]abc[/]` renders using the active theme's `Key` style, not a hardcoded color, and any theme can override these roles (see [themes, styles & colors](themes-and-styles.md)). The CRUD/structured-output roles power `CliList`/`CliDetails` (see [Semantic value styles](#semantic-value-styles)):

```csharp
TigerConsole.MarkupLine("[Heading]Devices[/]");
TigerConsole.MarkupLine("[Key]camera-main[/] [Value]Sony A7IV[/] [Path]/media/inbox[/]");
TigerConsole.MarkupLine("[Link]https://example.com[/]");
```

`[Link]` renders the URL as **visible, copyable text** in every sink. Clickability is a **progressive enhancement**: on the ANSI sink TigerCli additionally wraps the visible text in an OSC 8 hyperlink (target = the link's own visible text) so supporting terminals make it clickable, controlled by `TigerConsole.HyperlinkMode` ([CliHyperlinkMode](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliHyperlinkMode.html).Auto/`Never`/`Always`, default `Auto`). The URL always stays visible and copyable. Nested links are rejected, and there is no hidden-target syntax (`[Link=…]label[/]` is not supported yet). See [themes & styles → Clickable links (OSC 8)](themes-and-styles.md#clickable-links-osc-8).

**Foreground-only vs foreground/background.** Foreground-only tokens change only the text color and **preserve the surrounding background**, so they nest cleanly inside a background-bearing token:

```csharp
// Accent changes only the foreground; the Alert background is kept under it and
// restored when the inner tag closes.
TigerConsole.MarkupLine("[Alert]Error: [Accent]Connection[/] failed.[/]");

// Same rule for surfaces: Accent keeps the Panel background.
TigerConsole.MarkupLine("[Panel]Connection [Accent]Test[/][/]");
```

**Panel vs Dialog.** `[Panel]` uses the reusable elevated **PanelSurface**; `[Dialog]` uses the dialog/control-specific **DialogSurface**, which falls back to `PanelSurface` when a theme does not override it. When a surface defines only a background, the markup foreground falls back to the theme's text ink so the text stays readable.

Semantic names take precedence over raw color names, and unknown tags still throw `FormatException`. The parser itself stays theme-free: the active theme is supplied as an `IMarkupStyleResolver` (`ThemeMarkupStyleResolver`) by the call sites (`TigerConsole.Markup*` and the `CliGrid` measure pass).

## Text Decorations

Markup supports the text decorations `[Bold]`, `[Italic]`, and `[Underline]` (case-insensitive). They
are rendered as ANSI SGR attributes when ANSI output is active. TigerCli controls the emitted ANSI
sequence, not the final font rendering.

```csharp
TigerConsole.MarkupLine("[Bold]Important[/]");
TigerConsole.MarkupLine("[Italic]hint[/]");
TigerConsole.MarkupLine("[Underline]link[/]");
```

The short aliases `[b]`, `[i]`, `[u]` (case-insensitive — `[B]`, `[I]`, `[U]`) are convenience
equivalents of `[Bold]`, `[Italic]`, `[Underline]`. They are **standalone-only** — compose them by
nesting, never inside a composed visual expression (`[b red on blue]` is invalid; use
`[B][Red on Blue]…[/][/]`).

```csharp
TigerConsole.MarkupLine("[b]Bold[/]");
TigerConsole.MarkupLine("[B][Red ON Blue]Error![/][/]");
```

`[Bold]` / [CliTextDecoration](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliTextDecoration.html).Bold emits ANSI SGR 1. Some terminals treat SGR 1 as an
"intense text" request rather than guaranteed font weight. Depending on terminal settings, it may
render as a bold font, brighter colors, both, or neither. Windows Terminal exposes this through the
`Intense text style` setting. Italic and underline are also terminal/font dependent. Do not rely on
bold alone to carry critical meaning; combine important emphasis with semantic styles or text where
appropriate.

Decorations are **additive**: nesting ORs them onto the surrounding style, and closing a tag restores the previous style.

```csharp
// Bold | Underline inside the nested span; just Bold after the inner [/].
TigerConsole.MarkupLine("[Bold]a[Underline]b[/]c[/]");
```

### Raw style expressions

A raw (non-semantic) tag is a **style expression** with the grammar:

```text
[<decoration>* <foreground>? (on <background>)?]
```

- Zero or more decoration tokens (`Bold`, `Italic`, `Underline`) come **first**; their order is unimportant.
- An optional foreground color follows the decorations.
- An optional background, introduced by `on`, comes last (and may appear with no foreground).
- Colors are [CliColor](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliColor.html) names, or an app-registered raw color alias — never semantic style names. TigerCli ships **no** built-in color aliases; apps register their own (see [themes, styles & colors](themes-and-styles.md)).

```csharp
TigerConsole.MarkupLine("[Bold Yellow]Warning[/]");
TigerConsole.MarkupLine("[Bold Italic Yellow on Green]Styled[/]");
TigerConsole.MarkupLine("[Underline Italic on Blue]No foreground override[/]");
```

### Semantic styles vs raw expressions

A **semantic** style tag (`[Accent]`, `[Panel]`, `[Success]`, …) must be the **only** token in the tag. A semantic style may itself define foreground, background, and/or decorations as a complete style. Compose a semantic style with extra decorations (or a surface) through **nesting**, not by mixing tokens in one tag:

```csharp
TigerConsole.MarkupLine("[Bold][Accent]Title[/][/]");          // Bold OR Accent's own decorations
TigerConsole.MarkupLine("[Panel][Bold][Accent]Title[/][/][/]"); // Accent fg, Panel bg, Bold
```

The following forms are **invalid** and throw `FormatException`:

| Invalid | Why | Use instead |
|---------|-----|-------------|
| `[Yellow Bold]` | decoration after a color | `[Bold Yellow]` |
| `[Bold Accent]` | semantic name mixed into a raw expression | `[Bold][Accent]…[/][/]` |
| `[Accent on Panel]` | semantic names are not raw colors | `[Panel][Accent]…[/][/]` |
| `[ConnectionName on Blue]` | a custom style is semantic, not a color | `[on Blue][ConnectionName]…[/][/]` |
| `[Heading Red on Blue]` | a semantic role is standalone-only | `[Heading]…[/]` |
| `[b red on blue]` | a short alias is standalone-only | `[B][Red on Blue]…[/][/]` |

Raw foreground/background syntax is for **colors only**; semantic style names (framework or custom) never appear inside a raw style expression. For the full theme/style/color-alias registration model, see [themes, styles & colors](themes-and-styles.md).

## CliGrid

`CliGrid` is TigerCli's low-level structured layout primitive. It represents a rectangular layout made of cells, rows, and columns. A grid can measure content, wrap or truncate text, apply styles and alignment, support spans and frames, and render to multiple targets.

App command handlers should generally not build `CliGrid` directly — render `CliList`, `CliDetails`, `CliTable`, or an app-specific `CliRenderableComponent` instead. `CliGrid` is the layer for building higher-level renderables, controls, widgets, sinks, and framework features, plus low-level rendering code and tests.

For reusable custom output, derive from `CliRenderableComponent`. Override `ToGrid()`, call the protected `ToGrid(columnCount, rowCount)` helper, then populate the returned grid:

```csharp
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

public sealed class SummaryComponent : CliRenderableComponent
{
    public string Title { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    public override CliGrid ToGrid()
    {
        var grid = ToGrid(columnCount: 2, rowCount: 1);

        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle
        {
            Width = 12,
            HorizontalAlignment = CliTextAlignment.Right
        }));

        grid.Set(0, 0, Title);
        grid.Set(1, 0, Value);

        return grid;
    }
}

TigerConsole.Render(new SummaryComponent
{
    Title = "Status",
    Value = "Ready"
});
```

Important concepts: [CliTextAlignment](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliTextAlignment.html) controls the horizontal alignment used by a cell.

- Cells hold content and optional cell style.
- Rows and columns can have definitions and default styles.
- `Set(...)` can span cells with `colSpan` and `rowSpan`.
- Width, min/max width, wrapping, truncation, alignment, padding, and color live in `CliCellStyle`.
- Frames can be added with `AddFrameArea(...)`.
- Layout is measured before rendering, so output can adapt to width constraints.
- `CliRenderableComponent.ToGrid(columnCount, rowCount)` copies shared component layout settings to the generated grid.

For most app output, start with `CliList`/`CliDetails`, then `CliTable`. Reach for a custom `CliRenderableComponent` when you need an irregular reusable layout, custom framing, cell spans, or component-specific rendering.

## CliTable

`CliTable` is the medium-level API for table-shaped data: headers, records, frames, orientation, null display, and per-field styles. For normal CRUD output, prefer [`CliList`](#clilist) and [`CliDetails`](#clidetails), which build on `CliTable`; use `CliTable` directly when those builders are not suitable. Its [CliTableStylePreset](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliTableStylePreset.html) and [CliTableOrientation](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliTableOrientation.html) options cover common visual recipes and layout direction. For simple tables, choose a preset, add a header in one call, then add records:

```csharp
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

var table = new CliTable()
    .ApplyPreset(CliTableStylePreset.Torino)
    .AddHeader(settings.T("Name"), settings.T("Count"));

table.AddRecord("Projects", 3);
table.AddRecord("Connections", 2);

TigerConsole.Render(table);
```

`AddHeader(params string[])` takes plain captions (e.g. `settings.T("Name")` or a literal), and `AddRecord(params object?[])` takes simple values — `string`, enum, `bool`, `int`, or `null`. `CliTableStyles` exposes predefined "city" table styles (recipes); each resolves against the active TigerCli theme (`ITheme`), so the table follows the current theme. A style sets defaults you can still customize before adding records.

`CliTable` produces a `CliGrid` internally. This means the same measurement and rendering pipeline applies:

```csharp
var grid = table.ToGrid();
TigerConsole.RenderGrid(grid);
```

See [CliTable](cli-table.md) for table orientation, frames, header behavior, record shape, null handling, and rendering tables to testable lines.

## CliList

`CliList<T>` is a focused builder for **list command output** — the multi-row table a `list` command prints. It removes the manual pattern of rendering headings, spacing, indentation, loops, and per-column formatting by hand with `MarkupLine`. You declare columns once (label + selector), then render a sequence of items:

```csharp
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

var list = new CliList<Device>()
    .ApplyPreset(CliTableStylePreset.Lucca)
    .AddTitle(s.T("Devices"))
    .AddKeyColumn(s.T("Id"), device => device.Id)
    .AddColumn(s.T("Name"), device => device.Name)
    .AddColumn(s.T("Model"), device => device.Model)
    .AddKeyColumn(s.T("Group"), device => device.GroupId);

TigerConsole.Render(list.Render(devices));
```

`Render(items)` projects each item into a record and returns a `CliTable`, so output goes through the normal `CliTable` → `CliGrid` pipeline. An **empty** sequence yields a header-only table — a consistent empty-state default that still shows the columns and title; a command that wants a custom empty message can branch before calling `Render`.

Columns:

- `AddColumn(label, selector)` — a normal column; values use the preset's body styling.
- `AddColumn(label, selector, style: ThemeStyle.X)` — a column whose **values** use a semantic style.
- `AddKeyColumn(label, selector)` — convenience for `style: ThemeStyle.Key` (identity/anchor values: IDs, names, codes, slugs, group IDs).
- `AddPathColumn(label, selector)` — convenience for `style: ThemeStyle.Path` (filesystem paths).
- `AddLinkColumn(label, selector)` — convenience for `style: ThemeStyle.Link` (navigable/link values). Each row's value is also its own hyperlink target: visible/copyable everywhere, and clickable on the ANSI sink when `HyperlinkMode` allows.

See [Semantic value styles](#semantic-value-styles) for what the styles mean and how they resolve.

**Title alignment.** The title is centered by default. For command-result output a left-aligned section title often reads better. Pass an alignment to `AddTitle`, or set it separately with `SetTitleAlignment` — both affect layout only, preserving the title's semantic style:

```csharp
new CliList<FileEntry>()
    .AddTitle("Files written:", alignment: CliTextAlignment.Left)
    .AddPathColumn("Path", f => f.Path);

// equivalently
new CliList<FileEntry>()
    .AddTitle("Files written:")
    .SetTitleAlignment(CliTextAlignment.Left);
```

Wrapping and truncation (see [Wrapping and truncation](#wrapping-and-truncation)) are opt-in per column:

```csharp
var list = new CliList<Device>()
    .ApplyPreset(CliTableStylePreset.Lucca)
    .DefaultWrapping(CliWrapping.WordWrap)                 // default for columns that don't override
    .AddKeyColumn(s.T("Id"), d => d.Id)
    .AddColumn(s.T("Description"), d => d.Description)
        .SetWrapping(CliWrapping.WordWrapTruncate)         // override for this column
        .SetWidth(maxWidth: 40);                           // the bound it wraps/truncates within
```

`SetWrapping` / `SetWidth` configure the **most-recently added column**. A list is vertical, so each column is its own grid column and carries both its wrapping mode and its width bound.

## CliDetails

`CliDetails` is a focused builder for **one-record key/value detail views** — the "Name: value" panel a command prints to show a single object. It removes the noisy, error-prone pattern of maintaining a parallel label list and value list by hand:

```csharp
// Manual pattern: app code keeps labels and values aligned, decides visibility, formats missing values.
var header = new List<string>();
var record = new List<object?>();
header.Add(s.T("Name:"));   record.Add(profile.Name);
header.Add(s.T("Server:")); record.Add(profile.Server);
if (profile.Username is not null) { header.Add(s.T("Username:")); record.Add(profile.Username); }
// ...build a table from header + record by hand...
```

`CliDetails` expresses the same intent declaratively:

```csharp
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

var details = new CliDetails()
    .ApplyPreset(CliTableStylePreset.Details)
    .AddTitle(s.T("SQL Server connection"))
    .Add(s.T("Name:"), profile.Name)
    .Add(s.T("Server:"), profile.Server)
    .Add(s.T("Authentication:"), profile.Authentication)
    .AddOptional(s.T("Username:"), profile.Username)
    .Add(s.T("Database:"), profile.Database, missingDisplay: s.T("(not selected)"));

TigerConsole.Render(details);
```

```text
       SQL Server connection
┌─────────────────┬────────────────┐
│ Name:           │ prod-core      │
│ Server:         │ localhost      │
│ Authentication: │ SqlPassword    │
│ Database:       │ (not selected) │
└─────────────────┴────────────────┘
```

(`Username:` is absent: `AddOptional` skips a missing value, while `Database:` is shown with an explicit missing display.)

Like `CliList`, the title is centered by default; pass `AddTitle("…", alignment: CliTextAlignment.Left)` or call `SetTitleAlignment(CliTextAlignment.Left)` to left-align (or right-align) it. Alignment affects layout only — the title's semantic style is preserved.

### Choosing a rendering abstraction

- Use **`CliList`** for `list` command output (multiple records).
- Use **`CliDetails`** for `show`/details output (a single record presented as labelled fields).
- Use **`CliTable`** directly only when you need lower-level table control (custom header/record construction, frames, orientation, per-field styling beyond a semantic value style).
- Use **`MarkupLine`** for simple messages — confirmations, status, errors — not for normal list/details rendering.

With [app default output presets](#app-default-output-presets) configured, individual commands rarely need `.ApplyPreset(...)` at all.

`CliDetails` is a builder, not a separate renderer: `ToTable()` converts it to a `CliTable` and `TigerConsole.Render(details)` goes through the normal `CliTable` → `CliGrid` pipeline. A detail view is always horizontal — labels are row headers and the single record is the value column — so `ToTable()` forces `CliTableOrientation.Horizontal` regardless of the applied preset; a non-details preset such as Roma or Milano contributes only visual styling. The default preset is `CliTableStylePreset.Details`.

### Add vs AddOptional vs AddWhen

| Method | Renders the field when… | Missing value behavior |
|--------|-------------------------|------------------------|
| `Add(label, value)` | always | shows the missing display |
| `AddOptional(label, value)` | value is present | field is omitted entirely |
| `AddWhen(condition, label, value)` | `condition` is `true` | when shown, behaves like `Add` |

This separates two distinct intents: *"optional field, hide when absent"* (`AddOptional`) versus *"always show this field, mark it as missing when absent"* (`Add` with a missing display).

```csharp
details.AddOptional(s.T("Username:"), profile.Username);              // no row when null/blank
details.Add(s.T("Database:"), profile.Database, "(not selected)");    // Database: (not selected)
```

`Add`, `AddOptional`, and `AddWhen` each accept an optional `style:` parameter that applies a [semantic value style](#semantic-value-styles) to the **value** (never the label):

```csharp
details
    .AddKey(s.T("Id:"), profile.Id)                               // value styled as Key
    .Add(s.T("Server:"), profile.Server, style: ThemeStyle.Key)   // explicit style
    .AddPath(s.T("Config:"), profile.ConfigPath)                  // value styled as Path
    .AddOptionalPath(s.T("Log:"), profile.LogPath);               // Path, omitted when absent
```

Convenience helpers wrap the common cases: `AddKey` (→ `ThemeStyle.Key`), `AddPath` / `AddOptionalPath` (→ `ThemeStyle.Path`), and `AddLink` / `AddOptionalLink` (→ `ThemeStyle.Link`). There is intentionally no `AddKeyWhen`/`AddPathWhen`/`AddOptionalKey` — key values are normally present, and a rare conditional/optional key is expressed with the generic styled overload (`AddWhen(condition, label, value, style: ThemeStyle.Key)` or `AddOptional(label, value, style: ThemeStyle.Key)`).

### Semantic value styles

`CliList` columns and `CliDetails` fields can carry a semantic `ThemeStyle` that styles their **values** through the active theme — so commands never hardcode colors:

| Style | Use for | Default mapping |
|-------|---------|-----------------|
| `ThemeStyle.Key` | identity/anchor values (IDs, names, codes, slugs, group IDs) | accent foreground |
| `ThemeStyle.Value` | normal primary field values | normal text foreground |
| `ThemeStyle.Path` | filesystem/local path-like values | muted foreground |
| `ThemeStyle.Link` | navigable/link values (visible, copyable; the value also becomes a clickable OSC 8 target on the ANSI sink — see `CliHyperlinkMode`) | accent foreground + underline |
| `ThemeStyle.Heading` | section/list/details headings and structured-output titles | accent foreground + bold |

These are foreground-only roles: the background stays untouched so the table/detail surface (panel, zebra, alert) shows through. Each role falls back to a base token, so every theme styles values without an override; a theme can override `Key`/`Value`/`Path`/`Link` on its `ThemeBase` subclass to recolor them. Custom semantic styles (registered via `TigerConsole.CustomStyles`) continue to work unchanged.

### Wrapping and truncation

`CliList` and `CliDetails` expose the same wrapping/truncation their underlying `CliTable` already supports — they add no rendering logic. Wrapping is a `CliCellStyle.Wrapping` on the **value** cell (via a `CliWrapping` mode such as `WordWrap`, `SingleLineTruncate`, `WordWrapTruncate`, `CharWrap`, …) combined with a **width bound**. It affects **layout only**: semantic value styles (`Key`/`Path`/`Link`/…) and the preset surface are preserved.

| Concern | `CliList` (vertical) | `CliDetails` (horizontal) |
|---------|----------------------|---------------------------|
| Default wrapping | `DefaultWrapping(wrapping)` | `DefaultWrapping(wrapping)` |
| Per-item override | `SetWrapping(wrapping)` — last column | `SetWrapping(wrapping)` — last field |
| Width bound | `SetWidth(width, minWidth, maxWidth)` — per column | `SetValueWidth(width, minWidth, maxWidth)` — one shared value column |

Two rules follow from the table pipeline:

- **A width bound is what makes text wrap or truncate.** A wrapping mode alone does nothing until content exceeds a width — set a `maxWidth`, or render the list/detail view under a soft/hard max width (a narrow terminal, `SoftMaxWidth`, or a component `MaxWidth`) so over-wide values wrap/truncate.
- **Precedence:** a per-column/per-field `SetWrapping` overrides `DefaultWrapping`; a column/field with neither keeps the preset's (non-wrapping) body styling.

`CliList` width is per-column because each column is its own grid column; `CliDetails` width is a single view-level bound because a horizontal detail view has exactly one value column.

### Wrapping in a detail view

`CliDetails` fields support the same [wrapping and truncation](#wrapping-and-truncation) as `CliList`, with one difference driven by orientation: a detail view is **horizontal**, so every value lives in a **single shared value column**. Wrapping *mode* is therefore per-field, while the value-column *width* is a single view-level bound:

```csharp
var details = new CliDetails()
    .ApplyPreset(CliTableStylePreset.Details)
    .DefaultWrapping(CliWrapping.WordWrap)        // default field-value wrapping mode
    .SetValueWidth(maxWidth: 50)                  // shared value-column width (all fields)
    .AddKey(s.T("Id:"), profile.Id)
    .Add(s.T("Notes:"), profile.Notes)
        .SetWrapping(CliWrapping.WordWrapTruncate); // per-field override for the last field
```

`SetWrapping` configures the **most-recently added field**, and is a graceful **no-op** when the preceding add was skipped (a missing `AddOptional` or a false `AddWhen`) — there is no field to configure, and an earlier field is never accidentally reconfigured.

### Missing value semantics

A value is **missing** when it is `null`, or a `string` that is empty or all-whitespace. Falsy-but-meaningful values — `false` and `0` — are **present** and render normally. So `AddOptional("Trusted:", false)` and `AddOptional("Timeout:", 0)` both render their fields.

When a field is rendered for a missing value, it shows a **missing display** (markup-aware):

- Per field: the `missingDisplay` argument, e.g. `Add("Database:", db, "(not selected)")`.
- Default for the view: `SetMissingDisplay("(n/a)")`.
- Built-in default: `CliDetails.DefaultMissingDisplay` — a muted `[Muted](not set)[/]`.

Missing displays support markup the same way header captions do, so `[Muted](not selected)[/]` resolves through the active theme.

## App Default Output Presets

Instead of repeating `.ApplyPreset(...)` in every command, set default presets for the structured output builders once, on the app builder:

```csharp
var app = TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .SetDefaultOutputPresets(
        details: CliTableStylePreset.Details,
        list: CliTableStylePreset.Lucca,
        table: CliTableStylePreset.Milano)
    .SetDefaultCommand<MyCommand>()
    .Build();
```

Rules:

- An explicit `.ApplyPreset(...)` on an individual `CliDetails`, `CliList`, or `CliTable` still wins over the app default.
- The `details` and `list` defaults apply to `CliDetails` and `CliList<T>` instances that have not applied their own preset/style.
- The `table` preset is optional. When set, it applies to direct `CliTable` instances rendered without an explicit preset/style.
- Without `SetDefaultOutputPresets`, existing behavior is unchanged — e.g. `CliDetails` keeps its built-in `Details` preset default.
- Defaults apply only during that app's run; they do not leak between apps.

App defaults keep CRUD output visually consistent across commands without per-command styling code.

## Auto-Fit And Terminal Width

Structured output can measure and adapt instead of relying on hand-counted spaces.

When rendering to the console, TigerCli uses a console sink that exposes the current console width as a soft layout constraint. You can also set layout constraints explicitly:

```csharp
var grid = table.ToGrid();
grid.SoftMaxWidth = 80;
TigerConsole.RenderGrid(grid);
```

### Redirected, piped, and captured output

TigerCli supports redirected/captured output (`app list > out.txt`, `app list | tool`, test/CI capture).
The console width comes from a redirection-safe helper (`TerminalCapabilities.GetSafeOutputWidth()`):
`Console.WindowWidth` is a terminal *capability*, not a guaranteed value, and reading it under
redirection can throw ("The handle is invalid."). When the real width cannot be detected, TigerCli
falls back to a deterministic width of **120 columns** (`TerminalCapabilities.DefaultOutputWidth`).

- Rendering never throws because the terminal width is unavailable.
- Visible text and structured output are still emitted (laid out at 120 columns).
- This is a **layout/wrapping** fallback only. It does **not** imply ANSI/color support — color is
  decided independently by the color-mode model, which still suppresses ANSI on a redirected stream
  under `Auto`. Clickable links remain visible/copyable regardless.

This matters for CLI tools because it gives you:

- more predictable layout than manual padding
- wrapping and truncation in one place
- less spacing code inside command handlers
- output that is easier to snapshot-test

Not every output should be forced to fit every terminal. For very large datasets, prefer app-level choices such as filtering, paging, file export, or a simpler format.

## Rendering To Lines

TigerCli rendering can produce plain lines for tests or composition.

```csharp
var lines = TigerConsole.RenderGridToLines(grid);
```

For any `CliRenderableComponent`, including `CliTable`, use:

```csharp
var lines = TigerConsole.RenderToLines(table);
```

This is one reason TigerCli output is testable. Tests can assert on the same measured layout without using the real console or parsing ANSI sequences.

```csharp
Assert.Contains(lines, line => line.Contains("Projects", StringComparison.Ordinal));
```

App-level command tests can also capture stdout and stderr through `TigerCliAppTestHost`; see [app testing](app-testing.md).

## Rendering To HTML

TigerCli rendering can also produce deterministic HTML — useful for snapshot-style tests and for
generating documentation examples from real rendering. Output is `<pre class="tigercli">` + styled
`<span>` (and optionally `<a>`); it is opt-in and never affects console/ANSI output. [HtmlHyperlinkMode](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.HtmlHyperlinkMode.html) controls whether safe markup links render as anchors.

```csharp
// Any renderable (CliTable, CliList result, CliDetails, …):
string html = TigerConsole.RenderToHtml(table);

// Markup:
string fragment = TigerConsole.MarkupToHtml("[Heading]Devices[/]");

// Anchors for links (default is visible text only):
var options = new HtmlSinkOptions { WrapInPre = true, HyperlinkMode = HtmlHyperlinkMode.Anchor };
string withLinks = TigerConsole.RenderToHtml(details, options);
```

Example output:

```html
<pre class="tigercli"><span class="tc-bold" style="color:#FF0000">Error</span>
<a class="tc-link" href="https://example.com">https://example.com</a></pre>
```

Link text is always visible/copyable; text/attributes are escaped; no ANSI is emitted. Semantic token
names (e.g. `[Heading]`) are not reconstructed — the resolved style is rendered. See
[HtmlSink](../reference/html-sink.md) for options, the CSS class set, and safety rules.

## Structured Output In Command Handlers

Command handlers should keep output on the TigerCli path:

```csharp
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

public sealed class SummaryCommand
    : TigerCliAsyncCommandHandler<SummarySettings>
{
    public override Task<int> ExecuteAsync(SummarySettings settings)
    {
        var table = new CliTable()
            .ApplyPreset(CliTableStylePreset.Torino)
            .AddHeader(settings.T("Name"), settings.T("Value"));

        table.AddRecord("Project", settings.ProjectName);
        table.AddRecord("Status", "Ready");

        TigerConsole.Render(table);
        return Task.FromResult(0);
    }
}
```

Guidelines:

- Use `CliList` for `list` command output and `CliDetails` for `show`/details output.
- Use `CliTable` directly only when you need lower-level table control.
- Set app-wide presets once with [`SetDefaultOutputPresets(...)`](#app-default-output-presets) instead of repeating `.ApplyPreset(...)` per command.
- Use `TigerConsole.MarkupLine(...)` for simple messages, not for normal list/details rendering.
- Use a custom `CliRenderableComponent` when layout matters beyond a table.
- Escape dynamic values in markup output. Use `settings.E(...)` for localized command messages from settings, and `CliMarkupParser.Escape(...)` for raw value escaping.
- Keep errors on stderr with `TigerConsole.MarkupErrorLine(...)`.

See [CRUD command apps](crud-commands.md) for how list/show/add/edit/delete map onto these abstractions and prompts.

## What Structured Output Is Not

TigerCli is not trying to be a giant widget catalog. It provides a coherent rendering model for command-line applications: markup text, grids, tables, render sinks, and inline semi-interactive controls.

For long-lived full-screen terminal applications with complex navigation, TigerCli may not be the right layer. The current structured output model is strongest for command output, generated help, testable render components, and semi-interactive inline prompts.

## Related Docs

- Render tables with [CliTable](cli-table.md).
- Understand escaping with the [help rendering trust model](../reference/help-rendering-trust-model.md).
- Use inline controls with [semi-interactive prompts](semi-interactive-prompts.md).
- Test output with [app testing](app-testing.md).
- Build command handlers with [command apps](command-apps.md).
