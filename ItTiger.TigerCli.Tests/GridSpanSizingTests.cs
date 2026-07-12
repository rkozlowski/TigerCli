using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tests;

public class GridSpanSizingTests : TestBase
{
    private static CliGridColumnDefinition AutoColumn(int? maxWidth = null, int? minWidth = null)
    {
        CliCellStyle? style = null;
        if (maxWidth.HasValue || minWidth.HasValue)
            style = new CliCellStyle { MaxWidth = maxWidth, MinWidth = minWidth };
        return new CliGridColumnDefinition(style) { Sizing = CliColumnSizing.Auto };
    }

    private static CliGridColumnDefinition StarColumn(int? maxWidth = null)
    {
        var style = maxWidth.HasValue ? new CliCellStyle { MaxWidth = maxWidth.Value } : null;
        return new CliGridColumnDefinition(style) { Sizing = CliColumnSizing.Star };
    }

    // 1) Span with sibling content driving initial widths.
    //    cols start at [3, 3] (from "ABC" / "DEF"). Span "1234567890" (10 chars).
    //    Deficit 4 split equally → cols [5, 5].
    [Fact]
    public void Span_WithSiblingContent_DistributesDeficit()
    {
        var grid = new CliGrid(2, 2);
        grid.Set(0, 0, "1234567890", colSpan: 2);
        grid.Set(0, 1, "ABC");
        grid.Set(1, 1, "DEF");

        AssertSnapshot(grid,
            "1234567890",
            "ABC  DEF  ");
    }

    // 2) Span over one populated col and one empty.
    //    cols start at [5, 1]; deficit = 4; equal split → cols [7, 3].
    [Fact]
    public void Span_OverPopulatedAndEmpty_SplitsEqually()
    {
        var grid = new CliGrid(2, 2);
        grid.Set(0, 0, "1234567890", colSpan: 2);
        grid.Set(0, 1, "HELLO");

        AssertSnapshot(grid,
            "1234567890",
            "HELLO     ");
    }

    // 3) Span across two Auto cols with no sibling content.
    //    cols start at [1, 1]; deficit = 8; equal split → cols [5, 5].
    [Fact]
    public void Span_AllAuto_NoSiblings_SplitsEqually()
    {
        var grid = new CliGrid(2, 1);
        grid.Set(0, 0, "1234567890", colSpan: 2);

        AssertSnapshot(grid, "1234567890");
    }

    // 4) Span across two Star cols, SoftMaxWidth ceiling above span content.
    //    Phase 2 absorbs deficit into Star cols (no Auto present).
    //    Then GrowStarColumns expands further to fill the soft ceiling — observably
    //    different from scenario 3 (final width = 20, content padded by GrowStarColumns).
    [Fact]
    public void Span_AllStar_WithSoftMax_AbsorbsAndFills()
    {
        var grid = new CliGrid(2, 1) { SoftMaxWidth = 20 };
        grid.SetColumn(0, StarColumn());
        grid.SetColumn(1, StarColumn());
        grid.Set(0, 0, "1234567890", colSpan: 2);

        // EnsureSpanWidthContribution: deficit 8 → Star cols [5, 5].
        // GrowStarColumns: extra 10 → cols [10, 10]. Span content left-aligned across 20.
        AssertSnapshot(grid, "1234567890          ");
    }

    // 5) Span across mixed Auto + Star. Star absorbs the entire deficit so the Auto
    //    column stays at content width (it exists to be content-driven, not to widen).
    [Fact]
    public void Span_MixedAutoStar_StarAbsorbsAll()
    {
        var grid = new CliGrid(2, 2);
        grid.SetColumn(0, AutoColumn());
        grid.SetColumn(1, StarColumn());
        grid.Set(0, 0, "1234567890", colSpan: 2);
        grid.Set(0, 1, "A");
        grid.Set(1, 1, "B");

        // cols start [1, 1]; deficit 8; phase 1 (Star=[1]): col 1 += 8 → 9.
        // Phase 2 not invoked. Auto col 0 stays at 1.
        // Final cols [1, 9].
        AssertSnapshot(grid,
            "1234567890",
            "AB        ");
    }

    // 6) Span over Star col with MaxWidth + Auto. Star caps first, Auto takes the rest.
    [Fact]
    public void Span_StarCappedThenAuto()
    {
        var grid = new CliGrid(2, 2);
        grid.SetColumn(0, StarColumn(maxWidth: 3));
        grid.SetColumn(1, AutoColumn());
        grid.Set(0, 0, "1234567890", colSpan: 2);
        grid.Set(0, 1, "A");
        grid.Set(1, 1, "B");

        // cols start [1, 1]; deficit = 8.
        // Phase 1 (Star=[0], cap=3): delta=8, avail=2 → col 0 = 3. Remaining = 6.
        // Phase 2 (Auto=[1]): col 1 += 6 → 7.
        // Final cols [3, 7]; row 1 "A  B      " (3-wide + 7-wide).
        AssertSnapshot(grid,
            "1234567890",
            "A  B      ");
    }

    // 7) Span where both Auto cols are locked and no Star.
    //    Deficit can't be absorbed → wrap kicks in (B-ladder regression test).
    [Fact]
    public void Span_LockedAutoOnly_FallsThroughToWrap()
    {
        var grid = new CliGrid(2, 1);
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { MinWidth = 3, MaxWidth = 3 }));
        grid.SetColumn(1, new CliGridColumnDefinition(new CliCellStyle { MinWidth = 3, MaxWidth = 3 }));
        grid.Set(0, 0, "abcdefghi", colSpan: 2);

        // cols locked at [3, 3]. Deficit 3 stranded.
        // RewrapAllCellsToCurrentWidths: WordWrap/SymbolWrap fail (no breakpoints in token),
        // CharWrap splits: "abcdef" / "ghi". Row height = 2.
        AssertSnapshot(grid,
            "abcdef",
            "ghi   ");
    }

    // 8) Two overlapping spans across different rows sharing a column.
    //    First span widens to satisfy its content; second span sees shared col already
    //    wider and its own deficit is reduced/zero.
    [Fact]
    public void TwoOverlappingSpans_SharedColSatisfiesBoth()
    {
        var grid = new CliGrid(3, 2);
        grid.Set(0, 0, "12345678", colSpan: 2);
        grid.Set(1, 1, "abcde", colSpan: 2);

        // Row 0 span: deficit 6 split equally → cols [4, 4, 1].
        // Row 1 span: currentSum 4+1=5, natural=5 → deficit 0. No-op.
        AssertSnapshot(grid,
            "12345678 ",
            "    abcde");
    }

    // 9) Span + SoftMaxWidth that forces shrink. After widening, sum > ceiling, shrink
    //    reduces widths and the wrap pass wraps the span content.
    [Fact]
    public void Span_WithSoftMaxCeiling_ShrinkAndWrap()
    {
        var grid = new CliGrid(2, 1) { SoftMaxWidth = 8 };
        grid.Set(0, 0, "abc def ghi jkl", colSpan: 2);

        // cols start [1, 1]; widen: deficit 13 → cols [8, 7]. Sum 15 > 8.
        // Shrink proportionally to ceiling → cols [4, 4].
        // WordWrap: "abc def" / "ghi jkl", each padded to 8.
        AssertSnapshot(grid,
            "abc def ",
            "ghi jkl ");
    }

    // 10) colSpan=1 regression — EnsureSpanWidthContribution is a no-op.
    [Fact]
    public void ColSpanOne_NoChange()
    {
        var grid = new CliGrid(2, 1);
        grid.Set(0, 0, "ABC");
        grid.Set(1, 0, "DEF");

        AssertSnapshot(grid, "ABCDEF");
    }

    // 11) Span coexisting with a non-spanned subgrid sibling row.
    //    Verifies EnsureSpanWidthContribution interacts cleanly with subgrid measurement.
    [Fact]
    public void Span_WithSubgridSibling_NoCrashRendersCorrectly()
    {
        var inner = new CliGrid(1, 1);
        inner.Set(0, 0, "AB");

        var grid = new CliGrid(3, 2);
        grid.Set(0, 0, "1234567890", colSpan: 2);
        grid.SetSubgrid(0, 1, inner);
        grid.Set(1, 1, "C");
        grid.Set(2, 1, "D");

        // Initial cols: col 0 = 2 (subgrid "AB"), col 1 = 1 ("C"), col 2 = 1 ("D"). Sum 4.
        // Span deficit = 10 - (col 0 + col 1) = 7. Auto split: per=3, rem=1.
        //   col 0 += 4 → 6, col 1 += 3 → 4.
        // Final cols [6, 4, 1].
        // Row 0: span "1234567890" in 10-wide + " " for col 2 = 11 chars.
        // Row 1: subgrid "AB" padded to 6 + "C" padded to 4 + "D" = "AB    C   D".
        AssertSnapshot(grid,
            "1234567890 ",
            "AB    C   D");
    }
}
