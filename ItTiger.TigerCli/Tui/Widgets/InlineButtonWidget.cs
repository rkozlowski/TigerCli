using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Widgets;

/// <summary>
/// A reusable button widget. It carries a label, the <see cref="DialogResultKind"/> it produces when
/// activated, and an enabled state; it renders itself for the focused/unfocused/disabled states.
/// A single button is primarily a value/rendering widget — navigation and activation across a row of
/// buttons live in <see cref="InlineButtonGroupWidget"/>.
/// </summary>
/// <remarks>
/// The selected button renders textual markers (<c>[▸ Yes ◂]</c>) so the selection is visible without
/// relying on colour; an unselected button keeps the same width (<c>[  No  ]</c>). Selection and group
/// focus are independent: a selected button keeps its markers whether or not its group has focus —
/// group focus only changes the selected button's background (active vs muted).
/// </remarks>
public sealed class InlineButtonWidget : InlineWidget
{
    /// <summary>The text displayed on the button.</summary>
    public string Label { get; }

    /// <summary>The dialog result requested when the button is activated.</summary>
    public DialogResultKind Result { get; }

    /// <summary>Whether the button can be activated.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Whether the owning button group currently has focus. Set by <see cref="InlineButtonGroupWidget"/>.
    /// It only affects the selected button's background (active vs inactive); it never hides the
    /// selection markers. <see cref="InlineWidget.HasFocus"/> carries the independent "is the selected
    /// button" concept that drives the markers.
    /// </summary>
    public bool GroupFocused { get; set; }

    private CliGrid? _cachedGrid;

    /// <summary>Creates a button that requests a dialog result when activated.</summary>
    /// <param name="shell">The shell that supplies the theme.</param>
    /// <param name="label">The text displayed on the button.</param>
    /// <param name="result">The dialog result requested when the button is activated.</param>
    /// <param name="enabled">Whether the button can be activated.</param>
    public InlineButtonWidget(ICliAppShell shell, string label, DialogResultKind result, bool enabled = true)
        : base(shell)
    {
        Label = label ?? string.Empty;
        Result = result;
        Enabled = enabled;
    }

    // A disabled button cannot take focus.
    /// <inheritdoc/>
    public override bool Focusable => Enabled;

    /// <summary>
    /// Activates the button. An enabled button requests its <see cref="Result"/>; a disabled button
    /// is still considered handled but produces no result, so the host does not fall back to Enter.
    /// </summary>
    public InlineKeyResult Activate()
        => Enabled ? InlineKeyResult.WithResult(Result) : InlineKeyResult.Handled;

    // A standalone button does not own navigation/activation keys; the group drives activation via
    // Activate(). Keys are therefore left unhandled so a host can apply its own fallback.
    /// <inheritdoc/>
    public override InlineKeyResult HandleKey(KeyEvent key) => InlineKeyResult.NotHandled;

    /// <inheritdoc/>
    public override CliGrid ToGrid()
    {
        var g = _cachedGrid ??= ToGrid(5, 1);

        // "selected" is the active button in the group (drives markers); group focus only chooses the
        // selected button's background — the active highlight when focused, a muted one when not.
        bool selected = Enabled && HasFocus;
        var baseStyle = !Enabled
            ? Shell.Theme.Resolve(ThemeStyle.ButtonDisabled)
            : selected
                ? Shell.Theme.Resolve(GroupFocused ? ThemeStyle.ButtonFocused : ThemeStyle.ButtonInactiveSelected)
                : Shell.Theme.Resolve(ThemeStyle.Button);

        var noPad = new CliCellStyle
        {
            Padding = CliCellPadding.None,
            FormattingMode = CliFormattingMode.Raw,
            Wrapping = CliWrapping.SingleLine
        };
        var frame = baseStyle.MergeWith(noPad);
        // Markers keep the selected button background but take the marker ink for the glyph. They show
        // for the selected button regardless of group focus, so selection stays visible when unfocused.
        var marker = selected
            ? baseStyle.MergeWith(Shell.Theme.Resolve(ThemeStyle.ButtonMarker)).MergeWith(noPad)
            : frame;

        string left = selected ? ConsoleSymbol.MarkerRight.ToString() : " ";
        string right = selected ? ConsoleSymbol.MarkerLeft.ToString() : " ";

        g.DefaultCellStyle = baseStyle;
        g.Set(0, 0, "[", style: frame);
        g.Set(1, 0, left, style: marker);
        g.Set(2, 0, $" {Label} ", style: frame);
        g.Set(3, 0, right, style: marker);
        g.Set(4, 0, "]", style: frame);
        g.InvalidateLayout();

        return g;
    }
}
