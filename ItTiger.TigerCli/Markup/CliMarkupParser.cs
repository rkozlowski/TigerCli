using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using System;
using System.Collections.Generic;
using System.Text;

namespace ItTiger.TigerCli.Markup
{
    /// <summary>
    /// Parses TigerCli bracket markup into styled text segments.
    /// Mirrors TigerConsole.Markup semantics but produces data instead of writing to the console.
    /// </summary>
    public static class CliMarkupParser
    {
        /// <summary>
        /// Parse markup into a flat list of segments. The base style is used as the starting style
        /// (foreground/background). Nested tags override those selectively and are properly unwound by [/].
        /// When <paramref name="styles"/> is supplied, a simple single-name tag is first resolved as a
        /// curated semantic token (e.g. <c>[Accent]</c>, <c>[Alert]</c>); semantic names take
        /// precedence over raw colour names. When it is <c>null</c>, behaviour is unchanged (raw
        /// colours only). When <paramref name="colorAliases"/> is supplied, raw colour positions
        /// (foreground and the background after <c>on</c>) first consult registered colour aliases,
        /// which take precedence over <see cref="CliColor"/> enum names; semantic style names are never
        /// accepted in raw colour positions. The parser stays pure — it never reads a theme, an alias
        /// table, or global console state; both resolvers are injected by the call site.
        /// </summary>
        public static List<CliTextSegment> Parse(
            string markup,
            CliCharStyle? baseStyle = null,
            IMarkupStyleResolver? styles = null,
            IColorAliasResolver? colorAliases = null)
        {
            var segments = new List<CliTextSegment>();
            if (markup is null)
                return segments;

            // Starting style (don’t assume Console colors in a pure parser).
            var current = baseStyle ?? new CliCharStyle(null, null);
            var styleStack = new Stack<CliCharStyle>();
            var buffer = new StringBuilder();

            // Local helpers
            void Flush()
            {
                if (buffer.Length == 0) return;
                segments.Add(new CliTextSegment(buffer.ToString(), current));
                buffer.Clear();
            }

            // Applies a parsed tag onto the surrounding style: foreground/background replace when
            // supplied; decorations are additive (OR onto the current effective decorations); the
            // hyperlink target replaces when supplied and otherwise inherits, so non-link styling
            // nested inside a [Link] span keeps the surrounding link target.
            static CliCharStyle Apply(CliCharStyle src, CliColor? fg, CliColor? bg, CliTextDecoration deco,
                string? hyperlinkTarget = null)
                => new CliCharStyle(
                    fg ?? src.Foreground,
                    bg ?? src.Background,
                    src.Decorations | deco)
                {
                    HyperlinkTarget = hyperlinkTarget ?? src.HyperlinkTarget
                };

            (bool isClose, CliColor? fg, CliColor? bg, CliTextDecoration deco, bool isHyperlink) ParseTag(string tag)
            {
                tag = tag.Trim();

                // Closing tag
                if (tag == "/")
                    return (true, null, null, CliTextDecoration.None, false);

                // Tokenize the tag body by whitespace.
                var tokens = tag.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    throw new FormatException("Empty tag.");

                // Standalone short decoration aliases: [b]/[i]/[u] (case-insensitive) map to
                // Bold/Italic/Underline. These are recognized ONLY as the sole token in a tag — they
                // are intentionally NOT valid inside composed visual expressions (e.g. "[b red on blue]"
                // is invalid), so they are handled here, before raw parsing, and are absent from the
                // composed-expression decoration parser (TryParseDecoration).
                if (tokens.Length == 1 && TryParseShortDecorationAlias(tokens[0], out var aliasDeco))
                    return (false, null, null, aliasDeco, false);

                // Form 1 — semantic style tag: a curated semantic token must be the *only* token in
                // the tag (e.g. [Accent], [Panel]). Resolved before raw colour parsing, so semantic
                // names win on any collision with a raw colour name. A semantic style may carry its
                // own decorations; foreground-only tokens return a null background, which Apply
                // leaves untouched — preserving the surrounding background. A hyperlink token (e.g.
                // [Link]) is flagged so the main loop can derive its target from the span's text.
                if (tokens.Length == 1 && styles is not null
                    && styles.TryResolve(tokens[0], out var semFg, out var semBg, out var semDeco))
                {
                    return (false, semFg, semBg, semDeco, styles.IsHyperlinkToken(tokens[0]));
                }

                // Form 2 — raw style expression:
                //   [<decoration>* <foreground>? (on <background>)?]
                // Leading decoration tokens, then an optional foreground colour, then an optional
                // "on <background>". Semantic style names are never valid here.
                return ParseRawStyleExpression(tokens, tag);
            }

            (bool isClose, CliColor? fg, CliColor? bg, CliTextDecoration deco, bool isHyperlink)
                ParseRawStyleExpression(string[] tokens, string tag)
            {
                var deco = CliTextDecoration.None;
                CliColor? fg = null, bg = null;
                int i = 0;

                // Leading decorations (order unimportant); duplicates simply OR back the same flag.
                while (i < tokens.Length && TryParseDecoration(tokens[i], out var d))
                {
                    deco |= d;
                    i++;
                }

                // Optional foreground colour (unless the next token introduces a background via "on").
                if (i < tokens.Length && !IsOn(tokens[i]))
                {
                    if (!TryParseColor(tokens[i], out var fgParsed))
                        throw new FormatException($"Invalid style expression: '{tag}'.");
                    fg = fgParsed;
                    i++;
                }

                // Optional "on <background>".
                if (i < tokens.Length && IsOn(tokens[i]))
                {
                    i++;
                    if (i >= tokens.Length)
                        throw new FormatException($"Missing background color after 'on' in: '{tag}'.");
                    if (!TryParseColor(tokens[i], out var bgParsed))
                        throw new FormatException($"Unknown background color: '{tokens[i]}'.");
                    bg = bgParsed;
                    i++;
                }

                // Any remaining tokens are malformed (e.g. a decoration after a colour, as in
                // "[Yellow Bold]"): decorations must precede colours and a semantic name cannot be
                // mixed into a raw expression.
                if (i < tokens.Length)
                    throw new FormatException($"Invalid style expression: '{tag}'.");

                return (false, fg, bg, deco, false);
            }

            // Raw colour resolution: a registered colour alias wins over a CliColor enum name (the alias
            // is deliberate app policy), then the enum name is the fallback. Semantic style names never
            // reach here — they are handled as single-token tags before raw parsing.
            bool TryParseColor(string token, out CliColor color)
            {
                if (colorAliases is not null && colorAliases.TryResolve(token, out color))
                    return true;
                return CliColorMapper.TryParse(token, out color);
            }

            static bool IsOn(string token)
                => string.Equals(token, "on", StringComparison.OrdinalIgnoreCase);

            static bool TryParseDecoration(string token, out CliTextDecoration decoration)
            {
                switch (token.ToLowerInvariant())
                {
                    case "bold": decoration = CliTextDecoration.Bold; return true;
                    case "italic": decoration = CliTextDecoration.Italic; return true;
                    case "underline": decoration = CliTextDecoration.Underline; return true;
                    default: decoration = CliTextDecoration.None; return false;
                }
            }

            // Short decoration aliases recognized only as a standalone single-token tag (see ParseTag).
            // Deliberately separate from TryParseDecoration so they never participate in composed
            // visual expressions such as "[b red on blue]".
            static bool TryParseShortDecorationAlias(string token, out CliTextDecoration decoration)
            {
                switch (token.ToLowerInvariant())
                {
                    case "b": decoration = CliTextDecoration.Bold; return true;
                    case "i": decoration = CliTextDecoration.Italic; return true;
                    case "u": decoration = CliTextDecoration.Underline; return true;
                    default: decoration = CliTextDecoration.None; return false;
                }
            }

            // Parallel to styleStack: whether each open scope is a hyperlink scope, so closing tags
            // can release a link and nesting can be rejected. activeHyperlinks counts open link scopes.
            var scopeIsHyperlink = new Stack<bool>();
            int activeHyperlinks = 0;

            int i = 0;
            while (i < markup.Length)
            {
                char ch = markup[i];

                if (ch == '[')
                {
                    // Escape: [[ -> literal '['
                    if (i + 1 < markup.Length && markup[i + 1] == '[')
                    {
                        buffer.Append('[');
                        i += 2;
                        continue;
                    }

                    // Find closing bracket
                    int end = markup.IndexOf(']', i + 1);
                    if (end == -1)
                        throw new FormatException("Unclosed tag.");

                    // Flush text collected under the previous style
                    Flush();

                    var tag = markup.Substring(i + 1, end - i - 1);
                    var (isClose, fg, bg, deco, isHyperlink) = ParseTag(tag);

                    if (isClose)
                    {
                        if (styleStack.Count == 0)
                            throw new FormatException("Mismatched closing tag.");
                        current = styleStack.Pop(); // restore previous style
                        if (scopeIsHyperlink.Pop())
                            activeHyperlinks--;
                    }
                    else
                    {
                        string? hyperlinkTarget = null;
                        if (isHyperlink)
                        {
                            // Nested hyperlinks are not supported (e.g. [Link]a[Link]b[/][/]).
                            if (activeHyperlinks > 0)
                                throw new FormatException("Nested [Link] is not supported.");

                            // Derive the target from the span's visible text (markup stripped, escapes
                            // applied). Whitespace-only spans carry no target.
                            var visible = ExtractLinkVisibleText(markup, end + 1);
                            hyperlinkTarget = string.IsNullOrWhiteSpace(visible) ? null : visible;
                            activeHyperlinks++;
                        }

                        // Push current, then apply overrides
                        styleStack.Push(current);
                        scopeIsHyperlink.Push(isHyperlink);
                        current = Apply(current, fg, bg, deco, hyperlinkTarget);
                    }

                    i = end + 1;
                }
                else if (ch == ']' && i + 1 < markup.Length && markup[i + 1] == ']')
                {
                    // Escape: ]] -> literal ']'
                    buffer.Append(']');
                    i += 2;
                }
                else
                {
                    buffer.Append(ch);
                    i++;
                }
            }

            // Flush trailing text
            Flush();

            if (styleStack.Count > 0)
                throw new FormatException("Unclosed tag(s) in markup.");

            return segments;
        }

        /// <summary>
        /// Escapes markup-significant brackets in raw text so it can be inserted into TigerCli
        /// markup as literal text. This method does not perform localization.
        /// </summary>
        public static string Escape(string text)
        {
            return text.Replace("[", "[[").Replace("]", "]]");
        }

        /// <summary>
        /// Returns the plain visible text of the markup scope beginning at <paramref name="index"/>
        /// (the character after a tag's closing <c>]</c>), up to but not including the scope's matching
        /// <c>[/]</c>. Mirrors the main loop's text handling: <c>[[</c> → <c>[</c>, <c>]]</c> → <c>]</c>,
        /// and nested tags contribute only their visible text (markup stripped). Used to derive a
        /// hyperlink target from a <c>[Link]…[/]</c> span.
        /// </summary>
        private static string ExtractLinkVisibleText(string markup, int index)
        {
            var sb = new StringBuilder();
            int depth = 0;
            int i = index;

            while (i < markup.Length)
            {
                char ch = markup[i];

                if (ch == '[')
                {
                    if (i + 1 < markup.Length && markup[i + 1] == '[')
                    {
                        sb.Append('[');
                        i += 2;
                        continue;
                    }

                    int end = markup.IndexOf(']', i + 1);
                    if (end == -1)
                        throw new FormatException("Unclosed tag.");

                    var inner = markup.Substring(i + 1, end - i - 1).Trim();
                    if (inner == "/")
                    {
                        if (depth == 0)
                            return sb.ToString(); // matching close of the link scope
                        depth--;
                    }
                    else
                    {
                        depth++;
                    }

                    i = end + 1;
                    continue;
                }

                if (ch == ']' && i + 1 < markup.Length && markup[i + 1] == ']')
                {
                    sb.Append(']');
                    i += 2;
                    continue;
                }

                sb.Append(ch);
                i++;
            }

            throw new FormatException("Unclosed tag(s) in markup.");
        }
    }
}
