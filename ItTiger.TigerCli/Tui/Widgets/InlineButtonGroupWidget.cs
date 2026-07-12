using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Widgets;

/// <summary>
/// A reusable horizontal row of buttons — the main reusable unit for message-box style button rows
/// (<c>[OK]</c>, <c>[Yes] [No]</c>, <c>[OK] [Cancel]</c>, <c>[Abort] [Retry] [Ignore]</c>).
/// </summary>
/// <remarks>
/// Left/Right (and Home/End) move the active button across all buttons, including disabled ones, so a
/// disabled button can be the active one. Enter/Spacebar activate the active button: an enabled
/// button returns its <see cref="DialogResultKind"/>; a disabled button is consumed without a result
/// (so the dialog does not fall back to Enter-confirm). Escape is intentionally left unhandled so the
/// dialog's cancel fallback can fire.
/// </remarks>
public sealed class InlineButtonGroupWidget : InlineWidget
{
    private const int Gap = 4; // spaces between adjacent buttons

    private readonly IReadOnlyList<InlineButtonWidget> _buttons;
    private int _active;

    /// <summary>Creates a horizontal group of buttons with one active button.</summary>
    /// <param name="shell">The shell that supplies the theme and culture.</param>
    /// <param name="buttons">The buttons to display, from left to right.</param>
    /// <param name="activeIndex">The initially active button index, or <c>null</c> for the first button.</param>
    public InlineButtonGroupWidget(
        ICliAppShell shell,
        IReadOnlyList<InlineButtonWidget> buttons,
        int? activeIndex = null)
        : base(shell)
    {
        _buttons = buttons ?? Array.Empty<InlineButtonWidget>();
        _active = _buttons.Count == 0 ? -1 : Math.Clamp(activeIndex ?? 0, 0, _buttons.Count - 1);
    }

    /// <summary>The buttons displayed by the group, from left to right.</summary>
    public IReadOnlyList<InlineButtonWidget> Buttons => _buttons;

    /// <summary>The active button index, or <c>-1</c> when the group is empty.</summary>
    public int ActiveIndex => _active;

    /// <summary>The active button, or <c>null</c> when the group is empty.</summary>
    public InlineButtonWidget? ActiveButton =>
        (_active >= 0 && _active < _buttons.Count) ? _buttons[_active] : null;

    // The group is focusable when at least one button can take focus.
    /// <inheritdoc/>
    public override bool Focusable => _buttons.Any(b => b.Enabled);
    /// <inheritdoc/>
    public override string? Hint => TigerCliResources.Get("Tui_ButtonGroup_Hint", Shell.Culture);

    /// <inheritdoc/>
    public override InlineKeyResult HandleKey(KeyEvent key)
    {
        if (_buttons.Count == 0)
            return InlineKeyResult.NotHandled;

        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                return MoveActive(-1);
            case ConsoleKey.RightArrow:
                return MoveActive(1);
            case ConsoleKey.Home:
                return SetActive(0);
            case ConsoleKey.End:
                return SetActive(_buttons.Count - 1);
            case ConsoleKey.Enter:
            case ConsoleKey.Spacebar:
                // Always consumed (even for a disabled active button, which yields no result).
                return ActiveButton?.Activate() ?? InlineKeyResult.NotHandled;
            default:
                // Escape and everything else fall through to the dialog.
                return InlineKeyResult.NotHandled;
        }
    }

    private InlineKeyResult MoveActive(int delta) => SetActive(_active + delta);

    private InlineKeyResult SetActive(int index)
    {
        _active = Math.Clamp(index, 0, _buttons.Count - 1);
        return InlineKeyResult.Handled;
    }

    /// <inheritdoc/>
    public override CliGrid ToGrid()
    {
        if (_buttons.Count == 0)
        {
            var empty = ToGrid(1, 1);
            empty.Set(0, 0, string.Empty);
            return empty;
        }

        // One column per button plus a spacer column between adjacent buttons.
        int columns = _buttons.Count * 2 - 1;
        var g = ToGrid(columns, 1);
        g.DefaultCellStyle = Shell.Theme.Resolve(ThemeStyle.DialogSurface);

        var gapStyle = new CliCellStyle { Padding = CliCellPadding.None, FormattingMode = CliFormattingMode.Raw };

        for (int i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            // Selection and group focus are independent: the active button is always "selected" (it
            // keeps its markers even when the group is unfocused); group focus only changes the
            // selected button's background via GroupFocused.
            button.HasFocus = i == _active;
            button.GroupFocused = HasFocus;

            int col = i * 2;
            g.SetSubgrid(col, 0, button.ToGrid());

            if (i < _buttons.Count - 1)
                g.Set(col + 1, 0, new string(' ', Gap), style: gapStyle);
        }

        g.InvalidateLayout();
        return g;
    }
}
