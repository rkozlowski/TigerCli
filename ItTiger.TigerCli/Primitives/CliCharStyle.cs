using ItTiger.TigerCli.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItTiger.TigerCli.Primitives;


/// <summary>
/// Character-level style for foreground/background colours, text decorations, and hyperlink target.
/// </summary>
public struct CliCharStyle(
    CliColor? foreground,
    CliColor? background = null,
    CliTextDecoration decorations = CliTextDecoration.None)
{
    /// <summary>Optional foreground colour.</summary>
    public CliColor? Foreground { get; set; } = foreground;

    /// <summary>Optional background colour.</summary>
    public CliColor? Background { get; set;  } = background;

    /// <summary>
    /// Text decoration flags (bold/italic/underline). Additive during markup cascading: nested
    /// scopes OR their flags onto the effective style. Defaults to <see cref="CliTextDecoration.None"/>.
    /// </summary>
    public CliTextDecoration Decorations { get; set; } = decorations;

    /// <summary>
    /// Resolved hyperlink target (URI) for this text run, or <c>null</c> when the run is not a link.
    /// When set, an ANSI sink may wrap the visible text in an OSC 8 hyperlink (see
    /// <see cref="ItTiger.TigerCli.Terminal.AnsiSink"/>); all other sinks ignore it and render the
    /// visible text only, so link text always stays visible and copyable. The target is resolved once
    /// (from the markup span's visible text, or a hyperlink cell's full visible value) and carried on
    /// the style so it survives wrapping, splitting, truncation, and segment reassembly. Backgrounds
    /// and colours are unaffected.
    /// </summary>
    public string? HyperlinkTarget { get; set; } = null;

    /// <summary>
    /// Returns <c>true</c> when <paramref name="other"/> has the same render-relevant identity as this
    /// style: identical foreground, background, decorations, and hyperlink target. This is the single
    /// source of truth for deciding whether two adjacent <see cref="CliTextSegment"/>s may be merged /
    /// coalesced — two segments may only be merged when this returns <c>true</c>, otherwise a style
    /// difference (e.g. bold, underline, or a link target) would be silently dropped. Every field that
    /// affects rendering must be compared here; add any new render-relevant field to this method.
    /// </summary>
    public readonly bool HasSameRenderingAs(CliCharStyle other)
        => Foreground == other.Foreground
        && Background == other.Background
        && Decorations == other.Decorations
        && string.Equals(HyperlinkTarget, other.HyperlinkTarget, StringComparison.Ordinal);

    /// <summary>
    /// Creates a copy of a character style, or <c>null</c> when <paramref name="style"/> is <c>null</c>.
    /// </summary>
    public static CliCharStyle? Clone(CliCharStyle? style)
    {
        if (style == null)
            return null;

        return new CliCharStyle
        {
            Foreground = style?.Foreground,
            Background = style?.Background,
            Decorations = style?.Decorations ?? CliTextDecoration.None,
            HyperlinkTarget = style?.HyperlinkTarget
        };
    }
}
