using System.Collections.Generic;
using System.Linq;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers decoration isolation for link-styled values: the link text itself is underlined, but the
/// underline must never leak onto structural output — frame/separator glyphs, header captions, or
/// the whitespace padding/alignment fill around the value. The leak had three roots, each covered
/// here: the element data style was applied to the whole grid axis (crossing frames and captions),
/// decorations merge additively in the style cascade (so frame cells could never shed them), and
/// padding/fill segments reused the content char style verbatim.
/// Assertions are at the resolved-segment level, so every sink benefits.
/// </summary>
public sealed class LinkDecorationIsolationTests
{
    private static readonly ITheme Blue = new TigerBlueTheme();

    private const string FrameGlyphs = "─│┌┐└┘├┤┬┴┼═║╔╗╚╝╠╣╦╩╬╒╓╕╖╘╙╛╜╞╟╡╢╤╥╧╨╪╫";

    private static List<List<CliTextSegment>> RenderSegments(CliGrid grid, int width = 120)
    {
        var sink = new TextSegmentLinesSink { SoftMaxWidth = width };
        return TigerConsole.RenderGridToSegmentedLines(grid, sink);
    }

    private static IEnumerable<CliTextSegment> AllSegments(List<List<CliTextSegment>> lines)
        => lines.SelectMany(static line => line);

    private static bool ContainsFrameGlyph(string text)
        => text.Any(static ch => FrameGlyphs.Contains(ch));

    private static bool IsUnderlined(CliTextSegment segment)
        => (segment.Style.Decorations & CliTextDecoration.Underline) != 0;

    private static void AssertNoUnderlinedFrameOrWhitespace(List<List<CliTextSegment>> lines)
    {
        foreach (var segment in AllSegments(lines))
        {
            if (!IsUnderlined(segment) || segment.Text.Length == 0)
                continue;

            Assert.False(
                ContainsFrameGlyph(segment.Text),
                $"Frame glyphs must not be underlined, but segment '{segment.Text}' is.");
            Assert.False(
                string.IsNullOrWhiteSpace(segment.Text),
                "Padding/fill whitespace must not be underlined.");
        }
    }

    // ---- CliList: link column in a framed vertical table ----

    private sealed record Site(string Name, string Url);

    private static List<List<CliTextSegment>> RenderLinkList()
    {
        var list = new CliList<Site>()
            .ApplyPreset(CliTableStylePreset.Lucca, Blue)
            .AddColumn("Name", s => s.Name)
            .AddLinkColumn("Url", s => s.Url);

        return RenderSegments(list.Render(
        [
            new Site("A", "https://a.example.com"),
            new Site("B", "https://b.example.com"),
        ]).ToGrid());
    }

    [Fact]
    public void CliList_LinkColumn_LinkValuesUseThemeDecoration()
    {
        var lines = RenderLinkList();

        var linkSegments = AllSegments(lines)
            .Where(static s => s.Text.Contains("https://"))
            .ToList();

        Assert.NotEmpty(linkSegments);
        Assert.All(linkSegments, s => Assert.True(IsUnderlined(s), $"TigerBlue link value '{s.Text}' must be underlined."));
    }

    [Fact]
    public void CliList_LinkColumn_FrameAndPaddingAreNotUnderlined()
    {
        AssertNoUnderlinedFrameOrWhitespace(RenderLinkList());
    }

    [Fact]
    public void CliList_LinkColumn_HeaderCaptionIsNotUnderlined()
    {
        // The link column's grid axis crosses the header caption cell; the value ink must not
        // reach it — a caption is never a link.
        var underlinedCaption = AllSegments(RenderLinkList())
            .Where(IsUnderlined)
            .FirstOrDefault(static s => s.Text.Contains("Url") && !s.Text.Contains("https://"));

        Assert.Null(underlinedCaption?.Text);
    }

    // ---- CliDetails: link row in a framed horizontal table ----

    private static List<List<CliTextSegment>> RenderLinkDetails()
        => RenderSegments(new CliDetails()
            .ApplyPreset(CliTableStylePreset.Details, Blue)
            .Add("Name:", "Frame diagnostics")
            .AddLink("Url:", "https://example.com/devices/cam-042")
            .ToGrid());

    [Fact]
    public void CliDetails_LinkRow_LinkValueUsesThemeDecoration()
    {
        var linkSegments = AllSegments(RenderLinkDetails())
            .Where(static s => s.Text.Contains("https://"))
            .ToList();

        Assert.NotEmpty(linkSegments);
        Assert.All(linkSegments, s => Assert.True(IsUnderlined(s), $"TigerBlue link value '{s.Text}' must be underlined."));
    }

    [Fact]
    public void CliDetails_LinkRow_SeparatorsLabelAndPaddingAreNotUnderlined()
    {
        // Horizontal orientation: the element axis is the whole grid row, so the '│' separators
        // and the 'Url:' label sit on the same axis as the link value.
        var lines = RenderLinkDetails();

        AssertNoUnderlinedFrameOrWhitespace(lines);

        var underlinedLabel = AllSegments(lines)
            .Where(IsUnderlined)
            .FirstOrDefault(static s => s.Text.Contains("Url:"));
        Assert.Null(underlinedLabel?.Text);
    }

    // ---- Engine-level: frame cells never inherit axis decorations ----

    [Fact]
    public void Grid_FrameCells_DoNotInheritAxisDecorations()
    {
        // A frame area crosses a column whose axis style carries Underline. The content cell in
        // that column keeps the decoration; the frame glyphs crossing the column must not.
        var grid = new CliGrid(3, 3);
        var area = grid.AddFrameArea(CliFrameJoinStyle.SimplifiedCompatible, 0, 0, 2, 2);
        area.AddOuterFrame(new CliFrameSegment(CliFrameSegmentStyle.SingleFrame));
        grid.SetColumn(1, new CliGridColumnDefinition(
            new CliCellStyle(new CliCharStyle(null, null, CliTextDecoration.Underline))));
        grid.Set(1, 1, "x");

        var lines = RenderSegments(grid, width: 20);

        var contentSegment = AllSegments(lines).Single(static s => s.Text.Contains('x'));
        Assert.True(IsUnderlined(contentSegment), "The content cell keeps the axis decoration.");

        AssertNoUnderlinedFrameOrWhitespace(lines);
        Assert.DoesNotContain(
            AllSegments(lines),
            static s => ContainsFrameGlyph(s.Text)
                && (s.Style.Decorations & CliTextDecoration.Underline) != 0);
    }

    [Fact]
    public void Grid_PaddingAndAlignmentFill_DoNotInheritCellDecorations()
    {
        // An underlined cell with Padding=Both in a wide column: the value stays underlined, the
        // padding spaces and the alignment fill up to the column width do not.
        var grid = new CliGrid(1, 1);
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = 12 }));
        grid.Set(0, 0, "link", new CliCellStyle(new CliCharStyle(null, null, CliTextDecoration.Underline))
        {
            Padding = CliCellPadding.Both,
        });

        var lines = RenderSegments(grid, width: 20);
        var segments = AllSegments(lines).Where(static s => s.Text.Length > 0).ToList();

        var value = segments.Single(static s => s.Text.Contains("link"));
        Assert.Equal("link", value.Text); // padding must not merge into the underlined value run
        Assert.True(IsUnderlined(value));

        foreach (var segment in segments.Where(static s => string.IsNullOrWhiteSpace(s.Text)))
            Assert.False(IsUnderlined(segment), $"Whitespace segment '{segment.Text}' must not be underlined.");
    }

    // ---- HtmlSink confirmation: no underline class on frame/separator characters ----

    [Fact]
    public void HtmlSink_LinkList_AppliesNoUnderlineToFrameCharacters()
    {
        var list = new CliList<Site>()
            .ApplyPreset(CliTableStylePreset.Lucca, Blue)
            .AddLinkColumn("Url", s => s.Url);

        var html = TigerConsole.RenderGridToHtml(
            list.Render([new Site("A", "https://a.example.com")]).ToGrid(),
            new HtmlSinkOptions { SoftMaxWidth = 120 });

        foreach (var span in html.Split("<span "))
        {
            if (!span.StartsWith("class=\"", System.StringComparison.Ordinal) || !span.Contains("tc-underline"))
                continue;

            int start = span.IndexOf('>') + 1;
            int end = span.IndexOf("</span>", System.StringComparison.Ordinal);
            var visible = span[start..end];
            Assert.False(ContainsFrameGlyph(visible), $"Underlined HTML span must not contain frame glyphs: '{visible}'");
            Assert.False(string.IsNullOrWhiteSpace(visible), "Underlined HTML span must not be pure whitespace padding.");
        }
    }
}
