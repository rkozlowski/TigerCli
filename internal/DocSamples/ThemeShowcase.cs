using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace DocSamples;

/// <summary>
/// Theme style showcase for reviewing the built-in themes before release: every
/// <see cref="ThemeStyle"/> role rendered in reviewable combinations — base inks on the default
/// background, full-width surface blocks with common inks on each surface, dialog/control roles on
/// the dialog surface, and the structured-output/table/progress roles. Styles are resolved
/// explicitly against each theme (never the ambient <c>TigerConsole.CurrentTheme</c>), and every
/// style name appears as visible text rendered in its own resolved style.
/// <para>One HTML page per theme is committed under <c>docs/examples/</c> and drift-checked with
/// the rest of the artifact set; the <c>themes</c> mode renders the same grids to the current
/// console with ANSI for live review.</para>
/// </summary>
public static class ThemeShowcase
{
    private static ITheme[] BuiltInThemes() => [new TigerBlueTheme(), new DarkTheme(), new LightTheme()];

    /// <summary>The committed HTML artifacts: <c>theme-tiger-blue.html</c>, <c>theme-dark.html</c>,
    /// <c>theme-light.html</c>, each on the shared documentation terminal width.</summary>
    public static IReadOnlyList<DocArtifact> Generate()
    {
        var artifacts = new List<DocArtifact>();
        foreach (var theme in BuiltInThemes())
        {
            var sections = BuildSections(theme)
                .Select(s => new DocPage.Section(
                    s.Heading, s.Description, null,
                    TigerConsole.RenderGridToHtml(s.Grid, DocTerminal.Html())))
                .ToList();

            artifacts.Add(DocArtifact.Text(
                $"theme-{theme.Name}.html",
                DocPage.BuildPage(
                    $"Theme: {theme.Name}",
                    $"Every ThemeStyle role of the built-in '{theme.Name}' theme, resolved through the "
                    + "ThemeBase fallback rules and rendered through HtmlSink. Style names are rendered "
                    + "in their own resolved style; surface sections show the common foreground inks on "
                    + "each surface background.",
                    sections)));
        }

        return artifacts;
    }

    /// <summary>The <c>themes [name]</c> mode: renders the showcase with ANSI to the current
    /// console, for all built-in themes or the one named theme.</summary>
    public static int RenderToConsole(string? themeName)
    {
        var themes = BuiltInThemes();
        if (themeName is not null)
        {
            var match = Array.Find(themes,
                t => string.Equals(t.Name, themeName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                Console.Error.WriteLine(
                    $"Unknown theme '{themeName}'. Built-in themes: {string.Join(", ", themes.Select(t => t.Name))}");
                return 1;
            }

            themes = [match];
        }

        foreach (var theme in themes)
        {
            Console.WriteLine();
            Console.WriteLine($"════ Theme: {theme.Name} ════");
            foreach (var section in BuildSections(theme))
            {
                Console.WriteLine();
                Console.WriteLine($"── {section.Heading} ──");
                TigerConsole.RenderGrid(section.Grid);
            }
        }

        return 0;
    }

    // ---- sections ----------------------------------------------------------------------------

    private sealed record ShowcaseSection(string Heading, string Description, CliGrid Grid);

    private static IReadOnlyList<ShowcaseSection> BuildSections(ITheme theme) =>
    [
        new("Base text inks",
            "Foreground inks rendered on the theme's default Background surface.",
            BuildBaseInks(theme)),

        new("Surfaces",
            "Each surface as a full-width block: the surface name (in the theme's Text ink), a "
            + "sample in the surface's own style, then the common foreground inks on that surface.",
            BuildSurfaces(theme)),

        new("Dialog and controls",
            "Dialog/prompt roles on the DialogSurface: title, frame, text inputs, selection "
            + "highlights, buttons with focus markers, scrollbar, and the status row.",
            BuildDialog(theme)),

        new("Structured output, tables, and progress",
            "CRUD value roles (Heading, Key, Value, Path, Link), the table foreground inks, and the "
            + "progress-bar inks, on the default Background.",
            BuildStructured(theme)),
    ];

    private static readonly (ThemeStyle Ink, string Sample)[] BaseInks =
    [
        (ThemeStyle.Text, "Normal body text."),
        (ThemeStyle.MutedText, "Secondary, de-emphasized text."),
        (ThemeStyle.Accent, "Emphasized accent text."),
        (ThemeStyle.Success, "Operation completed."),
        (ThemeStyle.Warning, "Check the configuration."),
        (ThemeStyle.Error, "The operation failed."),
        (ThemeStyle.Alert, "Immediate attention required."),
    ];

    private static CliGrid BuildBaseInks(ITheme theme)
    {
        var background = theme.Resolve(ThemeStyle.Background);
        var grid = CreateGrid(contentColumns: 2, rows: BaseInks.Length);
        for (int i = 0; i < BaseInks.Length; i++)
        {
            FillRow(grid, i, background,
                (BaseInks[i].Ink.ToString(), Padded(theme.Resolve(BaseInks[i].Ink))),
                (BaseInks[i].Sample, Padded(theme.Resolve(BaseInks[i].Ink))));
        }

        return grid;
    }

    private static readonly ThemeStyle[] Surfaces =
    [
        ThemeStyle.Background,
        ThemeStyle.PanelSurface,
        ThemeStyle.DialogSurface,
        ThemeStyle.Status,
        ThemeStyle.WarningSurface,
        ThemeStyle.ErrorSurface,
    ];

    private static readonly ThemeStyle[] SurfaceInks =
    [
        ThemeStyle.Text,
        ThemeStyle.MutedText,
        ThemeStyle.Accent,
        ThemeStyle.Success,
        ThemeStyle.Warning,
        ThemeStyle.Error,
    ];

    private static CliGrid BuildSurfaces(ITheme theme)
    {
        // Per surface: a header row (name in the Text ink so it stays readable, plus a sample in the
        // surface's own style — which reveals surfaces that define no foreground), one row of inks on
        // the surface, and a Background spacer row between surfaces.
        var background = theme.Resolve(ThemeStyle.Background);
        var grid = CreateGrid(contentColumns: SurfaceInks.Length, rows: (Surfaces.Length * 3) - 1);
        for (int i = 0; i < Surfaces.Length; i++)
        {
            var surface = theme.Resolve(Surfaces[i]);
            int row = i * 3;
            grid.SetRow(row, new CliGridRowDefinition(surface));
            grid.Set(0, row, Surfaces[i].ToString(), Padded(theme.Resolve(ThemeStyle.Text)));
            grid.Set(1, row, "text in the surface's own style", Padded(theme.Resolve(Surfaces[i])),
                colSpan: grid.ColumnCount - 1);
            FillRow(grid, row + 1, surface,
                SurfaceInks.Select(ink => (ink.ToString(), (CliCellStyle?)Padded(theme.Resolve(ink)))).ToArray());
            if (i < Surfaces.Length - 1)
                FillRow(grid, row + 2, background);
        }

        return grid;
    }

    private static CliGrid BuildDialog(ITheme theme)
    {
        var dialog = theme.Resolve(ThemeStyle.DialogSurface);
        var frame = theme.Resolve(ThemeStyle.Frame);
        var grid = CreateGrid(contentColumns: 1, rows: 18);
        int r = 0;

        FillSpanRow(grid, r++, dialog, "DialogTitle — Connection settings", Padded(theme.Resolve(ThemeStyle.DialogTitle)));
        FillRow(grid, r++, dialog);
        FillRow(grid, r++, dialog, ("┌─ Frame ────────────────┐", Padded(theme.Resolve(ThemeStyle.Frame))));
        FillRow(grid, r++, dialog, ("│ frames and borders     │", Padded(theme.Resolve(ThemeStyle.Frame))));
        FillRow(grid, r++, dialog, ("└────────────────────────┘", Padded(theme.Resolve(ThemeStyle.Frame))));
        FillRow(grid, r++, dialog);
        FillRow(grid, r++, dialog, ("TextInput", Padded(theme.Resolve(ThemeStyle.TextInput))));
        FillRow(grid, r++, dialog, ("InactiveTextInput", Padded(theme.Resolve(ThemeStyle.InactiveTextInput))));
        FillRow(grid, r++, dialog);
        FillRow(grid, r++, dialog, ("Selected", Padded(theme.Resolve(ThemeStyle.Selected))));
        FillRow(grid, r++, dialog, ("SelectedListItem", Padded(theme.Resolve(ThemeStyle.SelectedListItem))));
        FillRow(grid, r++, dialog, ("InactiveSelectedListItem", Padded(theme.Resolve(ThemeStyle.InactiveSelectedListItem))));
        FillRow(grid, r++, dialog, ("SelectedMenuItem", Padded(theme.Resolve(ThemeStyle.SelectedMenuItem))));
        FillRow(grid, r++, dialog);
        SetSubgridRow(grid, r++, dialog, 0, BuildButtonsStrip(theme));
        FillRow(grid, r++, dialog);
        SetSubgridRow(grid, r++, dialog, 0, BuildScrollStrip(theme));
        FillSpanRow(grid, r++, dialog, "Status — Enter accepts · Esc cancels", Padded(theme.Resolve(ThemeStyle.Status)));
        return grid;
    }

    private static CliGrid BuildButtonsStrip(ITheme theme)
    {
        var cells = new (string Text, CliCellStyle? Style)[]
        {
            ("▸", Padded(theme.Resolve(ThemeStyle.ButtonMarker))),
            ("ButtonFocused", Padded(theme.Resolve(ThemeStyle.ButtonFocused))),
            ("◂", Padded(theme.Resolve(ThemeStyle.ButtonMarker))),
            ("Button", Padded(theme.Resolve(ThemeStyle.Button))),
            ("ButtonInactiveSelected", Padded(theme.Resolve(ThemeStyle.ButtonInactiveSelected))),
            ("ButtonDisabled", Padded(theme.Resolve(ThemeStyle.ButtonDisabled))),
        };

        var strip = new CliGrid(cells.Length, 1);
        strip.SetRow(0, new CliGridRowDefinition(theme.Resolve(ThemeStyle.DialogSurface)));
        for (int c = 0; c < cells.Length; c++)
            strip.Set(c, 0, cells[c].Text, cells[c].Style);
        return strip;
    }

    private static CliGrid BuildScrollStrip(ITheme theme)
    {
        var strip = new CliGrid(2, 1);
        strip.SetRow(0, new CliGridRowDefinition(theme.Resolve(ThemeStyle.DialogSurface)));
        strip.Set(0, 0, "ScrollBar ░░░░░░░░░░", Padded(theme.Resolve(ThemeStyle.ScrollBar)));
        strip.Set(1, 0, "ScrollIndicator ██", Padded(theme.Resolve(ThemeStyle.ScrollIndicator)));
        return strip;
    }

    private static CliGrid BuildStructured(ITheme theme)
    {
        var background = theme.Resolve(ThemeStyle.Background);
        var grid = CreateGrid(contentColumns: 2, rows: 12);
        int r = 0;

        void InkRow(ThemeStyle ink, string sample)
        {
            FillRow(grid, r++, background,
                (ink.ToString(), Padded(theme.Resolve(ink))),
                (sample, Padded(theme.Resolve(ink))));
        }

        InkRow(ThemeStyle.Heading, "Connection profiles");
        InkRow(ThemeStyle.Key, "cam-042");
        InkRow(ThemeStyle.Value, "Loading dock camera");
        InkRow(ThemeStyle.Path, "C:\\media\\incoming");
        InkRow(ThemeStyle.Link, "https://example.com/devices/cam-042");
        FillRow(grid, r++, background);
        InkRow(ThemeStyle.TableTitleForeground, "Connection profiles — 3 records");
        InkRow(ThemeStyle.TableHeaderForeground, "Name   Server   Environment");
        InkRow(ThemeStyle.TableBodyForeground, "local-dev   localhost,1433   Development");
        InkRow(ThemeStyle.TableFrameForeground, "┌─┬─┐  ├─┼─┤  └─┴─┘");
        FillRow(grid, r++, background);
        // Hosted in the sample column so the strip's width does not inflate the name column.
        SetSubgridRow(grid, r++, background, 1, BuildProgressStrip(theme));
        return grid;
    }

    private static CliGrid BuildProgressStrip(ITheme theme)
    {
        // The done/remaining bar segments must stay contiguous, so the bar cells carry no padding.
        var background = theme.Resolve(ThemeStyle.Background);
        var strip = new CliGrid(3, 2);
        strip.SetRow(0, new CliGridRowDefinition(background));
        strip.SetRow(1, new CliGridRowDefinition(background));
        strip.Set(0, 0, "██████████████", theme.Resolve(ThemeStyle.ProgressBarDone));
        strip.Set(1, 0, "░░░░░░░░░░", theme.Resolve(ThemeStyle.ProgressBarRemaining));
        strip.Set(2, 0, "ProgressBarDone + ProgressBarRemaining", Padded(theme.Resolve(ThemeStyle.Text)));
        strip.Set(0, 1, "████████████████████████", theme.Resolve(ThemeStyle.ProgressBarComplete), colSpan: 2);
        strip.Set(2, 1, "ProgressBarComplete", Padded(theme.Resolve(ThemeStyle.ProgressBarComplete)));
        return strip;
    }

    // ---- grid helpers --------------------------------------------------------------------------

    // Every section grid gets a trailing star column so surface/background rows paint the full
    // documentation-terminal width, making surface changes obvious.
    private static CliGrid CreateGrid(int contentColumns, int rows)
    {
        var grid = new CliGrid(contentColumns + 1, rows);
        grid.SetColumn(contentColumns, new CliGridColumnDefinition(null) { Sizing = CliColumnSizing.Star });
        return grid;
    }

    // Cell content is trimmed during formatting, so visual breathing room comes from real cell
    // padding. Resolve returns a per-call clone, so mutating it here is safe.
    private static CliCellStyle Padded(CliCellStyle style)
    {
        style.Padding = CliCellPadding.Both;
        return style;
    }

    // Sets the row style and the given cells, filling every remaining column (including the star
    // filler) with an empty cell so the row's surface paints edge to edge.
    private static void FillRow(CliGrid grid, int row, CliCellStyle? rowStyle,
        params (string Text, CliCellStyle? Style)[] cells)
    {
        if (rowStyle is not null)
            grid.SetRow(row, new CliGridRowDefinition(rowStyle));
        for (int c = 0; c < grid.ColumnCount; c++)
        {
            if (c < cells.Length)
                grid.Set(c, row, cells[c].Text, cells[c].Style);
            else
                grid.Set(c, row, "");
        }
    }

    private static void FillSpanRow(CliGrid grid, int row, CliCellStyle? rowStyle, string text, CliCellStyle? style)
    {
        if (rowStyle is not null)
            grid.SetRow(row, new CliGridRowDefinition(rowStyle));
        grid.Set(0, row, text, style, colSpan: grid.ColumnCount);
    }

    private static void SetSubgridRow(CliGrid grid, int row, CliCellStyle rowStyle, int column, CliGrid subgrid)
    {
        grid.SetRow(row, new CliGridRowDefinition(rowStyle));
        grid.SetSubgrid(column, row, subgrid);
        for (int c = 0; c < grid.ColumnCount; c++)
        {
            if (c != column)
                grid.Set(c, row, "");
        }
    }
}
