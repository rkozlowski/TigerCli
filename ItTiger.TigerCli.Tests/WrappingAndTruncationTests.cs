using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;


public class WrappingAndTruncationTests : TestBase
{
    [Fact]
    public void WordWrap_Basic()
    {
        var g = Grid1x1("The quick brown fox jumps over the lazy dog", 10, CliWrapMode.WordWrap);
        AssertSnapshot(g,
            "The quick ",
            "brown fox ",
            "jumps over",
            "the lazy  ",
            "dog       ");
    }

    [Fact]
    public void SymbolWrap_PreservesSymbols()
    {
        var g = Grid1x1("foo_bar/baz.qux!", 7, CliWrapMode.SymbolWrap);
        AssertSnapshot(g,
            "foo_bar",
            "/baz.  ",
            "qux!   ");
    }

    [Fact]
    public void CharWrap_LongToken_SplitsAnywhere()
    {
        var g = Grid1x1("Supercalifragilisticexpialidocious", 8, CliWrapMode.CharWrap);
        AssertSnapshot(g,
            "Supercal",
            "ifragili",
            "sticexpi",
            "alidocio",
            "us      ");
    }

    [Fact]
    public void SingleLine_Truncation_AppendsIndicator()
    {
        var g = Grid1x1("Hello world", 5, CliWrapMode.SingleLine, allowTruncation: true, indicator: "…");
        var lines = TigerConsole.RenderGridToLines(g);
        Assert.Single(lines);
        Assert.Equal("Hell…", lines[0]);   // width 5 = 4 chars + indicator
        Assert.Equal(5, lines[0].Length);
    }

    [Fact]
    public void Multiline_TruncatesEachExplicitLine()
    {
        var g = Grid1x1("Alpha\nBetaGamma", 5, CliWrapMode.Multiline, allowTruncation: true, indicator: "…");
        AssertSnapshot(g,
            "Alpha",   // fits
            "Beta…");  // truncated second line
    }

    [Fact]
    public void CJK_SymbolWrap_BreaksOnPunctuation()
    {
        var g = Grid1x1("東京/大阪-京都,名古屋", 3, CliWrapMode.SymbolWrap);
        AssertSnapshot(g,
            "東京/",
            "大阪-",
            "京都,",
            "名古屋");
    }

    public class WrappingInvariants : TestBase
    {
        [Theory]
        [InlineData("LongWordWithoutSpacesHere", 6, CliWrapMode.WordWrap)]
        [InlineData("foo_bar/baz.qux!", 5, CliWrapMode.SymbolWrap)]
        [InlineData("漢字測試文本", 4, CliWrapMode.CharWrap)]
        public void NoLineExceedsCellWidth(string text, int width, CliWrapMode mode)
        {
            var g = Grid1x1(text, width, mode, allowTruncation: false);
            var lines = TigerConsole.RenderGridToLines(g);
            Assert.All(lines, line => Assert.True(line.Length <= width, $"Overflow: '{line}' (len {line.Length}) > {width}"));
        }
    }
}
