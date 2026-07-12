using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Tui.Selection;

/// <summary>
/// Immutable column definition for a multi-column select (see
/// <see cref="ItTiger.TigerCli.Tui.Controls.InlineMultiColumnSelect"/> and
/// <c>TigerTui.MultiColumnSelectIndexAsync</c>). A column owns sizing and a default cell alignment/style;
/// the control maps each column onto a real <c>CliGrid</c> column, and <c>CliGrid</c> owns the actual
/// width resolution. Use <see cref="CliColumnSizing.Star"/> on the column that should absorb the
/// remaining width so a selected row's highlight spans the whole list.
/// </summary>
public sealed class SelectColumn
{
    /// <summary>
    /// Creates a column. A fixed <paramref name="width"/> is mutually exclusive with
    /// <see cref="CliColumnSizing.Star"/> sizing. Leave <paramref name="style"/> null to fall back to the
    /// per-cell style, then to <see cref="ThemeStyle.Text"/>.
    /// </summary>
    public SelectColumn(
        CliColumnSizing sizing = CliColumnSizing.Auto,
        CliTextAlignment alignment = CliTextAlignment.Left,
        int? width = null,
        int? minWidth = null,
        int? maxWidth = null,
        ThemeStyle? style = null)
    {
        if (width.HasValue && sizing == CliColumnSizing.Star)
            throw new ArgumentException("A column cannot be both a fixed width and star-sized.");
        if (width.HasValue && width.Value < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Column width must be at least 1.");
        if (minWidth is < 0)
            throw new ArgumentOutOfRangeException(nameof(minWidth), "Column min width cannot be negative.");
        if (maxWidth is < 1)
            throw new ArgumentOutOfRangeException(nameof(maxWidth), "Column max width must be at least 1.");

        Sizing = sizing;
        Alignment = alignment;
        Width = width;
        MinWidth = minWidth;
        MaxWidth = maxWidth;
        Style = style;
    }

    /// <summary>How the column is sized after content sizing (<see cref="CliColumnSizing.Star"/> absorbs the remainder).</summary>
    public CliColumnSizing Sizing { get; }

    /// <summary>Default horizontal alignment for cells in this column (a cell may override it).</summary>
    public CliTextAlignment Alignment { get; }

    /// <summary>Fixed column width in cells, or <c>null</c> for content/star sizing.</summary>
    public int? Width { get; }

    /// <summary>Optional minimum resolved width for the column.</summary>
    public int? MinWidth { get; }

    /// <summary>Optional maximum resolved width for the column (long content truncates to it).</summary>
    public int? MaxWidth { get; }

    /// <summary>
    /// Optional default theme style for cells in this column, applied to non-selected, non-disabled cells
    /// that do not declare their own <see cref="SelectCell.Style"/>. A selected row overrides this with the
    /// list-selection style so the whole row highlights consistently.
    /// </summary>
    public ThemeStyle? Style { get; }
}
