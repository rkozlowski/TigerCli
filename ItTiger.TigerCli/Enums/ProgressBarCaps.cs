namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Optional end-cap decoration for an activity progress bar. Caps are orthogonal to the bar's glyph
/// <see cref="ProgressBarStyle"/>: any style can be drawn with or without caps. When caps are present they
/// occupy the bar's outer cell(s) and the chosen glyph style fills the interior; caps share the bar's
/// (base) colour. The default is <see cref="None"/>, so unset callers get an uncapped, full-width bar.
/// </summary>
public enum ProgressBarCaps
{
    /// <summary>No end caps; the bar fills the full width. The framework default.</summary>
    None,

    /// <summary>Square brackets: <c>[</c> at the left end and <c>]</c> at the right end.</summary>
    Brackets,
}
