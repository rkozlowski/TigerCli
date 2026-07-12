namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Selects a TigerCli UI theme. <see cref="Default"/> means "use the current theme"
/// (<see cref="Terminal.TigerConsole.CurrentTheme"/>); the others name a built-in framework theme.
/// <see cref="Custom"/> indicates a named custom theme and is not resolvable on its own — it needs
/// the theme's name (see <c>TigerConsole.GetTheme(string)</c>).
/// </summary>
public enum TigerCliTheme
{
    /// <summary>Use <see cref="Terminal.TigerConsole.CurrentTheme"/>; not a separate theme instance.</summary>
    Default = 0,

    /// <summary>Built-in dark theme (<c>"dark"</c>).</summary>
    Dark,

    /// <summary>Built-in light theme (<c>"light"</c>).</summary>
    Light,

    /// <summary>Built-in blue-accented theme (<c>"tiger-blue"</c>).</summary>
    TigerBlue,

    /// <summary>A custom theme identified by name; the enum value alone does not identify it.</summary>
    Custom
}
