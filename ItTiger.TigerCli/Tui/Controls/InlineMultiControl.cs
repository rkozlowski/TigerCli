using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Base class for dialog-hostable inline controls composed from several top-level widgets.
/// </summary>
/// <remarks>
/// Composite controls keep the dialog contract as one hosted control while arranging several
/// reusable <see cref="InlineWidget"/> instances across <see cref="InlineDialogArea"/> regions.
/// Tab moves focus forward, Shift+Tab moves focus backward, and other keys are routed to the
/// focused widget through <see cref="HandleFocusedWidgetKey(KeyEvent)"/>. Non-focusable widgets are
/// skipped by focus traversal.
/// </remarks>
public abstract class InlineMultiControl : InlineControlBase
{
    private readonly List<InlineMultiControlWidget> _widgets = new();
    private int _focusedWidgetIndex = -1;

    /// <summary>Creates a composite control hosted by the supplied shell.</summary>
    /// <param name="shell">The shell hosting the control.</param>
    protected InlineMultiControl(ICliAppShell shell)
        : base(shell)
    {
    }

    /// <summary>Metadata for one widget slot owned by an <see cref="InlineMultiControl"/>.</summary>
    protected sealed class InlineMultiControlWidget
    {
        /// <summary>The widget rendered and keyed by this slot.</summary>
        public required InlineWidget Widget { get; init; }

        /// <summary>The dialog area where the widget is placed.</summary>
        public InlineDialogArea Area { get; init; } = InlineDialogArea.InFrame;

        /// <summary>Decoration requested by this slot when it is focused.</summary>
        public CliControlDecoration Decoration { get; init; }

        /// <summary>Scroll mode applied to this slot's host cell.</summary>
        public CliScrollMode ScrollMode { get; init; }

        /// <summary>Scrollbar or indicator thumb source for this slot.</summary>
        public CliScrollThumbMode ThumbMode { get; init; } = CliScrollThumbMode.Offset;

        /// <summary>Optional style applied to the widget host cell.</summary>
        public CliCellStyle? ContentStyle { get; init; }

        /// <summary>Optional hint surfaced while this slot is focused.</summary>
        public string? Hint { get; init; }
    }

    /// <summary>Registered widget slots in focus/navigation order.</summary>
    protected IReadOnlyList<InlineMultiControlWidget> Widgets => _widgets;

    /// <summary>Index of the currently focused widget slot, or <c>-1</c> when none is focused.</summary>
    protected int FocusedWidgetIndex => _focusedWidgetIndex;

    /// <summary>The currently focused widget, or <c>null</c> when none is focused.</summary>
    protected InlineWidget? FocusedWidget =>
        _focusedWidgetIndex >= 0 && _focusedWidgetIndex < _widgets.Count
            ? _widgets[_focusedWidgetIndex].Widget
            : null;

    /// <summary>The currently focused widget slot metadata, or <c>null</c> when none is focused.</summary>
    protected InlineMultiControlWidget? FocusedWidgetSlot =>
        _focusedWidgetIndex >= 0 && _focusedWidgetIndex < _widgets.Count
            ? _widgets[_focusedWidgetIndex]
            : null;

    /// <summary>Hint for the focused slot or widget.</summary>
    public override string? Hint => FocusedWidgetSlot?.Hint ?? FocusedWidget?.Hint;

    /// <summary>
    /// Reserves width for the widest hint this composite can surface across all of its widget slots,
    /// so moving focus between widgets (and thus changing the exposed hint) does not resize the dialog.
    /// </summary>
    public override int HintReservedWidth
    {
        get
        {
            // Start from the current hint width so control-specific hints (e.g. a validation message)
            // are also covered.
            int max = base.HintReservedWidth;
            foreach (var slot in _widgets)
            {
                var hint = slot.Hint ?? slot.Widget.Hint;
                if (hint is not null)
                    max = Math.Max(max, hint.Length);
            }
            return max;
        }
    }

    /// <summary>
    /// Registers a widget slot and returns its index. The first focusable widget is focused
    /// automatically.
    /// </summary>
    protected int AddWidget(
        InlineWidget widget,
        InlineDialogArea area,
        CliControlDecoration decoration = CliControlDecoration.None,
        CliScrollMode scrollMode = CliScrollMode.None,
        CliScrollThumbMode thumbMode = CliScrollThumbMode.Offset,
        CliCellStyle? contentStyle = null,
        string? hint = null)
    {
        ArgumentNullException.ThrowIfNull(widget);

        _widgets.Add(new InlineMultiControlWidget
        {
            Widget = widget,
            Area = area,
            Decoration = decoration,
            ScrollMode = scrollMode,
            ThumbMode = thumbMode,
            ContentStyle = contentStyle,
            Hint = hint,
        });

        if (_focusedWidgetIndex < 0 && widget.Focusable)
            SetFocusedWidgetIndex(_widgets.Count - 1);
        else
            UpdateWidgetFocusFlags();

        return _widgets.Count - 1;
    }

    /// <summary>
    /// Moves focus to a focusable widget slot by index. Returns <c>false</c> when the index is invalid
    /// or the target widget is not focusable.
    /// </summary>
    protected bool SetFocusedWidgetIndex(int index)
    {
        if (index < 0 || index >= _widgets.Count || !_widgets[index].Widget.Focusable)
            return false;

        if (_focusedWidgetIndex == index)
        {
            UpdateWidgetFocusFlags();
            return true;
        }

        int previous = _focusedWidgetIndex;
        _focusedWidgetIndex = index;
        UpdateWidgetFocusFlags();
        OnFocusChanged(previous, _focusedWidgetIndex);
        return true;
    }

    /// <summary>Returns widget descriptors for the hosting dialog, with exactly the focused slot marked active.</summary>
    public override IReadOnlyList<InlineDialogWidget> GetWidgets()
    {
        UpdateWidgetFocusFlags();

        var descriptors = new InlineDialogWidget[_widgets.Count];
        for (int i = 0; i < _widgets.Count; i++)
        {
            var slot = _widgets[i];
            descriptors[i] = new InlineDialogWidget
            {
                Area = slot.Area,
                Grid = slot.Widget.ToGrid(),
                IsFocused = i == _focusedWidgetIndex,
                Decoration = slot.Decoration,
                ScrollMode = slot.ScrollMode,
                ThumbMode = slot.ThumbMode,
                ContentStyle = slot.ContentStyle,
            };
        }

        return descriptors;
    }

    /// <summary>Handles Tab/Shift+Tab focus traversal, otherwise routes to the focused widget.</summary>
    public override InlineKeyResult HandleKey(KeyEvent key)
    {
        if (IsForwardTab(key))
            return MoveFocus(1);

        if (IsBackwardTab(key))
            return MoveFocus(-1);

        return HandleFocusedWidgetKey(key);
    }

    /// <summary>Renders the focused widget grid, or an empty grid when no widget exists.</summary>
    public override CliGrid ToGrid()
    {
        return FocusedWidget?.ToGrid() ?? ToGrid(1, 1);
    }

    /// <summary>
    /// Handles a non-focus-traversal key for the focused widget. Override to coordinate domain state
    /// across widgets before or after forwarding the key.
    /// </summary>
    protected virtual InlineKeyResult HandleFocusedWidgetKey(KeyEvent key)
    {
        return FocusedWidget?.HandleKey(key) ?? InlineKeyResult.NotHandled;
    }

    /// <summary>Called after focus changes from one widget index to another.</summary>
    protected virtual void OnFocusChanged(int previousIndex, int currentIndex)
    {
    }

    private InlineKeyResult MoveFocus(int direction)
    {
        if (_widgets.Count == 0)
            return InlineKeyResult.NotHandled;

        int start = _focusedWidgetIndex >= 0 ? _focusedWidgetIndex : 0;
        for (int step = 1; step <= _widgets.Count; step++)
        {
            int candidate = (start + (direction * step)) % _widgets.Count;
            if (candidate < 0)
                candidate += _widgets.Count;

            if (_widgets[candidate].Widget.Focusable)
            {
                SetFocusedWidgetIndex(candidate);
                return InlineKeyResult.Handled;
            }
        }

        UpdateWidgetFocusFlags();
        return InlineKeyResult.Handled;
    }

    private void UpdateWidgetFocusFlags()
    {
        for (int i = 0; i < _widgets.Count; i++)
            _widgets[i].Widget.HasFocus = i == _focusedWidgetIndex;
    }

    private static bool IsForwardTab(KeyEvent key)
    {
        return key.Key == ConsoleKey.Tab && key.Mods == ConsoleModifiers.None;
    }

    private static bool IsBackwardTab(KeyEvent key)
    {
        return key.Key == ConsoleKey.Tab && key.Mods == ConsoleModifiers.Shift;
    }
}
