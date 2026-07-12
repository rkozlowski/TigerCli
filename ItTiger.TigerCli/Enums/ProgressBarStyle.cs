namespace ItTiger.TigerCli.Enums;

/// <summary>
/// A predefined progress-bar glyph appearance for an activity dialog's progress-bar element. Selects the
/// filled/track glyph pair the bar is drawn with; the colour stays uniform — a single theme style paints
/// the whole strip, exactly like the existing default bar. Predefined styles give adjacent or stacked bars
/// a consistent, less-noisy look. The default keeps the original <c>█</c>/<c>░</c> appearance, so unset
/// callers are unaffected.
/// </summary>
/// <remarks>
/// This enum covers <em>glyph families only</em>. End caps/brackets are an orthogonal decoration that
/// composes with any style — see <see cref="ProgressBarCaps"/> — so they are not members here. All current
/// styles are single-colour; multi-colour line styles (done / not-done / a recoloured bar at 100%) are
/// intended future additions that need a per-character-styled overlay and are not part of this enum yet.
/// </remarks>
public enum ProgressBarStyle
{
    /// <summary>The framework default: <c>█</c> filled, <c>░</c> track. Unchanged behaviour.</summary>
    Default,

    /// <summary>Heavy horizontal line: <c>━</c> filled, <c>─</c> track.</summary>
    Line,

    /// <summary>Squares: <c>■</c> filled, <c>□</c> track.</summary>
    Square,

    /// <summary>Vertical rectangles: <c>▮</c> filled, <c>▯</c> track.</summary>
    VerticalBar,

    /// <summary>Parallelogram dashes: <c>▰</c> filled, <c>▱</c> track.</summary>
    Dash,
}
