using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Overlay core plumbing for multi-style overlays. Verifies that the new <see cref="CliStyledOverlayRenderer"/>
/// can emit per-character styles in a single overlay, that a glyph with no style falls back to the overlay's
/// base <see cref="CliOverlay.Style"/>, and — crucially — that every existing single-style overlay (driven by
/// the plain <see cref="CliOverlayRenderer"/> char path) keeps rendering with the uniform base style.
/// </summary>
public sealed class OverlayStyledRenderingTests : TestBase
{
    // A single fixed-width, single-line cell whose space-filled line a horizontal overlay can overwrite.
    private static CliGrid HorizontalHost(int width)
    {
        var grid = new CliGrid(1, 1);
        grid.SetColumn(0, new CliGridColumnDefinition(
            new CliCellStyle { Width = width, MinWidth = width, MaxWidth = width }));
        grid.Set(0, 0, string.Empty, new CliCellStyle
        {
            FormattingMode = CliFormattingMode.Raw,
            Wrapping = CliWrapping.SingleLine,
        });
        return grid;
    }

    // Flattens a rendered line into one (char, style) pair per visible character.
    private static List<(char ch, CliCharStyle style)> CharsWithStyle(CliGrid grid, int line = 0)
    {
        var sink = new TextSegmentLinesSink();
        var lines = TigerConsole.RenderGridToSegmentedLines(grid, sink);
        var result = new List<(char, CliCharStyle)>();
        foreach (var seg in lines[line])
            foreach (var c in seg.Text)
                result.Add((c, seg.Style));
        return result;
    }

    // -------------------- legacy char path stays uniform --------------------

    [Fact]
    public void LegacyCharRenderer_UsesOverlayBaseStyle()
    {
        var baseStyle = new CliCharStyle(CliColor.Red);
        var grid = HorizontalHost(4);
        grid.AddOverlay(new CliOverlay(
            new CliPoint(0, 0), CliOrientation.Horizontal, 1, baseStyle,
            (_, _) => (true, new[] { 'a', 'b' })));

        var chars = CharsWithStyle(grid);
        Assert.Equal('a', chars[0].ch);
        Assert.Equal(CliColor.Red, chars[0].style.Foreground);
        Assert.Equal('b', chars[1].ch);
        Assert.Equal(CliColor.Red, chars[1].style.Foreground);
    }

    // -------------------- styled path: two styles in one overlay --------------------

    [Fact]
    public void StyledRenderer_EmitsTwoDifferentStylesInOneOverlay()
    {
        var baseStyle = new CliCharStyle(CliColor.Red);
        var green = new CliCharStyle(CliColor.Green);
        var blue = new CliCharStyle(CliColor.Blue);

        var grid = HorizontalHost(4);
        grid.AddOverlay(new CliOverlay(
            new CliPoint(0, 0), CliOrientation.Horizontal, 1, baseStyle,
            (CliStyledOverlayRenderer)((_, _) => (true, new[]
            {
                new CliOverlayGlyph('A', green),
                new CliOverlayGlyph('B', green),
                new CliOverlayGlyph('C', blue),
                new CliOverlayGlyph('D', blue),
            }))));

        var chars = CharsWithStyle(grid);
        Assert.Equal("ABCD", new string(chars.Select(c => c.ch).ToArray()));
        Assert.Equal(CliColor.Green, chars[0].style.Foreground);
        Assert.Equal(CliColor.Green, chars[1].style.Foreground);
        Assert.Equal(CliColor.Blue, chars[2].style.Foreground);
        Assert.Equal(CliColor.Blue, chars[3].style.Foreground);
    }

    // -------------------- styled path: null glyph style falls back to base --------------------

    [Fact]
    public void StyledRenderer_NullGlyphStyle_FallsBackToBaseStyle()
    {
        var baseStyle = new CliCharStyle(CliColor.Red);
        var green = new CliCharStyle(CliColor.Green);

        var grid = HorizontalHost(4);
        grid.AddOverlay(new CliOverlay(
            new CliPoint(0, 0), CliOrientation.Horizontal, 1, baseStyle,
            (CliStyledOverlayRenderer)((_, _) => (true, new[]
            {
                new CliOverlayGlyph('A'),          // no style -> base
                new CliOverlayGlyph('B', green),
            }))));

        var chars = CharsWithStyle(grid);
        Assert.Equal('A', chars[0].ch);
        Assert.Equal(CliColor.Red, chars[0].style.Foreground);   // base style
        Assert.Equal('B', chars[1].ch);
        Assert.Equal(CliColor.Green, chars[1].style.Foreground); // per-glyph style
    }

    [Fact]
    public void ForegroundOnlyOverlay_PreservesUnderlyingBackground()
    {
        var grid = HorizontalHost(2);
        grid.DefaultCellStyle = new CliCellStyle(new CliCharStyle(null, CliColor.Navy));
        grid.AddOverlay(new CliOverlay(
            new CliPoint(0, 0), CliOrientation.Horizontal, 1,
            new CliCharStyle(CliColor.Cyan),
            (_, _) => (true, new[] { 'A', 'B' })));

        var chars = CharsWithStyle(grid);
        Assert.All(chars, c =>
        {
            Assert.Equal(CliColor.Cyan, c.style.Foreground);
            Assert.Equal(CliColor.Navy, c.style.Background);
        });
    }

    [Fact]
    public void OverlayExplicitBackground_ReplacesUnderlyingBackground()
    {
        var grid = HorizontalHost(1);
        grid.DefaultCellStyle = new CliCellStyle(new CliCharStyle(null, CliColor.Navy));
        grid.AddOverlay(new CliOverlay(
            new CliPoint(0, 0), CliOrientation.Horizontal, 1,
            new CliCharStyle(CliColor.White, CliColor.DarkRed),
            (_, _) => (true, new[] { 'A' })));

        var cell = Assert.Single(CharsWithStyle(grid));
        Assert.Equal(CliColor.White, cell.style.Foreground);
        Assert.Equal(CliColor.DarkRed, cell.style.Background);
    }

    // -------------------- length validation on both paths --------------------

    [Fact]
    public void StyledRenderer_OverflowingContent_Throws()
    {
        var grid = HorizontalHost(3);
        grid.AddOverlay(new CliOverlay(
            new CliPoint(0, 0), CliOrientation.Horizontal, 1, default,
            (CliStyledOverlayRenderer)((_, _) => (true, new[]
            {
                new CliOverlayGlyph('A'), new CliOverlayGlyph('B'),
                new CliOverlayGlyph('C'), new CliOverlayGlyph('D'), // 4 > renderLength 3
            }))));

        Assert.Throws<TigerCliException>(() => TigerConsole.RenderGridToLines(grid));
    }

    [Fact]
    public void LegacyCharRenderer_OverflowingContent_Throws()
    {
        var grid = HorizontalHost(3);
        grid.AddOverlay(new CliOverlay(
            new CliPoint(0, 0), CliOrientation.Horizontal, 1, default,
            (_, _) => (true, new[] { 'A', 'B', 'C', 'D' }))); // 4 > renderLength 3

        Assert.Throws<TigerCliException>(() => TigerConsole.RenderGridToLines(grid));
    }

    // -------------------- existing overlays stay uniform --------------------

    [Fact]
    public void ProgressBarOverlay_RemainsUniformStyle()
    {
        var baseStyle = new CliCharStyle(CliColor.Red);
        var grid = HorizontalHost(10);
        grid.AddOverlay(new CliOverlay(
            new CliPoint(0, 0), CliOrientation.Horizontal, 1, baseStyle,
            CliOverlayRenderers.ProgressBar(() => 0.5)));

        var chars = CharsWithStyle(grid);
        // Two glyphs differ (filled vs track) but the whole bar shares the single base style.
        Assert.Contains(chars, c => c.ch == ConsoleSymbol.FullBlock);
        Assert.Contains(chars, c => c.ch == ConsoleSymbol.ShadeLight);
        Assert.All(chars, c => Assert.Equal(CliColor.Red, c.style.Foreground));
    }

    [Fact]
    public void DynamicTextOverlay_RemainsUniformStyle()
    {
        var baseStyle = new CliCharStyle(CliColor.Green);
        var grid = HorizontalHost(4);
        grid.AddOverlay(new CliOverlay(
            new CliPoint(0, 0), CliOrientation.Horizontal, 1, baseStyle,
            CliOverlayRenderers.DynamicText(() => "hi")));

        var chars = CharsWithStyle(grid);
        Assert.Equal('h', chars[0].ch);
        Assert.Equal('i', chars[1].ch);
        Assert.Equal(CliColor.Green, chars[0].style.Foreground);
        Assert.Equal(CliColor.Green, chars[1].style.Foreground);
    }

    [Fact]
    public void VerticalScrollBarOverlay_RemainsUniformStyle()
    {
        // A scrollable subgrid produces vertical scroll info that the real scrollbar renderer reads.
        var baseStyle = new CliCharStyle(CliColor.Magenta);
        var select = new InlineSelect(new TestShell(), Enumerable.Range(0, 12).Select(i => $"item{i}").ToList(), preselectIndex: 0);
        var host = new CliGrid(1, 1);
        host.SetRow(0, new CliGridRowDefinition(new CliCellStyle { Height = 7, MaxHeight = 7 }));
        host.SetSubgrid(0, 0, select.ToGrid(), CliScrollMode.Vertical, CliScrollThumbMode.ActivePoint);
        host.AddOverlay(new CliOverlay(
            new CliPoint(0, 0), CliOrientation.Vertical, 1, baseStyle,
            CliOverlayRenderers.VerticalScrollBar()));

        var sink = new TextSegmentLinesSink();
        var lines = TigerConsole.RenderGridToSegmentedLines(host, sink);

        // Every scrollbar glyph (arrows, thumb, track) carries the single base style.
        char[] barGlyphs = { ConsoleSymbol.TriangleUp, ConsoleSymbol.TriangleDown, ConsoleSymbol.FullBlock, ConsoleSymbol.SingleV };
        int seen = 0;
        foreach (var line in lines)
            foreach (var seg in line)
                foreach (var ch in seg.Text)
                    if (Array.IndexOf(barGlyphs, ch) >= 0)
                    {
                        Assert.Equal(CliColor.Magenta, seg.Style.Foreground);
                        seen++;
                    }
        Assert.True(seen >= 7, $"expected the scrollbar strip to be rendered, saw {seen} glyphs");
    }
}
