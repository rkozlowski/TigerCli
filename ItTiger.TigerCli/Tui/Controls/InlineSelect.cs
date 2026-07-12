using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Dialog-hostable single-selection list control backed by <see cref="InlineSelectWidget"/>.
/// </summary>
/// <remarks>
/// The widget owns navigation, selected row state, rendering, and active-point placement. The
/// control adds dialog-level payload, confirmability, hint text, and widget metadata. The payload is
/// the selected row index, or <c>null</c> when the list is empty.
/// </remarks>
public sealed class InlineSelect : InlineControlBase
{
    private readonly InlineSelectWidget _widget;

    /// <summary>Creates a single-selection control over the supplied labels.</summary>
    public InlineSelect(ICliAppShell shell, IReadOnlyList<string?> labels, int? preselectIndex = null,
        CliFormattingMode itemsFormattingMode = CliFormattingMode.Raw)
        : base(shell)
    {
        _widget = new InlineSelectWidget(shell, labels, preselectIndex, itemsFormattingMode)
        {
            HasFocus = true
        };
    }

    /// <summary>Selected row index, or <c>null</c> when no row can be selected.</summary>
    public override object? Payload => (_widget.SelectedIndex >= 0) ? _widget.SelectedIndex : null;

    /// <summary>False when there are no selectable items.</summary>
    public override bool CanConfirm => _widget.Labels.Count > 0;

    /// <summary>Localized select navigation hint.</summary>
    public override string? Hint => TigerCliResources.Get("Tui_Select_Hint", Shell.Culture);

    /// <summary>The scrollbar thumb follows the logical active point/selected row.</summary>
    public override CliScrollThumbMode ThumbMode => CliScrollThumbMode.ActivePoint;

    /// <summary>Select controls request a vertical scrollbar.</summary>
    public override CliControlDecoration ControlDecoration => CliControlDecoration.VerticalScrollBar;

    /// <summary>Select controls scroll vertically.</summary>
    public override CliScrollMode ScrollMode => CliScrollMode.Vertical;

    /// <summary>Select controls are placed in the scrollable in-frame dialog area.</summary>
    public override InlineDialogArea DialogArea => InlineDialogArea.InFrameScrollable;

    /// <summary>Delegates list navigation keys to the owned widget; Enter/Escape are left to the dialog.</summary>
    public override InlineKeyResult HandleKey(KeyEvent key) => _widget.HandleKey(key);

    /// <summary>Exposes one focused, vertically scrollable list widget to the hosting dialog.</summary>
    public override IReadOnlyList<InlineDialogWidget> GetWidgets()
    {
        return new[]
        {
            new InlineDialogWidget
            {
                Area = InlineDialogArea.InFrameScrollable,
                Grid = ToGrid(),
                IsFocused = true,
                Decoration = ControlDecoration,
                ScrollMode = ScrollMode,
                ThumbMode = ThumbMode,
                ContentStyle = ContentStyle,
            }
        };
    }

    /// <summary>Returns the owned widget grid after synchronizing inherited layout settings.</summary>
    public override CliGrid ToGrid()
    {
        SyncWidgetLayout();
        _widget.HasFocus = true;
        return _widget.ToGrid();
    }

    private void SyncWidgetLayout()
    {
        _widget.IsInteractive = IsInteractive;
        _widget.Width = Width;
        _widget.MinWidth = MinWidth;
        _widget.SoftMaxWidth = SoftMaxWidth;
        _widget.MaxWidth = MaxWidth;
        _widget.Height = Height;
        _widget.MinHeight = MinHeight;
        _widget.SoftMaxHeight = SoftMaxHeight;
        _widget.MaxHeight = MaxHeight;
        _widget.DefaultCellStyle = DefaultCellStyle;
    }
}
