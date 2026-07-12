namespace ItTiger.TigerCli.Enums;

/// <summary>
/// A semantic accent role a table style recipe (see <see cref="Rendering.CliTableStyleRecipe"/>)
/// references for foreground-only elements such as the title or frame. The theme supplies the
/// actual colour; the recipe never hard-codes one.
/// </summary>
public enum TableAccent
{
    /// <summary>Use the theme's default ink for the element (e.g. the default title/frame foreground).</summary>
    Default,

    /// <summary>The theme's primary accent (<see cref="ThemeStyle.Accent"/>).</summary>
    Accent,

    /// <summary>The theme's success accent (<see cref="ThemeStyle.Success"/>).</summary>
    Success,

    /// <summary>The theme's warning accent (<see cref="ThemeStyle.Warning"/>).</summary>
    Warning
}
