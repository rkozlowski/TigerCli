using System;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the live-TUI sink policy: <see cref="ConsoleTerminal"/> must resolve its render sink
/// through the same <c>ConsoleSinkFactory</c> policy as normal <see cref="TigerConsole"/> output,
/// instead of hardcoding the 16-colour <c>ConsoleSink</c>. This is what keeps 256-colour theme
/// roles (e.g. TigerBlue's Navy panel surface) faithful in live dialogs, menus, folder pickers,
/// and status rows whenever ANSI output is active.
/// </summary>
public sealed class ConsoleTerminalSinkTests
{
    private const string Esc = "\u001b";

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

    // Stream-generic sinks are wrapped so they report the terminal's layout bounds; the wrapper
    // does not change the colour encoding these tests are about.
    private static ICliRenderSink Encoding(ICliRenderSink sink) => TerminalBoundsSink.Unwrap(sink);

    [Fact]
    public void Sink_ForcedAnsi256_IsAnsiSink_NotConsoleSink()
    {
        var sink = WithColorMode(CliColorMode.Ansi256, () => new ConsoleTerminal().Sink);

        Assert.IsType<AnsiSink>(Encoding(sink));
    }

    // Every sink the factory hands out writes to a console stream, so every one of them must report
    // that stream's width to the measure pass. When the ANSI and plain sinks reported "unbounded",
    // structured output measured with no ceiling and long lines were left to the terminal's own
    // auto-wrap, which breaks mid-word and drops the layout's indentation.
    [Theory]
    [InlineData(CliColorMode.Auto)]
    [InlineData(CliColorMode.Ansi256)]
    [InlineData(CliColorMode.Standard16)]
    [InlineData(CliColorMode.Never)]
    public void Sink_ReportsTerminalWidth_ForEveryColorMode(CliColorMode mode)
    {
        var sink = WithColorMode(mode, () => new ConsoleTerminal().Sink);

        Assert.Equal(TerminalCapabilities.GetSafeOutputWidth(), sink.SoftMaxWidth);
    }

    [Fact]
    public void Sink_Standard16_IsConsoleSink()
    {
        var sink = WithColorMode(CliColorMode.Standard16, () => new ConsoleTerminal().Sink);

        Assert.IsType<ConsoleSink>(sink);
    }

    [Fact]
    public void Sink_Never_IsPlainTextWriterSink()
    {
        var sink = WithColorMode(CliColorMode.Never, () => new ConsoleTerminal().Sink);

        Assert.IsType<TextWriterSink>(Encoding(sink));
    }

    // The interactive render loop measures against Terminal.Sink and then renders via
    // Terminal.RenderGrid; both must observe the same instance so measurement and output agree.
    [Fact]
    public void Sink_IsStableWithinOneColorMode()
    {
        WithColorMode(CliColorMode.Ansi256, () =>
        {
            var terminal = new ConsoleTerminal();
            Assert.Same(terminal.Sink, terminal.Sink);
            return 0;
        });
    }

    // --color is applied per run on the process-global ColorMode; a terminal instance must follow
    // a mode change rather than serving a sink cached under the previous mode.
    [Fact]
    public void Sink_ReresolvesWhenColorModeChanges()
    {
        var original = TigerConsole.ColorMode;
        try
        {
            var terminal = new ConsoleTerminal();

            TigerConsole.ColorMode = CliColorMode.Standard16;
            Assert.IsType<ConsoleSink>(terminal.Sink);

            TigerConsole.ColorMode = CliColorMode.Ansi256;
            Assert.IsType<AnsiSink>(Encoding(terminal.Sink));
        }
        finally
        {
            TigerConsole.ColorMode = original;
        }
    }

    // ---- ANSI clear lines (dialog trim/resize/restore cleanup) ----

    [Fact]
    public void BuildAnsiClearLine_256ColorBackground_EmitsFaithfulSgrAndReset()
    {
        var line = ConsoleTerminal.BuildAnsiClearLine(CliColor.Navy, width: 4);

        Assert.Equal($"{Esc}[48;5;17m    {Esc}[0m", line);
    }

    [Fact]
    public void BuildAnsiClearLine_Standard16Background_EmitsClassicSgr()
    {
        var line = ConsoleTerminal.BuildAnsiClearLine(CliColor.DarkBlue, width: 3);

        Assert.Equal($"{Esc}[44m   {Esc}[0m", line);
    }

    [Fact]
    public void BuildAnsiClearLine_NullBackground_ClearsToTerminalDefault()
    {
        var line = ConsoleTerminal.BuildAnsiClearLine(bgColor: null, width: 2);

        Assert.Equal($"{Esc}[0m  ", line);
    }
}
