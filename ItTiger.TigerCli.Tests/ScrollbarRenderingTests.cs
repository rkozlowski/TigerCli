using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class ScrollbarRenderingTests : TestBase
{
    private static CliGrid BuildHost(
        ICliAppShell shell,
        IReadOnlyList<string> items,
        int viewportRows,
        int preselect,
        CliScrollThumbMode thumbMode,
        out InlineSelect select)
    {
        select = new InlineSelect(shell, items, preselectIndex: preselect);
        var host = new CliGrid(1, 1);
        host.SetRow(0, new CliGridRowDefinition(new CliCellStyle { Height = viewportRows, MaxHeight = viewportRows }));
        host.SetSubgrid(0, 0, select.ToGrid(), CliScrollMode.Vertical, thumbMode);
        return host;
    }

    // -------------------- GetVerticalScrollInfo: ActivePoint mode --------------------

    [Fact]
    public void ActivePointMode_AtTop_OffsetIsZero_MaxOffsetIsTotalMinusOne()
    {
        var host = BuildHost(new TestShell(), ["A", "B", "C", "D", "E"], viewportRows: 2, preselect: 0,
            CliScrollThumbMode.ActivePoint, out _);
        TigerConsole.RenderGridToLines(host);

        var info = host.GetVerticalScrollInfo();
        Assert.NotNull(info);
        var (visible, offset, viewport, total, maxOffset) = info!.Value;
        Assert.True(visible);
        Assert.Equal(5, total);
        Assert.Equal(2, viewport);
        Assert.Equal(0, offset);
        Assert.Equal(total - 1, maxOffset);
    }

    [Fact]
    public void ActivePointMode_AtMiddle_OffsetIsMiddleIndex()
    {
        var host = BuildHost(new TestShell(), ["A", "B", "C", "D", "E"], viewportRows: 2, preselect: 2,
            CliScrollThumbMode.ActivePoint, out _);
        TigerConsole.RenderGridToLines(host);

        var info = host.GetVerticalScrollInfo();
        Assert.NotNull(info);
        Assert.Equal(2, info!.Value.offset);
        Assert.Equal(info.Value.total - 1, info.Value.maxOffset);
    }

    [Fact]
    public void ActivePointMode_AtBottom_OffsetEqualsTotalMinusOne()
    {
        var host = BuildHost(new TestShell(), ["A", "B", "C", "D", "E"], viewportRows: 2, preselect: 4,
            CliScrollThumbMode.ActivePoint, out _);
        TigerConsole.RenderGridToLines(host);

        var info = host.GetVerticalScrollInfo();
        Assert.NotNull(info);
        var (_, offset, _, total, maxOffset) = info!.Value;
        Assert.Equal(total - 1, offset);
        Assert.Equal(total - 1, maxOffset);
    }

    // -------------------- GetVerticalScrollInfo: Offset mode --------------------

    [Fact]
    public void OffsetMode_AtTop_OffsetIsZero_MaxOffsetIsTotalMinusViewport()
    {
        var host = BuildHost(new TestShell(), ["A", "B", "C", "D", "E"], viewportRows: 2, preselect: 0,
            CliScrollThumbMode.Offset, out _);
        TigerConsole.RenderGridToLines(host);

        var info = host.GetVerticalScrollInfo();
        Assert.NotNull(info);
        var (visible, offset, viewport, total, maxOffset) = info!.Value;
        Assert.True(visible);
        Assert.Equal(5, total);
        Assert.Equal(2, viewport);
        Assert.Equal(0, offset);
        Assert.Equal(total - viewport, maxOffset);
    }

    [Fact]
    public void OffsetMode_AtBottom_OffsetEqualsMaxOffset()
    {
        // ApplyAlignmentAndFill auto-scrolls to bring the last item into view,
        // so for Offset mode this yields ScrollOffsetY = total - viewport.
        var host = BuildHost(new TestShell(), ["A", "B", "C", "D", "E"], viewportRows: 2, preselect: 4,
            CliScrollThumbMode.Offset, out _);
        TigerConsole.RenderGridToLines(host);

        var info = host.GetVerticalScrollInfo();
        Assert.NotNull(info);
        var (_, offset, viewport, total, maxOffset) = info!.Value;
        Assert.Equal(total - viewport, maxOffset);
        Assert.Equal(maxOffset, offset);
    }

    [Fact]
    public void Hidden_WhenContentFitsViewport()
    {
        // 3 items, viewport 3 -> total == viewport -> not visible.
        var host = BuildHost(new TestShell(), ["A", "B", "C"], viewportRows: 3, preselect: 0,
            CliScrollThumbMode.Offset, out _);
        TigerConsole.RenderGridToLines(host);

        var info = host.GetVerticalScrollInfo();
        Assert.NotNull(info);
        Assert.False(info!.Value.visible);
    }

    // -------------------- End-to-end scrollbar rendering --------------------
    //
    // RenderScrollBar is private to InlineDialog. We exercise the same formula
    // through a host grid + overlay that mirrors RenderScrollBar.

    private static char[] RenderScrollBar(CliGrid grid, int length)
    {
        var info = grid.GetVerticalScrollInfo();
        if (info == null || !info.Value.visible || length <= 2)
            return [];

        var (_, offset, viewport, total, maxOffset) = info.Value;

        char[] chars = new char[length];
        chars[0] = ConsoleSymbol.TriangleUp;
        chars[length - 1] = ConsoleSymbol.TriangleDown;

        int trackLength = length - 2;
        if (trackLength > 0)
        {
            int thumbSize = trackLength * viewport / total;
            if (thumbSize < 1) thumbSize = 1;
            if (thumbSize > trackLength) thumbSize = trackLength;

            int maxThumbPos = trackLength - thumbSize;
            int thumbPos = maxOffset <= 0
                ? 0
                : (int)Math.Round((double)offset * maxThumbPos / maxOffset);
            if (thumbPos < 0) thumbPos = 0;
            if (thumbPos > maxThumbPos) thumbPos = maxThumbPos;

            for (int i = 0; i < trackLength; i++)
            {
                chars[i + 1] = (i >= thumbPos && i < thumbPos + thumbSize)
                    ? ConsoleSymbol.FullBlock
                    : ConsoleSymbol.SingleV;
            }
        }
        return chars;
    }

    [Fact]
    public void Render_ActivePointMode_ThumbAtTop_WhenSelectionAtFirstItem()
    {
        var host = BuildHost(new TestShell(), Items(12), viewportRows: 7, preselect: 0,
            CliScrollThumbMode.ActivePoint, out _);
        TigerConsole.RenderGridToLines(host);

        var bar = RenderScrollBar(host, length: 7);
        // length 7, trackLength 5, thumbSize = 5*7/12 = 2, maxThumbPos = 3.
        // offset 0, maxOffset 11 -> thumbPos 0.
        Assert.Equal(
            new[] { ConsoleSymbol.TriangleUp, ConsoleSymbol.FullBlock, ConsoleSymbol.FullBlock,
                    ConsoleSymbol.SingleV, ConsoleSymbol.SingleV, ConsoleSymbol.SingleV,
                    ConsoleSymbol.TriangleDown },
            bar);
    }

    [Fact]
    public void Render_ActivePointMode_ThumbAtBottom_WhenSelectionAtLastItem()
    {
        var host = BuildHost(new TestShell(), Items(12), viewportRows: 7, preselect: 11,
            CliScrollThumbMode.ActivePoint, out _);
        TigerConsole.RenderGridToLines(host);

        var bar = RenderScrollBar(host, length: 7);
        // offset 11, maxOffset 11 -> thumbPos = 3 = maxThumbPos.
        Assert.Equal(
            new[] { ConsoleSymbol.TriangleUp,
                    ConsoleSymbol.SingleV, ConsoleSymbol.SingleV, ConsoleSymbol.SingleV,
                    ConsoleSymbol.FullBlock, ConsoleSymbol.FullBlock,
                    ConsoleSymbol.TriangleDown },
            bar);
    }

    [Fact]
    public void Render_OffsetMode_ThumbAtBottom_WhenScrolledToBottom()
    {
        var host = BuildHost(new TestShell(), Items(12), viewportRows: 7, preselect: 11,
            CliScrollThumbMode.Offset, out _);
        TigerConsole.RenderGridToLines(host);

        var bar = RenderScrollBar(host, length: 7);
        // viewport 7 of total 12 -> thumbSize 5*7/12 = 2.
        // Offset mode: ScrollOffsetY = 12 - 7 = 5, maxOffset = 5 -> thumbPos = maxThumbPos = 3.
        Assert.Equal(ConsoleSymbol.FullBlock, bar[4]);
        Assert.Equal(ConsoleSymbol.FullBlock, bar[5]);
        Assert.Equal(ConsoleSymbol.TriangleDown, bar[6]);
    }

    [Fact]
    public void Render_ThumbSize_Reflects_Viewport_Over_Total()
    {
        // 12 / 16 visible -> trackLength 12 -> thumbSize = 12 * 12 / 16 = 9.
        var host = BuildHost(new TestShell(), Items(16), viewportRows: 12, preselect: 0,
            CliScrollThumbMode.ActivePoint, out _);
        TigerConsole.RenderGridToLines(host);

        var bar = RenderScrollBar(host, length: 14);
        int thumbChars = bar.Count(c => c == ConsoleSymbol.FullBlock);
        Assert.Equal(9, thumbChars);
    }

    private static string[] Items(int n)
    {
        var arr = new string[n];
        for (int i = 0; i < n; i++) arr[i] = $"item{i}";
        return arr;
    }
}
