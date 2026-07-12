namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Controls whether TigerCli's ANSI output emits OSC 8 clickable hyperlinks for text that carries a
/// resolved hyperlink target (e.g. <c>[Link]…[/]</c> markup, or <c>CliDetails.AddLink</c> /
/// <c>CliList.AddLinkColumn</c> values). Clickability is a progressive enhancement only — the link
/// text is always written visibly and copyably regardless of this setting. Selected via
/// <see cref="ItTiger.TigerCli.Terminal.TigerConsole.HyperlinkMode"/>.
/// </summary>
public enum CliHyperlinkMode
{
    /// <summary>
    /// Emit OSC 8 hyperlinks only when ANSI support was capability-detected for the stream (the
    /// <see cref="CliColorMode.Auto"/> path resolved to an ANSI sink). When ANSI is force-enabled or
    /// support is otherwise uncertain, no hyperlink escape sequences are emitted — only visible text.
    /// </summary>
    Auto,

    /// <summary>Never emit OSC 8 hyperlinks; render link text visibly without escape sequences.</summary>
    Never,

    /// <summary>Emit OSC 8 hyperlinks whenever an <see cref="ItTiger.TigerCli.Terminal.AnsiSink"/> is used.</summary>
    Always
}
