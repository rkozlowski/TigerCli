using System.IO;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the <see cref="HtmlSink"/> contract: deterministic <c>&lt;pre class="tigercli"&gt;</c> +
/// styled <c>&lt;span&gt;</c>/<c>&lt;a&gt;</c> output, HTML/attribute escaping, decoration classes,
/// inline colour hex, visible/copyable links, safe href handling, and no ANSI. Renders are built from
/// explicit segments (for exact assertions) and through the public markup/component helpers.
/// </summary>
public sealed class HtmlSinkTests : TestBase
{

    private static readonly ITheme Blue = new TigerBlueTheme();

    private static CliTextSegment Seg(
        string text,
        CliColor? fg = null,
        CliColor? bg = null,
        CliTextDecoration deco = CliTextDecoration.None,
        string? target = null)
        => new(text, new CliCharStyle(fg, bg, deco) { HyperlinkTarget = target });

    private static string Render(HtmlSinkOptions? options, params CliTextSegment[] segments)
    {
        var writer = new StringWriter();
        var sink = new HtmlSink(writer, options);
        foreach (var s in segments)
            sink.Write(s);
        sink.Flush();
        return writer.ToString();
    }

    private static string Render(params CliTextSegment[] segments) => Render(null, segments);

    // ---- 1. wrapper ----

    [Fact]
    public void PlainText_WrappedInPre_ByDefault()
    {
        Assert.Equal("<pre class=\"tigercli\">hello</pre>", Render(Seg("hello")));
    }

    [Fact]
    public void EmptyRender_StillProducesWellFormedWrapper()
    {
        var writer = new StringWriter();
        new HtmlSink(writer).Flush();
        Assert.Equal("<pre class=\"tigercli\"></pre>", writer.ToString());
    }

    // ---- 2. WrapInPre = false ----

    [Fact]
    public void WrapInPreFalse_EmitsOnlyInnerHtml()
    {
        var html = Render(new HtmlSinkOptions { WrapInPre = false }, Seg("hello"));
        Assert.Equal("hello", html);
    }

    // ---- 3. text escaping ----

    [Fact]
    public void TextContent_IsHtmlEscaped()
    {
        var html = Render(new HtmlSinkOptions { WrapInPre = false }, Seg("a <b> & c"));
        Assert.Equal("a &lt;b&gt; &amp; c", html);
    }

    [Fact]
    public void Markup_TextContent_IsHtmlEscaped()
    {
        var html = TigerConsole.MarkupToHtml("1 < 2 && 3 > 0", new HtmlSinkOptions { WrapInPre = false });
        Assert.Contains("1 &lt; 2 &amp;&amp; 3 &gt; 0", html);
        Assert.DoesNotContain("<b>", html);
    }

    // ---- 4. attribute escaping in href ----

    [Fact]
    public void AnchorHref_IsAttributeEscaped()
    {
        var html = Render(
            new HtmlSinkOptions { WrapInPre = false, HyperlinkMode = HtmlHyperlinkMode.Anchor },
            Seg("link", target: "https://e.com/?a=1&b=2\"x"));

        Assert.Contains("href=\"https://e.com/?a=1&amp;b=2&quot;x\"", html);
        // The raw, unescaped quote/ampersand never reach the output attribute.
        Assert.DoesNotContain("b=2\"x", html);
    }

    // ---- 5/6/7. decorations as deterministic classes ----

    [Fact]
    public void Bold_RendersDeterministically()
        => Assert.Equal(
            "<pre class=\"tigercli\"><span class=\"tc-bold\">x</span></pre>",
            Render(Seg("x", deco: CliTextDecoration.Bold)));

    [Fact]
    public void Italic_RendersDeterministically()
        => Assert.Equal(
            "<pre class=\"tigercli\"><span class=\"tc-italic\">x</span></pre>",
            Render(Seg("x", deco: CliTextDecoration.Italic)));

    [Fact]
    public void Underline_RendersDeterministically()
        => Assert.Equal(
            "<pre class=\"tigercli\"><span class=\"tc-underline\">x</span></pre>",
            Render(Seg("x", deco: CliTextDecoration.Underline)));

    [Fact]
    public void CombinedDecorations_RenderInFixedOrder()
        => Assert.Equal(
            "<pre class=\"tigercli\"><span class=\"tc-bold tc-italic tc-underline\">x</span></pre>",
            Render(Seg("x", deco: CliTextDecoration.Bold | CliTextDecoration.Italic | CliTextDecoration.Underline)));

    // ---- 8/9. colours as deterministic inline hex ----

    [Fact]
    public void Foreground_RendersDeterministicHex()
        => Assert.Equal(
            "<pre class=\"tigercli\"><span style=\"color:#FF0000\">x</span></pre>",
            Render(Seg("x", fg: CliColor.Red)));

    [Fact]
    public void Background_RendersDeterministicHex()
        => Assert.Equal(
            "<pre class=\"tigercli\"><span style=\"background-color:#000080\">x</span></pre>",
            Render(Seg("x", bg: CliColor.DarkBlue)));

    [Fact]
    public void ForegroundAndBackground_RenderInFixedOrder()
        => Assert.Equal(
            "<pre class=\"tigercli\"><span style=\"color:#FF0000; background-color:#000080\">x</span></pre>",
            Render(Seg("x", fg: CliColor.Red, bg: CliColor.DarkBlue)));

    // ---- 10. adjacent segments ----

    [Fact]
    public void AdjacentSegments_RenderIndependently()
    {
        var html = Render(
            new HtmlSinkOptions { WrapInPre = false },
            Seg("A", fg: CliColor.Red),
            Seg("B", fg: CliColor.Red),
            Seg("C", deco: CliTextDecoration.Bold),
            Seg("D"));

        Assert.Equal(
            "<span style=\"color:#FF0000\">A</span>"
            + "<span style=\"color:#FF0000\">B</span>"
            + "<span class=\"tc-bold\">C</span>"
            + "D",
            html);
    }

    // ---- 11. whitespace / newlines preserved ----

    [Fact]
    public void WhitespaceAndNewlines_ArePreserved()
    {
        var writer = new StringWriter();
        var sink = new HtmlSink(writer, new HtmlSinkOptions { WrapInPre = false });
        sink.Write(Seg("a  b"));
        sink.NewLine();
        sink.Write(Seg("c"));
        sink.Flush();

        Assert.Equal("a  b\nc", writer.ToString());
    }

    // ---- 12/13. link modes ----

    [Fact]
    public void LinkMarkup_TextMode_RendersVisibleText_NoAnchor()
    {
        var html = TigerConsole.MarkupToHtml(
            "[Link]https://example.com[/]",
            new HtmlSinkOptions { HyperlinkMode = HtmlHyperlinkMode.Text });

        Assert.Contains("https://example.com", html);
        Assert.Contains("tc-link", html);
        Assert.DoesNotContain("<a", html);
        Assert.DoesNotContain("href", html);
    }

    [Fact]
    public void LinkMarkup_AnchorMode_RendersAnchorWithVisibleText()
    {
        var html = TigerConsole.MarkupToHtml(
            "[Link]https://example.com[/]",
            new HtmlSinkOptions { HyperlinkMode = HtmlHyperlinkMode.Anchor });

        Assert.Contains("<a", html);
        Assert.Contains("href=\"https://example.com\"", html);
        Assert.Contains(">https://example.com</a>", html); // visible text unchanged
        Assert.Contains("tc-link", html);
    }

    [Fact]
    public void DirectLinkSegment_TextMode_IsSpanWithLinkClass()
        => Assert.Equal(
            "<pre class=\"tigercli\"><span class=\"tc-link\">https://example.com</span></pre>",
            Render(Seg("https://example.com", target: "https://example.com")));

    [Fact]
    public void DirectLinkSegment_AnchorMode_IsAnchorWithLinkClass()
        => Assert.Equal(
            "<pre class=\"tigercli\"><a class=\"tc-link\" href=\"https://example.com\">https://example.com</a></pre>",
            Render(
                new HtmlSinkOptions { HyperlinkMode = HtmlHyperlinkMode.Anchor },
                Seg("https://example.com", target: "https://example.com")));

    // ---- 14. empty/missing target ----

    [Fact]
    public void AnchorMode_NoTarget_EmitsNoAnchor()
    {
        var html = Render(
            new HtmlSinkOptions { WrapInPre = false, HyperlinkMode = HtmlHyperlinkMode.Anchor },
            Seg("plain"),
            Seg("empty", target: ""),
            Seg("blank", target: "   "));

        Assert.DoesNotContain("<a", html);
        Assert.Contains("plain", html);
        Assert.Contains("empty", html);
        Assert.Contains("blank", html);
    }

    // ---- 15. unsafe/control characters in href ----

    [Fact]
    public void AnchorMode_DangerousScheme_FallsBackToSpan()
    {
        var html = Render(
            new HtmlSinkOptions { WrapInPre = false, HyperlinkMode = HtmlHyperlinkMode.Anchor },
            Seg("click me", target: "javascript:alert(1)"));

        Assert.DoesNotContain("<a", html);
        Assert.DoesNotContain("javascript:", html);
        Assert.Equal("<span class=\"tc-link\">click me</span>", html); // text visible, link role kept
    }

    [Fact]
    public void AnchorMode_ControlCharsInTarget_AreStripped()
    {
        // Newline + ESC inside the target must be stripped (not just escaped) so nothing can break out
        // of the href attribute; the visible text is unaffected.
        var html = Render(
            new HtmlSinkOptions { WrapInPre = false, HyperlinkMode = HtmlHyperlinkMode.Anchor },
            Seg("link", target: "https://e.com/a\nbc"));

        Assert.Equal("<a class=\"tc-link\" href=\"https://e.com/abc\">link</a>", html);
        Assert.DoesNotContain("\n", html);
        Assert.False(html.Contains((char)0x1B));
    }

    // ---- 16/17/18. structured output ----

    [Fact]
    public void CliDetails_AddLink_RendersToHtml()
    {
        var details = new CliDetails()
            .ApplyPreset(CliTableStylePreset.Details, Blue)
            .AddKey("Id:", "d-1")
            .Add("Name:", "Front door")
            .AddLink("Url:", "https://example.com/devices/d-1");

        var html = TigerConsole.RenderToHtml(details, new HtmlSinkOptions { HyperlinkMode = HtmlHyperlinkMode.Anchor });

        Assert.StartsWith("<pre class=\"tigercli\">", html);
        Assert.Contains("Front door", html);
        Assert.Contains("https://example.com/devices/d-1", html);
        Assert.False(html.Contains((char)0x1B)); // no ANSI/ESC
    }

    [Fact]
    public void CliList_AddLinkColumn_RendersToHtml()
    {
        var devices = new[]
        {
            new Device("d-1", "Front door", "https://example.com/devices/d-1"),
            new Device("d-2", "Garage", "https://example.com/devices/d-2"),
        };

        var table = new CliList<Device>()
            .ApplyPreset(CliTableStylePreset.Lucca, Blue)
            .AddColumn("Id", d => d.Id)
            .AddColumn("Name", d => d.Name)
            .AddLinkColumn("Url", d => d.Url)
            .Render(devices);

        var html = TigerConsole.RenderToHtml(table);

        Assert.StartsWith("<pre class=\"tigercli\">", html);
        Assert.Contains("Front door", html);
        Assert.Contains("https://example.com/devices/d-1", html);
        Assert.False(html.Contains((char)0x1B)); // no ANSI/ESC
    }

    [Fact]
    public void StructuredOutput_ContainsNoAnsiSequences()
    {
        var details = new CliDetails()
            .ApplyPreset(CliTableStylePreset.Details, Blue)
            .AddKey("Id:", "d-1")
            .AddLink("Url:", "https://example.com");

        var textMode = TigerConsole.RenderToHtml(details);
        var anchorMode = TigerConsole.RenderToHtml(details, new HtmlSinkOptions { HyperlinkMode = HtmlHyperlinkMode.Anchor });

        Assert.False(textMode.Contains((char)0x1B));
        Assert.False(anchorMode.Contains((char)0x1B));
        Assert.DoesNotContain("[0m", textMode);
    }

    // ---- 19. SoftMaxWidth (emulated terminal width) ----

    // 1x1 grid with a word-wrapping cell and no fixed column width, so the measured layout is
    // bounded only by the sink's SoftMaxWidth (or the grid's own).
    private static CliGrid WordWrapGrid(string text)
    {
        var grid = new CliGrid(1, 1);
        grid.Set(0, 0, text, new CliCellStyle
        {
            Wrapping = new CliWrapping(CliWrapMode.WordWrap, false, "…")
        });
        return grid;
    }

    // Grid renders always carry the grid's default colours as spans; these tests assert layout
    // (wrapping), so compare the visible text with the markup stripped.
    private static string VisibleText(string html)
        => System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty);

    [Fact]
    public void SoftMaxWidth_DefaultsToNull_LayoutIsUnbounded()
    {
        Assert.Null(new HtmlSink(new StringWriter()).SoftMaxWidth);

        var html = TigerConsole.RenderGridToHtml(
            WordWrapGrid("The quick brown fox jumps over the lazy dog"),
            new HtmlSinkOptions { WrapInPre = false });

        Assert.Equal("The quick brown fox jumps over the lazy dog\n", VisibleText(html));
    }

    [Fact]
    public void SoftMaxWidth_FlowsFromOptionsToSink()
    {
        var sink = new HtmlSink(new StringWriter(), new HtmlSinkOptions { SoftMaxWidth = 40 });
        Assert.Equal(40, sink.SoftMaxWidth);
    }

    [Fact]
    public void SoftMaxWidth_BoundsMeasureLikeATerminalWidth()
    {
        var html = TigerConsole.RenderGridToHtml(
            WordWrapGrid("The quick brown fox jumps over the lazy dog"),
            new HtmlSinkOptions { WrapInPre = false, SoftMaxWidth = 10 });

        Assert.Equal(
            "The quick \n"
            + "brown fox \n"
            + "jumps over\n"
            + "the lazy  \n"
            + "dog       \n",
            VisibleText(html));
    }

    [Fact]
    public void SoftMaxWidth_GridOwnSoftMaxWidth_TakesPrecedence()
    {
        var grid = WordWrapGrid("The quick brown fox jumps over the lazy dog");
        grid.SoftMaxWidth = 20;

        var html = TigerConsole.RenderGridToHtml(
            grid,
            new HtmlSinkOptions { WrapInPre = false, SoftMaxWidth = 10 });

        var lines = VisibleText(html).TrimEnd('\n').Split('\n');
        Assert.All(lines, line => Assert.True(line.Length <= 20, $"Overflow: '{line}' (len {line.Length}) > 20"));
        Assert.Contains(lines, line => line.Length > 10); // wrapped at 20, not at the sink's 10
    }

    [Fact]
    public void SoftMaxWidth_AlreadyMeasuredGrid_KeepsItsLayout()
    {
        var grid = WordWrapGrid("The quick brown fox jumps over the lazy dog");
        TigerConsole.RenderGridToLines(grid); // measures unbounded (StringLinesSink has no width)

        // The measured layout is rendered as-is; SoftMaxWidth only affects the measure pass. This is
        // what lets a grid measured at a TestShell viewport width render to HTML with layout intact.
        var html = TigerConsole.RenderGridToHtml(
            grid,
            new HtmlSinkOptions { WrapInPre = false, SoftMaxWidth = 10 });

        Assert.Equal("The quick brown fox jumps over the lazy dog\n", VisibleText(html));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SoftMaxWidth_NonPositive_Throws(int width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HtmlSinkOptions { SoftMaxWidth = width });
    }

    private sealed record Device(string Id, string Name, string Url);
}
