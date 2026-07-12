using System.Collections.Generic;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Guards segment merge/coalesce correctness: two <see cref="CliTextSegment"/>s may only be merged
/// when their full render-relevant identity matches (foreground, background, decorations, hyperlink
/// target). Covers the central <see cref="CliCharStyle.HasSameRenderingAs"/> predicate and the grid
/// coalescing site (<c>CliGrid.CompactLine</c>) end-to-end via rendering.
/// </summary>
public sealed class SegmentMergeTests
{
    // ---- Central render-equality predicate ----

    [Fact]
    public void HasSameRenderingAs_IdenticalStyles_True()
    {
        var a = new CliCharStyle(CliColor.Cyan, CliColor.Black, CliTextDecoration.Bold) { HyperlinkTarget = "u" };
        var b = new CliCharStyle(CliColor.Cyan, CliColor.Black, CliTextDecoration.Bold) { HyperlinkTarget = "u" };
        Assert.True(a.HasSameRenderingAs(b));
    }

    [Fact]
    public void HasSameRenderingAs_DiffersByBold_False()
    {
        var a = new CliCharStyle(CliColor.Cyan, CliColor.Black, CliTextDecoration.None);
        var b = new CliCharStyle(CliColor.Cyan, CliColor.Black, CliTextDecoration.Bold);
        Assert.False(a.HasSameRenderingAs(b));
    }

    [Fact]
    public void HasSameRenderingAs_DiffersByItalic_False()
    {
        var a = new CliCharStyle(CliColor.Cyan, CliColor.Black, CliTextDecoration.None);
        var b = new CliCharStyle(CliColor.Cyan, CliColor.Black, CliTextDecoration.Italic);
        Assert.False(a.HasSameRenderingAs(b));
    }

    [Fact]
    public void HasSameRenderingAs_DiffersByUnderline_False()
    {
        var a = new CliCharStyle(CliColor.Cyan, CliColor.Black, CliTextDecoration.None);
        var b = new CliCharStyle(CliColor.Cyan, CliColor.Black, CliTextDecoration.Underline);
        Assert.False(a.HasSameRenderingAs(b));
    }

    [Fact]
    public void HasSameRenderingAs_DiffersByHyperlinkTarget_False()
    {
        var a = new CliCharStyle(CliColor.Cyan) { HyperlinkTarget = "https://a" };
        var b = new CliCharStyle(CliColor.Cyan) { HyperlinkTarget = "https://b" };
        Assert.False(a.HasSameRenderingAs(b));

        var c = new CliCharStyle(CliColor.Cyan) { HyperlinkTarget = null };
        Assert.False(a.HasSameRenderingAs(c));
    }

    [Fact]
    public void HasSameRenderingAs_DiffersByForegroundOrBackground_False()
    {
        var baseStyle = new CliCharStyle(CliColor.Cyan, CliColor.Black);
        Assert.False(baseStyle.HasSameRenderingAs(new CliCharStyle(CliColor.Red, CliColor.Black)));
        Assert.False(baseStyle.HasSameRenderingAs(new CliCharStyle(CliColor.Cyan, CliColor.Blue)));
    }

    // ---- Grid coalescing (CompactLine) end-to-end ----
    //
    // Render a preformatted single-cell grid sized exactly to the visible text (no padding/fill), then
    // inspect the segments that reach the sink. Adjacent runs that differ by a decoration or hyperlink
    // target must remain separate; identical runs must coalesce.

    private static List<CliTextSegment> RenderSegments(string markup, int width)
    {
        var grid = new CliGrid(1, 1);
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = width, MinWidth = width, MaxWidth = width }));
        grid.SetRow(0, new CliGridRowDefinition(new CliCellStyle()));
        grid.Set(0, 0, markup, new CliCellStyle { FormattingMode = CliFormattingMode.Preformatted });

        var sink = new CapturingSink();
        TigerConsole.RenderGrid(grid, sink);
        return sink.FirstNonEmptyLine();
    }

    private static CliTextSegment Find(List<CliTextSegment> segs, string text)
        => Assert.Single(segs, s => s.Text == text);

    [Fact]
    public void CompactLine_DoesNotMerge_SegmentsDifferingOnlyByBold()
    {
        var segs = RenderSegments("[Bold]A[/]B", width: 2);

        Assert.True(Find(segs, "A").Style.Decorations.HasFlag(CliTextDecoration.Bold));
        Assert.False(Find(segs, "B").Style.Decorations.HasFlag(CliTextDecoration.Bold));
    }

    [Fact]
    public void CompactLine_DoesNotMerge_SegmentsDifferingOnlyByItalic()
    {
        var segs = RenderSegments("[Italic]A[/]B", width: 2);

        Assert.True(Find(segs, "A").Style.Decorations.HasFlag(CliTextDecoration.Italic));
        Assert.False(Find(segs, "B").Style.Decorations.HasFlag(CliTextDecoration.Italic));
    }

    [Fact]
    public void CompactLine_DoesNotMerge_SegmentsDifferingOnlyByUnderline()
    {
        var segs = RenderSegments("[Underline]A[/]B", width: 2);

        Assert.True(Find(segs, "A").Style.Decorations.HasFlag(CliTextDecoration.Underline));
        Assert.False(Find(segs, "B").Style.Decorations.HasFlag(CliTextDecoration.Underline));
    }

    [Fact]
    public void CompactLine_DoesNotMerge_SegmentsDifferingOnlyByHyperlinkTarget()
    {
        // "https://example.com" (link target) immediately followed by " plain" (no target).
        var segs = RenderSegments("[Link]https://example.com[/] plain", width: 25);

        var link = Find(segs, "https://example.com");
        Assert.Equal("https://example.com", link.Style.HyperlinkTarget);

        // The trailing text must not have inherited the link target via a merge.
        Assert.Contains(segs, s => s.Text.Contains("plain") && s.Style.HyperlinkTarget is null);
    }

    [Fact]
    public void CompactLine_Merges_AdjacentSegmentsWithIdenticalStyle()
    {
        // Two bold runs with no other difference must coalesce into a single "AB" segment.
        var segs = RenderSegments("[Bold]A[/][Bold]B[/]", width: 2);

        var merged = Assert.Single(segs);
        Assert.Equal("AB", merged.Text);
        Assert.True(merged.Style.Decorations.HasFlag(CliTextDecoration.Bold));
    }

    [Fact]
    public void Render_DecorationsDoNotBleedAcrossSegments()
    {
        // After the styled span closes, the following text carries no decoration (no bleed).
        foreach (var (markup, deco) in new[]
        {
            ("[Bold]A[/]B", CliTextDecoration.Bold),
            ("[Italic]A[/]B", CliTextDecoration.Italic),
            ("[Underline]A[/]B", CliTextDecoration.Underline),
        })
        {
            var segs = RenderSegments(markup, width: 2);
            Assert.True(Find(segs, "A").Style.Decorations.HasFlag(deco));
            Assert.Equal(CliTextDecoration.None, Find(segs, "B").Style.Decorations);
        }
    }

    // Captures the styled segments written to a sink, grouped by line.
    private sealed class CapturingSink : ICliRenderSink
    {
        private readonly List<List<CliTextSegment>> _lines = new();
        private List<CliTextSegment> _current = new();

        public int? SoftMaxWidth => null;
        public int? SoftMaxHeight => null;
        public int? MaxWidth => null;
        public int? MaxHeight => null;

        public void Write(CliTextSegment segment) => _current.Add(segment);
        public void NewLine() { _lines.Add(_current); _current = new(); }
        public void Flush() { if (_current.Count > 0) { _lines.Add(_current); _current = new(); } }
        public void Reset() { _lines.Clear(); _current = new(); }

        public List<CliTextSegment> FirstNonEmptyLine()
        {
            foreach (var line in _lines)
            {
                var nonEmpty = line.FindAll(s => s.Text.Length > 0);
                if (nonEmpty.Count > 0)
                    return nonEmpty;
            }
            return new List<CliTextSegment>();
        }
    }
}
