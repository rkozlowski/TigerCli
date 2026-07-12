using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineSelectModalFlowTests : TestBase
{
    [Fact]
    public async Task ModalFlow_HomeEndKeys_UpdateSelectionBeforeConfirm()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green", "Blue", "Yellow"], preselectIndex: 1);
        var dialog = new InlineDialog(shell, "Pick one", select);
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.End);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(3, select.Payload);

        shell.Terminal.EnqueueKey(ConsoleKey.Home);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(0, select.Payload);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal(0, result.Payload);
    }

    [Fact]
    public async Task ModalFlow_PageDownPageUp_UsesViewportPageSize()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(viewportHeight: 8);
        var select = new InlineSelect(shell, Items(10));
        var dialog = new InlineDialog(shell, "Pick one", select);
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.PageDown);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(3, select.Payload);

        shell.Terminal.EnqueueKey(ConsoleKey.PageUp);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(0, select.Payload);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal(0, result.Payload);
    }

    [Fact]
    public async Task ModalFlow_NavigationClampsAtFirstAndLastItem()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["A", "B", "C"]);
        var dialog = new InlineDialog(shell, "Pick one", select);
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKeys(ConsoleKey.UpArrow, ConsoleKey.UpArrow);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(0, select.Payload);

        shell.Terminal.EnqueueKeys(ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(2, select.Payload);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal(2, result.Payload);
    }

    [Fact]
    public async Task ModalFlow_EmptySelectNavigationDoesNotCreatePayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var select = new InlineSelect(shell, []);
        var dialog = new InlineDialog(shell, "Pick one", select);
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKeys(ConsoleKey.DownArrow, ConsoleKey.End, ConsoleKey.Home, ConsoleKey.UpArrow);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Null(select.Payload);
        Assert.Equal(DialogResultKind.NoResult, dialog.Result);
        Assert.Contains("No items available", shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
        Assert.Null(result.Payload);
    }

    [Fact]
    public async Task ModalFlow_LongListScrolling_UpdatesRenderedScrollInfoAfterDownKeys()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(viewportHeight: 8);
        var select = new InlineSelect(shell, Items(12));
        var dialog = new InlineDialog(shell, "Pick one", select);
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKeys(ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(5, select.Payload);
        var scroll = shell.Terminal.LastRenderedGrid?.GetVerticalScrollInfo();
        Assert.NotNull(scroll);
        Assert.True(scroll!.Value.visible);
        Assert.Equal(5, scroll.Value.offset);
        Assert.Equal(12, scroll.Value.total);
        Assert.Equal(scroll.Value.total - 1, scroll.Value.maxOffset);
        Assert.Contains("item5", shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }

    [Fact]
    public async Task ModalFlow_EndKey_MovesActivePointThumbToBottom()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(viewportHeight: 8);
        var select = new InlineSelect(shell, Items(12));
        var dialog = new InlineDialog(shell, "Pick one", select);
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.End);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(11, select.Payload);
        var scroll = shell.Terminal.LastRenderedGrid?.GetVerticalScrollInfo();
        Assert.NotNull(scroll);
        Assert.True(scroll!.Value.visible);
        Assert.Equal(scroll.Value.total - 1, scroll.Value.offset);
        Assert.Equal(scroll.Value.total - 1, scroll.Value.maxOffset);
        Assert.Contains("item11", shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal(11, result.Payload);
    }

    [Fact]
    public async Task ModalFlow_RerendersAfterNonTerminatingNavigation()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green", "Blue"]);
        var dialog = new InlineDialog(shell, "Pick one", select);
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        int initialRenderCount = shell.Terminal.RenderCount;

        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(1, select.Payload);
        Assert.True(shell.Terminal.RenderCount > initialRenderCount);
        Assert.Contains("Pick one", shell.Terminal.LastRenderedText);
        Assert.Contains("Green", shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }

    private static string[] Items(int n)
    {
        var items = new string[n];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = $"item{i}";
        }

        return items;
    }
}
