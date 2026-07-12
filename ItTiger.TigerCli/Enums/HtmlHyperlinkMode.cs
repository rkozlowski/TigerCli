namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Controls how <see cref="ItTiger.TigerCli.Terminal.HtmlSink"/> renders a text run that carries a
/// <see cref="ItTiger.TigerCli.Primitives.CliCharStyle.HyperlinkTarget"/>. In both modes the visible
/// link text is always emitted unchanged (visible/copyable first, matching the ANSI link philosophy);
/// the mode only decides whether an <c>&lt;a href&gt;</c> is additionally emitted.
/// </summary>
public enum HtmlHyperlinkMode
{
    /// <summary>
    /// Render the link text visibly as styled text only (link classes/styles); never emit
    /// <c>&lt;a href&gt;</c>. The conservative, documentation-friendly default.
    /// </summary>
    Text,

    /// <summary>
    /// When a non-empty, safe <c>HyperlinkTarget</c> is present, wrap the visible text in
    /// <c>&lt;a href="…"&gt;</c> (the <c>href</c> is sanitized and attribute-escaped). An empty or
    /// unsafe target falls back to <see cref="Text"/> rendering.
    /// </summary>
    Anchor
}
