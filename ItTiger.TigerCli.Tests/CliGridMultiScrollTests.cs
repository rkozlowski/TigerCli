using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Engine-level tests for the multiple-scrollable-cell model on <see cref="CliGrid"/>.
///
/// Keystone invariant under test:
///   ActivePoint = focus pointer = active scrollable cell = scroll-info source.
///
/// A grid may host several scrollable cells, but only the cell under the grid's resolved
/// active point is "active": only it runs active-point-follow. Inactive scrollable cells keep
/// their own offsets, still slice by those offsets, and clamp stale offsets when content shrinks.
///
/// These tests use <see cref="CliScrollThumbMode.Offset"/> so that GetVerticalScrollInfo.offset
/// reports the stored ScrollOffsetY directly, which makes offset preservation easy to assert.
/// </summary>
public sealed class CliGridMultiScrollTests : TestBase
{
    private static InlineSelect Select(ICliAppShell shell, int count, int preselect)
    {
        var items = new string[count];
        for (int i = 0; i < count; i++)
            items[i] = $"item{i}";
        return new InlineSelect(shell, items, preselectIndex: preselect);
    }

    // Two vertical scrollable subgrids stacked in cells (0,0) and (0,1), each with a fixed
    // viewport height so their content overflows and scrolls.
    private static CliGrid TwoVerticalHost(
        InlineSelect top,
        InlineSelect bottom,
        int viewportRows,
        CliScrollThumbMode thumb = CliScrollThumbMode.Offset)
    {
        var host = new CliGrid(1, 2);
        host.SetRow(0, new CliGridRowDefinition(new CliCellStyle { Height = viewportRows, MaxHeight = viewportRows }));
        host.SetRow(1, new CliGridRowDefinition(new CliCellStyle { Height = viewportRows, MaxHeight = viewportRows }));
        host.SetSubgrid(0, 0, top.ToGrid(), CliScrollMode.Vertical, thumb);
        host.SetSubgrid(0, 1, bottom.ToGrid(), CliScrollMode.Vertical, thumb);
        return host;
    }

    // Force a fresh measure; offsets in _scrollCells are preserved across this (InvalidateLayout
    // does not touch them), which is exactly the behavior several of these tests rely on.
    private static void Rerender(CliGrid host)
    {
        host.InvalidateLayout();
        TigerConsole.RenderGridToLines(host);
    }

    private static void Activate(CliGrid host, int column, int row)
    {
        host.ActivePoint = new ActivePoint(column, row, 0);
        Rerender(host);
    }

    // ------------------------------------------------------------------
    // Coexistence
    // ------------------------------------------------------------------

    [Fact]
    public void TwoScrollableSubgrids_InDifferentCells_DoNotThrow()
    {
        var shell = new TestShell();
        var host = TwoVerticalHost(Select(shell, 10, 0), Select(shell, 10, 0), viewportRows: 3);

        var lines = TigerConsole.RenderGridToLines(host);

        Assert.NotEmpty(lines);
        Assert.NotNull(host.GetVerticalScrollInfo(0, 0));
        Assert.NotNull(host.GetVerticalScrollInfo(0, 1));
    }

    // ------------------------------------------------------------------
    // Only the active cell follows the active point
    // ------------------------------------------------------------------

    [Fact]
    public void ActiveCell_FollowsActivePoint_InactiveCellDoesNot()
    {
        var shell = new TestShell();
        var top = Select(shell, 10, 9);    // selection at bottom -> wants to scroll
        var bottom = Select(shell, 10, 5); // selection mid-list, but inactive -> must not follow
        var host = TwoVerticalHost(top, bottom, viewportRows: 3);

        Activate(host, 0, 0); // top is active

        Assert.Equal(7, host.GetVerticalScrollInfo(0, 0)!.Value.offset); // followed: 9 - 3 + 1
        Assert.Equal(0, host.GetVerticalScrollInfo(0, 1)!.Value.offset); // inactive: not followed
    }

    [Fact]
    public void ChangingActivePoint_ChangesWhichCellFollows()
    {
        var shell = new TestShell();
        var top = Select(shell, 10, 9);
        var bottom = Select(shell, 10, 5);
        var host = TwoVerticalHost(top, bottom, viewportRows: 3);

        Activate(host, 0, 0);
        Assert.Equal(7, host.GetVerticalScrollInfo(0, 0)!.Value.offset);
        Assert.Equal(0, host.GetVerticalScrollInfo(0, 1)!.Value.offset);

        Activate(host, 0, 1); // focus moves to the bottom cell

        Assert.Equal(7, host.GetVerticalScrollInfo(0, 0)!.Value.offset); // preserved, no follow
        Assert.Equal(3, host.GetVerticalScrollInfo(0, 1)!.Value.offset); // now follows: 5 - 3 + 1
    }

    // ------------------------------------------------------------------
    // Inactive cells preserve offset across focus changes
    // ------------------------------------------------------------------

    [Fact]
    public void InactiveCell_PreservesOffset_WhenActivePointMovesAwayAndBack()
    {
        var shell = new TestShell();
        var top = Select(shell, 10, 9);
        var bottom = Select(shell, 10, 0);
        var host = TwoVerticalHost(top, bottom, viewportRows: 3);

        Activate(host, 0, 0);
        Assert.Equal(7, host.GetVerticalScrollInfo(0, 0)!.Value.offset);

        Activate(host, 0, 1); // focus away from top
        Assert.Equal(7, host.GetVerticalScrollInfo(0, 0)!.Value.offset); // unchanged while inactive

        Activate(host, 0, 0); // focus back to top
        Assert.Equal(7, host.GetVerticalScrollInfo(0, 0)!.Value.offset); // no jump back to the top
    }

    [Fact]
    public void InactiveScrollableCell_AppliesStoredOffset_DuringSlicing()
    {
        var shell = new TestShell();
        var top = Select(shell, 10, 9);
        var bottom = Select(shell, 10, 0);
        var host = TwoVerticalHost(top, bottom, viewportRows: 3);

        Activate(host, 0, 0);  // top scrolls to its bottom (offset 7)
        Activate(host, 0, 1);  // top now inactive, but keeps offset 7

        var rendered = TigerConsole.RenderGridToLines(host);
        var all = string.Join("\n", rendered);

        // The bottom (active) cell shows item0..item2; item9 can only come from the top cell
        // still sliced to its stored offset of 7 -> proof the inactive cell applied its offset.
        Assert.Contains("item9", all);
        Assert.Contains("item0", all);
    }

    // ------------------------------------------------------------------
    // Inactive cells clamp stale offsets when content shrinks
    // ------------------------------------------------------------------

    [Fact]
    public void InactiveScrollableCell_ClampsStaleOffset_WhenContentShrinks()
    {
        var shell = new TestShell();
        var top = Select(shell, 10, 9);
        var bottom = Select(shell, 10, 0);
        var host = TwoVerticalHost(top, bottom, viewportRows: 3);

        Activate(host, 0, 0);
        Assert.Equal(7, host.GetVerticalScrollInfo(0, 0)!.Value.offset);

        // Replace the top cell's content with a much shorter list. The scrollable-cell entry
        // (and its stored offset of 7) is preserved across the re-Set, so it is now stale.
        var smallTop = Select(shell, 4, 0);
        host.SetSubgrid(0, 0, smallTop.ToGrid(), CliScrollMode.Vertical, CliScrollThumbMode.Offset);

        Activate(host, 0, 1); // keep top inactive so only clamping (not follow) runs

        var info = host.GetVerticalScrollInfo(0, 0)!.Value;
        Assert.Equal(1, info.maxOffset);          // 4 items, viewport 3 -> max offset 1
        Assert.True(info.offset <= info.maxOffset); // stale 7 clamped down into range
    }

    // ------------------------------------------------------------------
    // Scroll-info source: parameterless = active cell; cell-addressed = requested cell
    // ------------------------------------------------------------------

    [Fact]
    public void ParameterlessScrollInfo_ReportsActiveCell()
    {
        var shell = new TestShell();
        var top = Select(shell, 10, 9);
        var bottom = Select(shell, 10, 5);
        var host = TwoVerticalHost(top, bottom, viewportRows: 3);

        Activate(host, 0, 0);
        Assert.Equal(host.GetVerticalScrollInfo(0, 0)!.Value.offset, host.GetVerticalScrollInfo()!.Value.offset);
        Assert.NotEqual(host.GetVerticalScrollInfo(0, 1)!.Value.offset, host.GetVerticalScrollInfo()!.Value.offset);

        Activate(host, 0, 1);
        Assert.Equal(host.GetVerticalScrollInfo(0, 1)!.Value.offset, host.GetVerticalScrollInfo()!.Value.offset);
    }

    [Fact]
    public void CellAddressedScrollInfo_ReportsRequestedCell_RegardlessOfActiveCell()
    {
        var shell = new TestShell();
        var top = Select(shell, 10, 9);
        var bottom = Select(shell, 10, 5);
        var host = TwoVerticalHost(top, bottom, viewportRows: 3);

        Activate(host, 0, 0);

        // The inactive cell can still be queried directly and reports its own (unfollowed) offset.
        Assert.Equal(0, host.GetVerticalScrollInfo(0, 1)!.Value.offset);
        Assert.Equal(7, host.GetVerticalScrollInfo(0, 0)!.Value.offset);
    }

    [Fact]
    public void CellAddressedScrollInfo_ForNonScrollableCoordinate_IsNull()
    {
        var shell = new TestShell();
        var host = TwoVerticalHost(Select(shell, 10, 0), Select(shell, 10, 0), viewportRows: 3);
        TigerConsole.RenderGridToLines(host);

        // (1, 0) is not a scrollable cell coordinate in this 1-column grid.
        Assert.Null(host.GetVerticalScrollInfo(1, 0));
    }

    // ------------------------------------------------------------------
    // Single scrollable cell: behavior unchanged (auto-propagation + active info)
    // ------------------------------------------------------------------

    [Fact]
    public void SingleScrollableCell_AutoPropagatesActivePoint_AndReportsActiveInfo()
    {
        var shell = new TestShell();
        var select = Select(shell, 5, 2);

        var host = new CliGrid(1, 1);
        host.SetRow(0, new CliGridRowDefinition(new CliCellStyle { Height = 2, MaxHeight = 2 }));
        host.SetSubgrid(0, 0, select.ToGrid(), CliScrollMode.Vertical, CliScrollThumbMode.Offset);

        TigerConsole.RenderGridToLines(host);

        // No explicit ActivePoint was set: the sole scrollable cell auto-propagates it.
        Assert.NotNull(host.ActivePoint);
        Assert.Equal(0, host.ActivePoint!.Column);
        Assert.Equal(0, host.ActivePoint.Row);

        var info = host.GetVerticalScrollInfo();
        Assert.NotNull(info);
        Assert.True(info!.Value.visible);
    }
}
