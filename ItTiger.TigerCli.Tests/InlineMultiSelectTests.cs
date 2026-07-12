using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineMultiSelectTests
{
    [Flags]
    private enum TestFlags
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4,
        ReadWrite = Read | Write
    }

    private enum PlainEnum
    {
        One = 1,
        Two = 2
    }

    [Fact]
    public void ToGrid_UsesTwoColumnLayoutAndLabelActivePoint()
    {
        var shell = new TestShell();
        var control = new InlineMultiSelect(shell, ["Red", "Green"], [1]);

        var grid = control.ToGrid();
        var lines = TigerConsole.RenderGridToLines(grid);
        var selectedStyle = shell.Theme.Resolve(ThemeStyle.SelectedListItem);

        Assert.Equal(2, grid.ColumnCount);
        Assert.Equal(2, grid.RowCount);
        Assert.Equal("↑↓ Move   Space Toggle; + All; - None; * Invert; Enter Confirm   Esc Cancel", control.Hint);
        Assert.Equal(CliControlDecoration.VerticalScrollBar, control.ControlDecoration);
        Assert.Equal(CliScrollMode.Vertical, control.ScrollMode);
        Assert.Equal(1, grid.ActivePoint!.Column);
        Assert.Equal(0, grid.ActivePoint.Row);
        Assert.Contains($"[{ConsoleSymbol.Square}]", string.Join("\n", lines));
        Assert.Equal(selectedStyle.CharStyle?.Background, grid.GetCellStyle(0, 0).CharStyle?.Background);
        Assert.Equal(selectedStyle.CharStyle?.Background, grid.GetCellStyle(1, 0).CharStyle?.Background);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_ReturnsSelectedIndexes()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MultiSelectIndexesAsync(shell, "Pick", ["Red", "Green", "Blue"], ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal([0, 1], result!);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_SpaceTogglesSelectedState()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MultiSelectIndexesAsync(shell, "Pick", ["Red", "Green"], ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal([0], result!);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_PlusSelectsAll()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Add, keyChar: '+');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MultiSelectIndexesAsync(shell, "Pick", ["Red", "Green", "Blue"], ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal([0, 1, 2], result!);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_MinusDeselectsAll()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Subtract, keyChar: '-');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MultiSelectIndexesAsync(shell, "Pick", ["Red", "Green", "Blue"], [0, 2], ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_AsteriskInvertsAll()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Multiply, keyChar: '*');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MultiSelectIndexesAsync(shell, "Pick", ["Red", "Green", "Blue"], [0, 2], ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal([1], result!);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_EmptySelectionIsValidWhenItemsExist()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MultiSelectIndexesAsync(shell, "Pick", ["Red", "Green"], ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_PreselectedIndexesAreInitiallyChecked()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = TigerTui.MultiSelectIndexesAsync(shell, "Pick", ["Red", "Green"], [1], ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        Assert.Contains($"[{ConsoleSymbol.Square}]", shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.NotNull(result);
        Assert.Equal([1], result!);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_ReturnsIndexesInOriginalItemOrder()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.UpArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.UpArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MultiSelectIndexesAsync(shell, "Pick", ["Red", "Green", "Blue"], ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal([0, 2], result!);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_EscapeReturnsNull()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await TigerTui.MultiSelectIndexesAsync(shell, "Pick", ["Red"], ct: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_NoKeyBeforeTimeoutReturnsNull()
    {
        var shell = new TestShell();

        var result = await TigerTui.MultiSelectIndexesAsync(shell, "Pick", ["Red"], timeout: TimeSpan.FromMilliseconds(20), ct: TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_TokenCancellationReturnsNull()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));

        var shell = new TestShell();
        var result = await TigerTui.MultiSelectIndexesAsync(shell, "Pick", ["Red"], timeout: TimeSpan.FromSeconds(10), ct: cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_EmptyItemListIsNotConfirmableAndCancelsToNull()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await TigerTui.MultiSelectIndexesAsync(shell, "Pick", [], ct: TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Contains("No items available", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task MultiSelectIndexesAsync_NullShellThrows()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => TigerTui.MultiSelectIndexesAsync(null!, "Pick", ["Red"], ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MultiSelectAsync_MapsSelectedIndexesBackToItems()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MultiSelectAsync(shell, "Pick", [10, 20, 30], i => $"Item {i}", ct: TestContext.Current.CancellationToken);

        Assert.Equal([10, 20], result);
    }

    [Fact]
    public async Task MultiSelectAsync_PreselectedItemsWork()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MultiSelectAsync(shell, "Pick", ["A", "B"], s => s, ["B"], ct: TestContext.Current.CancellationToken);

        Assert.Equal(["B"], result);
    }

    [Fact]
    public async Task MultiSelectAsync_UnknownPreselectedItemIsRejected()
    {
        var shell = new TestShell();

        await Assert.ThrowsAsync<ArgumentException>(
            () => TigerTui.MultiSelectAsync(shell, "Pick", ["A", "B"], s => s, ["C"], ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MultiSelectFlagsAsync_ListsOnlyNonZeroSingleBitValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = TigerTui.MultiSelectFlagsAsync<TestFlags>(shell, "Flags", ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        Assert.Contains("Read", shell.Terminal.LastRenderedText);
        Assert.Contains("Write", shell.Terminal.LastRenderedText);
        Assert.Contains("Execute", shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("[ ] None", shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("ReadWrite", shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);
    }

    [Fact]
    public async Task MultiSelectFlagsAsync_SelectedValuePreselectsContainedBitsAndConfirmReturnsOr()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MultiSelectFlagsAsync(shell, "Flags", TestFlags.ReadWrite, ct: TestContext.Current.CancellationToken);

        Assert.Equal(TestFlags.ReadWrite, result);
    }

    [Fact]
    public async Task MultiSelectFlagsAsync_TogglingAllSelectedBitsReturnsZero()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await TigerTui.MultiSelectFlagsAsync(shell, "Flags", TestFlags.ReadWrite, ct: TestContext.Current.CancellationToken);

        Assert.Equal(TestFlags.None, result);
    }

    [Fact]
    public async Task MultiSelectFlagsAsync_UndefinedBitsInSelectedAreRejected()
    {
        var shell = new TestShell();

        await Assert.ThrowsAsync<ArgumentException>(
            () => TigerTui.MultiSelectFlagsAsync(shell, "Flags", (TestFlags)8, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MultiSelectFlagsAsync_NonFlagsEnumIsRejected()
    {
        var shell = new TestShell();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => TigerTui.MultiSelectFlagsAsync(shell, "Flags", PlainEnum.One, ct: TestContext.Current.CancellationToken));
    }
}
