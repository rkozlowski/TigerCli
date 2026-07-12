using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Drives <see cref="TigerTui.MessageBoxAsync"/> through the real modal loop against a test terminal,
/// proving button activation and the Escape → Cancel fallback complete the dialog with the expected
/// <see cref="DialogResultKind"/>.
/// </summary>
public sealed class TigerTuiMessageBoxTests
{
    [Fact]
    public async Task MessageBox_Ok_EnterReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MessageBoxAsync(shell, "Saved.", MessageBoxButtons.Ok, ct: ct);

        Assert.Equal(DialogResultKind.Ok, result);
        Assert.Contains("Saved.", shell.Terminal.LastRenderedText);
        Assert.Contains("OK", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task MessageBox_YesNo_EnterReturnsYes()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MessageBoxAsync(shell, "Delete this item?", MessageBoxButtons.YesNo, ct: ct);

        Assert.Equal(DialogResultKind.Yes, result);
    }

    [Fact]
    public async Task MessageBox_YesNo_RightEnterReturnsNo()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MessageBoxAsync(shell, "Delete this item?", MessageBoxButtons.YesNo, ct: ct);

        Assert.Equal(DialogResultKind.No, result);
    }

    [Fact]
    public async Task MessageBox_Escape_ReturnsCancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        // Even the Ok set (no explicit Cancel button) cancels via the dialog fallback.
        var result = await TigerTui.MessageBoxAsync(shell, "Proceed?", MessageBoxButtons.Ok, ct: ct);

        Assert.Equal(DialogResultKind.Cancel, result);
    }

    [Fact]
    public async Task MessageBox_AbortRetryIgnore_NavigatesAndActivates()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow); // Abort -> Retry
        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow); // Retry -> Ignore
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MessageBoxAsync(shell, "File is locked.", MessageBoxButtons.AbortRetryIgnore, ct: ct);

        Assert.Equal(DialogResultKind.Ignore, result);
    }

    [Fact]
    public async Task MessageBox_RendersTitle_WhenProvided()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        await TigerTui.MessageBoxAsync(shell, "All done.", MessageBoxButtons.Ok, title: "Status", ct: ct);

        Assert.Contains("Status", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task WarningAsync_CompletesAndUsesWarningSurface()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        // The default button is still [ OK ], and the dialog still routes through the normal pipeline.
        var result = await TigerTui.WarningAsync(shell, "Low disk space.", ct: ct);

        Assert.Equal(DialogResultKind.Ok, result);
        Assert.Contains("Low disk space.", shell.Terminal.LastRenderedText);
        Assert.Contains("OK", shell.Terminal.LastRenderedText);

        // The rendered dialog surface is the theme's warning surface, not the plain dialog surface.
        var expected = shell.Theme.Resolve(ThemeStyle.WarningSurface).CharStyle?.Background;
        Assert.Equal(expected, shell.Terminal.LastRenderedGrid?.DefaultCellStyle?.CharStyle?.Background);
    }

    [Fact]
    public async Task ErrorAsync_CompletesAndUsesErrorSurface()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.ErrorAsync(shell, "Connection failed.", ct: ct);

        Assert.Equal(DialogResultKind.Ok, result);
        Assert.Contains("Connection failed.", shell.Terminal.LastRenderedText);
        Assert.Contains("OK", shell.Terminal.LastRenderedText);

        var expected = shell.Theme.Resolve(ThemeStyle.ErrorSurface).CharStyle?.Background;
        Assert.Equal(expected, shell.Terminal.LastRenderedGrid?.DefaultCellStyle?.CharStyle?.Background);
    }

    [Fact]
    public async Task ErrorAsync_RespectsButtonSet_AndEscapeCancels()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await TigerTui.ErrorAsync(shell, "Retry the operation?", MessageBoxButtons.OkCancel, ct: ct);

        Assert.Equal(DialogResultKind.Cancel, result);
    }
}
