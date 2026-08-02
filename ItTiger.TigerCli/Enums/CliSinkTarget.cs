namespace ItTiger.TigerCli.Enums;

/// <summary>
/// What a text-writing render sink is writing to. This decides the layout bounds the sink reports
/// to the measure pass — a terminal has a width that content must wrap to, an in-memory buffer does
/// not — so it is a property of the destination, never of the colour encoding used to reach it.
/// </summary>
public enum CliSinkTarget
{
    /// <summary>
    /// The process's standard output terminal. The sink reports that terminal's width and height
    /// (see <see cref="ItTiger.TigerCli.Terminal.TerminalCapabilities.GetSafeOutputWidth"/>), so
    /// structured output wraps in the layout instead of relying on the terminal's own auto-wrap.
    /// </summary>
    Terminal,

    /// <summary>
    /// The process's standard error terminal. As <see cref="Terminal"/>, but redirection is gated on
    /// stderr rather than stdout.
    /// </summary>
    ErrorTerminal,

    /// <summary>
    /// A writer that is not a terminal — a string buffer, a file, or a capture stream. The sink
    /// reports no layout bounds, so a render stays content-driven and identical on every machine
    /// regardless of the terminal that happens to be attached.
    /// </summary>
    Buffer
}
