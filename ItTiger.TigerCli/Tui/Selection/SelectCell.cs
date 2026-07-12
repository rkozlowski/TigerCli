using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Tui.Selection;

/// <summary>
/// Immutable content of one cell in a <see cref="SelectRow"/>: its <see cref="Text"/> plus optional
/// per-cell overrides for semantic <see cref="Style"/>, <see cref="Alignment"/>, and
/// <see cref="FormattingMode"/>. A cell is structured data, not a preformatted line — the control renders
/// it through the normal <c>CliGrid</c> cell pipeline. On the selected row the cell's own style is
/// overridden by the list-selection style so the row highlights consistently.
/// </summary>
public sealed class SelectCell
{
    /// <summary>An empty cell (blank text), useful for a column that is only sometimes populated.</summary>
    public static SelectCell Empty { get; } = new(string.Empty);

    /// <summary>
    /// Creates a cell. <paramref name="formattingMode"/> defaults to <see cref="CliFormattingMode.Raw"/>
    /// (the text is literal); pass <see cref="CliFormattingMode.Preformatted"/> when <paramref name="text"/>
    /// carries trusted TigerCli markup that should be parsed.
    /// </summary>
    public SelectCell(
        string text,
        ThemeStyle? style = null,
        CliTextAlignment? alignment = null,
        CliFormattingMode? formattingMode = null)
    {
        Text = text ?? string.Empty;
        Style = style;
        Alignment = alignment;
        FormattingMode = formattingMode;
    }

    /// <summary>The cell text.</summary>
    public string Text { get; }

    /// <summary>
    /// Optional semantic style for this cell when its row is neither selected nor disabled. Falls back to
    /// the column's <see cref="SelectColumn.Style"/>, then to <see cref="ThemeStyle.Text"/>.
    /// </summary>
    public ThemeStyle? Style { get; }

    /// <summary>Optional alignment override for this cell (otherwise the column alignment applies).</summary>
    public CliTextAlignment? Alignment { get; }

    /// <summary>
    /// Optional formatting mode override for this cell (otherwise <see cref="CliFormattingMode.Raw"/>).
    /// </summary>
    public CliFormattingMode? FormattingMode { get; }
}
