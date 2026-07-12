namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Controls how TigerCli's default console output paths emit colour. Selected globally via
/// <see cref="ItTiger.TigerCli.Terminal.TigerConsole.ColorMode"/> or per app/run via the
/// <c>--color</c> framework option. Only the default (non-TUI) output paths honour this; the
/// full-interactive TUI continues to use the legacy console sink regardless.
/// </summary>
public enum CliColorMode
{
    /// <summary>
    /// Detect terminal capability. Upgrades to <see cref="ItTiger.TigerCli.Terminal.AnsiSink"/>
    /// (faithful 256-colour) only when ANSI 256 is safely supported for the target stream;
    /// otherwise falls back to the current <c>ConsoleSink</c> behaviour. Never emits ANSI to a
    /// redirected/captured stream.
    /// </summary>
    Auto,

    /// <summary>No styling: plain text only, with no colour escape sequences or console colour changes.</summary>
    Never,

    /// <summary>Force the current <c>ConsoleSink</c> / <c>ConsoleErrorSink</c> behaviour (16 colours; 16–255 degraded).</summary>
    Standard16,

    /// <summary>
    /// Force <see cref="ItTiger.TigerCli.Terminal.AnsiSink"/> (faithful 256-colour ANSI), even when
    /// the stream is redirected. Explicit opt-in.
    /// </summary>
    Ansi256
}
