using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers the S1a rich-result foundation: the rich <c>*ResultAsync</c> entry points preserve the exact
/// <see cref="DialogResultKind"/> (so framework internals can branch on Cancel vs TokenCancel vs
/// Timeout), while the existing simple adapters keep their established collapsed behavior.
/// </summary>
public sealed class TigerTuiResultTests
{
    private static readonly string?[] Labels = ["a", "b", "c"];

    // ── Rich API preserves DialogResultKind ──────────────────────────────────

    [Fact]
    public async Task SelectIndexResultAsync_Enter_ReturnsOkWithValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.SelectIndexResultAsync(shell, "Pick", Labels, preselectIndex: 1, ct: ct);

        Assert.Equal(DialogResultKind.Ok, result.ResultKind);
        Assert.True(result.IsOk);
        Assert.Equal(1, result.Value);
        Assert.True(result.TryGetValue(out var value));
        Assert.Equal(1, value);
    }

    [Fact]
    public async Task SelectIndexResultAsync_Escape_ReturnsCancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await TigerTui.SelectIndexResultAsync(shell, "Pick", Labels, ct: ct);

        Assert.Equal(DialogResultKind.Cancel, result.ResultKind);
        Assert.False(result.IsOk);
        Assert.False(result.TryGetValue(out _));
    }

    [Fact]
    public async Task SelectIndexResultAsync_TokenCancellation_ReturnsTokenCancel()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));
        var shell = new TestShell();

        var result = await TigerTui
            .SelectIndexResultAsync(shell, "Pick", Labels, timeout: TimeSpan.FromSeconds(10), ct: cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(DialogResultKind.TokenCancel, result.ResultKind);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task SelectIndexResultAsync_Timeout_ReturnsTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();

        var result = await TigerTui
            .SelectIndexResultAsync(shell, "Pick", Labels, timeout: TimeSpan.FromMilliseconds(20), ct: ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Timeout, result.ResultKind);
        Assert.False(result.IsOk);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task InputResultAsync_Enter_ReturnsOkWithText()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.InputResultAsync(shell, "Name", initialValue: "seed", ct: ct);

        Assert.Equal(DialogResultKind.Ok, result.ResultKind);
        Assert.Equal("seed", result.Value);
    }

    [Fact]
    public async Task InputResultAsync_Escape_ReturnsCancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await TigerTui.InputResultAsync(shell, "Name", ct: ct);

        Assert.Equal(DialogResultKind.Cancel, result.ResultKind);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task ConfirmResultAsync_Enter_ReturnsYesWithTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.ConfirmResultAsync(shell, "Continue?", ct: ct);

        // A confirmation preserves the deliberate Yes/No kind (not collapsed to Ok) and carries the bool.
        Assert.Equal(DialogResultKind.Yes, result.ResultKind);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task ConfirmResultAsync_RightEnter_ReturnsNoWithFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.ConfirmResultAsync(shell, "Continue?", ct: ct);

        Assert.Equal(DialogResultKind.No, result.ResultKind);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task ConfirmResultAsync_Escape_ReturnsCancel_DistinctFromNo()
    {
        // The rich API keeps Escape as Cancel — distinct from a deliberate No answer — where the simple
        // ConfirmAsync adapter folds both into "false".
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await TigerTui.ConfirmResultAsync(shell, "Continue?", ct: ct);

        Assert.Equal(DialogResultKind.Cancel, result.ResultKind);
        Assert.NotEqual(DialogResultKind.No, result.ResultKind);
        Assert.False(result.IsOk);
    }

    // ── Simple adapters keep their existing observable behavior ──────────────

    [Fact]
    public async Task SelectIndexAsync_Enter_ReturnsIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var index = await TigerTui.SelectIndexAsync(shell, "Pick", Labels, preselectIndex: 2, ct: ct);

        Assert.Equal(2, index);
    }

    [Fact]
    public async Task SelectIndexAsync_Escape_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var index = await TigerTui.SelectIndexAsync(shell, "Pick", Labels, ct: ct);

        Assert.Null(index);
    }

    [Fact]
    public async Task SelectIndexAsync_TokenCancellation_ReturnsNull()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));
        var shell = new TestShell();

        var index = await TigerTui
            .SelectIndexAsync(shell, "Pick", Labels, timeout: TimeSpan.FromSeconds(10), ct: cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Null(index);
    }

    [Fact]
    public async Task SelectIndexAsync_Timeout_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();

        var index = await TigerTui
            .SelectIndexAsync(shell, "Pick", Labels, timeout: TimeSpan.FromMilliseconds(20), ct: ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Null(index);
    }

    [Fact]
    public async Task ConfirmAsync_Escape_ReturnsFalse_AdapterUnchanged()
    {
        // Adapter parity: the simple ConfirmAsync still folds Escape/Cancel to false even though the
        // rich API reports Cancel.
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await TigerTui.ConfirmAsync(shell, "Continue?", ct: ct);

        Assert.False(result);
    }

    [Fact]
    public async Task InputAsync_Escape_ReturnsNull_AdapterUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var value = await TigerTui.InputAsync(shell, "Name", ct: ct);

        Assert.Null(value);
    }
}
