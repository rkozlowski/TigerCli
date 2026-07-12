using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ItTiger.TigerCli.Markup
{
    /// <summary>
    /// Theme-backed <see cref="IMarkupStyleResolver"/> exposing the curated framework semantic markup
    /// tokens plus, optionally, an app's registered custom semantic styles. Each framework token maps
    /// to a <see cref="ThemeStyle"/> and a channel mode; colours come from
    /// <see cref="ITheme.Resolve(ThemeStyle)"/>, so the same markup renders differently per theme. Only
    /// the curated subset is exposed — table-only ink/surface tokens are intentionally absent. Custom
    /// styles (<see cref="CliCustomStyle"/>) resolve through their base <see cref="ThemeStyle"/> with
    /// optional family/theme overrides; their names can never collide with framework tokens (the custom
    /// style registry rejects such names), so framework tokens are matched first.
    /// </summary>
    public sealed class ThemeMarkupStyleResolver : IMarkupStyleResolver
    {
        private enum Channel
        {
            /// <summary>Foreground only; background returned as <c>null</c> (inherits parent).</summary>
            Foreground,

            /// <summary>Both foreground and background taken from the resolved token.</summary>
            ForegroundBackground,

            /// <summary>Background from a surface token; foreground falls back to the Text ink.</summary>
            Surface
        }

        // Curated Phase 1 markup tokens. Names resolve case-insensitively.
        private static readonly Dictionary<string, (ThemeStyle Style, Channel Channel)> Tokens =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Foreground-only accents: background stays null so the parent background is preserved.
                ["Accent"] = (ThemeStyle.Accent, Channel.Foreground),
                ["Muted"] = (ThemeStyle.MutedText, Channel.Foreground),
                ["Success"] = (ThemeStyle.Success, Channel.Foreground),
                ["Warning"] = (ThemeStyle.Warning, Channel.Foreground),
                ["Error"] = (ThemeStyle.Error, Channel.Foreground),

                // Semantic CRUD/structured-output roles. Foreground-only; any decorations the resolved
                // theme style carries (Heading bold, Link underline) flow through. The name is
                // framework-known but the visual meaning stays theme/developer-configurable.
                ["Heading"] = (ThemeStyle.Heading, Channel.Foreground),
                ["Key"] = (ThemeStyle.Key, Channel.Foreground),
                ["Value"] = (ThemeStyle.Value, Channel.Foreground),
                ["Path"] = (ThemeStyle.Path, Channel.Foreground),
                ["Link"] = (ThemeStyle.Link, Channel.Foreground),

                // Combined foreground/background ink tokens.
                ["Selected"] = (ThemeStyle.Selected, Channel.ForegroundBackground),
                ["Alert"] = (ThemeStyle.Alert, Channel.ForegroundBackground),

                // Surface-backed tokens: background from the surface, foreground falls back to the
                // Text ink when the surface defines only a background. [Panel] uses PanelSurface;
                // [Dialog] uses DialogSurface (which itself falls back to PanelSurface in the theme).
                ["Panel"] = (ThemeStyle.PanelSurface, Channel.Surface),
                ["Dialog"] = (ThemeStyle.DialogSurface, Channel.Surface),
            };

        // Framework semantic tokens whose visible span text is also the hyperlink target. The parser
        // derives the target from the span and an ANSI sink may emit an OSC 8 hyperlink.
        private static readonly HashSet<string> HyperlinkTokens =
            new(StringComparer.OrdinalIgnoreCase) { "Link" };

        /// <summary>The framework semantic token names (case-insensitive). Snapshot.</summary>
        public static IReadOnlyCollection<string> FrameworkTokenNames => Tokens.Keys.ToArray();

        /// <summary>True when <paramref name="name"/> is a curated framework semantic token (case-insensitive).</summary>
        public static bool IsFrameworkToken(string? name)
            => name is not null && Tokens.ContainsKey(name.Trim());

        /// <inheritdoc />
        public bool IsHyperlinkToken(string name)
            => name is not null && HyperlinkTokens.Contains(name.Trim());

        private readonly ITheme _theme;
        private readonly TigerCustomStyleRegistry? _customStyles;

        /// <summary>Creates a resolver for framework semantic tokens using the supplied theme.</summary>
        /// <param name="theme">The theme used to resolve semantic styles.</param>
        public ThemeMarkupStyleResolver(ITheme theme)
            : this(theme, customStyles: null)
        {
        }

        /// <summary>
        /// Creates a resolver that resolves framework tokens through <paramref name="theme"/> and, when
        /// supplied, an app's registered custom semantic styles through the same theme.
        /// </summary>
        public ThemeMarkupStyleResolver(ITheme theme, TigerCustomStyleRegistry? customStyles)
        {
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _customStyles = customStyles;
        }

        /// <inheritdoc/>
        public bool TryResolve(
            string name,
            out CliColor? foreground,
            out CliColor? background,
            out CliTextDecoration decorations)
        {
            foreground = null;
            background = null;
            decorations = CliTextDecoration.None;

            if (name is null)
                return false;

            if (!Tokens.TryGetValue(name.Trim(), out var token))
                return TryResolveCustomStyle(name, out foreground, out background, out decorations);

            var charStyle = _theme.Resolve(token.Style).CharStyle;

            // A semantic style may define its own decorations; they OR onto the surrounding style.
            decorations = charStyle?.Decorations ?? CliTextDecoration.None;

            switch (token.Channel)
            {
                case Channel.Foreground:
                    foreground = charStyle?.Foreground;
                    background = null;
                    break;

                case Channel.ForegroundBackground:
                    foreground = charStyle?.Foreground;
                    background = charStyle?.Background;
                    break;

                case Channel.Surface:
                    background = charStyle?.Background;
                    foreground = charStyle?.Foreground
                        ?? _theme.Resolve(ThemeStyle.Text).CharStyle?.Foreground;
                    break;
            }

            return true;
        }

        // Custom styles carry whatever foreground/background/decorations their resolved style defines.
        // A foreground-only custom style (e.g. base ThemeStyle.Accent) leaves the background null, so
        // the surrounding background is preserved — just like the framework foreground-only tokens.
        private bool TryResolveCustomStyle(
            string name,
            out CliColor? foreground,
            out CliColor? background,
            out CliTextDecoration decorations)
        {
            foreground = null;
            background = null;
            decorations = CliTextDecoration.None;

            if (_customStyles is null || !_customStyles.TryGet(name, out var custom))
                return false;

            var charStyle = custom.Resolve(_theme);
            foreground = charStyle?.Foreground;
            background = charStyle?.Background;
            decorations = charStyle?.Decorations ?? CliTextDecoration.None;
            return true;
        }
    }
}
