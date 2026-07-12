using System;
using System.IO;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the <see cref="AnsiSink"/> SGR contract: faithful 0–255 colour rendering with the
/// ConsoleColor-order remap for 0–15, deterministic null=default behaviour, style diffing, and
/// reset-on-newline/flush.
/// </summary>
public sealed class AnsiSinkTests
{
    private const string Esc = "\u001b";

    private static CliTextSegment Seg(string text, CliColor? fg = null, CliColor? bg = null)
        => new(text, new CliCharStyle(fg, bg));

    private static string Render(params CliTextSegment[] segments)
    {
        var writer = new StringWriter();
        var sink = new AnsiSink(writer);
        foreach (var s in segments)
            sink.Write(s);
        sink.Flush();
        return writer.ToString();
    }

    // ---- 0–15 use the ConsoleColor-order classic SGR remap (NOT 38;5;<index>) ----

    [Fact]
    public void Foreground_Standard_UsesRemappedClassicCode()
    {
        // DarkBlue == 1, but ANSI index 1 is red; the remap must emit 34, not 38;5;1.
        Assert.Equal($"{Esc}[34mx{Esc}[0m", Render(Seg("x", CliColor.DarkBlue)));
    }

    [Fact]
    public void Background_Standard_UsesRemappedClassicCode()
    {
        // DarkRed foreground maps to 31, so its background is 41.
        Assert.Equal($"{Esc}[41mx{Esc}[0m", Render(Seg("x", bg: CliColor.DarkRed)));
    }

    [Theory]
    [InlineData(CliColor.Black, 30)]
    [InlineData(CliColor.DarkBlue, 34)]
    [InlineData(CliColor.DarkGreen, 32)]
    [InlineData(CliColor.DarkCyan, 36)]
    [InlineData(CliColor.DarkRed, 31)]
    [InlineData(CliColor.DarkMagenta, 35)]
    [InlineData(CliColor.DarkYellow, 33)]
    [InlineData(CliColor.Gray, 37)]
    [InlineData(CliColor.DarkGray, 90)]
    [InlineData(CliColor.Blue, 94)]
    [InlineData(CliColor.Green, 92)]
    [InlineData(CliColor.Cyan, 96)]
    [InlineData(CliColor.Red, 91)]
    [InlineData(CliColor.Magenta, 95)]
    [InlineData(CliColor.Yellow, 93)]
    [InlineData(CliColor.White, 97)]
    public void Foreground_Standard_FullRemapTable(CliColor color, int code)
    {
        Assert.Equal($"{Esc}[{code}mx{Esc}[0m", Render(Seg("x", color)));
        // Background is the foreground code + 10.
        Assert.Equal($"{Esc}[{code + 10}mx{Esc}[0m", Render(Seg("x", bg: color)));
    }

    // ---- 16–255 emit faithful 256-colour sequences ----

    [Fact]
    public void Foreground_Extended_Uses38_5()
    {
        // OceanBlue == 24.
        Assert.Equal($"{Esc}[38;5;24mx{Esc}[0m", Render(Seg("x", CliColor.OceanBlue)));
    }

    [Fact]
    public void Background_Extended_Uses48_5()
    {
        // Sand2 == 221.
        Assert.Equal($"{Esc}[48;5;221mx{Esc}[0m", Render(Seg("x", bg: CliColor.Sand2)));
    }

    [Fact]
    public void Foreground_Grayscale_Uses38_5()
    {
        // Gray85 == 253.
        Assert.Equal($"{Esc}[38;5;253mx{Esc}[0m", Render(Seg("x", CliColor.Gray85)));
    }

    // ---- exact 15/16 boundary: 16 (Black1) must use 256-colour, never the classic table ----

    [Fact]
    public void Foreground_Black1_Uses38_5_16()
    {
        // Black1 == 16, the first extended index; it must emit 38;5;16, not index the 0–15 table.
        Assert.Equal($"{Esc}[38;5;16mx{Esc}[0m", Render(Seg("x", CliColor.Black1)));
    }

    [Fact]
    public void Background_Black1_Uses48_5_16()
    {
        Assert.Equal($"{Esc}[48;5;16mx{Esc}[0m", Render(Seg("x", bg: CliColor.Black1)));
    }

    [Fact]
    public void Background_Black1_DoesNotThrow()
    {
        // Regression: value 16 once fell through the standard-colour guard and indexed the
        // 16-entry classic SGR table, throwing IndexOutOfRangeException on Linux.
        var ex = Record.Exception(() => Render(Seg("x", bg: CliColor.Black1)));
        Assert.Null(ex);
    }

    // ---- foreground + background coalesce into one sequence ----

    [Fact]
    public void ForegroundAndBackground_Combine_IntoSingleSequence()
    {
        Assert.Equal(
            $"{Esc}[38;5;24;48;5;221mx{Esc}[0m",
            Render(Seg("x", CliColor.OceanBlue, CliColor.Sand2)));
    }

    // ---- diffing ----

    [Fact]
    public void AdjacentSameStyle_NoRedundantEscape()
    {
        // Red == 91. Only one opening escape, then both texts, then one reset.
        Assert.Equal(
            $"{Esc}[91mab{Esc}[0m",
            Render(Seg("a", CliColor.Red), Seg("b", CliColor.Red)));
    }

    [Fact]
    public void ColorToNullForeground_Emits39()
    {
        // Second segment clears the foreground to default; no trailing reset (style no longer active).
        Assert.Equal(
            $"{Esc}[91ma{Esc}[39mb",
            Render(Seg("a", CliColor.Red), Seg("b")));
    }

    [Fact]
    public void ColorToNullBackground_Emits49()
    {
        Assert.Equal(
            $"{Esc}[41ma{Esc}[49mb",
            Render(Seg("a", bg: CliColor.DarkRed), Seg("b")));
    }

    // ---- reset behaviour ----

    [Fact]
    public void Flush_EmitsReset_WhenStyleActive()
    {
        Assert.EndsWith($"{Esc}[0m", Render(Seg("x", CliColor.Red)));
    }

    [Fact]
    public void Flush_DoesNotEmitReset_ForPlainOutput()
    {
        Assert.Equal("plain", Render(Seg("plain")));
    }

    [Fact]
    public void NewLine_EmitsResetBeforeNewline_WhenStyleActive()
    {
        var writer = new StringWriter();
        var sink = new AnsiSink(writer);
        sink.Write(Seg("x", CliColor.Red));
        sink.NewLine();
        sink.Flush();

        // Reset precedes the newline so the background does not bleed past the line.
        Assert.Equal($"{Esc}[91mx{Esc}[0m{Environment.NewLine}", writer.ToString());
    }

    [Fact]
    public void NewLine_ResetsTrackedState_StyleReemittedOnNextLine()
    {
        var writer = new StringWriter();
        var sink = new AnsiSink(writer);
        sink.Write(Seg("x", CliColor.Red));
        sink.NewLine();
        sink.Write(Seg("y", CliColor.Red));
        sink.Flush();

        Assert.Equal(
            $"{Esc}[91mx{Esc}[0m{Environment.NewLine}{Esc}[91my{Esc}[0m",
            writer.ToString());
    }

    [Fact]
    public void TextContent_WrittenVerbatim()
    {
        var result = Render(Seg("a=b;c d", CliColor.Red));
        Assert.Contains("a=b;c d", result);
    }

    [Fact]
    public void Dimensions_AreNull()
    {
        var sink = new AnsiSink(new StringWriter());
        Assert.Null(sink.SoftMaxWidth);
        Assert.Null(sink.SoftMaxHeight);
        Assert.Null(sink.MaxWidth);
        Assert.Null(sink.MaxHeight);
    }

    // ---- text decorations ----

    private static CliTextSegment SegD(string text, CliTextDecoration deco, CliColor? fg = null, CliColor? bg = null)
        => new(text, new CliCharStyle(fg, bg, deco));

    [Fact]
    public void Bold_EmitsSgr1_AndResets()
    {
        Assert.Equal($"{Esc}[1mx{Esc}[0m", Render(SegD("x", CliTextDecoration.Bold)));
    }

    [Fact]
    public void Italic_EmitsSgr3_AndResets()
    {
        Assert.Equal($"{Esc}[3mx{Esc}[0m", Render(SegD("x", CliTextDecoration.Italic)));
    }

    [Fact]
    public void Underline_EmitsSgr4_AndResets()
    {
        Assert.Equal($"{Esc}[4mx{Esc}[0m", Render(SegD("x", CliTextDecoration.Underline)));
    }

    [Fact]
    public void BoldOff_EmittedWhenDecorationLeaves_NoTrailingReset()
    {
        // First segment bold; second plain → 22 turns just bold off, no style remains active.
        Assert.Equal(
            $"{Esc}[1ma{Esc}[22mb",
            Render(SegD("a", CliTextDecoration.Bold), SegD("b", CliTextDecoration.None)));
    }

    [Fact]
    public void BoldPlusColour_EmitsDecorationAndColour_InOneSequence()
    {
        // Decoration code precedes the colour; Red == 91.
        Assert.Equal($"{Esc}[1;91mx{Esc}[0m", Render(SegD("x", CliTextDecoration.Bold, CliColor.Red)));
    }

    [Fact]
    public void NestedColourInsideBold_KeepsBold()
    {
        // a (bold) → b (bold + red): only the colour changes, bold is never turned off.
        Assert.Equal(
            $"{Esc}[1ma{Esc}[91mb{Esc}[0m",
            Render(SegD("a", CliTextDecoration.Bold), SegD("b", CliTextDecoration.Bold, CliColor.Red)));
    }

    [Fact]
    public void NestedUnderlineInsideBold_EmitsUnderlineOff_KeepsBold()
    {
        // a (bold) → b (bold+underline) → c (bold): leaving the underline scope emits 24, keeps bold.
        Assert.Equal(
            $"{Esc}[1ma{Esc}[4mb{Esc}[24mc{Esc}[0m",
            Render(
                SegD("a", CliTextDecoration.Bold),
                SegD("b", CliTextDecoration.Bold | CliTextDecoration.Underline),
                SegD("c", CliTextDecoration.Bold)));
    }

    [Fact]
    public void NewLine_ResetIncludesDecorations()
    {
        var writer = new StringWriter();
        var sink = new AnsiSink(writer);
        sink.Write(SegD("x", CliTextDecoration.Bold));
        sink.NewLine();
        sink.Flush();

        // Reset (which clears bold) precedes the newline.
        Assert.Equal($"{Esc}[1mx{Esc}[0m{Environment.NewLine}", writer.ToString());
    }

    // ---- MarkupToAnsi integration ----

    [Fact]
    public void MarkupToAnsi_Bold_EmitsOnThenReset()
    {
        Assert.Equal($"{Esc}[1mx{Esc}[0m", TigerConsole.MarkupToAnsi("[Bold]x[/]"));
    }

    [Fact]
    public void MarkupToAnsi_Italic_EmitsOnThenReset()
    {
        Assert.Equal($"{Esc}[3mx{Esc}[0m", TigerConsole.MarkupToAnsi("[Italic]x[/]"));
    }

    [Fact]
    public void MarkupToAnsi_Underline_EmitsOnThenReset()
    {
        Assert.Equal($"{Esc}[4mx{Esc}[0m", TigerConsole.MarkupToAnsi("[Underline]x[/]"));
    }

    [Fact]
    public void MarkupToAnsi_BoldYellow_EmitsBothInOneSequence()
    {
        // Yellow == 93; decoration precedes the colour.
        Assert.Equal($"{Esc}[1;93mx{Esc}[0m", TigerConsole.MarkupToAnsi("[Bold Yellow]x[/]"));
    }

    [Fact]
    public void MarkupToAnsi_NestedUnderlineInsideBold_KeepsBold()
    {
        // a (bold) b (bold+underline) c (bold): underline turned off on leaving, bold retained.
        Assert.Equal(
            $"{Esc}[1ma{Esc}[4mb{Esc}[24mc{Esc}[0m",
            TigerConsole.MarkupToAnsi("[Bold]a[Underline]b[/]c[/]"));
    }
}
