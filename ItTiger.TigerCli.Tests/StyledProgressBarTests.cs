using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Slice 2: the styled (multi-colour) progress-bar overlay factory built on the slice-1 overlay core. The
/// factory returns a <see cref="CliStyledOverlayRenderer"/> whose segments are <see cref="CliOverlayGlyph"/>
/// values (glyph + optional pre-resolved style). Completed-state semantics: the <c>completed</c> segment is
/// used only when the fraction reaches exactly 100% — never below it, even when rounding fills the interior.
/// </summary>
public sealed class StyledProgressBarTests : TestBase
{
    private static readonly CliCharStyle Green = new(CliColor.Green);
    private static readonly CliCharStyle Gray = new(CliColor.Gray);
    private static readonly CliCharStyle Cyan = new(CliColor.Cyan);
    private static readonly CliCharStyle Yellow = new(CliColor.Yellow);

    private static CliOverlayGlyph[] Render(CliStyledOverlayRenderer renderer, int length)
    {
        var (visible, content) = renderer(new CliGrid(1, 1), length);
        Assert.True(visible);
        return content;
    }

    // -------------------- two-style: done / not-done --------------------

    [Fact]
    public void TwoStyle_DoneAndTrack_RenderWithDistinctStyles()
    {
        var content = Render(
            CliOverlayRenderers.ProgressBar(
                () => 0.5,
                done: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Green),
                track: new CliOverlayGlyph(ConsoleSymbol.ShadeLight, Gray)),
            length: 10);

        Assert.Equal(10, content.Length);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(ConsoleSymbol.FullBlock, content[i].Char);
            Assert.Equal(CliColor.Green, content[i].Style!.Value.Foreground);
        }
        for (int i = 5; i < 10; i++)
        {
            Assert.Equal(ConsoleSymbol.ShadeLight, content[i].Char);
            Assert.Equal(CliColor.Gray, content[i].Style!.Value.Foreground);
        }
    }

    // -------------------- three-style: completed at 100% --------------------

    [Fact]
    public void ThreeStyle_AtFull_UsesCompletedSegment()
    {
        var content = Render(
            CliOverlayRenderers.ProgressBar(
                () => 1.0,
                done: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Green),
                track: new CliOverlayGlyph(ConsoleSymbol.ShadeLight, Gray),
                completed: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Cyan)),
            length: 6);

        Assert.All(content, g =>
        {
            Assert.Equal(ConsoleSymbol.FullBlock, g.Char);
            Assert.Equal(CliColor.Cyan, g.Style!.Value.Foreground); // completed, not done
        });
    }

    [Fact]
    public void ThreeStyle_BelowFull_DoesNotApplyCompleted_EvenWhenRoundingFillsInterior()
    {
        // width 4, inner 4, fraction 0.99 -> round(3.96) = 4 -> the interior is visually full, but the value
        // is below 100%, so the filled cells must still use `done`, never `completed`.
        var content = Render(
            CliOverlayRenderers.ProgressBar(
                () => 0.99,
                done: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Green),
                track: new CliOverlayGlyph(ConsoleSymbol.ShadeLight, Gray),
                completed: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Cyan)),
            length: 4);

        Assert.All(content, g => Assert.Equal(CliColor.Green, g.Style!.Value.Foreground));
        Assert.DoesNotContain(content, g => g.Style!.Value.Foreground == CliColor.Cyan);
    }

    [Fact]
    public void ThreeStyle_PartiallyFilled_UsesDoneForFilled_CompletedUnused()
    {
        var content = Render(
            CliOverlayRenderers.ProgressBar(
                () => 0.5,
                done: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Green),
                track: new CliOverlayGlyph(ConsoleSymbol.ShadeLight, Gray),
                completed: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Cyan)),
            length: 4);

        Assert.Equal(CliColor.Green, content[0].Style!.Value.Foreground);
        Assert.Equal(CliColor.Green, content[1].Style!.Value.Foreground);
        Assert.Equal(CliColor.Gray, content[2].Style!.Value.Foreground);
        Assert.Equal(CliColor.Gray, content[3].Style!.Value.Foreground);
        Assert.DoesNotContain(content, g => g.Style!.Value.Foreground == CliColor.Cyan);
    }

    // -------------------- caps / brackets --------------------

    [Fact]
    public void Caps_ReserveEnds_AndFillStyledInterior()
    {
        var content = Render(
            CliOverlayRenderers.ProgressBar(
                () => 0.5,
                done: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Green),
                track: new CliOverlayGlyph(ConsoleSymbol.ShadeLight, Gray),
                leftCap: '[', rightCap: ']'),
            length: 10);

        Assert.Equal('[', content[0].Char);
        Assert.Null(content[0].Style);             // caps fall back to overlay base style
        Assert.Equal(']', content[9].Char);
        Assert.Null(content[9].Style);

        // 8 interior cells, half done / half track.
        Assert.Equal(4, content.Count(g => g.Char == ConsoleSymbol.FullBlock));
        Assert.Equal(4, content.Count(g => g.Char == ConsoleSymbol.ShadeLight));
    }

    [Fact]
    public void Caps_DroppedWhenStripTooShort()
    {
        var content = Render(
            CliOverlayRenderers.ProgressBar(
                () => 1.0,
                done: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Green),
                track: new CliOverlayGlyph(ConsoleSymbol.ShadeLight, Gray),
                leftCap: '[', rightCap: ']'),
            length: 2);

        Assert.DoesNotContain(content, g => g.Char is '[' or ']');
        Assert.All(content, g => Assert.Equal(ConsoleSymbol.FullBlock, g.Char));
    }

    // -------------------- length / bounds --------------------

    [Fact]
    public void StyledBar_FillsExactlyRenderLength()
    {
        var content = Render(
            CliOverlayRenderers.ProgressBar(
                () => 0.3,
                done: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Green),
                track: new CliOverlayGlyph(ConsoleSymbol.ShadeLight, Gray)),
            length: 7);

        Assert.Equal(7, content.Length);
    }

    [Fact]
    public void StyledBar_ZeroLength_NotVisible()
    {
        var (visible, content) = CliOverlayRenderers.ProgressBar(
            () => 0.5,
            done: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Green),
            track: new CliOverlayGlyph(ConsoleSymbol.ShadeLight, Gray))(new CliGrid(1, 1), 0);

        Assert.False(visible);
        Assert.Empty(content);
    }

    [Fact]
    public void StyledBar_ClampsOutOfRangeFraction()
    {
        var over = Render(
            CliOverlayRenderers.ProgressBar(
                () => 2.0,
                done: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Green),
                track: new CliOverlayGlyph(ConsoleSymbol.ShadeLight, Gray)),
            length: 6);
        Assert.Equal(6, over.Count(g => g.Char == ConsoleSymbol.FullBlock));

        var under = Render(
            CliOverlayRenderers.ProgressBar(
                () => -1.0,
                done: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Green),
                track: new CliOverlayGlyph(ConsoleSymbol.ShadeLight, Gray)),
            length: 6);
        Assert.Equal(6, under.Count(g => g.Char == ConsoleSymbol.ShadeLight));
    }

    // -------------------- end-to-end through the overlay application path --------------------

    [Fact]
    public void StyledBar_AppliedAsOverlay_RendersPerSegmentStyles()
    {
        var grid = new CliGrid(1, 1);
        grid.SetColumn(0, new CliGridColumnDefinition(
            new CliCellStyle { Width = 10, MinWidth = 10, MaxWidth = 10 }));
        grid.Set(0, 0, string.Empty, new CliCellStyle
        {
            FormattingMode = CliFormattingMode.Raw,
            Wrapping = CliWrapping.SingleLine,
        });

        // Base style Yellow so cap/null-style glyphs are visibly distinct from the segment styles.
        grid.AddOverlay(new CliOverlay(
            new CliPoint(0, 0), CliOrientation.Horizontal, 1, Yellow,
            CliOverlayRenderers.ProgressBar(
                () => 0.5,
                done: new CliOverlayGlyph(ConsoleSymbol.FullBlock, Green),
                track: new CliOverlayGlyph(ConsoleSymbol.ShadeLight, Gray))));

        var sink = new TextSegmentLinesSink();
        var lines = TigerConsole.RenderGridToSegmentedLines(grid, sink);
        var chars = new List<(char ch, CliCharStyle style)>();
        foreach (var seg in lines[0])
            foreach (var c in seg.Text)
                chars.Add((c, seg.Style));

        Assert.Equal(10, chars.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(ConsoleSymbol.FullBlock, chars[i].ch);
            Assert.Equal(CliColor.Green, chars[i].style.Foreground);
        }
        for (int i = 5; i < 10; i++)
        {
            Assert.Equal(ConsoleSymbol.ShadeLight, chars[i].ch);
            Assert.Equal(CliColor.Gray, chars[i].style.Foreground);
        }
    }
}
