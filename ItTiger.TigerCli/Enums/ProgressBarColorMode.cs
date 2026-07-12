namespace ItTiger.TigerCli.Enums;

/// <summary>
/// How an activity progress bar is coloured. Orthogonal to <see cref="ProgressBarStyle"/> (glyph family)
/// and <see cref="ProgressBarCaps"/> (end caps). The default is <see cref="Single"/>, so existing bars are
/// unchanged.
/// </summary>
/// <remarks>
/// In multi-colour modes the bar uses a single glyph from the chosen <see cref="ProgressBarStyle"/> for
/// every segment and distinguishes them by colour, drawn from semantic theme styles
/// (<c>ThemeStyle.ProgressBarDone</c> / <c>ProgressBarRemaining</c> / <c>ProgressBarComplete</c>) resolved
/// by the activity control. The <see cref="ThreeColor"/> "complete" colour is applied only when progress
/// is truly 100%.
/// </remarks>
public enum ProgressBarColorMode
{
    /// <summary>
    /// The framework default: one uniform theme style paints the whole strip, with distinct filled/track
    /// glyphs from the chosen <see cref="ProgressBarStyle"/> (e.g. <c>█</c>/<c>░</c>). Unchanged behaviour.
    /// </summary>
    Single,

    /// <summary>
    /// Two colours: the done portion uses the "done" theme style and the remainder the "remaining" style;
    /// both use the same glyph so they are distinguished by colour alone.
    /// </summary>
    TwoColor,

    /// <summary>
    /// Three colours: done / remaining as in <see cref="TwoColor"/>, plus a distinct "complete" colour that
    /// recolours the filled portion only when progress reaches exactly 100%.
    /// </summary>
    ThreeColor,
}
