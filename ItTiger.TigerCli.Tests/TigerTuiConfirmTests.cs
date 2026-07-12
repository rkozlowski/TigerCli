using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerTuiConfirmTests
{
    [Fact]
    public async Task ConfirmAsync_DefaultEnter_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.ConfirmAsync(shell, "Continue?", ct: ct);

        Assert.True(result);
        Assert.Contains("Yes", shell.Terminal.LastRenderedText);
        Assert.Contains("No", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task ConfirmAsync_RightEnter_ReturnsFalse()
    {
        // The Yes/No message box navigates horizontally (Left/Right), so Right moves Yes -> No.
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.ConfirmAsync(shell, "Continue?", ct: ct);

        Assert.False(result);
    }

    [Fact]
    public async Task ConfirmAsync_PreselectTrueEnter_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.ConfirmAsync(shell, "Continue?", preselect: true, ct: ct);

        Assert.True(result);
    }

    [Fact]
    public async Task ConfirmAsync_PreselectFalseEnter_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.ConfirmAsync(shell, "Continue?", preselect: false, ct: ct);

        Assert.False(result);
    }

    [Fact]
    public async Task ConfirmAsync_Escape_ReturnsFalse()
    {
        // The message box maps Escape -> Cancel, which ConfirmAsync converts to false.
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await TigerTui.ConfirmAsync(shell, "Continue?", ct: ct);

        Assert.False(result);
    }

    [Fact]
    public async Task ConfirmAsync_NoKeyBeforeTimeout_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();

        var result = await TigerTui.ConfirmAsync(shell, "Continue?", timeout: TimeSpan.FromMilliseconds(20), ct: ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Null(result);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ConfirmAsync_TokenCancellation_ReturnsNull()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));

        var shell = new TestShell();

        var result = await TigerTui.ConfirmAsync(shell, "Continue?", timeout: TimeSpan.FromSeconds(10), ct: cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }
}
