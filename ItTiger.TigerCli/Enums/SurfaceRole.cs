namespace ItTiger.TigerCli.Enums;

/// <summary>
/// A reusable UI surface family resolved by an <see cref="Tui.Abstractions.ITheme"/> into concrete
/// colours (see <see cref="Primitives.SurfaceColors"/>). Surfaces are general UI concepts, not
/// table-specific: a table style, a dialog, or any other component can sit on the same surface.
/// </summary>
public enum SurfaceRole
{
    /// <summary>The base console/app surface (the theme's background).</summary>
    Default,

    /// <summary>An elevated/panel surface. Also the default base for <see cref="ThemeStyle.DialogSurface"/>.</summary>
    Panel,

    /// <summary>An attention/warning surface (used by the Palermo table style).</summary>
    Alert
}
