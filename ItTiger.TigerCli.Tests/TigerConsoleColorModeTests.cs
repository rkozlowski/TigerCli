using System;
using System.IO;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Exercises the sink factory indirectly through the public default output paths under each
/// <see cref="CliColorMode"/>. <see cref="TigerConsole.ColorMode"/> and the console streams are
/// process-global, so each test saves and restores them. ANSI presence is asserted via the CSI
/// token <c>[38;5;24</c> (the printable tail of <c>ESC[38;5;24…m</c>), which plain output never
/// contains; plain output is asserted by exact equality.
/// </summary>
public sealed class TigerConsoleColorModeTests
{
    private static string CaptureOut(CliColorMode mode, Action action)
    {
        var originalOut = Console.Out;
        var originalMode = TigerConsole.ColorMode;
        using var sw = new StringWriter();
        try
        {
            TigerConsole.ColorMode = mode;
            Console.SetOut(sw);
            action();
            return sw.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
            TigerConsole.ColorMode = originalMode;
        }
    }

    private static string CaptureError(CliColorMode mode, Action action)
    {
        var originalError = Console.Error;
        var originalMode = TigerConsole.ColorMode;
        using var sw = new StringWriter();
        try
        {
            TigerConsole.ColorMode = mode;
            Console.SetError(sw);
            action();
            return sw.ToString();
        }
        finally
        {
            Console.SetError(originalError);
            TigerConsole.ColorMode = originalMode;
        }
    }

    [Fact]
    public void Ansi256_Markup_EmitsAnsiEscape()
    {
        // OceanBlue == 24 -> faithful 256-colour foreground in an ESC[38;5;24…m sequence.
        var output = CaptureOut(CliColorMode.Ansi256, () => TigerConsole.Markup("[OceanBlue]Hello[/]"));
        Assert.Contains("[38;5;24", output);
        Assert.Contains("Hello", output);
    }

    [Fact]
    public void Never_Markup_RendersPlainText()
    {
        var output = CaptureOut(CliColorMode.Never, () => TigerConsole.Markup("[OceanBlue]Hello[/]"));
        Assert.Equal("Hello", output);
    }

    [Fact]
    public void Standard16_Markup_NoAnsiInCapturedText()
    {
        // ConsoleSink applies colour through the Console.*Color API, so captured text has no escapes.
        var output = CaptureOut(CliColorMode.Standard16, () => TigerConsole.Markup("[OceanBlue]Hello[/]"));
        Assert.Equal("Hello", output);
    }

    [Fact]
    public void Ansi256_MarkupError_EmitsAnsiOnStderr()
    {
        var output = CaptureError(CliColorMode.Ansi256, () => TigerConsole.MarkupError("[OceanBlue]Hello[/]"));
        Assert.Contains("[38;5;24", output);
    }

    [Fact]
    public void Ansi256_RenderGrid_NoSinkOverload_EmitsAnsi()
    {
        var grid = new CliGrid(1, 1);
        grid.Set(0, 0, "Hi", new CliCellStyle(new CliCharStyle(CliColor.OceanBlue)));

        var output = CaptureOut(CliColorMode.Ansi256, () => TigerConsole.RenderGrid(grid));
        Assert.Contains("[38;5;24", output);
    }

    [Fact]
    public void Auto_RedirectedOutput_DoesNotEmitAnsi()
    {
        // Under the test runner stdout is redirected, so Auto must resolve to ConsoleSink (no escapes).
        var output = CaptureOut(CliColorMode.Auto, () => TigerConsole.Markup("[OceanBlue]Hello[/]"));
        Assert.Equal("Hello", output);
    }
}
