using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Stage 2: cursor mode for inline modal dialogs. The dialog forwards the focused widget's cursor
/// intent (a focused text input → <see cref="CursorMode.Normal"/>, everything else → Hidden), and the
/// inline modal loop is the sole authority for the terminal cursor: hidden while drawing, shown again
/// after a render only when the measured grid requires it, and restored on exit. Cursor placement
/// stays owned by <see cref="CliGrid"/> (the measured active point).
/// </summary>
public sealed class InlineDialogCursorTests : TestBase
{
    // A composite control exposing a text input and a select in distinct areas, with explicit focus
    // control so a text input can be focused or unfocused.
    private sealed class CursorProbeControl : InlineMultiControl
    {
        public readonly InlineTextInputWidget Input;
        public readonly InlineSelectWidget List;
        public readonly int InputIndex;
        public readonly int ListIndex;

        public CursorProbeControl(ICliAppShell shell)
            : base(shell)
        {
            Input = new InlineTextInputWidget(shell, "abc");
            List = new InlineSelectWidget(shell, ["Red", "Green"], preselectIndex: 0);

            InputIndex = AddWidget(Input, InlineDialogArea.AboveFrameWithIndicators,
                CliControlDecoration.HorizontalIndicators, CliScrollMode.Horizontal, CliScrollThumbMode.ActivePoint);
            ListIndex = AddWidget(List, InlineDialogArea.InFrameScrollable,
                CliControlDecoration.VerticalScrollBar, CliScrollMode.Vertical, CliScrollThumbMode.ActivePoint);

            SetFocusedWidgetIndex(ListIndex);
        }

        public override object? Payload => List.SelectedValue;

        public void FocusInput() => SetFocusedWidgetIndex(InputIndex);
        public void FocusList() => SetFocusedWidgetIndex(ListIndex);
    }

    // ------------------------------------------------------------------
    // 1. Focused text input dialog produces visible cursor intent
    // ------------------------------------------------------------------

    [Fact]
    public void FocusedTextInputDialog_PropagatesNormalCursorIntent()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell, "abc");
        var dialog = new InlineDialog(shell, title: null, input, "Name");

        Assert.Equal(CursorMode.Normal, dialog.ToGrid().CursorMode);
    }

    // ------------------------------------------------------------------
    // 2. Unfocused text input does not leave the cursor visible
    // ------------------------------------------------------------------

    [Fact]
    public void Dialog_TextInputPresentButNotFocused_PropagatesHiddenCursorIntent()
    {
        var shell = new TestShell();
        var control = new CursorProbeControl(shell); // list is focused; the text input is not
        var dialog = new InlineDialog(shell, title: null, control);

        Assert.Equal(CursorMode.Hidden, dialog.ToGrid().CursorMode);
    }

    // A dialog whose only widget needs no cursor (a select) also reports Hidden intent.
    [Fact]
    public void SingleSelectDialog_PropagatesHiddenCursorIntent()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"]);
        var dialog = new InlineDialog(shell, title: null, select);

        Assert.Equal(CursorMode.Hidden, dialog.ToGrid().CursorMode);
    }

    // ------------------------------------------------------------------
    // 3. Moving focus away from the text input hides the cursor
    // ------------------------------------------------------------------

    [Fact]
    public void MovingFocusAwayFromTextInput_FlipsCursorIntentToHidden()
    {
        var shell = new TestShell();
        var control = new CursorProbeControl(shell);
        var dialog = new InlineDialog(shell, title: null, control);

        control.FocusInput();
        Assert.Equal(CursorMode.Normal, dialog.ToGrid().CursorMode);

        control.FocusList();
        Assert.Equal(CursorMode.Hidden, dialog.ToGrid().CursorMode);
    }

    // ------------------------------------------------------------------
    // 4. Cursor position is based on the measured active point (CliGrid-owned), not manual math
    // ------------------------------------------------------------------

    [Fact]
    public async Task ModalFlow_CursorPosition_DerivesFromMeasuredActivePoint()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var input = new InlineTextInput(shell, "abc", width: 10); // short text: no horizontal scroll
        var dialog = new InlineDialog(shell, title: null, input, "Name");
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        var grid = shell.Terminal.LastRenderedGrid;
        Assert.NotNull(grid);
        var ap = grid!.MeasuredActivePoint;
        Assert.NotNull(ap);
        var origin = grid.GetMeasuredCellOrigin(ap!.Column, ap.Row);
        Assert.NotNull(origin);

        // The terminal cursor column is exactly the measured cell origin plus the measured in-line
        // offset — the dialog/shell never recompute it.
        Assert.Equal(origin!.Value.Column + ap.OffsetInLine, shell.Terminal.CursorLeft);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);
    }

    // ------------------------------------------------------------------
    // 5. Modal render hides cursor before drawing; shows after only when required
    // ------------------------------------------------------------------

    [Fact]
    public async Task ModalRender_HidesCursorBeforeDrawing_ShowsAfterWhenRequired()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var input = new InlineTextInput(shell, "abc", width: 10);
        var dialog = new InlineDialog(shell, title: null, input, "Name");
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        // Hidden while drawing; made visible again only after the render completes (focused input).
        Assert.False(shell.Terminal.CursorVisibleAtLastRender);
        Assert.True(shell.Terminal.CursorVisible);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);
    }

    [Fact]
    public async Task ModalRender_NonCursorDialog_LeavesCursorHidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"]);
        var dialog = new InlineDialog(shell, title: null, select);
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        // No cursor required: hidden during the draw and still hidden after it.
        Assert.False(shell.Terminal.CursorVisibleAtLastRender);
        Assert.False(shell.Terminal.CursorVisible);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);
    }

    // ------------------------------------------------------------------
    // 6. Leaving RunModalAsync restores cursor visibility
    // ------------------------------------------------------------------

    [Fact]
    public async Task LeavingRunModalAsync_RestoresCursorVisibility()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        Assert.True(shell.Terminal.CursorVisible); // visible before entering the modal

        var input = new InlineTextInput(shell, "abc", width: 10);
        var dialog = new InlineDialog(shell, title: null, input, "Name");
        var runTask = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.True(shell.Terminal.CursorVisible); // restored on exit
    }
}
