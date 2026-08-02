using System;
using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// Chooses the concrete <see cref="ICliRenderSink"/> for TigerCli's default console output paths
/// based on <see cref="TigerConsole.ColorMode"/> and, for <see cref="CliColorMode.Auto"/>, the
/// detected <see cref="TerminalCapabilities"/>. stdout and stderr are decided independently. Sinks
/// are created fresh on each call and read <see cref="Console.Out"/> / <see cref="Console.Error"/>
/// at construction, so a redirected stream (e.g. a test's <c>StringWriter</c>) is honoured.
/// </summary>
internal static class ConsoleSinkFactory
{
    /// <summary>Creates the sink for standard output under the current colour mode.</summary>
    public static ICliRenderSink CreateOutputSink() => Create(TigerConsole.ColorMode, isError: false);

    /// <summary>Creates the sink for standard error under the current colour mode.</summary>
    public static ICliRenderSink CreateErrorSink() => Create(TigerConsole.ColorMode, isError: true);

    /// <summary>
    /// Creates the sink for the live interactive terminal (<see cref="ConsoleTerminal"/>). Uses the
    /// same stdout policy as <see cref="CreateOutputSink"/>, so live TUI dialogs/controls render
    /// through the same effective colour path (ANSI 256 / 16-colour / plain) as normal
    /// <see cref="TigerConsole"/> output.
    /// </summary>
    public static ICliRenderSink CreateTerminalSink() => Create(TigerConsole.ColorMode, isError: false);

    // Every sink returned here writes to a console stream, so every one of them reports that
    // stream's layout bounds to the measure pass. AnsiSink and TextWriterSink are stream-generic
    // and carry no bounds of their own, so they are wrapped; ConsoleSink/ConsoleErrorSink already
    // read the terminal directly. Colour encoding must not decide whether output wraps.
    private static ICliRenderSink Create(CliColorMode mode, bool isError)
    {
        switch (mode)
        {
            case CliColorMode.Never:
                return WithTerminalBounds(new TextWriterSink(isError ? Console.Error : Console.Out), isError);

            case CliColorMode.Ansi256:
                // ANSI is forced (possibly to a redirected stream), not capability-detected.
                return WithTerminalBounds(
                    new AnsiSink(
                        isError ? Console.Error : Console.Out,
                        EmitHyperlinks(capabilityDetected: false),
                        emitTerminalControls: !IsRedirected(isError)),
                    isError);

            case CliColorMode.Standard16:
                return isError ? new ConsoleErrorSink() : new ConsoleSink();

            case CliColorMode.Auto:
            default:
                var support = isError ? TerminalCapabilities.ForStderr() : TerminalCapabilities.ForStdout();
                if (support == CliAnsiSupport.Ansi256)
                    return WithTerminalBounds(
                        new AnsiSink(
                            isError ? Console.Error : Console.Out,
                            EmitHyperlinks(capabilityDetected: true),
                            emitTerminalControls: true),
                        isError);
                return isError ? new ConsoleErrorSink() : new ConsoleSink();
        }
    }

    private static ICliRenderSink WithTerminalBounds(ICliRenderSink sink, bool isError)
        => new TerminalBoundsSink(sink, isError);

    // Decides OSC 8 hyperlink emission for an ANSI sink. Always => on; Never => off; Auto => on only
    // when ANSI was capability-detected (a real terminal), not when forced (which may be redirected /
    // uncertain), matching "prefer visible styled text without escape sequences when support is uncertain."
    private static bool EmitHyperlinks(bool capabilityDetected) => TigerConsole.HyperlinkMode switch
    {
        CliHyperlinkMode.Never => false,
        CliHyperlinkMode.Always => true,
        _ => capabilityDetected,
    };

    private static bool IsRedirected(bool isError) =>
        isError ? Console.IsErrorRedirected : Console.IsOutputRedirected;
}
