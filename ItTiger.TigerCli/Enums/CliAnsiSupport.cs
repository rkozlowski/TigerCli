namespace ItTiger.TigerCli.Enums;

/// <summary>
/// The level of ANSI support detected for a console stream by
/// <see cref="ItTiger.TigerCli.Terminal.TerminalCapabilities"/>. Used by the sink factory to decide
/// whether <see cref="CliColorMode.Auto"/> may upgrade to an ANSI sink.
/// </summary>
public enum CliAnsiSupport
{
    /// <summary>No ANSI upgrade is safe (redirected, NO_COLOR, dumb terminal, or unprobeable Windows console).</summary>
    None,

    /// <summary>ANSI escape sequences are supported, but only the standard 16 colours are assumed.</summary>
    Ansi16,

    /// <summary>Faithful ANSI 256-colour output is safely supported.</summary>
    Ansi256
}
