using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class ActivePointPropagationTests : TestBase
{
    [Fact]
    public void Subgrid_MapsSelectedIndex_ToOwnMeasuredActivePoint()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green", "Blue"], preselectIndex: 1);

        var grid = select.ToGrid();
        TigerConsole.RenderGridToLines(grid);

        Assert.NotNull(grid.MeasuredActivePoint);
        Assert.Equal(0, grid.MeasuredActivePoint!.Column);
        Assert.Equal(1, grid.MeasuredActivePoint.Row);
    }

    [Fact]
    public void Dialog_HostingSelect_ExposesParentMeasuredActivePoint()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green", "Blue"], preselectIndex: 1);
        var dialog = new InlineDialog(shell, "Pick one", select);

        var dialogGrid = dialog.ToGrid();
        TigerConsole.RenderGridToLines(dialogGrid);

        Assert.NotNull(dialogGrid.ActivePoint);
        Assert.NotNull(dialogGrid.MeasuredActivePoint);

        // The dialog hosts the select subgrid at column 1, content row.
        Assert.Equal(1, dialogGrid.ActivePoint!.Column);
        Assert.Equal(1, dialogGrid.MeasuredActivePoint!.Column);
        Assert.Equal(dialogGrid.ActivePoint.Row, dialogGrid.MeasuredActivePoint.Row);
    }

    [Fact]
    public void Dialog_AfterSelectionMoves_ParentMeasuredActivePointUpdates()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green", "Blue"]);
        var dialog = new InlineDialog(shell, "Pick one", select);

        var dialogGrid = dialog.ToGrid();
        TigerConsole.RenderGridToLines(dialogGrid);

        var firstMap = dialogGrid.MeasuredActivePoint;
        var firstScroll = dialogGrid.GetVerticalScrollInfo();
        Assert.NotNull(firstMap);
        Assert.NotNull(firstScroll);
        // ActivePoint thumb mode: offset is the absolute active-line index.
        int firstAbsolute = firstScroll!.Value.offset;

        Assert.True(select.HandleKey(new KeyEvent(ConsoleKey.DownArrow, ConsoleModifiers.None)).IsHandled);

        // dialog.ToGrid() reapplies the (now updated) subgrid; rendering remeasures.
        var refreshed = dialog.ToGrid();
        TigerConsole.RenderGridToLines(refreshed);

        var secondMap = refreshed.MeasuredActivePoint;
        var secondScroll = refreshed.GetVerticalScrollInfo();
        Assert.NotNull(secondMap);
        Assert.NotNull(secondScroll);
        int secondAbsolute = secondScroll!.Value.offset;

        // The absolute line within the rendered subgrid content advances by one
        // row, regardless of whether the viewport is large enough to avoid scrolling.
        Assert.Equal(firstAbsolute + 1, secondAbsolute);
    }

    [Fact]
    public void NonScrollableHost_DoesNotAutoPropagateActivePoint()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["A", "B", "C"], preselectIndex: 1);
        var sub = select.ToGrid();

        // Subgrid placed in a regular (non-scrollable) cell. No scrollable cell
        // on the host at all — propagation must not kick in.
        var host = new CliGrid(1, 1);
        host.SetSubgrid(0, 0, sub);

        TigerConsole.RenderGridToLines(host);

        // The subgrid itself still maps its selection — that part is unchanged.
        Assert.NotNull(sub.MeasuredActivePoint);

        // But because the host has no scrollable cell, propagation must not run.
        Assert.Null(host.ActivePoint);
        Assert.Null(host.MeasuredActivePoint);
    }

    [Fact]
    public void MultipleSubgrids_OnlyScrollableOneCanPropagate()
    {
        var shell = new TestShell();

        // Both controls are real subgrids with their own ActivePoint, but only
        // one of them is hosted in the host grid's scrollable cell.
        var nonScrollable = new InlineSelect(shell, ["A", "B", "C"], preselectIndex: 1);
        var scrollable = new InlineSelect(shell, ["X", "Y", "Z"], preselectIndex: 2);

        var host = new CliGrid(1, 2);
        host.SetSubgrid(0, 0, nonScrollable.ToGrid());
        host.SetSubgrid(0, 1, scrollable.ToGrid(), CliScrollMode.Vertical, CliScrollThumbMode.ActivePoint);

        TigerConsole.RenderGridToLines(host);

        Assert.NotNull(host.ActivePoint);
        Assert.NotNull(host.MeasuredActivePoint);

        // The scrollable cell is at (0, 1) — that's where propagation must land,
        // regardless of which non-scrollable subgrid happens to come first.
        Assert.Equal(0, host.ActivePoint!.Column);
        Assert.Equal(1, host.ActivePoint.Row);
        Assert.Equal(0, host.MeasuredActivePoint!.Column);
        Assert.Equal(1, host.MeasuredActivePoint.Row);
    }

    [Fact]
    public void ScrollableHost_ParentMeasuredActivePoint_IsViewportRelative_AfterScroll()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["A", "B", "C", "D", "E"], preselectIndex: 4);

        // Build a parent grid whose host cell forces a 2-line viewport over a
        // 5-item subgrid. With ActivePoint thumb mode, this exercises the
        // scroll/clip pipeline.
        var host = new CliGrid(1, 1);
        host.SetRow(0, new CliGridRowDefinition(new CliCellStyle { Height = 2, MaxHeight = 2 }));
        host.SetSubgrid(0, 0, select.ToGrid(), CliScrollMode.Vertical, CliScrollThumbMode.ActivePoint);

        TigerConsole.RenderGridToLines(host);

        Assert.NotNull(host.ActivePoint);
        Assert.Equal(0, host.ActivePoint!.Column);
        Assert.Equal(0, host.ActivePoint.Row);

        Assert.NotNull(host.MeasuredActivePoint);

        var scroll = host.GetVerticalScrollInfo();
        Assert.NotNull(scroll);
        var (visible, offset, viewport, total, maxOffset) = scroll!.Value;

        Assert.True(visible);
        Assert.Equal(5, total);
        Assert.Equal(2, viewport);

        // ActivePoint thumb mode: the movement range covers every active line,
        // so maxOffset = total - 1, not total - viewport.
        Assert.Equal(total - 1, maxOffset);

        // After AdjustActivePointForAlignment, the parent's MeasuredActivePoint
        // is viewport-relative — i.e. within [0, viewport).
        Assert.InRange(host.MeasuredActivePoint!.LineIndex, 0, viewport - 1);

        // GetVerticalScrollInfo exposes the absolute active-line index. The
        // selected item is the last row, so the absolute index = total-1.
        Assert.Equal(total - 1, offset);
    }
}
