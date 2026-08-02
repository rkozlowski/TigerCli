using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Structured help rendering: composes help output as a document of separate frameless
/// <see cref="CliGrid"/> blocks rendered through the normal measure/render pipeline. Each block
/// fills the destination width, so the layout engine owns wrapping, indentation, and the complete
/// themed row surface.
/// </summary>
/// <remarks>
/// <para>
/// The help shape is a key line followed by its description on the next lines, indented; keys
/// (option signatures, environment-variable names, command names) can be long, so nothing is laid
/// out as a same-row key/description table. Continuation lines produced by wrapping inherit the
/// description indent because the indent is a structural grid column, not leading whitespace in the
/// text (the measure pass trims leading/trailing whitespace from cell content).
/// </para>
/// <para>
/// Help is a themed document, so every block starts from the selected theme's Text style over its
/// Background style. Plain body text, structural indentation, trailing fill, and blank separator
/// rows therefore render on one coherent theme surface rather than inheriting the terminal's
/// background. This is what makes a light theme readable on a dark terminal.
/// </para>
/// <para>
/// All inputs are trusted, already-composed markup strings (escaped by the caller where needed),
/// resolved against the active theme exactly like <see cref="TigerConsole.MarkupLine(string)"/>
/// output. Blocks render through the ambient output sink, so color/no-color policy, the sink's
/// layout width, and test capture scopes behave the same as for line-based output.
/// </para>
/// </remarks>
internal static class TigerCliHelpRenderer
{
    // Two-space indent under a heading, matching the legacy help text layout.
    private const int IndentWidth = 2;

    // Description lines sit four columns further in than the key line they belong to.
    private const int DetailIndentWidth = 4;

    /// <summary>
    /// Renders the title block: the app/command title line plus an optional indented description.
    /// </summary>
    public static void RenderTitleBlock(string titleMarkup, string? descriptionMarkup)
        => RenderBlock(titleMarkup, descriptionMarkup is null ? [] : [descriptionMarkup]);

    /// <summary>
    /// Renders a section: an accent-styled heading followed by indented body lines.
    /// </summary>
    public static void RenderSection(string headingMarkup, IReadOnlyList<string> lineMarkups)
        => RenderBlock(headingMarkup, lineMarkups);

    /// <summary>
    /// Renders an exit-code section with a full-width heading and enum title followed by compact
    /// code, name, and description columns. Exit codes are a genuine table — a numeric code, a
    /// short name, and a description — so this section keeps its aligned columns.
    /// </summary>
    public static void RenderExitCodeSection(
        string sectionHeadingMarkup,
        string titleMarkup,
        IReadOnlyList<(int Value, string NameMarkup, string? DescriptionMarkup)> exitCodes)
    {
        var grid = NewBlock(3, exitCodes.Count + 2);

        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle
        {
            HorizontalAlignment = CliTextAlignment.Right,
            Padding = CliCellPadding.Right
        }));
        grid.SetColumn(1, new CliGridColumnDefinition(new CliCellStyle
        {
            Padding = CliCellPadding.Both,
            MinWidth = exitCodes.Count == 0 ? 0 : exitCodes.Max(exitCode => exitCode.NameMarkup.Length + 2)
        }));
        grid.SetColumn(2, new CliGridColumnDefinition(new CliCellStyle())
        {
            Sizing = CliColumnSizing.Star
        });

        var fullWidthLeftAligned = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Left };
        grid.Set(0, 0, sectionHeadingMarkup, fullWidthLeftAligned, colSpan: 3);
        grid.Set(0, 1, titleMarkup, fullWidthLeftAligned, colSpan: 3);

        for (int i = 0; i < exitCodes.Count; i++)
        {
            var exitCode = exitCodes[i];
            var row = i + 2;
            grid.Set(0, row, $"[Key]{exitCode.Value}[/]");
            grid.Set(1, row, $"[Accent]{exitCode.NameMarkup}[/]");
            grid.Set(2, row, exitCode.DescriptionMarkup ?? "");
        }

        Render(grid);
    }

    /// <summary>
    /// Renders an argument, option, prompted-value, command, or environment-variable section: each
    /// item is a key line at a two-space indent whose description lines continue at six spaces.
    /// </summary>
    public static void RenderDetailSection(
        string sectionHeadingMarkup,
        IReadOnlyList<(string SignatureMarkup, IReadOnlyList<string> DetailMarkups)> items)
    {
        var rowCount = 1 + items.Sum(item => 1 + item.DetailMarkups.Count);
        var grid = NewBlock(3, rowCount);

        // Indentation is structural: two fixed-width columns hold it so wrapped continuation lines
        // start under the same column as the first description line.
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = IndentWidth, MinWidth = IndentWidth }));
        grid.SetColumn(1, new CliGridColumnDefinition(new CliCellStyle { Width = DetailIndentWidth, MinWidth = DetailIndentWidth }));
        grid.SetColumn(2, new CliGridColumnDefinition(new CliCellStyle()) { Sizing = CliColumnSizing.Star });

        grid.Set(0, 0, sectionHeadingMarkup, new CliCellStyle { HorizontalAlignment = CliTextAlignment.Left }, colSpan: 3);

        var row = 1;
        foreach (var item in items)
        {
            grid.Set(1, row, item.SignatureMarkup, colSpan: 2);
            row++;

            foreach (var detail in item.DetailMarkups)
            {
                grid.Set(2, row, detail);
                row++;
            }
        }

        Render(grid);
    }

    /// <summary>
    /// Renders a command or alias list. Names are keys like option signatures and can be long, so
    /// the list uses the same key-line-plus-indented-description shape as
    /// <see cref="RenderDetailSection"/> rather than an aligned two-column table.
    /// </summary>
    public static void RenderNameDescriptionSection(
        string sectionHeadingMarkup,
        IReadOnlyList<(string NameMarkup, string? DescriptionMarkup)> items)
    {
        var detailItems = items
            .Select(item => (
                item.NameMarkup,
                (IReadOnlyList<string>)(string.IsNullOrEmpty(item.DescriptionMarkup)
                    ? []
                    : [item.DescriptionMarkup])))
            .ToArray();

        RenderDetailSection(sectionHeadingMarkup, detailItems);
    }

    /// <summary>Renders a concise indented note section.</summary>
    public static void RenderNoteSection(string headingMarkup, IReadOnlyList<string> noteMarkups)
        => RenderBlock(headingMarkup, noteMarkups);

    /// <summary>Renders a single muted help hint without structural indentation.</summary>
    public static void RenderHint(string hintMarkup)
    {
        var grid = NewBlock(1, 1);
        grid.Set(0, 0, hintMarkup, new CliCellStyle { HorizontalAlignment = CliTextAlignment.Left });
        Render(grid);
    }

    /// <summary>Renders an optional copyright line and compact metadata link rows.</summary>
    public static void RenderMetadataFooter(
        string? copyrightMarkup,
        IReadOnlyList<(string LabelMarkup, string ValueMarkup)> links)
    {
        var rowCount = links.Count + (copyrightMarkup is null ? 0 : 1);
        var grid = NewBlock(2, rowCount);
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Padding = CliCellPadding.Right }));
        grid.SetColumn(1, new CliGridColumnDefinition(new CliCellStyle { Padding = CliCellPadding.Left }) { Sizing = CliColumnSizing.Star });

        var row = 0;
        if (copyrightMarkup is not null)
        {
            grid.Set(0, row, copyrightMarkup, new CliCellStyle { HorizontalAlignment = CliTextAlignment.Left }, colSpan: 2);
            row++;
        }

        foreach (var link in links)
        {
            grid.Set(0, row, link.LabelMarkup);
            grid.Set(1, row, link.ValueMarkup);
            row++;
        }

        Render(grid);
    }

    private static void RenderBlock(string headMarkup, IReadOnlyList<string> indentedLineMarkups)
    {
        // The heading/title is its own single-cell grid so its width never couples to the body
        // lines (no colSpan wrapping surprises), and the body is a two-column grid whose first
        // column is a fixed structural indent — indentation must be structural because the measure
        // pass trims leading/trailing whitespace from cell content lines.
        var head = NewBlock(1, 1);
        head.Set(0, 0, headMarkup);
        Render(head);

        if (indentedLineMarkups.Count == 0)
            return;

        var body = NewBlock(2, indentedLineMarkups.Count);
        body.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle
        {
            Width = IndentWidth,
            MinWidth = IndentWidth
        }));
        body.SetColumn(1, new CliGridColumnDefinition(new CliCellStyle()) { Sizing = CliColumnSizing.Star });
        for (int i = 0; i < indentedLineMarkups.Count; i++)
            body.Set(1, i, indentedLineMarkups[i]);
        Render(body);
    }

    /// <summary>
    /// Renders a full-width themed separator row between help blocks.
    /// </summary>
    public static void RenderBlankLine()
    {
        var grid = NewBlock(1, 1);
        grid.Set(0, 0, "");
        Render(grid);
    }

    // Every help block is an ordinary full-width grid. Background supplies the surface and Text
    // supplies its default ink; semantic roles then override that base through the normal cascade.
    // The final Star column consumes the sink's reported width, making wrapping and row fill one
    // layout decision. With an unbounded buffer sink, the grid remains content-driven.
    private static CliGrid NewBlock(int columnCount, int rowCount)
    {
        var theme = TigerConsole.CurrentTheme;
        var text = theme.Resolve(ThemeStyle.Text).CharStyle;
        var background = theme.Resolve(ThemeStyle.Background).CharStyle;
        var baseStyle = new CliCellStyle(new CliCharStyle(
            text?.Foreground,
            background?.Background))
        {
            FormattingMode = CliFormattingMode.Preformatted
        };

        var grid = new CliGrid(columnCount, rowCount) { DefaultCellStyle = baseStyle };
        grid.SetColumn(
            columnCount - 1,
            new CliGridColumnDefinition(new CliCellStyle()) { Sizing = CliColumnSizing.Star });
        return grid;
    }

    private static void Render(CliGrid grid) => TigerConsole.RenderGrid(grid, TigerConsole.GetOutputSink());
}
