using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tests;

public class GridStarSizingTests : TestBase
{
    private static CliGridColumnDefinition StarColumn(int? maxWidth = null)
    {
        var style = maxWidth.HasValue ? new CliCellStyle { MaxWidth = maxWidth.Value } : null;
        return new CliGridColumnDefinition(style) { Sizing = CliColumnSizing.Star };
    }

    private static CliGridColumnDefinition AutoColumn(int? maxWidth = null)
    {
        var style = maxWidth.HasValue ? new CliCellStyle { MaxWidth = maxWidth.Value } : null;
        return new CliGridColumnDefinition(style) { Sizing = CliColumnSizing.Auto };
    }

    // 1) Single Star col, SoftMaxWidth=20, sibling Auto col with 2-char content.
    //    Star col fills the residual.
    [Fact]
    public void StarColumn_FillsResidualSpace()
    {
        var grid = new CliGrid(2, 1) { SoftMaxWidth = 20 };
        grid.SetColumn(0, AutoColumn());
        grid.SetColumn(1, StarColumn());
        grid.Set(0, 0, "AB");
        grid.Set(1, 0, "XY");

        AssertSnapshot(grid, "ABXY                ");
    }

    // 2) Two Star cols, no Auto, SoftMaxWidth=20. Equal split (each gets +8).
    [Fact]
    public void TwoStarColumns_ShareResidualEqually()
    {
        var grid = new CliGrid(2, 1) { SoftMaxWidth = 20 };
        grid.SetColumn(0, StarColumn());
        grid.SetColumn(1, StarColumn());
        grid.Set(0, 0, "AB");
        grid.Set(1, 0, "CD");

        AssertSnapshot(grid, "AB        CD        ");
    }

    // 3) Two Star cols, odd remainder (SoftMaxWidth=21).
    //    DistributeExtraToScrollAxes gives leftover to lower-indexed columns first.
    [Fact]
    public void TwoStarColumns_OddRemainder_LeftBiased()
    {
        var grid = new CliGrid(2, 1) { SoftMaxWidth = 21 };
        grid.SetColumn(0, StarColumn());
        grid.SetColumn(1, StarColumn());
        grid.Set(0, 0, "AB");
        grid.Set(1, 0, "CD");

        // extra=17; per=8, remainder=1. col0 += 9 → 11, col1 += 8 → 10. Total 21.
        AssertSnapshot(grid, "AB         CD        ");
    }

    // 4) One Auto (5-char content), one Star (1-char content), SoftMaxWidth=20.
    //    Auto stays at content size; Star absorbs the rest.
    [Fact]
    public void AutoPlusStar_AutoStaysContentSized()
    {
        var grid = new CliGrid(2, 1) { SoftMaxWidth = 20 };
        grid.SetColumn(0, AutoColumn());
        grid.SetColumn(1, StarColumn());
        grid.Set(0, 0, "HELLO");
        grid.Set(1, 0, "X");

        AssertSnapshot(grid, "HELLOX              ");
    }

    // 5) Star col with no SoftMaxWidth. Star is a no-op; column stays at content width.
    [Fact]
    public void StarColumn_NoSoftMaxWidth_NoGrowth()
    {
        var grid = new CliGrid(2, 1);
        grid.SetColumn(0, AutoColumn());
        grid.SetColumn(1, StarColumn());
        grid.Set(0, 0, "AB");
        grid.Set(1, 0, "CD");

        AssertSnapshot(grid, "ABCD");
    }

    // 6) Star column capped by MaxWidth; no other Star to absorb the residual.
    //    Star fills only up to its cap.
    [Fact]
    public void StarColumn_CappedByMaxWidth()
    {
        var grid = new CliGrid(2, 1) { SoftMaxWidth = 20 };
        grid.SetColumn(0, AutoColumn());
        grid.SetColumn(1, StarColumn(maxWidth: 8));
        grid.Set(0, 0, "HI");
        grid.Set(1, 0, "X");

        // col0 = 2 (Auto "HI"), col1 = 8 (Star capped). Total 10.
        AssertSnapshot(grid, "HIX       ");
    }

    // 7) Two Star cols, first one has MaxWidth=5.
    //    Current behavior: DistributeExtraToScrollAxes does not redistribute leftover
    //    after a column hits its cap. col0 caps at 5 (3 units stranded), col1 takes
    //    its base 8 units only. Total falls short of SoftMaxWidth.
    [Fact]
    public void TwoStarColumns_OneCapped_LeftoverStranded()
    {
        var grid = new CliGrid(2, 1) { SoftMaxWidth = 20 };
        grid.SetColumn(0, StarColumn(maxWidth: 5));
        grid.SetColumn(1, StarColumn());
        grid.Set(0, 0, "AB");
        grid.Set(1, 0, "CD");

        // extra=16; per=8, rem=0.
        // col0 delta=8, capped at 5-2=3 → width 5.
        // col1 delta=8 → width 10. (Stranded 3 units not redistributed.)
        AssertSnapshot(grid, "AB   CD        ");
    }

    // 8) Regression: all-Auto grid behaves exactly as before Star was introduced.
    [Fact]
    public void AllAutoColumns_NoChange()
    {
        var grid = new CliGrid(2, 1) { SoftMaxWidth = 20 };
        // No SetColumn → defaults to Auto.
        grid.Set(0, 0, "AB");
        grid.Set(1, 0, "CD");

        AssertSnapshot(grid, "ABCD");
    }

    // 9) Star col alongside a horizontally-scrolling subgrid cell.
    //    Scrolling fills first (capped by its column MaxWidth), then Star takes
    //    the residual. Verifies the chosen ordering: scrolling before Star.
    [Fact]
    public void StarColumn_AfterHorizontallyScrollingSibling()
    {
        var inner = new CliGrid(1, 1);
        inner.Set(0, 0, "X");

        var grid = new CliGrid(2, 1) { SoftMaxWidth = 8 };
        grid.SetColumn(0, AutoColumn(maxWidth: 3));
        grid.SetColumn(1, StarColumn());
        grid.SetSubgrid(0, 0, inner, CliScrollMode.Horizontal);
        grid.Set(1, 0, "S");

        // GrowScrollingColumns: col0 1→3 (capped at MaxWidth=3).
        // GrowStarColumns: col1 1→5 (absorbs the residual: 8 - 3 - 1).
        AssertSnapshot(grid, "X  S    ");
    }
}
