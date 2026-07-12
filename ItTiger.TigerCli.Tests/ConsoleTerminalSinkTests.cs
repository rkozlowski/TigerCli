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

    [Fact]
    public void Sink_ForcedAnsi256_IsAnsiSink_NotConsoleSink()
    {
        var sink = WithColorMode(CliColorMode.Ansi256, () => new ConsoleTerminal().Sink);

        Assert.IsType<AnsiSink>(sink);
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

        Assert.IsType<TextWriterSink>(sink);
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
            Assert.IsType<AnsiSink>(terminal.Sink);
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
