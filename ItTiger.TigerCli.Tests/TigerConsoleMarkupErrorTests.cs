using System.Globalization;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerConsoleMarkupErrorTests
{
    [Fact]
    public void MarkupError_WritesToStderr_AndNotStdout()
    {
        var (stdout, stderr) = CaptureConsole(() => TigerConsole.MarkupError("hello"));

        Assert.Equal("hello", stderr);
        Assert.Equal(string.Empty, stdout);
    }

    [Fact]
    public void MarkupError_DoesNotAppendNewline()
    {
        var (_, stderr) = CaptureConsole(() => TigerConsole.MarkupError("hello"));

        Assert.Equal("hello", stderr);
    }

    [Fact]
    public void MarkupErrorLine_WritesToStderr_WithTrailingNewline()
    {
        var (stdout, stderr) = CaptureConsole(() => TigerConsole.MarkupErrorLine("hello"));

        Assert.Equal("hello" + Environment.NewLine, stderr);
        Assert.Equal(string.Empty, stdout);
    }

    [Fact]
    public void MarkupError_ParsesMarkup_AndStripsTagsFromCapturedText()
    {
        var (_, stderr) = CaptureConsole(() => TigerConsole.MarkupError("[red]error[/] text"));

        Assert.Equal("error text", stderr);
    }

    [Fact]
    public void MarkupErrorLine_ParsesMarkup_AndStripsTagsFromCapturedText()
    {
        var (_, stderr) = CaptureConsole(() => TigerConsole.MarkupErrorLine("[yellow]warn[/]"));

        Assert.Equal("warn" + Environment.NewLine, stderr);
    }

    [Fact]
    public void MarkupError_RendersEscapedBrackets_Literally()
    {
        var escaped = CliMarkupParser.Escape("[item]");

        var (_, stderr) = CaptureConsole(() => TigerConsole.MarkupError(escaped));

        Assert.Equal("[item]", stderr);
    }

    [Fact]
    public void MarkupError_UnescapedBracketsAroundUnknownTag_Throws()
    {
        Assert.Throws<FormatException>(() => TigerConsole.MarkupError("[item]"));
    }

    [Fact]
    public void MarkupError_WithFormatProvider_FormatsValue()
    {
        var (_, stderr) = CaptureConsole(() =>
            TigerConsole.MarkupError(CultureInfo.InvariantCulture, "value={0}", 1.5));

        Assert.Equal("value=1.5", stderr);
    }

    [Fact]
    public void MarkupErrorLine_WithFormatProvider_FormatsValue()
    {
        var (_, stderr) = CaptureConsole(() =>
            TigerConsole.MarkupErrorLine(CultureInfo.InvariantCulture, "value={0}", 1.5));

        Assert.Equal("value=1.5" + Environment.NewLine, stderr);
    }

    [Fact]
    public void MarkupErrorLine_RestoresOriginalConsoleStreams()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        CaptureConsole(() => TigerConsole.MarkupErrorLine("text"));

        Assert.Same(originalOut, Console.Out);
        Assert.Same(originalError, Console.Error);
    }

    private static (string Stdout, string Stderr) CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            action();
            return (stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
