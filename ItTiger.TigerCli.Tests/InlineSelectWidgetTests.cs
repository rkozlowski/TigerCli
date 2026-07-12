using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineSelectWidgetTests : TestBase
{
    [Fact]
    public void InitialSelectedIndex_ClampsToItems()
    {
        var shell = new TestShell();
        var widget = new InlineSelectWidget(shell, ["Red", "Green", "Blue"], preselectIndex: 20);

        Assert.Equal(2, widget.SelectedIndex);
        Assert.Equal("Blue", widget.SelectedValue);
    }

    [Fact]
    public void UpDownNavigation_ChangesSelection()
    {
        var shell = new TestShell();
        var widget = new InlineSelectWidget(shell, ["Red", "Green", "Blue"]);

        Assert.True(widget.HandleKey(Key(ConsoleKey.DownArrow)).IsHandled);
        Assert.True(widget.HandleKey(Key(ConsoleKey.DownArrow)).IsHandled);
        Assert.True(widget.HandleKey(Key(ConsoleKey.UpArrow)).IsHandled);

        Assert.Equal(1, widget.SelectedIndex);
        Assert.Equal("Green", widget.SelectedValue);
    }

    [Fact]
    public void Navigation_ClampsAtBoundaries()
    {
        var shell = new TestShell();
        var widget = new InlineSelectWidget(shell, ["Red", "Green"]);

        Assert.True(widget.HandleKey(Key(ConsoleKey.UpArrow)).IsHandled);
        Assert.Equal(0, widget.SelectedIndex);

        Assert.True(widget.HandleKey(Key(ConsoleKey.DownArrow)).IsHandled);
        Assert.True(widget.HandleKey(Key(ConsoleKey.DownArrow)).IsHandled);
        Assert.Equal(1, widget.SelectedIndex);
    }

    [Fact]
    public void HomeEndAndPaging_MoveSelection()
    {
        var shell = new TestShell(viewportHeight: 8);
        var widget = new InlineSelectWidget(shell, ["A", "B", "C", "D", "E"]);

        Assert.True(widget.HandleKey(Key(ConsoleKey.PageDown)).IsHandled);
        Assert.Equal(3, widget.SelectedIndex);

        Assert.True(widget.HandleKey(Key(ConsoleKey.PageUp)).IsHandled);
        Assert.Equal(0, widget.SelectedIndex);

        Assert.True(widget.HandleKey(Key(ConsoleKey.End)).IsHandled);
        Assert.Equal(4, widget.SelectedIndex);

        Assert.True(widget.HandleKey(Key(ConsoleKey.Home)).IsHandled);
        Assert.Equal(0, widget.SelectedIndex);
    }

    [Fact]
    public void EmptyList_IsNotConfirmableAndDoesNotHandleNavigation()
    {
        var shell = new TestShell();
        var widget = new InlineSelectWidget(shell, []);

        Assert.Equal(-1, widget.SelectedIndex);
        Assert.Null(widget.SelectedValue);
        Assert.False(widget.HandleKey(Key(ConsoleKey.DownArrow)).IsHandled);
        AssertSnapshot(widget.ToGrid(), " No items available ");
    }

    [Fact]
    public void RenderingMatchesInlineSelectWrapper()
    {
        var shell = new TestShell();
        var widget = new InlineSelectWidget(shell, ["Red", "Green"], preselectIndex: 1);
        var select = new InlineSelect(shell, ["Red", "Green"], preselectIndex: 1);

        Assert.Equal(
            TigerConsole.RenderGridToLines(select.ToGrid()),
            TigerConsole.RenderGridToLines(widget.ToGrid()));
    }

    [Fact]
    public void ActivePointTracksSelectedRow()
    {
        var shell = new TestShell();
        var widget = new InlineSelectWidget(shell, ["Red", "Green", "Blue"]);

        widget.HandleKey(Key(ConsoleKey.DownArrow));
        var grid = widget.ToGrid();

        Assert.NotNull(grid.ActivePoint);
        Assert.Equal(1, grid.ActivePoint!.Row);
    }

    [Fact]
    public void WrapperGetWidgets_ReturnsFocusedInFrameScrollableWidget()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"]);

        var widget = Assert.Single(select.GetWidgets());

        Assert.True(widget.IsFocused);
        Assert.Equal(InlineDialogArea.InFrameScrollable, widget.Area);
        Assert.Equal(select.ControlDecoration, widget.Decoration);
        Assert.Equal(select.ScrollMode, widget.ScrollMode);
        Assert.Equal(select.ThumbMode, widget.ThumbMode);
        Assert.Same(select.ToGrid(), widget.Grid);
    }

    [Fact]
    public void WrapperHandleKey_DelegatesSelectionToWidget()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"]);

        Assert.True(select.HandleKey(Key(ConsoleKey.DownArrow)).IsHandled);

        Assert.Equal(1, select.Payload);
        Assert.Equal(1, select.ToGrid().ActivePoint!.Row);
    }

    private static KeyEvent Key(ConsoleKey key) => new(key, ConsoleModifiers.None);
}
