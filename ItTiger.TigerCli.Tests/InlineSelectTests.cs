using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineSelectTests : TestBase
{
    [Fact]
    public void EmptySelect_RendersEmptyState()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, []);

        Assert.Null(select.ContentStyle);
        Assert.False(select.CanConfirm);
        AssertSnapshot(select.ToGrid(), " No items available ");
    }

    [Fact]
    public void EmptySelect_EnterDoesNotConfirm_AndEscapeCancels()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, []);
        var dialog = new InlineDialog(shell, "Pick one", select);

        Assert.True(dialog.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None)));
        Assert.Equal(DialogResultKind.NoResult, dialog.Result);
        Assert.Null(dialog.Payload);

        Assert.True(dialog.HandleKey(new KeyEvent(ConsoleKey.Escape, ConsoleModifiers.None)));
        Assert.Equal(DialogResultKind.Cancel, dialog.Result);
        Assert.Null(dialog.Payload);
    }

    [Fact]
    public void NonEmptySelect_EnterStillConfirmsSelection()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"], preselectIndex: 1);
        var dialog = new InlineDialog(shell, "Pick one", select);

        Assert.True(select.CanConfirm);
        Assert.True(dialog.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None)));

        Assert.Equal(DialogResultKind.Ok, dialog.Result);
        Assert.Equal(1, dialog.Payload);
    }

    [Fact]
    public void ToGrid_NullLabel_RendersNoSelectionViaNullDisplayValue()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, new string?[] { null, "Red", "Green" });

        var text = string.Join("\n", TigerConsole.RenderGridToLines(select.ToGrid()));

        // The null row renders the muted "(None)" markup through the grid null-display path
        // (markup is parsed, so the literal "[Muted]" tag must not appear in the output).
        Assert.Contains("(None)", text);
        Assert.DoesNotContain("[Muted]", text);
        Assert.Contains("Red", text);
        Assert.Contains("Green", text);
    }

    [Fact]
    public void ToGrid_AllNonNullLabels_RenderUnchanged()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"]);

        var text = string.Join("\n", TigerConsole.RenderGridToLines(select.ToGrid()));

        Assert.DoesNotContain("(None)", text);
        Assert.Contains("Red", text);
        Assert.Contains("Green", text);
    }

    [Fact]
    public void ToGrid_ReusesCachedGrid_AndUpdatesActivePoint()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green", "Blue"]);

        var firstGrid = select.ToGrid();

        Assert.Same(firstGrid, select.ToGrid());
        Assert.NotNull(firstGrid.ActivePoint);
        Assert.Equal(0, firstGrid.ActivePoint.Row);

        Assert.True(select.HandleKey(new KeyEvent(ConsoleKey.DownArrow, ConsoleModifiers.None)).IsHandled);

        var secondGrid = select.ToGrid();

        Assert.Same(firstGrid, secondGrid);
        Assert.NotNull(secondGrid.ActivePoint);
        Assert.Equal(1, secondGrid.ActivePoint.Row);
    }

    [Fact]
    public void ToGrid_CopiesSharedLayoutSettingsThroughComponentHelper()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red"])
        {
            Width = 20,
            MinWidth = 10,
            SoftMaxWidth = 30,
            MaxWidth = 40,
            Height = 2,
            MinHeight = 1,
            SoftMaxHeight = 3,
            MaxHeight = 4
        };

        var grid = select.ToGrid();

        Assert.Equal(select.Width, grid.Width);
        Assert.Equal(select.MinWidth, grid.MinWidth);
        Assert.Equal(select.SoftMaxWidth, grid.SoftMaxWidth);
        Assert.Equal(select.MaxWidth, grid.MaxWidth);
        Assert.Equal(select.Height, grid.Height);
        Assert.Equal(select.MinHeight, grid.MinHeight);
        Assert.Equal(select.SoftMaxHeight, grid.SoftMaxHeight);
        Assert.Equal(select.MaxHeight, grid.MaxHeight);
    }

    [Fact]
    public void SelectionChange_InvalidatesCachedGridAndBubblesToDialog()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green", "Blue"]);
        var dialog = new InlineDialog(shell, "Pick one", select);

        var dialogGrid = dialog.ToGrid();
        TigerConsole.RenderGridToLines(dialogGrid);

        Assert.True(dialogGrid.IsMeasured);

        Assert.True(select.HandleKey(new KeyEvent(ConsoleKey.DownArrow, ConsoleModifiers.None)).IsHandled);

        Assert.False(dialogGrid.IsMeasured);
        Assert.Same(dialogGrid, dialog.ToGrid());
    }

    [Fact]
    public async Task ModalFlow_DrainedInputCanBeInspectedBeforeConfirming()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green", "Blue"]);
        var dialog = new InlineDialog(shell, "Pick one", select);
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKeys(ConsoleKey.DownArrow, ConsoleKey.DownArrow);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(2, select.Payload);
        Assert.True(shell.Terminal.RenderCount >= 3);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal(2, result.Payload);
    }

    [Fact]
    public async Task ModalFlow_EscapeCancels()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var select = new InlineSelect(shell, ["Red", "Green"]);
        var dialog = new InlineDialog(shell, "Pick one", select);

        var result = await shell.RunModalAsync(dialog, ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
        Assert.Equal(1, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ModalFlow_NoKeyBeforeTimeoutReturnsTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"]);
        var dialog = new InlineDialog(shell, "Pick one", select);

        var result = await shell.RunModalAsync(dialog, TimeSpan.FromMilliseconds(20), ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Timeout, result.Kind);
        Assert.Null(result.Payload);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ModalFlow_KeyPressBeforeTimeoutResetsTimer()
    {
        // Virtual time keeps this deterministic: the timeout deadline only moves when
        // the test advances the manual clock, so no wall-clock race can flip the result.
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(useManualClock: true);
        var select = new InlineSelect(shell, ["Red", "Green"]);
        var dialog = new InlineDialog(shell, "Pick one", select);
        var timeout = TimeSpan.FromSeconds(1);
        var runTask = shell.RunModalAsync(dialog, timeout, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        // Move to just before the original deadline (0.6s < 1s), then press a key.
        // The key press must reset the inactivity timer to "now + 1s" = 1.6s.
        shell.AdvanceTime(TimeSpan.FromMilliseconds(600));
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        // Move past the ORIGINAL deadline (1.2s > 1s) but before the reset deadline
        // (1.2s < 1.6s). The dialog must still be alive to accept the Escape.
        shell.AdvanceTime(TimeSpan.FromMilliseconds(600));
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
        Assert.Equal(2, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ModalFlow_RepeatedKeyPressesKeepDialogAliveBeyondOriginalTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(useManualClock: true);
        var select = new InlineSelect(shell, ["Red", "Green", "Blue"]);
        var dialog = new InlineDialog(shell, "Pick one", select);
        var timeout = TimeSpan.FromSeconds(1);
        var runTask = shell.RunModalAsync(dialog, timeout, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        // Each press lands before its (reset) deadline and pushes the deadline out.
        // Cumulative virtual time (4 * 0.7s = 2.8s) far exceeds the 1s original timeout,
        // proving repeated presses keep the dialog alive.
        for (int i = 0; i < 3; i++)
        {
            shell.AdvanceTime(TimeSpan.FromMilliseconds(700));
            shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
            await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        }

        shell.AdvanceTime(TimeSpan.FromMilliseconds(700));
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal(2, result.Payload);
        Assert.Equal(4, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ModalFlow_EscapeBeforeTimeoutReturnsCancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var select = new InlineSelect(shell, ["Red", "Green"]);
        var dialog = new InlineDialog(shell, "Pick one", select);

        var result = await shell.RunModalAsync(dialog, TimeSpan.FromSeconds(10), ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
        Assert.Equal(1, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ModalFlow_EnterBeforeTimeoutReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var select = new InlineSelect(shell, ["Red", "Green"], preselectIndex: 1);
        var dialog = new InlineDialog(shell, "Pick one", select);

        var result = await shell.RunModalAsync(dialog, TimeSpan.FromSeconds(10), ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal(1, result.Payload);
    }

    [Fact]
    public async Task ModalFlow_ExternalCancellationBeforeTimeoutReturnsTokenCancel()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));

        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"]);
        var dialog = new InlineDialog(shell, "Pick one", select);

        var result = await shell.RunModalAsync(dialog, TimeSpan.FromSeconds(10), cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(DialogResultKind.TokenCancel, result.Kind);
        Assert.Null(result.Payload);
    }

    [Fact]
    public async Task ModalFlow_EmptySelectEnterDoesNotConfirm()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var select = new InlineSelect(shell, []);
        var dialog = new InlineDialog(shell, "Pick one", select);
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.NoResult, dialog.Result);
        Assert.True(shell.Terminal.RenderCount >= 2);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
        Assert.Null(result.Payload);
        Assert.Equal(2, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ModalFlow_InitialRenderIsCaptured()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var select = new InlineSelect(shell, ["Red", "Green"]);
        var dialog = new InlineDialog(shell, "Pick one", select);

        await shell.RunModalAsync(dialog, ct);

        Assert.Equal(1, shell.Terminal.RenderCount);
        Assert.NotNull(shell.Terminal.LastRenderedGrid);
        Assert.Contains("Pick one", shell.Terminal.LastRenderedText);
        Assert.Contains("Red", shell.Terminal.LastRenderedText);
    }
}
