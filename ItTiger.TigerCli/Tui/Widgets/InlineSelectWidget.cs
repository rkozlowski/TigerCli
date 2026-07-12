using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Widgets;

/// <summary>
/// A reusable single-selection list widget.
/// </summary>
public sealed class InlineSelectWidget : InlineWidget
{
    private string EmptyStateText => EmptyStateTextOverride ?? TigerCliResources.Get("Tui_Select_EmptyState", Shell.Culture);

    // Markup shown for a null label (the synthetic "no-selection" row), rendered through the
    // grid's null-display path so a real null sentinel - not the string "(None)" - drives display.
    private string NoSelectionText => TigerCliResources.Get("Tui_Select_NoSelection", Shell.Culture);

    private IReadOnlyList<string?> _labels;
    private readonly CliFormattingMode _itemsFormattingMode;
    private CliGrid? _cachedGrid;
    private int _cachedRows = -1;
    private int _selected;

    private int? _minWidth;
    private int? _maxWidth;

    /// <summary>Creates a single-selection list widget.</summary>
    /// <param name="shell">The shell that supplies the theme, culture, and viewport.</param>
    /// <param name="labels">The labels to display; a <c>null</c> label represents a no-selection row.</param>
    /// <param name="preselectIndex">The initially selected index, or <c>null</c> for the first label.</param>
    /// <param name="itemsFormattingMode">How labels are formatted.</param>
    /// <param name="minWidth">The optional minimum width in cells.</param>
    /// <param name="maxWidth">The optional maximum width in cells.</param>
    public InlineSelectWidget(
        ICliAppShell shell,
        IReadOnlyList<string?> labels,
        int? preselectIndex = null,
        CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw,
        int? minWidth = null,
        int? maxWidth = null)
        : base(shell)
    {
        _labels = labels ?? Array.Empty<string>();
        _itemsFormattingMode = itemsFormattingMode;
        _selected = (_labels.Count == 0) ? -1 : Math.Clamp(preselectIndex ?? 0, 0, _labels.Count - 1);
        _minWidth = minWidth;
        _maxWidth = maxWidth;
    }

    /// <summary>The labels currently displayed by the widget.</summary>
    public IReadOnlyList<string?> Labels => _labels;

    /// <summary>The selected index, or <c>-1</c> when the list is empty.</summary>
    public int SelectedIndex => _selected;

    /// <summary>The selected label, or <c>null</c> for an empty list or a selected no-selection row.</summary>
    public string? SelectedValue => (_selected >= 0) ? _labels[_selected] : null;

    /// <summary>Optional text displayed when the list contains no items.</summary>
    public string? EmptyStateTextOverride { get; set; }

    /// <summary>Replaces the displayed labels and selects an item in the new list.</summary>
    /// <param name="labels">The replacement labels.</param>
    /// <param name="selectedIndex">The selected index, or <c>null</c> for the first item.</param>
    public void SetItems(IReadOnlyList<string?> labels, int? selectedIndex = null)
    {
        _labels = labels ?? Array.Empty<string>();
        _selected = (_labels.Count == 0) ? -1 : Math.Clamp(selectedIndex ?? 0, 0, _labels.Count - 1);

        int rows = Math.Max(1, _labels.Count);
        if (_cachedRows != rows)
        {
            _cachedGrid = null;
            _cachedRows = -1;
        }
        else
        {
            _cachedGrid?.InvalidateLayout();
        }
    }

    /// <inheritdoc/>
    public override InlineKeyResult HandleKey(KeyEvent key)
    {
        if (_labels.Count == 0)
            return InlineKeyResult.NotHandled;

        int page = Math.Max(1, Shell.Viewport.Height - 4); // rough page (frame + maybe label later)
        int previousSelected = _selected;
        bool handled = true;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow: _selected = Math.Max(0, _selected - 1); break;
            case ConsoleKey.DownArrow: _selected = Math.Min(_labels.Count - 1, _selected + 1); break;
            case ConsoleKey.PageUp: _selected = Math.Max(0, _selected - (page - 1)); break;
            case ConsoleKey.PageDown: _selected = Math.Min(_labels.Count - 1, _selected + (page - 1)); break;
            case ConsoleKey.Home: _selected = (_labels.Count == 0) ? -1 : 0; break;
            case ConsoleKey.End: _selected = _labels.Count - 1; break;
            default: handled = false; break;
        }

        if (handled && _selected != previousSelected)
            _cachedGrid?.InvalidateLayout();

        return handled ? InlineKeyResult.Handled : InlineKeyResult.NotHandled;
    }

    /// <inheritdoc/>
    public override CliGrid ToGrid()
    {
        int rows = Math.Max(1, _labels.Count);
        var g = _cachedGrid;
        if (g == null || _cachedRows != rows)
        {
            g = ToGrid(1, rows);
            _cachedGrid = g;
            _cachedRows = rows;
        }

        var style = Shell.Theme.Resolve(ThemeStyle.DialogSurface);
        style.Padding = CliCellPadding.Both;
        style.Wrapping = CliWrapping.SingleLineTruncate;
        if (_minWidth != null)
            style.MinWidth = _minWidth;
        if (_maxWidth != null)
            style.MaxWidth = _maxWidth;
        g.DefaultCellStyle = style;

        // The list column fills the width the host gives it: the natural pass measures it
        // content-driven (Star never grows without a soft-max ceiling), and the span-aware
        // remeasure hands it the resolved in-frame width, which the Star column then fills.
        // This is what makes a selected row's highlight span the whole frame rather than only
        // the longest item. Min/Max travel on the column so they bound the fill.
        var columnStyle = new CliCellStyle();
        if (_minWidth != null)
            columnStyle.MinWidth = _minWidth;
        if (_maxWidth != null)
            columnStyle.MaxWidth = _maxWidth;
        g.SetColumn(0, new CliGridColumnDefinition(columnStyle) { Sizing = CliColumnSizing.Star });

        var fmOverride = new CliCellStyle { FormattingMode = _itemsFormattingMode };
        var norm = Shell.Theme.Resolve(ThemeStyle.Text).MergeWith(fmOverride);
        // Focus-aware selection ink: the active list-item highlight when the list is focused, the
        // explicit inactive selected-list style when it is not (the row stays selected, just muted).
        var selToken = HasFocus ? ThemeStyle.SelectedListItem : ThemeStyle.InactiveSelectedListItem;
        var sel = Shell.Theme.Resolve(selToken).MergeWith(fmOverride);

        if (_labels.Count == 0)
        {
            var empty = Shell.Theme.Resolve(ThemeStyle.MutedText).MergeWith(fmOverride);
            g.Set(0, 0, EmptyStateText, style: empty);
            g.ActivePoint = null;
            return g;
        }

        for (int r = 0; r < _labels.Count; r++)
        {
            var rowStyle = (r == _selected) ? sel : norm;
            var label = _labels[r];
            if (label == null)
            {
                var nullStyle = rowStyle.MergeWith(new CliCellStyle { NullDisplayValue = NoSelectionText });
                g.Set(0, r, null, style: nullStyle);
            }
            else
            {
                g.Set(0, r, label, style: rowStyle);
            }
        }

        if (_selected >= 0)
            g.ActivePoint = new ActivePoint(0, _selected, 0);

        g.InvalidateLayout();
        return g;
    }
}
