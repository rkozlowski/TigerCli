using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Selection;

namespace ItTiger.TigerCli.Tui.Widgets;

/// <summary>
/// A reusable single-selection list widget whose rows are structured multi-column data rather than one
/// preformatted string. Each <see cref="SelectRow"/> supplies one <see cref="SelectCell"/> per
/// <see cref="SelectColumn"/>; the widget maps them onto a real <c>CliGrid</c> so column widths, alignment,
/// truncation and the selected-row highlight are all owned by the shared rendering pipeline. The selected
/// row is styled consistently across every column; non-selected cells keep their own semantic styles.
/// </summary>
public sealed class InlineMultiColumnSelectWidget : InlineWidget
{
    private string EmptyStateText => EmptyStateTextOverride ?? TigerCliResources.Get("Tui_Select_EmptyState", Shell.Culture);

    private readonly IReadOnlyList<SelectColumn> _columns;
    private IReadOnlyList<SelectRow> _rows;
    private CliGrid? _cachedGrid;
    private int _cachedRows = -1;
    private int _selected;

    /// <summary>Creates a single-selection widget for structured, multi-column rows.</summary>
    /// <param name="shell">The shell that supplies the theme, culture, and viewport.</param>
    /// <param name="columns">The column definitions, from left to right.</param>
    /// <param name="rows">The rows available for selection.</param>
    /// <param name="preselectIndex">The initially selected row index, or <c>null</c> for the first enabled row.</param>
    public InlineMultiColumnSelectWidget(
        ICliAppShell shell,
        IReadOnlyList<SelectColumn> columns,
        IReadOnlyList<SelectRow> rows,
        int? preselectIndex = null)
        : base(shell)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
            throw new ArgumentException("A multi-column select needs at least one column.", nameof(columns));

        _columns = columns;
        _rows = rows ?? Array.Empty<SelectRow>();
        _selected = ResolveInitialSelection(preselectIndex);
    }

    /// <summary>The columns, left to right.</summary>
    public IReadOnlyList<SelectColumn> Columns => _columns;

    /// <summary>The rows, top to bottom.</summary>
    public IReadOnlyList<SelectRow> Rows => _rows;

    /// <summary>The selected row index, or <c>-1</c> when there is no enabled row.</summary>
    public int SelectedIndex => _selected;

    /// <summary>Overrides the empty-state text shown when there are no rows.</summary>
    public string? EmptyStateTextOverride { get; set; }

    private int ResolveInitialSelection(int? preselectIndex)
    {
        if (_rows.Count == 0)
            return -1;

        int start = preselectIndex is int p ? Math.Clamp(p, 0, _rows.Count - 1) : 0;
        // Prefer the requested row, then the nearest enabled row after it, then before it.
        if (!_rows[start].IsDisabled)
            return start;
        return NextEnabled(start, +1) ?? NextEnabled(start, -1) ?? -1;
    }

    // The nearest enabled row strictly in the given direction from <paramref name="from"/>, or null.
    private int? NextEnabled(int from, int direction)
    {
        for (int i = from + direction; i >= 0 && i < _rows.Count; i += direction)
        {
            if (!_rows[i].IsDisabled)
                return i;
        }

        return null;
    }

    private int FirstEnabled() => NextEnabled(-1, +1) ?? -1;
    private int LastEnabled() => NextEnabled(_rows.Count, -1) ?? -1;

    // Snaps to <paramref name="target"/> if enabled, else the nearest enabled row searching first in
    // <paramref name="preferredDirection"/> then the other way; keeps the current selection if none exists.
    private int SnapToEnabled(int target, int preferredDirection)
    {
        target = Math.Clamp(target, 0, _rows.Count - 1);
        if (!_rows[target].IsDisabled)
            return target;
        return NextEnabled(target, preferredDirection)
            ?? NextEnabled(target, -preferredDirection)
            ?? _selected;
    }

    /// <inheritdoc/>
    public override InlineKeyResult HandleKey(KeyEvent key)
    {
        if (_selected < 0)
            return InlineKeyResult.NotHandled;

        int page = Math.Max(1, Shell.Viewport.Height - 4); // rough page (frame chrome)
        int previousSelected = _selected;
        bool handled = true;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow: _selected = NextEnabled(_selected, -1) ?? _selected; break;
            case ConsoleKey.DownArrow: _selected = NextEnabled(_selected, +1) ?? _selected; break;
            case ConsoleKey.PageUp: _selected = SnapToEnabled(_selected - (page - 1), -1); break;
            case ConsoleKey.PageDown: _selected = SnapToEnabled(_selected + (page - 1), +1); break;
            case ConsoleKey.Home: _selected = FirstEnabled(); break;
            case ConsoleKey.End: _selected = LastEnabled(); break;
            default: handled = false; break;
        }

        if (handled && _selected != previousSelected)
            _cachedGrid?.InvalidateLayout();

        return handled ? InlineKeyResult.Handled : InlineKeyResult.NotHandled;
    }

    /// <inheritdoc/>
    public override CliGrid ToGrid()
    {
        int rows = Math.Max(1, _rows.Count);
        var g = _cachedGrid;
        if (g == null || _cachedRows != rows)
        {
            g = ToGrid(_columns.Count, rows);
            _cachedGrid = g;
            _cachedRows = rows;
        }

        var theme = Shell.Theme;
        var surface = theme.Resolve(ThemeStyle.DialogSurface);
        surface.Padding = CliCellPadding.Both;
        surface.Wrapping = CliWrapping.SingleLineTruncate;
        g.DefaultCellStyle = surface;
        // Row styles win at the intersection so a selected row highlights uniformly across the columns.
        g.StylePrecedence = CliStylePrecedence.RowOverColumn;

        for (int c = 0; c < _columns.Count; c++)
            g.SetColumn(c, BuildColumn(_columns[c]));

        if (_rows.Count == 0)
        {
            var empty = theme.Resolve(ThemeStyle.MutedText).MergeWith(new CliCellStyle
            {
                Padding = CliCellPadding.Both,
                Wrapping = CliWrapping.SingleLineTruncate,
                FormattingMode = CliFormattingMode.Raw,
            });
            g.Set(0, 0, EmptyStateText, style: empty, colSpan: _columns.Count);
            g.ActivePoint = null;
            g.InvalidateLayout();
            return g;
        }

        for (int r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            bool selected = r == _selected;
            for (int c = 0; c < _columns.Count; c++)
            {
                var cell = c < row.Cells.Count ? row.Cells[c] : SelectCell.Empty;
                g.Set(c, r, cell.Text, style: BuildCellStyle(_columns[c], cell, row, selected));
            }
        }

        if (_selected >= 0)
            g.ActivePoint = new ActivePoint(StarOrFirstColumn(), _selected, 0);

        g.InvalidateLayout();
        return g;
    }

    private CliGridColumnDefinition BuildColumn(SelectColumn col)
    {
        var style = new CliCellStyle
        {
            HorizontalAlignment = col.Alignment,
            Padding = CliCellPadding.Both,
            Wrapping = CliWrapping.SingleLineTruncate,
        };
        if (col.Width is int w)
            style.Width = w;
        if (col.MinWidth is int min)
            style.MinWidth = min;
        if (col.MaxWidth is int max)
            style.MaxWidth = max;

        return new CliGridColumnDefinition(style) { Sizing = col.Sizing };
    }

    private CliCellStyle BuildCellStyle(SelectColumn col, SelectCell cell, SelectRow row, bool selected)
    {
        // A selected row is styled consistently across every column (the list-selection ink, muted when the
        // list is unfocused); otherwise a disabled row is muted, and a normal cell keeps its own semantic
        // style (cell → column → Text).
        ThemeStyle token = selected
            ? (HasFocus ? ThemeStyle.SelectedListItem : ThemeStyle.InactiveSelectedListItem)
            : row.IsDisabled
                ? ThemeStyle.MutedText
                : cell.Style ?? col.Style ?? ThemeStyle.Text;

        var overrideStyle = new CliCellStyle
        {
            HorizontalAlignment = cell.Alignment ?? col.Alignment,
            Padding = CliCellPadding.Both,
            Wrapping = CliWrapping.SingleLineTruncate,
            FormattingMode = cell.FormattingMode ?? CliFormattingMode.Raw,
        };
        return Shell.Theme.Resolve(token).MergeWith(overrideStyle);
    }

    // The active point (scroll anchor) rides the star column when there is one so paging tracks the widest
    // column; otherwise it rides the first column.
    private int StarOrFirstColumn()
    {
        for (int c = 0; c < _columns.Count; c++)
        {
            if (_columns[c].Sizing == CliColumnSizing.Star)
                return c;
        }

        return 0;
    }
}
