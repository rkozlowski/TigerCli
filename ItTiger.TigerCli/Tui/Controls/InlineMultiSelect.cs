using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Dialog-hostable multi-selection checklist control.
/// </summary>
/// <remarks>
/// The payload is an <c>int[]</c> of selected indexes in original item order, or <c>null</c> when the
/// list has no items. Empty selection is valid when items exist. Keyboard behavior follows the
/// standard checklist model: Up/Down/PageUp/PageDown/Home/End move the active row, Space toggles
/// the active row, and <c>+</c>, <c>-</c>, and <c>*</c> select all, clear all, or invert all.
/// Enter/Escape are left to the hosting dialog.
/// </remarks>
public sealed class InlineMultiSelect : InlineControlBase
{
    private string EmptyStateText => TigerCliResources.Get("Tui_Select_EmptyState", Shell.Culture);
    private const string UncheckedText = " [ ]";
    private static readonly string CheckedText = $" [{ConsoleSymbol.Square}]";

    private readonly IReadOnlyList<string> _labels;
    private readonly bool[] _checked;
    private readonly CliFormattingMode _itemsFormattingMode;
    private int _active;
    private CliGrid? _cachedGrid;
    private int _cachedRows = -1;

    /// <summary>Creates a multi-selection checklist.</summary>
    public InlineMultiSelect(
        ICliAppShell shell,
        IReadOnlyList<string> labels,
        IReadOnlyCollection<int>? preselectedIndexes = null,
        CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw)
        : base(shell)
    {
        _labels = labels ?? Array.Empty<string>();
        _itemsFormattingMode = itemsFormattingMode;
        _checked = new bool[_labels.Count];
        _active = _labels.Count == 0 ? -1 : 0;

        if (preselectedIndexes is not null)
        {
            foreach (int index in preselectedIndexes)
            {
                if (index < 0 || index >= _labels.Count)
                    throw new ArgumentOutOfRangeException(nameof(preselectedIndexes), $"Preselected index {index} is outside the item range.");

                _checked[index] = true;
            }
        }
    }

    /// <summary>Selected indexes in original item order, or <c>null</c> when there are no items.</summary>
    public override object? Payload
    {
        get
        {
            if (_labels.Count == 0)
                return null;

            var selected = new List<int>();
            for (int i = 0; i < _checked.Length; i++)
            {
                if (_checked[i])
                    selected.Add(i);
            }

            return selected.ToArray();
        }
    }

    /// <summary>False when there are no items; empty selection is valid when items exist.</summary>
    public override bool CanConfirm => _labels.Count > 0;

    /// <summary>Localized checklist navigation hint.</summary>
    public override string? Hint => TigerCliResources.Get("Tui_MultiSelect_Hint", Shell.Culture);

    /// <summary>Checklist hints are raw localized text.</summary>
    public override CliFormattingMode HintMode => CliFormattingMode.Raw;

    /// <summary>The scrollbar thumb follows the logical active row.</summary>
    public override CliScrollThumbMode ThumbMode => CliScrollThumbMode.ActivePoint;

    /// <summary>Multi-select controls request a vertical scrollbar.</summary>
    public override CliControlDecoration ControlDecoration => CliControlDecoration.VerticalScrollBar;

    /// <summary>Multi-select controls scroll vertically.</summary>
    public override CliScrollMode ScrollMode => CliScrollMode.Vertical;

    /// <summary>Multi-select controls are placed in the scrollable in-frame dialog area.</summary>
    public override InlineDialogArea DialogArea => InlineDialogArea.InFrameScrollable;

    /// <summary>Handles checklist navigation and toggle keys; Enter/Escape are not consumed.</summary>
    public override InlineKeyResult HandleKey(KeyEvent key)
    {
        if (_labels.Count == 0)
            return InlineKeyResult.NotHandled;

        int page = Math.Max(1, Shell.Viewport.Height - 4);
        int previousActive = _active;
        bool stateChanged = false;
        bool handled = true;

        switch (key.KeyChar)
        {
            case '+':
                stateChanged = SetAll(true);
                break;
            case '-':
                stateChanged = SetAll(false);
                break;
            case '*':
                InvertAll();
                stateChanged = true;
                break;
            default:
                handled = false;
                break;
        }

        if (!handled)
        {
            handled = true;
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    _active = Math.Max(0, _active - 1);
                    break;
                case ConsoleKey.DownArrow:
                    _active = Math.Min(_labels.Count - 1, _active + 1);
                    break;
                case ConsoleKey.PageUp:
                    _active = Math.Max(0, _active - (page - 1));
                    break;
                case ConsoleKey.PageDown:
                    _active = Math.Min(_labels.Count - 1, _active + (page - 1));
                    break;
                case ConsoleKey.Home:
                    _active = 0;
                    break;
                case ConsoleKey.End:
                    _active = _labels.Count - 1;
                    break;
                case ConsoleKey.Spacebar:
                    _checked[_active] = !_checked[_active];
                    stateChanged = true;
                    break;
                default:
                    handled = false;
                    break;
            }
        }

        if (handled && (_active != previousActive || stateChanged))
            _cachedGrid?.InvalidateLayout();

        return handled ? InlineKeyResult.Handled : InlineKeyResult.NotHandled;
    }

    private bool SetAll(bool value)
    {
        var changed = false;
        for (int i = 0; i < _checked.Length; i++)
        {
            if (_checked[i] == value)
                continue;

            _checked[i] = value;
            changed = true;
        }

        return changed;
    }

    private void InvertAll()
    {
        for (int i = 0; i < _checked.Length; i++)
            _checked[i] = !_checked[i];
    }

    /// <summary>
    /// Renders a two-column checklist grid: marker column plus item label column. The active row is
    /// exposed through <c>ActivePoint</c> so the hosting dialog can scroll to it.
    /// </summary>
    public override CliGrid ToGrid()
    {
        int rows = Math.Max(1, _labels.Count);
        var g = _cachedGrid;
        if (g == null || _cachedRows != rows)
        {
            g = ToGrid(2, rows);
            _cachedGrid = g;
            _cachedRows = rows;
        }

        g.DefaultCellStyle = Shell.Theme.Resolve(ThemeStyle.DialogSurface);
        g.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle
        {
            Width = 4,
            MinWidth = 4,
            MaxWidth = 4,
            Padding = CliCellPadding.None,
            FormattingMode = CliFormattingMode.Raw
        }));

        var markerOverride = new CliCellStyle
        {
            Padding = CliCellPadding.None,
            FormattingMode = CliFormattingMode.Raw
        };
        var labelOverride = new CliCellStyle
        {
            Padding = CliCellPadding.Both,
            FormattingMode = _itemsFormattingMode
        };
        var emptyOverride = new CliCellStyle
        {
            Padding = CliCellPadding.Both,
            FormattingMode = _itemsFormattingMode
        };

        var normMarker = Shell.Theme.Resolve(ThemeStyle.Text).MergeWith(markerOverride);
        var normLabel = Shell.Theme.Resolve(ThemeStyle.Text).MergeWith(labelOverride);
        var selectedMarker = Shell.Theme.Resolve(ThemeStyle.SelectedListItem).MergeWith(markerOverride);
        var selectedLabel = Shell.Theme.Resolve(ThemeStyle.SelectedListItem).MergeWith(labelOverride);

        if (_labels.Count == 0)
        {
            var empty = Shell.Theme.Resolve(ThemeStyle.MutedText).MergeWith(emptyOverride);
            g.Set(0, 0, "    ", style: normMarker);
            g.Set(1, 0, EmptyStateText, style: empty);
            g.ActivePoint = null;
            g.InvalidateLayout();
            return g;
        }

        for (int r = 0; r < _labels.Count; r++)
        {
            bool active = r == _active;
            g.Set(0, r, _checked[r] ? CheckedText : UncheckedText, style: active ? selectedMarker : normMarker);
            g.Set(1, r, _labels[r], style: active ? selectedLabel : normLabel);
        }

        if (_active >= 0)
            g.ActivePoint = new ActivePoint(1, _active, 0);

        g.InvalidateLayout();
        return g;
    }
}
