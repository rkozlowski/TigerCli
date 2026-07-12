using System.Text.RegularExpressions;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Pins the CliGrid span/subgrid sizing contract introduced when <c>PreferredContentWidth</c> was
/// removed: a hosted subgrid's <b>natural</b> width is content-driven (it does not balloon to an
/// inherited soft-max), and a subgrid that declares a fill (Star) column <b>fills</b> the width the
/// host hands it — so a list row / selection highlight spans the whole hosting cell rather than only
/// the longest item. Background ownership of the fill is verified with <see cref="HtmlSink"/>.
///
/// These are exercised through the public surface (<see cref="CliGrid.SetSubgrid"/> places a
/// colSpan-1 subgrid). The colSpan&gt;1 placement used by the real dialogs is covered by
/// <see cref="CliGridSelectDialogFillTests"/>.
/// </summary>
public sealed class CliGridSubgridFillTests : TestBase
{
    // A one-cell subgrid whose single column is a Star (fill) column. Optionally tags the content
    // with a background so HtmlSink can show who owns the fill.
    private static CliGrid StarSubgrid(string content, CliColor? bg = null)
    {
        var g = new CliGrid(1, 1);
        if (bg is { } b)
            g.DefaultCellStyle = new CliCellStyle { CharStyle = new CliCharStyle(CliColor.White, b) };
        g.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle()) { Sizing = CliColumnSizing.Star });
        g.Set(0, 0, content);
        return g;
    }

    [Fact]
    public void HostedStarSubgrid_FillsHostColumn_WhenSiblingForcesItWider()
    {
        var outer = new CliGrid(1, 2) { SoftMaxWidth = 200 };
        outer.Set(0, 0, "ABCDEFGHIJ");          // 10 wide → forces the shared column to 10
        var sub = StarSubgrid("xy");            // natural content width 2
        outer.SetSubgrid(0, 1, sub);

        var lines = TigerConsole.RenderGridToLines(outer);

        // The subgrid itself grew to the resolved host-column width (it filled, was not just padded).
        Assert.Equal(10, sub.MeasuredWidth);
        Assert.Equal(10, lines[1].Length);
        Assert.StartsWith("xy", lines[1]);
    }

    // Minimal sink carrying a soft-max width, the way a real console sink does. Used to prove the
    // natural subgrid pass is content-driven even when an ambient soft-max ceiling is inherited.
    private sealed class SoftMaxSink : ICliRenderSink
    {
        public int? SoftMaxWidth { get; init; }
        public int? SoftMaxHeight => null;
        public int? MaxWidth => null;
        public int? MaxHeight => null;
        public void Write(CliTextSegment segment) { }
        public void NewLine() { }
        public void Flush() { }
        public void Reset() { }
    }

    [Fact]
    public void HostedStarSubgrid_NaturalWidth_IsContentDriven_NotBalloonedToSoftMax()
    {
        // The sink carries a soft-max of 80 (as a real console would). Without the fix the Star column
        // grows to that soft-max during the natural pass, dragging the whole outer grid to ~80 wide.
        var outer = new CliGrid(2, 1);
        outer.SetSubgrid(0, 0, StarSubgrid("abc"));   // content 3
        outer.Set(1, 0, "X");                          // 1

        outer.Measure(new SoftMaxSink { SoftMaxWidth = 80 });

        Assert.Equal(4, outer.MeasuredWidth);          // 3 + 1, content-driven (not ballooned to 80)
    }

    [Fact]
    public void HostedAutoSubgrid_DoesNotFill_OnlyContentWidth()
    {
        // A subgrid WITHOUT a fill column stays at content width; the host pads the rest. This pins
        // that "fill" is opt-in (Star), not automatic for every subgrid.
        var outer = new CliGrid(1, 2) { SoftMaxWidth = 200 };
        outer.Set(0, 0, "ABCDEFGHIJ");                 // 10
        var sub = new CliGrid(1, 1);
        sub.Set(0, 0, "xy");                           // plain Auto column, natural 2
        outer.SetSubgrid(0, 1, sub);

        TigerConsole.RenderGridToLines(outer);

        Assert.Equal(2, sub.MeasuredWidth);            // subgrid did not fill
    }

    // ---- Background ownership of the fill (HtmlSink) ----

    // Background-color hex for each visible character on a rendered HTML line, walking spans.
    private static List<string> PerCharBackground(string htmlLine)
    {
        var result = new List<string>();
        string current = "none";
        foreach (Match tok in Regex.Matches(htmlLine, "<span[^>]*>|</span>|<[^>]+>|[^<]+"))
        {
            var s = tok.Value;
            if (s.StartsWith("<span", StringComparison.Ordinal))
            {
                var bg = Regex.Match(s, "background-color:(#[0-9A-Fa-f]{6})");
                current = bg.Success ? bg.Groups[1].Value : "none";
            }
            else if (s == "</span>")
            {
                current = "none";
            }
            else if (!s.StartsWith('<'))
            {
                foreach (var _ in s)
                    result.Add(current);
            }
        }
        return result;
    }

    [Fact]
    public void Html_StarSubgridFill_Background_IsOwnedBySubgrid_AcrossFullWidth()
    {
        var red = HexOf(CliColor.Red);

        var outer = new CliGrid(1, 2) { SoftMaxWidth = 200 };
        outer.Set(0, 0, "ABCDEFGHIJ");                       // 10 → host column 10
        outer.SetSubgrid(0, 1, StarSubgrid("xy", bg: CliColor.Red));

        var html = TigerConsole.RenderGridToHtml(outer);
        var rowLine = InnerPreLines(html)[1];

        var bgs = PerCharBackground(rowLine);
        Assert.Equal(10, bgs.Count);                          // full host width is present
        Assert.All(bgs, b => Assert.Equal(red, b));           // every cell (incl. fill) is the subgrid's bg
    }

    private static string HexOf(CliColor color)
    {
        // Render a single styled cell and read back its background hex, so the test does not hard-code
        // palette values.
        var g = new CliGrid(1, 1);
        g.Set(0, 0, "x", new CliCellStyle { CharStyle = new CliCharStyle(CliColor.White, color) });
        var html = TigerConsole.RenderGridToHtml(g);
        return Regex.Match(html, "background-color:(#[0-9A-Fa-f]{6})").Groups[1].Value;
    }

    private static string[] InnerPreLines(string html)
    {
        const string open = "<pre class=\"tigercli\">";
        const string close = "</pre>";
        int s = html.IndexOf(open, StringComparison.Ordinal) + open.Length;
        int e = html.IndexOf(close, StringComparison.Ordinal);
        return html[s..e].Split('\n');
    }

    // ---- Horizontal-scroll viewport fill ----
    //
    // A horizontally-scrolling subgrid is an editing/scrolling viewport (e.g. a text input). Unlike a
    // content-sized list, it should FILL the available soft-max width so there is room to type/scroll.
    // The public API only places colSpan-1 subgrids; the colSpan>1, locked-anchor span that the real
    // above-frame path input uses is covered by the folder-select integration tests. These pin the two
    // publicly-reachable core behaviours: the viewport fills the soft-max, and a Star "remaining space"
    // column OUTSIDE the scroll span yields its elastic width to the viewport.

    // Measured width of column c via cell origins (origin(c+1) - origin(c)); last column uses total.
    private static int ColWidth(CliGrid g, int c)
    {
        int x0 = g.GetMeasuredCellOrigin(c, 0)!.Value.Column;
        int x1 = c + 1 < g.ColumnCount
            ? g.GetMeasuredCellOrigin(c + 1, 0)!.Value.Column
            : g.MeasuredWidth ?? x0;
        return x1 - x0;
    }

    private static CliGrid HScrollSubgrid(string content)
    {
        var g = new CliGrid(1, 1);
        g.Set(0, 0, content, new CliCellStyle { Wrapping = CliWrapping.SingleLine });
        return g;
    }

    [Fact]
    public void HorizontalScrollSubgrid_FillsToSoftMaxCeiling()
    {
        var outer = new CliGrid(2, 1) { SoftMaxWidth = 40 };
        outer.SetSubgrid(0, 0, HScrollSubgrid("abc"), CliScrollMode.Horizontal, CliScrollThumbMode.ActivePoint);
        outer.Set(1, 0, "X");                              // 1-wide sibling

        outer.Measure(new SoftMaxSink { SoftMaxWidth = 40 });

        Assert.Equal(40, outer.MeasuredWidth);             // the viewport filled the ceiling
        Assert.Equal(39, ColWidth(outer, 0));              // scroll column took the room (40 − sibling)
    }

    [Fact]
    public void HorizontalScrollViewport_OutranksStarColumn_OutsideItsSpan()
    {
        // Layout mirrors the dialog: a scroll viewport (col0), a pinned divider (col1, locked Width=1),
        // and a Star "remaining space" column (col2) that sits OUTSIDE the viewport. A wide full-width
        // row (colSpan 3) fattens the Star column during span-fit. After the fix the viewport claims
        // that elastic width and the Star column is released to its floor.
        var outer = new CliGrid(3, 2) { SoftMaxWidth = 40 };
        outer.SetColumn(1, new CliGridColumnDefinition(new CliCellStyle { Width = 1 }));
        outer.SetColumn(2, new CliGridColumnDefinition(new CliCellStyle()) { Sizing = CliColumnSizing.Star });
        outer.SetSubgrid(0, 0, HScrollSubgrid("vp"), CliScrollMode.Horizontal, CliScrollThumbMode.ActivePoint);
        outer.Set(1, 0, "|");
        outer.Set(0, 1, new string('W', 30), colSpan: 3);  // wide full-width row → would fatten the Star col

        outer.Measure(new SoftMaxSink { SoftMaxWidth = 40 });

        Assert.Equal(1, ColWidth(outer, 2));               // Star released to its floor (did not keep the width)
        Assert.True(ColWidth(outer, 0) >= 30,              // viewport claimed the freed budget
            $"expected viewport to fill, got col0={ColWidth(outer, 0)}");
        Assert.Equal(40, outer.MeasuredWidth);
    }

    [Fact]
    public void HorizontalScrollViewport_DoesNotWiden_AColumnInsideAnotherFixedSpan()
    {
        // A Star column is only released when it sits OUTSIDE every scroll span; a plain Auto sibling
        // that holds real content is never collapsed. Here col1 is Auto with 6-wide content and must
        // keep it while the viewport fills the rest.
        var outer = new CliGrid(2, 1) { SoftMaxWidth = 40 };
        outer.SetSubgrid(0, 0, HScrollSubgrid("vp"), CliScrollMode.Horizontal, CliScrollThumbMode.ActivePoint);
        outer.Set(1, 0, "SIXLEN");                         // Auto sibling, 6 wide

        outer.Measure(new SoftMaxSink { SoftMaxWidth = 40 });

        Assert.Equal(6, ColWidth(outer, 1));               // Auto sibling kept its content width
        Assert.Equal(34, ColWidth(outer, 0));              // viewport filled the remainder
    }
}
