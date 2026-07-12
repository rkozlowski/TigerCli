using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerTuiShellInjectionTests
{
    [Fact]
    public async Task SelectIndexAsync_WithShell_UsesInjectedShell()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.SelectIndexAsync(shell, "Pick one", ["Yes", "No"], ct: ct);

        Assert.Equal(1, result);
        Assert.Equal(2, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task InputAsync_WithShell_UsesInjectedShell()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.A, keyChar: 'a');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.InputAsync(shell, "Name", ct: ct);

        Assert.Equal("a", result);
        Assert.Contains("Name", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task SecretInputAsync_WithShell_UsesInjectedShellAndMasksRender()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Z, keyChar: 'z');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.SecretInputAsync(shell, "PIN", ct: ct);

        Assert.Equal("z", result);
        Assert.Contains(ConsoleSymbol.Bullet.ToString(), shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("z", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task SelectIndexAsync_NullShell_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => TigerTui.SelectIndexAsync((ICliAppShell)null!, "Pick one", ["Yes", "No"], ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InputAsync_NullShell_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => TigerTui.InputAsync((ICliAppShell)null!, "Name", ct: TestContext.Current.CancellationToken));
    }
}
