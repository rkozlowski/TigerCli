using System;
using System.Globalization;
using System.IO;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the defence against invalid <see cref="ConsoleColor"/> values leaking into
/// <see cref="CliColor"/>. On Linux with redirected/captured output, <see cref="Console.ForegroundColor"/>
/// can report an out-of-range sentinel (e.g. <c>-1</c>); that must degrade to "no color", never become
/// a <c>CliColor(-1)</c> that later throws in <see cref="CliColorPalette.GetRgb"/> during rendering.
/// </summary>
public sealed class ConsoleColorSafetyTests
{
    // ---- FromConsoleColorOrNull: only valid 0–15 map; everything else is null ----

    [Fact]
    public void FromConsoleColorOrNull_InvalidNegativeSentinel_ReturnsNull()
    {
        // This is exactly what Linux reports for an unavailable console color.
        Assert.Null(CliColorMapper.FromConsoleColorOrNull((ConsoleColor)(-1)));
    }

    [Fact]
    public void FromConsoleColorOrNull_OutOfRangeHigh_ReturnsNull()
    {
        // 16 is not a ConsoleColor-compatible value in TigerCli (only 0–15 are).
        Assert.Null(CliColorMapper.FromConsoleColorOrNull((ConsoleColor)16));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(15)]
    public void FromConsoleColorOrNull_StandardValues_MapToMatchingCliColor(int value)
    {
        Assert.Equal((CliColor)value, CliColorMapper.FromConsoleColorOrNull((ConsoleColor)value));
    }

    [Fact]
    public void FromConsoleColorOrNull_AllStandardConsoleColors_RoundTrip()
    {
        foreach (ConsoleColor cc in Enum.GetValues<ConsoleColor>())
            Assert.Equal((CliColor)(int)cc, CliColorMapper.FromConsoleColorOrNull(cc));
    }

    // ---- the blind cast is why the helper exists: it produces the poison value ----

    [Fact]
    public void ToCliColor_BlindCast_ProducesInvalidValue_HelperGuardsAgainstIt()
    {
        // Documents the unsafe behaviour the markup base-style path used to rely on.
        Assert.Equal((CliColor)(-1), CliColorMapper.ToCliColor((ConsoleColor)(-1)));
    }

    // ---- palette/mapper stay strict for invalid CliColor values ----

    [Fact]
    public void GetRgb_InvalidNegativeCliColor_StillThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CliColorPalette.GetRgb((CliColor)(-1)));
    }

    [Fact]
    public void ToConsoleColor_InvalidNegativeCliColor_StillThrows()
    {
        // ToConsoleColor degrades 16–255 via ToNearestStandard, but a value outside 0–255 is invalid
        // and must surface through GetRgb rather than being silently blessed.
        Assert.Throws<ArgumentOutOfRangeException>(() => CliColorMapper.ToConsoleColor((CliColor)(-1)));
    }

    // ---- the exact call the console sinks make on a poison style ----
    //
    // ConsoleSink/ConsoleErrorSink are internal, so they are exercised end-to-end through the public
    // Markup/MarkupError helpers below. The sink's per-segment work for a colored style is exactly
    // CliColorMapper.ToConsoleColor(color); the test above proves that call rejects CliColor(-1)
    // rather than silently rendering it, which is the crash the base-style fix prevents upstream.

    // ---- end-to-end markup helpers must survive captured stdout/stderr ----

    [Fact]
    public void Markup_And_MarkupLine_DoNotThrow_WhenConsoleCaptured()
    {
        WithCapturedConsole(() =>
        {
            TigerConsole.Markup("[Red]hello[/] world");
            TigerConsole.MarkupLine("[OceanBlue]line[/]");
        });
    }

    [Fact]
    public void MarkupLine_BlankLineWritesNewline_WhenConsoleCaptured()
    {
        string stdout = string.Empty;
        WithCapturedConsole(
            TigerConsole.MarkupLine,
            captureStdout: s => stdout = s);

        Assert.Equal(Environment.NewLine, stdout);
    }

    [Fact]
    public void MarkupError_And_MarkupErrorLine_DoNotThrow_WhenConsoleCaptured()
    {
        WithCapturedConsole(() =>
        {
            TigerConsole.MarkupError("[Red]error[/] text");
            TigerConsole.MarkupErrorLine("[Yellow]warn[/]");
        });
    }

    [Fact]
    public void MarkupErrorLine_RendersExpectedText_WhenConsoleCaptured()
    {
        string stderr = string.Empty;
        WithCapturedConsole(
            () => TigerConsole.MarkupErrorLine(CultureInfo.InvariantCulture, "value={0}", 1.5),
            captureStderr: s => stderr = s);

        Assert.Equal("value=1.5" + Environment.NewLine, stderr);
    }

    private static void WithCapturedConsole(
        Action action,
        Action<string>? captureStdout = null,
        Action<string>? captureStderr = null)
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
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        captureStdout?.Invoke(stdout.ToString());
        captureStderr?.Invoke(stderr.ToString());
    }
}
