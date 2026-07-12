using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Markup
{
    /// <summary>
    /// Resolves a curated semantic markup token (e.g. <c>Accent</c>, <c>Alert</c>) to concrete
    /// foreground/background colours. Keeps <see cref="CliMarkupParser"/> pure: the parser depends
    /// only on this abstraction, never on a theme or global console state.
    /// </summary>
    public interface IMarkupStyleResolver
    {
        /// <summary>
        /// Tries to resolve <paramref name="name"/> (case-insensitive) to a semantic style. Returns
        /// <c>false</c> for names that are not curated semantic tokens, so the parser can fall back
        /// to raw colour parsing. A <c>null</c> channel means "inherit": foreground-only tokens
        /// return a <c>null</c> background so the surrounding background is preserved.
        /// <paramref name="decorations"/> carries any bold/italic/underline flags the semantic style
        /// defines; <see cref="CliTextDecoration.None"/> when it defines none. The parser ORs these
        /// onto the surrounding effective decorations.
        /// </summary>
        bool TryResolve(
            string name,
            out CliColor? foreground,
            out CliColor? background,
            out CliTextDecoration decorations);

        /// <summary>
        /// Returns <c>true</c> when <paramref name="name"/> (case-insensitive) is a hyperlink token —
        /// a semantic style whose span's visible text is also its hyperlink target (e.g. <c>Link</c>).
        /// When the parser sees such a token it derives the target from the span's visible text and
        /// rejects nesting another hyperlink token inside it. Defaults to <c>false</c> so existing
        /// resolvers need no change; only link-aware resolvers override it.
        /// </summary>
        bool IsHyperlinkToken(string name) => false;
    }
}
