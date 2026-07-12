using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// S1b: process/system cancellation surfaced through the modal loop as
/// <see cref="DialogResultKind.SystemCancel"/>. Driven deterministically via
/// <see cref="TestShell.RaiseSystemCancellation"/> rather than real OS signals.
/// </summary>
public sealed class TigerTuiSystemCancelTests
{
    private static readonly string?[] Labels = ["a", "b", "c"];

    [Fact]
    public async Task RichApi_SystemCancellation_ReturnsSystemCancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        // Trip the system token before the modal runs; the loop returns on its first iteration.
        shell.RaiseSystemCancellation();

        var result = await TigerTui.SelectIndexResultAsync(shell, "Pick", Labels, ct: ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.SystemCancel, result.ResultKind);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task SystemCancel_IsDistinctFromOtherCancellationKinds()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.RaiseSystemCancellation();

        var result = await TigerTui.SelectIndexResultAsync(shell, "Pick", Labels, ct: ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.SystemCancel, result.ResultKind);
        Assert.NotEqual(DialogResultKind.Cancel, result.ResultKind);
        Assert.NotEqual(DialogResultKind.TokenCancel, result.ResultKind);
        Assert.NotEqual(DialogResultKind.Timeout, result.ResultKind);
    }

    [Fact]
    public async Task SimpleApi_SystemCancellation_ThrowsSystemCancellationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.RaiseSystemCancellation();

        await Assert.ThrowsAsync<TigerCliSystemCancellationException>(
            async () => await TigerTui.SelectIndexAsync(shell, "Pick", Labels, ct: ct)
                .WaitAsync(TimeSpan.FromSeconds(1), ct));
    }

    [Fact]
    public async Task SimpleConfirm_SystemCancellation_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.RaiseSystemCancellation();

        await Assert.ThrowsAsync<TigerCliSystemCancellationException>(
            async () => await TigerTui.ConfirmAsync(shell, "Continue?", ct: ct)
                .WaitAsync(TimeSpan.FromSeconds(1), ct));
    }

    [Fact]
    public async Task SystemCancel_TakesPrecedenceOverTokenCancellation()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.Cancel(); // caller token already cancelled …
        var shell = new TestShell();
        shell.RaiseSystemCancellation(); // … and the system token is tripped too

        var result = await TigerTui.SelectIndexResultAsync(shell, "Pick", Labels, ct: cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(DialogResultKind.SystemCancel, result.ResultKind);
    }

    [Fact]
    public async Task SystemCancel_TakesPrecedenceOverTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.RaiseSystemCancellation();

        // Timeout deadline is also due (zero), but the system token wins.
        var result = await TigerTui.SelectIndexResultAsync(shell, "Pick", Labels, timeout: TimeSpan.Zero, ct: ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.SystemCancel, result.ResultKind);
    }

    [Fact]
    public async Task SystemCancellation_DuringModal_RestoresTerminalState()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.RaiseSystemCancellation();

        var result = await TigerTui.SelectIndexResultAsync(shell, "Pick", Labels, ct: ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.SystemCancel, result.ResultKind);
        // The modal's finally ran RestoreState, re-showing the cursor hidden during rendering.
        Assert.True(shell.Terminal.CursorVisible);
    }

    [Fact]
    public async Task NormalSelection_StillReturnsOk_WhenNoSystemCancellation()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.SelectIndexResultAsync(shell, "Pick", Labels, preselectIndex: 1, ct: ct);

        Assert.Equal(DialogResultKind.Ok, result.ResultKind);
        Assert.Equal(1, result.Value);
    }
}
