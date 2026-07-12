using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Selection;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Dialog-hostable single-selection control over structured, multi-column rows. It wraps an
/// <see cref="InlineMultiColumnSelectWidget"/> and behaves like <see cref="InlineSelect"/> — keyboard
/// navigation, Enter confirms the highlighted row (payload = its index), Escape cancels — but each row is
/// laid out across several aligned columns instead of a single preformatted line.
/// </summary>
public sealed class InlineMultiColumnSelect : InlineControlBase
{
    private readonly InlineMultiColumnSelectWidget _widget;

    /// <summary>Creates a dialog-hostable single-selection control for structured rows.</summary>
    /// <param name="shell">The shell that supplies the theme, culture, and viewport.</param>
    /// <param name="columns">The column definitions, from left to right.</param>
    /// <param name="rows">The rows available for selection.</param>
    /// <param name="preselectIndex">The initially selected row index, or <c>null</c> for the first enabled row.</param>
    public InlineMultiColumnSelect(
        ICliAppShell shell,
        IReadOnlyList<SelectColumn> columns,
        IReadOnlyList<SelectRow> rows,
        int? preselectIndex = null)
        : base(shell)
    {
        _widget = new InlineMultiColumnSelectWidget(shell, columns, rows, preselectIndex)
        {
            HasFocus = true
        };
    }

    /// <inheritdoc/>
    public override object? Payload => _widget.SelectedIndex >= 0 ? _widget.SelectedIndex : null;
    /// <inheritdoc/>
    public override bool CanConfirm => _widget.SelectedIndex >= 0;
    /// <inheritdoc/>
    public override string? Hint => TigerCliResources.Get("Tui_Select_Hint", Shell.Culture);

    /// <inheritdoc/>
    public override CliScrollThumbMode ThumbMode => CliScrollThumbMode.ActivePoint;
    /// <inheritdoc/>
    public override CliControlDecoration ControlDecoration => CliControlDecoration.VerticalScrollBar;

    /// <inheritdoc/>
    public override CliScrollMode ScrollMode => CliScrollMode.Vertical;
    /// <inheritdoc/>
    public override InlineDialogArea DialogArea => InlineDialogArea.InFrameScrollable;

    /// <inheritdoc/>
    public override InlineKeyResult HandleKey(KeyEvent key) => _widget.HandleKey(key);

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
