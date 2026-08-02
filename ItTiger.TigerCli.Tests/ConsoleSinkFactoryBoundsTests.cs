using System;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the rule that layout bounds belong to the destination, not to the colour encoding: every
/// sink <c>ConsoleSinkFactory</c> hands out writes to a console stream, so every one of them reports
/// that stream's width to the measure pass — stdout and stderr, in every colour mode.
/// </summary>
/// <remarks>
/// The ANSI path is the one that regressed in practice. <c>Auto</c> resolves to <see cref="AnsiSink"/>
/// on any modern terminal, and while that sink reported "unbounded" the help document (and every
/// other structured render) was measured with no ceiling, so long lines reached the terminal intact
/// and its own auto-wrap broke them mid-word with no indentation on the continuation line.
/// </remarks>
public sealed class ConsoleSinkFactoryBoundsTests
{
    private static T WithColorMode<T>(CliColorMode mode, Func<T> body)
    {
        var original = TigerConsole.ColorMode;
        try
        {
            TigerConsole.ColorMode = mode;
            return body();
        }
        finally
        {
            TigerConsole.ColorMode = original;
        }
    }

    [Theory]
    [InlineData(CliColorMode.Auto)]
    [InlineData(CliColorMode.Ansi256)]
    [InlineData(CliColorMode.Standard16)]
    [InlineData(CliColorMode.Never)]
    public void OutputSink_ReportsStdoutTerminalWidth(CliColorMode mode)
    {
        var sink = WithColorMode(mode, ConsoleSinkFactory.CreateOutputSink);

        Assert.Equal(TerminalCapabilities.GetSafeOutputWidth(), sink.SoftMaxWidth);
    }

    [Theory]
    [InlineData(CliColorMode.Auto)]
    [InlineData(CliColorMode.Ansi256)]
    [InlineData(CliColorMode.Standard16)]
    [InlineData(CliColorMode.Never)]
    public void ErrorSink_ReportsStderrTerminalWidth(CliColorMode mode)
    {
        var sink = WithColorMode(mode, ConsoleSinkFactory.CreateErrorSink);

        Assert.Equal(TerminalCapabilities.GetSafeOutputWidth(forError: true), sink.SoftMaxWidth);
    }

    [Theory]
    [InlineData(CliColorMode.Auto)]
    [InlineData(CliColorMode.Ansi256)]
    [InlineData(CliColorMode.Standard16)]
    [InlineData(CliColorMode.Never)]
    public void TerminalSink_ReportsStdoutTerminalWidth(CliColorMode mode)
    {
        var sink = WithColorMode(mode, ConsoleSinkFactory.CreateTerminalSink);

        Assert.Equal(TerminalCapabilities.GetSafeOutputWidth(), sink.SoftMaxWidth);
    }

    // Forced ANSI is the path a caller reaches with --color 256; the sink itself is bounded, so the
    // factory needs no wrapper to make it behave like a terminal sink.
    [Fact]
    public void ForcedAnsiOutputSink_IsABoundedAnsiSink()
    {
        var sink = WithColorMode(CliColorMode.Ansi256, ConsoleSinkFactory.CreateOutputSink);

        var ansi = Assert.IsType<AnsiSink>(sink);
        Assert.Equal(CliSinkTarget.Terminal, ansi.Target);
        Assert.Equal(TerminalCapabilities.GetSafeOutputWidth(), ansi.SoftMaxWidth);
    }

    [Fact]
    public void ForcedAnsiErrorSink_IsABoundedAnsiSinkOnTheErrorStream()
    {
        var sink = WithColorMode(CliColorMode.Ansi256, ConsoleSinkFactory.CreateErrorSink);

        var ansi = Assert.IsType<AnsiSink>(sink);
        Assert.Equal(CliSinkTarget.ErrorTerminal, ansi.Target);
        Assert.Equal(TerminalCapabilities.GetSafeOutputWidth(forError: true), ansi.SoftMaxWidth);
    }

    // The colour-disabled console path writes to a terminal too, so it wraps like the coloured ones.
    [Fact]
    public void NoColorOutputSink_IsATerminalTargetedTextWriterSink()
    {
        var sink = WithColorMode(CliColorMode.Never, ConsoleSinkFactory.CreateOutputSink);

        Assert.IsType<TextWriterSink>(sink);
        Assert.Equal(TerminalCapabilities.GetSafeOutputWidth(), sink.SoftMaxWidth);
    }

    // In-memory ANSI helpers are not terminal renders: they must stay content-driven so generated
    // docs and snapshots do not depend on the terminal the generator happened to run in.
    [Fact]
    public void RenderGridToAnsi_IsNotBoundedByTheAmbientTerminal()
    {
        var grid = new Rendering.CliGrid(1, 1);
        grid.Set(0, 0, new string('x', TerminalCapabilities.GetSafeOutputWidth() + 40));

        var ansi = TigerConsole.RenderGridToAnsi(grid);

        Assert.Contains(new string('x', TerminalCapabilities.GetSafeOutputWidth() + 40), ansi, StringComparison.Ordinal);
    }
}
