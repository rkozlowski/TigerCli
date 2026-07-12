using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Themes;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// First runtime-restoration slice for the rewritten <see cref="InlineDialog"/>: focus is dynamic
/// state that drives the parent grid's <see cref="CliGrid.ActivePoint"/> from the focused widget's
/// host cell, without rebuilding the structural dialog grid; and the hosted widgets render using
/// their focused/unfocused state. Cursor mode and scroll overlays are deliberately out of scope here.
/// </summary>
public sealed class InlineDialogFocusTests : TestBase
{
    // A composite control with three widgets across distinct dialog areas, so focus can move between
    // them and the focused host cell can be read back from the dialog grid's active point.
    private sealed class FocusProbeControl : InlineMultiControl
    {
        public readonly InlineTextInputWidget Path;
        public readonly InlineSelectWidget List;
        public readonly InlineButtonGroupWidget Buttons;
        public readonly int PathIndex;
        public readonly int ListIndex;
        public readonly int ButtonsIndex;

        public FocusProbeControl(ICliAppShell shell)
            : base(shell)
        {
            Path = new InlineTextInputWidget(shell, "C:\\Temp");
            List = new InlineSelectWidget(shell, ["Red", "Green", "Blue"], preselectIndex: 1);
            Buttons = new InlineButtonGroupWidget(shell,
            [
                new InlineButtonWidget(shell, "OK", DialogResultKind.Ok),
                new InlineButtonWidget(shell, "Cancel", DialogResultKind.Cancel),
            ]);

            PathIndex = AddWidget(Path, InlineDialogArea.AboveFrameWithIndicators,
                CliControlDecoration.HorizontalIndicators, CliScrollMode.Horizontal, CliScrollThumbMode.ActivePoint);
            ListIndex = AddWidget(List, InlineDialogArea.InFrameScrollable,
                CliControlDecoration.VerticalScrollBar, CliScrollMode.Vertical, CliScrollThumbMode.ActivePoint);
            ButtonsIndex = AddWidget(Buttons, InlineDialogArea.BelowFrame);

            SetFocusedWidgetIndex(ListIndex);
        }

        public override object? Payload => List.SelectedValue;

        public void Focus(int index) => SetFocusedWidgetIndex(index);
    }

    // Host-cell coordinates for FocusProbeControl with no title/label (hint row is always present):
    //   AboveFrameWithIndicators (Path)   -> (column 1, row 0)
    //   TopFrame                          -> row 1
    //   InFrameScrollable (List)          -> (column 1, row 2)
    //   BottomFrame                       -> row 3
    //   BelowFrame (Buttons)              -> (column 0, row 4)
    //   Status (hint)                     -> row 5
    private static readonly (int col, int row) PathCell = (1, 0);
    private static readonly (int col, int row) ListCell = (1, 2);
    private static readonly (int col, int row) ButtonsCell = (0, 4);

    // ------------------------------------------------------------------
    // 1. Focused widget drives parent grid ActivePoint
    // ------------------------------------------------------------------

    [Fact]
    public void FocusedWidget_DrivesParentActivePoint()
    {
        var shell = new TestShell();
        var control = new FocusProbeControl(shell);
        control.Focus(control.ListIndex);
        var dialog = new InlineDialog(shell, title: null, control);

        var grid = dialog.ToGrid();

        Assert.NotNull(grid.ActivePoint);
        Assert.Equal(ListCell.col, grid.ActivePoint!.Column);
        Assert.Equal(ListCell.row, grid.ActivePoint.Row);
    }

    // ------------------------------------------------------------------
    // 2. Focus change updates ActivePoint
    // ------------------------------------------------------------------

    [Fact]
    public void FocusChange_UpdatesActivePoint()
    {
        var shell = new TestShell();
        var control = new FocusProbeControl(shell);
        var dialog = new InlineDialog(shell, title: null, control);

        control.Focus(control.ListIndex);
        var listPoint = dialog.ToGrid().ActivePoint;
        Assert.Equal((ListCell.col, ListCell.row), (listPoint!.Column, listPoint.Row));

        control.Focus(control.ButtonsIndex);
        var buttonsPoint = dialog.ToGrid().ActivePoint;
        Assert.Equal((ButtonsCell.col, ButtonsCell.row), (buttonsPoint!.Column, buttonsPoint.Row));

        control.Focus(control.PathIndex);
        var pathPoint = dialog.ToGrid().ActivePoint;
        Assert.Equal((PathCell.col, PathCell.row), (pathPoint!.Column, pathPoint.Row));
    }

    // ------------------------------------------------------------------
    // 3. Focus change reuses the cached dialog grid (no structural rebuild)
    // ------------------------------------------------------------------

    [Fact]
    public void FocusChange_ReusesCachedGrid_WhenSignatureUnchanged()
    {
        var shell = new TestShell();
        var control = new FocusProbeControl(shell);
        var dialog = new InlineDialog(shell, title: null, control);

        control.Focus(control.ListIndex);
        var first = dialog.ToGrid();

        control.Focus(control.ButtonsIndex);
        var second = dialog.ToGrid();

        // Focus is dynamic: the structural signature is unchanged, so the same grid instance is reused
        // rather than rebuilt — only the active point moved.
        Assert.Same(first, second);
        Assert.Equal((ButtonsCell.col, ButtonsCell.row), (second.ActivePoint!.Column, second.ActivePoint.Row));
    }

    // A single-widget dialog leaves ActivePoint null so CliGrid auto-propagates the sole widget's
    // active point (the existing behavior this slice must preserve).
    [Fact]
    public void SingleWidgetDialog_LeavesActivePointNull_ForAutoPropagation()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"], preselectIndex: 0);
        var dialog = new InlineDialog(shell, title: null, select);

        Assert.Null(dialog.ToGrid().ActivePoint);
    }

    // ------------------------------------------------------------------
    // 4. Widget rendering receives focused/unfocused state
    // ------------------------------------------------------------------

    // The selected button keeps its markers whether or not the group has focus — selection is
    // independent of group focus. (Group focus only changes the selected button's background.)
    [Fact]
    public void ButtonGroup_SelectedButton_KeepsMarkers_WhenGroupUnfocused()
    {
        var shell = new TestShell();
        var group = new InlineButtonGroupWidget(shell,
        [
            new InlineButtonWidget(shell, "OK", DialogResultKind.Ok),
            new InlineButtonWidget(shell, "Cancel", DialogResultKind.Cancel),
        ]);

        group.HasFocus = true;
        var focused = string.Join("\n", TigerConsole.RenderGridToLines(group.ToGrid()));

        group.HasFocus = false;
        var unfocused = string.Join("\n", TigerConsole.RenderGridToLines(group.ToGrid()));

        Assert.Contains(ConsoleSymbol.MarkerRight, focused);
        Assert.Contains(ConsoleSymbol.MarkerLeft, focused);
        // Markers persist when the group is unfocused: the button is still selected.
        Assert.Contains(ConsoleSymbol.MarkerRight, unfocused);
        Assert.Contains(ConsoleSymbol.MarkerLeft, unfocused);
    }

    // Style ownership (HtmlSink): the focused and unfocused selected-button rows differ only in the
    // selected button's background, so the styled HTML differs.
    [Fact]
    public void ButtonGroup_FocusedVsUnfocused_ProducesDifferentStyledHtml()
    {
        var shell = new TestShell();
        var group = new InlineButtonGroupWidget(shell,
        [
            new InlineButtonWidget(shell, "OK", DialogResultKind.Ok),
            new InlineButtonWidget(shell, "Cancel", DialogResultKind.Cancel),
        ]);

        group.HasFocus = true;
        var focusedHtml = TigerConsole.RenderGridToHtml(group.ToGrid());

        group.HasFocus = false;
        var unfocusedHtml = TigerConsole.RenderGridToHtml(group.ToGrid());

        Assert.DoesNotContain((char)0x1B, focusedHtml);
        Assert.NotEqual(focusedHtml, unfocusedHtml);
    }

    // The selected button uses the active selected background when its group is focused and the
    // explicit muted ButtonInactiveSelected background when not — selection (markers) is unchanged.
    [Fact]
    public void Button_SelectedButton_UsesActiveBackgroundWhenGroupFocused_MutedWhenNot()
    {
        var shell = new TestShell();
        var theme = shell.Theme;

        var focused = new InlineButtonWidget(shell, "OK", DialogResultKind.Ok) { HasFocus = true, GroupFocused = true };
        var focusedBg = focused.ToGrid().GetCellStyle(2, 0).CharStyle?.Background;

        var muted = new InlineButtonWidget(shell, "OK", DialogResultKind.Ok) { HasFocus = true, GroupFocused = false };
        var mutedBg = muted.ToGrid().GetCellStyle(2, 0).CharStyle?.Background;

        Assert.Equal(theme.Resolve(ThemeStyle.ButtonFocused).CharStyle?.Background, focusedBg);
        Assert.Equal(theme.Resolve(ThemeStyle.ButtonInactiveSelected).CharStyle?.Background, mutedBg);
        Assert.NotEqual(focusedBg, mutedBg);

        // The selected button still shows its markers even when its group is unfocused.
        var mutedText = string.Join("\n", TigerConsole.RenderGridToLines(muted.ToGrid()));
        Assert.Contains(ConsoleSymbol.MarkerRight, mutedText);
    }

    // ------------------------------------------------------------------
    // 5. Explicit inactive semantic styles (style-preserving assertions)
    // ------------------------------------------------------------------

    // The select widget uses the active SelectedListItem highlight when focused and the explicit
    // InactiveSelectedListItem style when not — distinct backgrounds in the bundled (default) theme.
    [Fact]
    public void Select_FocusedUsesActiveSelection_UnfocusedUsesInactiveSelection()
    {
        var shell = new TestShell();
        var theme = shell.Theme;
        var widget = new InlineSelectWidget(shell, ["Red", "Green", "Blue"], preselectIndex: 1);

        widget.HasFocus = true;
        var focusedBg = widget.ToGrid().GetCellStyle(0, 1).CharStyle?.Background;

        widget.HasFocus = false;
        var inactiveBg = widget.ToGrid().GetCellStyle(0, 1).CharStyle?.Background;

        Assert.Equal(theme.Resolve(ThemeStyle.SelectedListItem).CharStyle?.Background, focusedBg);
        Assert.Equal(theme.Resolve(ThemeStyle.InactiveSelectedListItem).CharStyle?.Background, inactiveBg);
        Assert.NotEqual(focusedBg, inactiveBg); // DarkTheme: Green (active) vs DarkGray (inactive)
    }

    // The text input uses the active TextInput ink when focused and the explicit InactiveTextInput ink
    // when not.
    [Fact]
    public void TextInput_FocusedUsesTextInput_UnfocusedUsesInactiveTextInput()
    {
        var shell = new TestShell();
        var theme = shell.Theme;
        var input = new InlineTextInputWidget(shell, "server");

        input.HasFocus = true;
        var focused = input.ToGrid().GetCellStyle(0, 0).CharStyle;

        input.HasFocus = false;
        var inactive = input.ToGrid().GetCellStyle(0, 0).CharStyle;

        var active = theme.Resolve(ThemeStyle.TextInput).CharStyle;
        var inactiveToken = theme.Resolve(ThemeStyle.InactiveTextInput).CharStyle;

        Assert.Equal(active?.Foreground, focused?.Foreground);
        Assert.Equal(active?.Background, focused?.Background);
        Assert.Equal(inactiveToken?.Foreground, inactive?.Foreground);
        Assert.Equal(inactiveToken?.Background, inactive?.Background);
        Assert.NotNull(focused);
        Assert.NotNull(inactive);
        Assert.False(focused.Value.HasSameRenderingAs(inactive.Value));
    }

    // InactiveTextInput must carry both a foreground and a background (a muted input field, not plain
    // body text).
    [Fact]
    public void InactiveTextInput_DefinesBothForegroundAndBackground()
    {
        var shell = new TestShell();
        var ink = shell.Theme.Resolve(ThemeStyle.InactiveTextInput).CharStyle;

        Assert.NotNull(ink?.Foreground);
        Assert.NotNull(ink?.Background);
    }
}
