using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Terminal;

public static partial class TigerConsole
{
    private static readonly object ColorModeSync = new();
    private static CliColorMode _colorMode = CliColorMode.Auto;

    /// <summary>
    /// The process-global colour mode for TigerCli's default console output paths
    /// (<see cref="RenderGrid(Rendering.CliGrid)"/>, <see cref="Markup(string)"/>, <see cref="MarkupError(string)"/>).
    /// Defaults to <see cref="CliColorMode.Auto"/>. Like <see cref="CurrentTheme"/>, this is a
    /// process-wide setting; tests that change it should restore the previous value in a
    /// <c>finally</c>. The full-interactive TUI is unaffected and continues to use the legacy
    /// console sink.
    /// </summary>
    public static CliColorMode ColorMode
    {
        get { lock (ColorModeSync) return _colorMode; }
        set { lock (ColorModeSync) _colorMode = value; }
    }

    private static CliHyperlinkMode _hyperlinkMode = CliHyperlinkMode.Auto;

    /// <summary>
    /// The process-global OSC 8 hyperlink emission mode for TigerCli's default console output paths.
    /// Defaults to <see cref="CliHyperlinkMode.Auto"/>. Clickability is a progressive enhancement —
    /// link text is always written visibly/copyably regardless of this setting; this only controls
    /// whether an <see cref="AnsiSink"/> additionally wraps that text in OSC 8 hyperlink sequences.
    /// Like <see cref="ColorMode"/>, this is process-wide; tests that change it should restore the
    /// previous value in a <c>finally</c>.
    /// </summary>
    public static CliHyperlinkMode HyperlinkMode
    {
        get { lock (ColorModeSync) return _hyperlinkMode; }
        set { lock (ColorModeSync) _hyperlinkMode = value; }
    }
}
