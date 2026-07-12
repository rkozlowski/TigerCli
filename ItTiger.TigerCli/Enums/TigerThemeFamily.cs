namespace ItTiger.TigerCli.Enums;

/// <summary>
/// The contrast family a theme belongs to. TigerCli classifies every theme as either dark-family or
/// light-family so that custom semantic styles can supply a single dark/light override pair instead
/// of one override per concrete theme. A theme's family is metadata, not inheritance: sealed themes
/// declare their family directly (see <c>ITheme.Family</c>).
/// </summary>
public enum TigerThemeFamily
{
    /// <summary>Dark-background family (e.g. the framework <c>dark</c> and <c>tiger-blue</c> themes).</summary>
    Dark,

    /// <summary>Light-background family (e.g. the framework <c>light</c> theme).</summary>
    Light
}
