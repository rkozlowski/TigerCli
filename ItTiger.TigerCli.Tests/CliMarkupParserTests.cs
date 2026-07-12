using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Tests;

public sealed class CliMarkupParserTests
{
    /// <summary>
    /// Minimal hand-rolled resolver so parser behaviour can be tested without a theme. Maps a few
    /// foreground-only and foreground/background tokens; everything else is unknown.
    /// </summary>
    private sealed class FakeResolver : IMarkupStyleResolver
    {
        public bool TryResolve(
            string name,
            out CliColor? foreground,
            out CliColor? background,
            out CliTextDecoration decorations)
        {
            foreground = null;
            background = null;
            decorations = CliTextDecoration.None;
            switch (name.Trim().ToLowerInvariant())
            {
                case "accent":      // foreground-only
                    foreground = CliColor.Cyan;
                    return true;
                case "alert":       // foreground + background
                    foreground = CliColor.White;
                    background = CliColor.DarkRed;
                    return true;
                case "red":         // collides with a raw colour name on purpose
                    foreground = CliColor.Green;
                    return true;
                case "title":       // semantic style that carries its own decoration
                    foreground = CliColor.Cyan;
                    decorations = CliTextDecoration.Underline;
                    return true;
                case "link":        // semantic link style: accent foreground + underline
                    foreground = CliColor.Cyan;
                    decorations = CliTextDecoration.Underline;
                    return true;
                default:
                    return false;
            }
        }

        public bool IsHyperlinkToken(string name)
            => name.Trim().Equals("link", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_DarkGray_IsAccepted()
    {
        var segments = CliMarkupParser.Parse("[DarkGray]muted[/]");

        var segment = Assert.Single(segments);
        Assert.Equal("muted", segment.Text);
        Assert.Equal(CliColor.DarkGray, segment.Style.Foreground);
    }

    [Fact]
    public void Parse_GreyUK_IsNotAcceptedByDefault()
    {
        Assert.Throws<FormatException>(() => CliMarkupParser.Parse("[grey]muted[/]"));
    }

    [Fact]
    public void Parse_ForegroundOnlyToken_PreservesParentBackground()
    {
        var baseStyle = new CliCharStyle(CliColor.Gray, CliColor.Black);

        var segment = Assert.Single(CliMarkupParser.Parse("[Accent]name[/]", baseStyle, new FakeResolver()));

        Assert.Equal("name", segment.Text);
        Assert.Equal(CliColor.Cyan, segment.Style.Foreground);
        Assert.Equal(CliColor.Black, segment.Style.Background); // inherited, not cleared
    }

    [Fact]
    public void Parse_ForegroundBackgroundToken_SetsBoth()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[Alert]boom[/]", baseStyle: null, new FakeResolver()));

        Assert.Equal(CliColor.White, segment.Style.Foreground);
        Assert.Equal(CliColor.DarkRed, segment.Style.Background);
    }

    [Fact]
    public void Parse_NestedForegroundOnlyInsideAlert_KeepsAlertBackgroundAndRestores()
    {
        var segments = CliMarkupParser.Parse(
            "[Alert]Error: [Accent]Connection[/] failed.[/]", baseStyle: null, new FakeResolver());

        // "Error: " under Alert
        Assert.Equal("Error: ", segments[0].Text);
        Assert.Equal(CliColor.White, segments[0].Style.Foreground);
        Assert.Equal(CliColor.DarkRed, segments[0].Style.Background);

        // "Connection" under Accent: foreground changes, Alert background preserved
        Assert.Equal("Connection", segments[1].Text);
        Assert.Equal(CliColor.Cyan, segments[1].Style.Foreground);
        Assert.Equal(CliColor.DarkRed, segments[1].Style.Background);

        // " failed." after closing Accent: Alert restored
        Assert.Equal(" failed.", segments[2].Text);
        Assert.Equal(CliColor.White, segments[2].Style.Foreground);
        Assert.Equal(CliColor.DarkRed, segments[2].Style.Background);
    }

    [Fact]
    public void Parse_RawColour_StillWorks_WithResolver()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[Blue]x[/]", baseStyle: null, new FakeResolver()));
        Assert.Equal(CliColor.Blue, segment.Style.Foreground);
    }

    [Fact]
    public void Parse_RawColourComposites_StillWork_WithResolver()
    {
        var onlyBg = Assert.Single(CliMarkupParser.Parse("[on Blue]x[/]", baseStyle: null, new FakeResolver()));
        Assert.Equal(CliColor.Blue, onlyBg.Style.Background);

        var both = Assert.Single(CliMarkupParser.Parse("[Red on Blue]x[/]", baseStyle: null, styles: null));
        Assert.Equal(CliColor.Red, both.Style.Foreground);
        Assert.Equal(CliColor.Blue, both.Style.Background);
    }

    [Fact]
    public void Parse_SemanticToken_WinsOverRawColourName()
    {
        // The fake maps "Red" to Green; semantic resolution runs before raw colour parsing.
        var segment = Assert.Single(CliMarkupParser.Parse("[Red]x[/]", baseStyle: null, new FakeResolver()));
        Assert.Equal(CliColor.Green, segment.Style.Foreground);
    }

    [Fact]
    public void Parse_SemanticName_WithoutResolver_Throws()
    {
        Assert.Throws<FormatException>(() => CliMarkupParser.Parse("[Accent]x[/]"));
    }

    [Fact]
    public void Parse_UnknownName_WithResolver_StillThrows()
    {
        Assert.Throws<FormatException>(
            () => CliMarkupParser.Parse("[Bogus]x[/]", baseStyle: null, new FakeResolver()));
    }

    [Fact]
    public void Parse_EscapedBrackets_StillWork_WithResolver()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[[Accent]]", baseStyle: null, new FakeResolver()));
        Assert.Equal("[Accent]", segment.Text);
    }

    // ---- Raw decoration tokens ----

    [Fact]
    public void Parse_Bold_AppliesBold()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[Bold]text[/]"));
        Assert.Equal(CliTextDecoration.Bold, segment.Style.Decorations);
        Assert.Null(segment.Style.Foreground);
        Assert.Null(segment.Style.Background);
    }

    [Fact]
    public void Parse_Italic_AppliesItalic()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[Italic]text[/]"));
        Assert.Equal(CliTextDecoration.Italic, segment.Style.Decorations);
    }

    [Fact]
    public void Parse_Underline_AppliesUnderline()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[Underline]text[/]"));
        Assert.Equal(CliTextDecoration.Underline, segment.Style.Decorations);
    }

    [Fact]
    public void Parse_DecorationTokens_AreCaseInsensitive()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[bOlD]text[/]"));
        Assert.Equal(CliTextDecoration.Bold, segment.Style.Decorations);
    }

    [Fact]
    public void Parse_BoldItalic_AppliesBoth()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[Bold Italic]text[/]"));
        Assert.Equal(CliTextDecoration.Bold | CliTextDecoration.Italic, segment.Style.Decorations);
    }

    [Fact]
    public void Parse_ItalicUnderline_OrderUnimportant()
    {
        var a = Assert.Single(CliMarkupParser.Parse("[Italic Underline]x[/]"));
        var b = Assert.Single(CliMarkupParser.Parse("[Underline Italic]x[/]"));
        Assert.Equal(CliTextDecoration.Italic | CliTextDecoration.Underline, a.Style.Decorations);
        Assert.Equal(a.Style.Decorations, b.Style.Decorations);
    }

    // ---- Raw decoration + colour expressions ----

    [Fact]
    public void Parse_BoldYellow_AppliesBoldAndForeground()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[Bold Yellow]text[/]"));
        Assert.Equal(CliTextDecoration.Bold, segment.Style.Decorations);
        Assert.Equal(CliColor.Yellow, segment.Style.Foreground);
        Assert.Null(segment.Style.Background);
    }

    [Fact]
    public void Parse_BoldItalicYellowOnGreen_AppliesAll()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[Bold Italic Yellow on Green]text[/]"));
        Assert.Equal(CliTextDecoration.Bold | CliTextDecoration.Italic, segment.Style.Decorations);
        Assert.Equal(CliColor.Yellow, segment.Style.Foreground);
        Assert.Equal(CliColor.Green, segment.Style.Background);
    }

    [Fact]
    public void Parse_UnderlineItalicOnBlue_AppliesDecorationsAndBackgroundOnly()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[Underline Italic on Blue]text[/]"));
        Assert.Equal(CliTextDecoration.Underline | CliTextDecoration.Italic, segment.Style.Decorations);
        Assert.Null(segment.Style.Foreground);
        Assert.Equal(CliColor.Blue, segment.Style.Background);
    }

    [Fact]
    public void Parse_BoldOnBlue_DecorationPlusBackgroundOnly()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[Bold on Blue]text[/]"));
        Assert.Equal(CliTextDecoration.Bold, segment.Style.Decorations);
        Assert.Null(segment.Style.Foreground);
        Assert.Equal(CliColor.Blue, segment.Style.Background);
    }

    // ---- Invalid / unsupported raw expressions ----

    [Fact]
    public void Parse_YellowBold_DecorationAfterColour_Throws()
        => Assert.Throws<FormatException>(() => CliMarkupParser.Parse("[Yellow Bold]text[/]"));

    [Fact]
    public void Parse_YellowOnBlueBold_DecorationAfterColour_Throws()
        => Assert.Throws<FormatException>(() => CliMarkupParser.Parse("[Yellow on Blue Bold]text[/]"));

    [Fact]
    public void Parse_BoldAccent_SemanticMixedIntoRaw_Throws()
        => Assert.Throws<FormatException>(
            () => CliMarkupParser.Parse("[Bold Accent]text[/]", baseStyle: null, new FakeResolver()));

    [Fact]
    public void Parse_AccentOnPanel_SemanticMixedIntoRaw_Throws()
        => Assert.Throws<FormatException>(
            () => CliMarkupParser.Parse("[Accent on Panel]text[/]", baseStyle: null, new FakeResolver()));

    // ---- Standalone short decoration aliases [b]/[i]/[u] ----

    [Fact]
    public void Parse_ShortBold_AppliesBold()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[b]text[/]"));
        Assert.Equal(CliTextDecoration.Bold, segment.Style.Decorations);
        Assert.Null(segment.Style.Foreground);
        Assert.Null(segment.Style.Background);
    }

    [Fact]
    public void Parse_ShortItalic_AppliesItalic()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[i]text[/]"));
        Assert.Equal(CliTextDecoration.Italic, segment.Style.Decorations);
    }

    [Fact]
    public void Parse_ShortUnderline_AppliesUnderline()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[u]text[/]"));
        Assert.Equal(CliTextDecoration.Underline, segment.Style.Decorations);
    }

    [Theory]
    [InlineData("B", CliTextDecoration.Bold)]
    [InlineData("I", CliTextDecoration.Italic)]
    [InlineData("U", CliTextDecoration.Underline)]
    public void Parse_ShortAliases_AreCaseInsensitive(string alias, CliTextDecoration expected)
    {
        var segment = Assert.Single(CliMarkupParser.Parse($"[{alias}]text[/]"));
        Assert.Equal(expected, segment.Style.Decorations);
    }

    [Fact]
    public void Parse_ShortBold_WrappingComposedColour_KeepsBoldInside()
    {
        // [B][Red ON Blue]Error![/][/] — standalone alias wraps a composed visual expression.
        var segment = Assert.Single(CliMarkupParser.Parse("[B][Red ON Blue]Error![/][/]"));
        Assert.Equal("Error!", segment.Text);
        Assert.Equal(CliTextDecoration.Bold, segment.Style.Decorations);
        Assert.Equal(CliColor.Red, segment.Style.Foreground);
        Assert.Equal(CliColor.Blue, segment.Style.Background);
    }

    [Fact]
    public void Parse_ShortAlias_InsideComposedExpression_Throws()
    {
        // Short aliases are standalone-only; they must not participate in composed visual expressions.
        Assert.Throws<FormatException>(() => CliMarkupParser.Parse("[b red on blue]Error![/]"));
        Assert.Throws<FormatException>(() => CliMarkupParser.Parse("[i yellow]Warning[/]"));
        Assert.Throws<FormatException>(() => CliMarkupParser.Parse("[u link]Docs[/]", baseStyle: null, new FakeResolver()));
    }

    // ---- Composed visual tokens vs standalone semantic tokens ----

    [Fact]
    public void Parse_ComposedFullNameDecorations_StillWork()
    {
        var segment = Assert.Single(CliMarkupParser.Parse("[Bold Red on Blue]Error![/]"));
        Assert.Equal(CliTextDecoration.Bold, segment.Style.Decorations);
        Assert.Equal(CliColor.Red, segment.Style.Foreground);
        Assert.Equal(CliColor.Blue, segment.Style.Background);
    }

    [Fact]
    public void Parse_SemanticRole_MixedIntoComposedExpression_Throws()
    {
        // Semantic styles are standalone-only and must not be mixed into composed visual expressions.
        var resolver = new FakeResolver();
        Assert.Throws<FormatException>(() => CliMarkupParser.Parse("[Heading Red on Blue]Title[/]", baseStyle: null, resolver));
        Assert.Throws<FormatException>(() => CliMarkupParser.Parse("[Key Bold]abc[/]", baseStyle: null, resolver));
        Assert.Throws<FormatException>(() => CliMarkupParser.Parse("[Link Underline]https://x[/]", baseStyle: null, resolver));
    }

    // ---- Link renders visible, copyable text ----

    [Fact]
    public void Parse_Link_KeepsVisibleText_WithLinkStyling()
    {
        var segment = Assert.Single(
            CliMarkupParser.Parse("[Link]https://example.com[/]", baseStyle: null, new FakeResolver()));

        Assert.Equal("https://example.com", segment.Text); // URL stays visible/copyable
        Assert.True(segment.Style.Decorations.HasFlag(CliTextDecoration.Underline));
        Assert.Equal(CliColor.Cyan, segment.Style.Foreground);
    }

    // ---- Hyperlink targets ----

    [Fact]
    public void Parse_Link_SetsHyperlinkTarget_FromVisibleText()
    {
        var segment = Assert.Single(
            CliMarkupParser.Parse("[Link]https://example.com[/]", baseStyle: null, new FakeResolver()));

        Assert.Equal("https://example.com", segment.Text);
        Assert.Equal("https://example.com", segment.Style.HyperlinkTarget);
    }

    [Fact]
    public void Parse_NonLinkMarkup_HasNoHyperlinkTarget()
    {
        var accent = Assert.Single(CliMarkupParser.Parse("[Accent]x[/]", baseStyle: null, new FakeResolver()));
        Assert.Null(accent.Style.HyperlinkTarget);

        var raw = Assert.Single(CliMarkupParser.Parse("[Bold Red]x[/]"));
        Assert.Null(raw.Style.HyperlinkTarget);
    }

    [Fact]
    public void Parse_NestedLinkInsideLink_IsRejected()
    {
        Assert.Throws<FormatException>(() =>
            CliMarkupParser.Parse("[Link]https://example.com/[Link]x[/][/]", baseStyle: null, new FakeResolver()));
    }

    [Fact]
    public void Parse_EscapedMarkupInsideLink_ContributesToTarget()
    {
        // ]] -> ] in both the visible text and the derived target.
        var segment = Assert.Single(
            CliMarkupParser.Parse("[Link]a]]b[/]", baseStyle: null, new FakeResolver()));

        Assert.Equal("a]b", segment.Text);
        Assert.Equal("a]b", segment.Style.HyperlinkTarget);
    }

    [Fact]
    public void Parse_NonLinkStylingInsideLink_KeepsTargetAcrossSpan()
    {
        // [Link]https://example.com/[b]docs[/][/] -> visible "https://example.com/docs",
        // target the same on every segment, with bold only on "docs".
        var segments = CliMarkupParser.Parse(
            "[Link]https://example.com/[b]docs[/][/]", baseStyle: null, new FakeResolver());

        var visible = string.Concat(segments.Select(s => s.Text));
        Assert.Equal("https://example.com/docs", visible);
        Assert.All(segments, s => Assert.Equal("https://example.com/docs", s.Style.HyperlinkTarget));

        var docs = Assert.Single(segments, s => s.Text == "docs");
        Assert.True(docs.Style.Decorations.HasFlag(CliTextDecoration.Bold));

        var prefix = Assert.Single(segments, s => s.Text == "https://example.com/");
        Assert.False(prefix.Style.Decorations.HasFlag(CliTextDecoration.Bold));
    }

    [Fact]
    public void Parse_EmptyLink_ProducesNoTarget()
    {
        var segments = CliMarkupParser.Parse("[Link][/]", baseStyle: null, new FakeResolver());
        Assert.All(segments, s => Assert.Null(s.Style.HyperlinkTarget));
    }

    // ---- Decoration composition through nesting ----

    [Fact]
    public void Parse_BoldThenColour_KeepsBoldInsideColourSpan()
    {
        // [Bold][Yellow]text[/][/] — bold remains active inside the nested colour span.
        var segment = Assert.Single(CliMarkupParser.Parse("[Bold][Yellow]text[/][/]"));
        Assert.Equal(CliTextDecoration.Bold, segment.Style.Decorations);
        Assert.Equal(CliColor.Yellow, segment.Style.Foreground);
    }

    [Fact]
    public void Parse_BoldThenUnderline_OrsDecorations()
    {
        // [Bold][Underline]text[/][/] — produces Bold | Underline inside the nested span.
        var segment = Assert.Single(CliMarkupParser.Parse("[Bold][Underline]text[/][/]"));
        Assert.Equal(CliTextDecoration.Bold | CliTextDecoration.Underline, segment.Style.Decorations);
    }

    [Fact]
    public void Parse_ClosingInnerTag_RestoresOuterDecorations()
    {
        // Outer Bold; inner Underline; after the inner [/] only Bold remains.
        var segments = CliMarkupParser.Parse("[Bold]a[Underline]b[/]c[/]");

        Assert.Equal("a", segments[0].Text);
        Assert.Equal(CliTextDecoration.Bold, segments[0].Style.Decorations);

        Assert.Equal("b", segments[1].Text);
        Assert.Equal(CliTextDecoration.Bold | CliTextDecoration.Underline, segments[1].Style.Decorations);

        Assert.Equal("c", segments[2].Text);
        Assert.Equal(CliTextDecoration.Bold, segments[2].Style.Decorations); // Underline restored away
    }

    [Fact]
    public void Parse_BoldThenSemantic_ComposesDecorations()
    {
        // [Bold][Accent]text[/][/] — Bold OR the (empty) Accent decorations, foreground from Accent.
        var segment = Assert.Single(
            CliMarkupParser.Parse("[Bold][Accent]text[/][/]", baseStyle: null, new FakeResolver()));
        Assert.Equal(CliTextDecoration.Bold, segment.Style.Decorations);
        Assert.Equal(CliColor.Cyan, segment.Style.Foreground);
    }

    [Fact]
    public void Parse_SemanticWithDecoration_ComposesWithOuterBold()
    {
        // "Title" semantic carries Underline; under outer Bold the result is Bold | Underline.
        var segment = Assert.Single(
            CliMarkupParser.Parse("[Bold][Title]text[/][/]", baseStyle: null, new FakeResolver()));
        Assert.Equal(CliTextDecoration.Bold | CliTextDecoration.Underline, segment.Style.Decorations);
        Assert.Equal(CliColor.Cyan, segment.Style.Foreground);
    }

    [Fact]
    public void Parse_SurfaceThenBoldThenAccent_ComposesCorrectly()
    {
        // [Alert][Bold][Accent]Title[/][/][/] — Accent fg, Alert bg preserved, Bold retained.
        var segment = Assert.Single(
            CliMarkupParser.Parse("[Alert][Bold][Accent]Title[/][/][/]", baseStyle: null, new FakeResolver()));
        Assert.Equal(CliColor.Cyan, segment.Style.Foreground);     // Accent
        Assert.Equal(CliColor.DarkRed, segment.Style.Background);  // Alert background preserved
        Assert.Equal(CliTextDecoration.Bold, segment.Style.Decorations);
    }
}
