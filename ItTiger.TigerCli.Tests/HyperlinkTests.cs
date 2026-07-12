using System;
using System.IO;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers OSC 8 clickable-link support: the <see cref="CliCharStyle.HyperlinkTarget"/> /
/// <see cref="CliCellStyle.IsHyperlink"/> style model, cell-level target derivation in
/// <see cref="CliGrid"/>, structured-output wiring (<see cref="CliDetails"/> / <see cref="CliList{T}"/>),
/// <see cref="AnsiSink"/> emission, and the <see cref="CliHyperlinkMode"/> capability/config model.
/// Visible/copyable text is preserved in every sink; clickability is a progressive enhancement.
/// </summary>
public sealed class HyperlinkTests
{
    private const string Esc = "";
    private static readonly string St = Esc + "\\";
    private static string Open(string uri) => Esc + "]8;;" + uri + St;
    private static readonly string Close = Esc + "]8;;" + St;

    private static CliTextSegment Seg(string text, string? target, CliColor? fg = null)
        => new(text, new CliCharStyle(fg) { HyperlinkTarget = target });

    private static string RenderAnsi(bool emitHyperlinks, params CliTextSegment[] segments)
    {
        var writer = new StringWriter();
        var sink = new AnsiSink(writer, emitHyperlinks);
        foreach (var s in segments)
            sink.Write(s);
        sink.Flush();
        return writer.ToString();
    }

    private static string RenderGridAnsi(CliGrid grid, bool emitHyperlinks)
    {
        var writer = new StringWriter();
        TigerConsole.RenderGrid(grid, new AnsiSink(writer, emitHyperlinks));
        return writer.ToString();
    }

    // ---- Style model: clone preserves the new fields ----

    [Fact]
    public void CliCharStyle_Clone_PreservesHyperlinkTarget()
    {
        var original = new CliCharStyle(CliColor.Cyan) { HyperlinkTarget = "https://example.com" };
        var clone = CliCharStyle.Clone(original);

        Assert.Equal("https://example.com", clone!.Value.HyperlinkTarget);
        Assert.Equal(CliColor.Cyan, clone.Value.Foreground);
    }

    [Fact]
    public void CliCellStyle_Clone_PreservesIsHyperlinkAndTarget()
    {
        var original = new CliCellStyle(new CliCharStyle(CliColor.Cyan) { HyperlinkTarget = "u" })
        {
            IsHyperlink = true
        };
        var clone = CliCellStyle.Clone(original);

        Assert.True(clone!.IsHyperlink);
        Assert.Equal("u", clone.CharStyle?.HyperlinkTarget);
    }

    [Fact]
    public void CliCharStyle_DefaultHyperlinkTarget_IsNull()
    {
        Assert.Null(new CliCharStyle(CliColor.Red).HyperlinkTarget);
        Assert.Null(new CliCellStyle().IsHyperlink);
    }

    // ---- AnsiSink OSC 8 emission ----

    [Fact]
    public void AnsiSink_Disabled_EmitsNoOsc8()
    {
        var output = RenderAnsi(emitHyperlinks: false, Seg("text", "https://example.com"));

        Assert.DoesNotContain("]8;;", output);
        Assert.Contains("text", output);
    }

    [Fact]
    public void AnsiSink_Enabled_WrapsTextInOsc8()
    {
        var output = RenderAnsi(emitHyperlinks: true, Seg("text", "https://example.com"));

        Assert.Equal(Open("https://example.com") + "text" + Close, output);
    }

    [Fact]
    public void AnsiSink_TargetChange_ClosesThenOpens()
    {
        var output = RenderAnsi(emitHyperlinks: true,
            Seg("a", "u1"), Seg("b", "u2"));

        Assert.Equal(Open("u1") + "a" + Close + Open("u2") + "b" + Close, output);
    }

    [Fact]
    public void AnsiSink_SameTargetContiguous_StaysOneLink()
    {
        var output = RenderAnsi(emitHyperlinks: true,
            Seg("a", "u"), Seg("b", "u"));

        Assert.Equal(Open("u") + "a" + "b" + Close, output);
    }

    [Fact]
    public void AnsiSink_TargetThenNoTarget_ClosesLink()
    {
        var output = RenderAnsi(emitHyperlinks: true,
            Seg("a", "u"), Seg("b", target: null));

        Assert.Equal(Open("u") + "a" + Close + "b", output);
    }

    [Fact]
    public void AnsiSink_NewLine_ClosesOpenLink()
    {
        var writer = new StringWriter();
        var sink = new AnsiSink(writer, emitHyperlinks: true);
        sink.Write(Seg("a", "u"));
        sink.NewLine();
        sink.Flush();

        Assert.Equal(Open("u") + "a" + Close + Environment.NewLine, writer.ToString());
    }

    [Fact]
    public void AnsiSink_Sgr_StillWorks_WithHyperlink()
    {
        var output = RenderAnsi(emitHyperlinks: true, Seg("x", "u", CliColor.Red));

        // SGR colour, then the OSC 8 open, then the text, then close + reset on flush.
        Assert.Contains(Esc + "[", output);          // an SGR sequence is present
        Assert.Contains(Open("u") + "x", output);     // link wraps the visible text
        Assert.Contains(Close, output);
        Assert.Contains("x", output);
    }

    [Fact]
    public void AnsiSink_SanitizesControlCharactersInTarget()
    {
        // ESC, newline and other control chars are stripped from the emitted target only.
        var output = RenderAnsi(emitHyperlinks: true, Seg("x", "a" + Esc + "b\nc"));

        // Ordinal comparisons: the OSC payload is exactly "abc", and no extra ESC leaked from the
        // target (only the 4 framing ESCs remain: open + ST, close + ST). Avoids culture-sensitive
        // matching that would treat the control chars in a needle as ignorable.
        Assert.True(output.Contains(Open("abc") + "x", StringComparison.Ordinal));

        int escCount = 0;
        foreach (var ch in output)
            if (ch == '')
                escCount++;
        Assert.Equal(4, escCount);
    }

    // ---- Grid cell-level derivation ----

    private static CliGrid HyperlinkCellGrid(string text, CliCellStyle style, int width)
    {
        var grid = new CliGrid(1, 1);
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = width, MinWidth = width, MaxWidth = width }));
        grid.SetRow(0, new CliGridRowDefinition(new CliCellStyle()));
        grid.Set(0, 0, text, style);
        return grid;
    }

    [Fact]
    public void Grid_IsHyperlinkCell_DerivesTargetFromVisibleText()
    {
        var grid = HyperlinkCellGrid("https://example.com",
            new CliCellStyle { IsHyperlink = true }, width: 20);

        var output = RenderGridAnsi(grid, emitHyperlinks: true);

        Assert.Contains(Open("https://example.com"), output);
    }

    [Fact]
    public void Grid_TruncatedHyperlinkCell_KeepsFullTarget()
    {
        // The visible text is truncated to the column width, but the derived target is the full value.
        var grid = HyperlinkCellGrid("https://example.com/very/long/path",
            new CliCellStyle
            {
                IsHyperlink = true,
                Wrapping = new CliWrapping(CliWrapMode.SingleLine, allowTruncation: true, "…")
            },
            width: 10);

        var output = RenderGridAnsi(grid, emitHyperlinks: true);

        Assert.Contains(Open("https://example.com/very/long/path"), output); // full target survives truncation
        Assert.Contains("…", output);                                         // visible text was truncated
    }

    [Fact]
    public void Grid_WrappedHyperlinkCell_EachLineKeepsFullTarget()
    {
        var grid = HyperlinkCellGrid("https://example.com/very/long/path",
            new CliCellStyle
            {
                IsHyperlink = true,
                Wrapping = new CliWrapping(CliWrapMode.CharWrap)
            },
            width: 10);

        var output = RenderGridAnsi(grid, emitHyperlinks: true);

        // The link wraps onto multiple lines; every opened link uses the full target, never a fragment.
        var idx = output.IndexOf("]8;;", StringComparison.Ordinal);
        Assert.True(idx >= 0, "expected at least one OSC 8 open");
        Assert.Contains(Open("https://example.com/very/long/path"), output);
    }

    [Fact]
    public void Grid_NoHyperlinkFlag_DerivesNoTarget()
    {
        var grid = HyperlinkCellGrid("https://example.com", new CliCellStyle(), width: 20);

        var output = RenderGridAnsi(grid, emitHyperlinks: true);

        Assert.DoesNotContain("]8;;", output);
    }

    [Fact]
    public void Grid_ExplicitSegmentTarget_WinsOverDerivedCellTarget()
    {
        // Preformatted [Link] sets an explicit target on the URL span; the cell is also flagged
        // IsHyperlink, so the trailing " x" derives the full-cell target. The URL keeps its own target.
        var grid = HyperlinkCellGrid("[Link]https://example.com[/] x",
            new CliCellStyle { IsHyperlink = true, FormattingMode = CliFormattingMode.Preformatted },
            width: 40);

        var output = RenderGridAnsi(grid, emitHyperlinks: true);

        Assert.Contains(Open("https://example.com") + "https://example.com", output); // explicit, exact URL
        Assert.Contains(Open("https://example.com x"), output);                       // derived for the rest
    }

    // ---- Structured output: CliDetails ----

    private static readonly ITheme Blue = new TigerBlueTheme();

    private static CliDetails NewDetails() =>
        new CliDetails().ApplyPreset(CliTableStylePreset.Details, Blue);

    [Fact]
    public void CliDetails_AddLink_RendersVisibleValue_NoMarkup()
    {
        var lines = TigerConsole.RenderToLines(NewDetails().AddLink("Website:", "https://example.com"));
        var text = string.Join("\n", lines);

        Assert.Contains("https://example.com", text);
        Assert.DoesNotContain("[Link]", text); // never generated as markup internally
        Assert.DoesNotContain("]8;;", text);    // plain sink emits no OSC 8
    }

    [Fact]
    public void CliDetails_AddLink_EmitsOsc8TargetInAnsi()
    {
        var output = RenderGridAnsi(NewDetails().AddLink("Website:", "https://example.com").ToGrid(),
            emitHyperlinks: true);

        Assert.Contains(Open("https://example.com"), output);
    }

    [Fact]
    public void CliDetails_AddOptionalLink_OmitsMissingValue()
    {
        var table = NewDetails()
            .Add("Name:", "prod")
            .AddOptionalLink("Docs:", null)
            .ToTable();

        Assert.Single(table.Header.Elements);
        Assert.Equal("Name:", table.Header.Elements[0].HeaderContent);
    }

    [Fact]
    public void CliDetails_AddOptionalLink_Present_EmitsOsc8()
    {
        var output = RenderGridAnsi(
            NewDetails().AddOptionalLink("Docs:", "https://docs.example.com").ToGrid(),
            emitHyperlinks: true);

        Assert.Contains(Open("https://docs.example.com"), output);
    }

    [Fact]
    public void CliDetails_MissingLinkValue_IsNotHyperlinked()
    {
        // AddLink always renders; a missing value shows the missing display and must NOT be a link.
        var table = NewDetails().AddLink("Website:", null).ToTable();

        Assert.False(table.Header.Elements[0].DataIsHyperlink);
    }

    // ---- Structured output: CliList ----

    private sealed record Site(string Name, string Url);

    [Fact]
    public void CliList_AddLinkColumn_DerivesPerRowTargets()
    {
        var sites = new[]
        {
            new Site("A", "https://a.example.com"),
            new Site("B", "https://b.example.com"),
        };

        var list = new CliList<Site>()
            .ApplyPreset(CliTableStylePreset.Lucca, Blue)
            .AddColumn("Name", s => s.Name)
            .AddLinkColumn("Website", s => s.Url);

        var output = RenderGridAnsi(list.Render(sites).ToGrid(), emitHyperlinks: true);

        Assert.Contains(Open("https://a.example.com"), output);
        Assert.Contains(Open("https://b.example.com"), output);
    }

    [Fact]
    public void CliList_AddLinkColumn_RendersVisibleValues_NoMarkup()
    {
        var sites = new[] { new Site("A", "https://a.example.com") };

        var lines = TigerConsole.RenderToLines(new CliList<Site>()
            .ApplyPreset(CliTableStylePreset.Lucca, Blue)
            .AddLinkColumn("Website", s => s.Url)
            .Render(sites));
        var text = string.Join("\n", lines);

        Assert.Contains("https://a.example.com", text);
        Assert.DoesNotContain("[Link]", text);
        Assert.DoesNotContain("]8;;", text);
    }

    // ---- Configuration / capability (CliHyperlinkMode) ----

    private static string CaptureMarkup(CliColorMode color, CliHyperlinkMode hyperlinks, string markup)
    {
        var origColor = TigerConsole.ColorMode;
        var origLink = TigerConsole.HyperlinkMode;
        var origOut = Console.Out;
        try
        {
            TigerConsole.ColorMode = color;
            TigerConsole.HyperlinkMode = hyperlinks;
            using var sw = new StringWriter();
            Console.SetOut(sw);
            TigerConsole.MarkupLine(markup);
            return sw.ToString();
        }
        finally
        {
            Console.SetOut(origOut);
            TigerConsole.ColorMode = origColor;
            TigerConsole.HyperlinkMode = origLink;
        }
    }

    [Fact]
    public void HyperlinkMode_Always_EmitsOsc8_OnAnsiSink()
    {
        var output = CaptureMarkup(CliColorMode.Ansi256, CliHyperlinkMode.Always,
            "[Link]https://example.com[/]");

        Assert.Contains("]8;;https://example.com", output);
    }

    [Fact]
    public void HyperlinkMode_Never_EmitsNoOsc8()
    {
        var output = CaptureMarkup(CliColorMode.Ansi256, CliHyperlinkMode.Never,
            "[Link]https://example.com[/]");

        Assert.DoesNotContain("]8;;", output);
        Assert.Contains("https://example.com", output); // text still visible
    }

    [Fact]
    public void HyperlinkMode_Auto_ForcedAnsi_EmitsNoOsc8()
    {
        // Forced Ansi256 is not capability-detected, so Auto prefers visible text without OSC 8.
        var output = CaptureMarkup(CliColorMode.Ansi256, CliHyperlinkMode.Auto,
            "[Link]https://example.com[/]");

        Assert.DoesNotContain("]8;;", output);
        Assert.Contains("https://example.com", output);
    }
}
