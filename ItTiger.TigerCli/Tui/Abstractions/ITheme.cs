
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Tui.Abstractions;


/// <summary>
/// Resolves semantic TigerCli style and surface roles to concrete render styles for TUI controls,
/// markup, and structured rendering.
/// </summary>
public interface ITheme
{
    /// <summary>
    /// Stable, unique theme identifier suitable for configuration (compared ordinal-ignore-case).
    /// Framework names (<c>"dark"</c>, <c>"light"</c>, <c>"tiger-blue"</c>) and <c>"default"</c> are reserved.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The contrast family this theme belongs to (dark or light). Custom semantic styles use the family
    /// to choose a dark/light override without needing one override per concrete theme. Defaults to
    /// <see cref="TigerThemeFamily.Dark"/>; light-background themes should return
    /// <see cref="TigerThemeFamily.Light"/>. Family is metadata, not inheritance.
    /// </summary>
    TigerThemeFamily Family => TigerThemeFamily.Dark;

    /// <summary>Resolves a single style token to a concrete <see cref="CliCellStyle"/>.</summary>
    CliCellStyle Resolve(ThemeStyle style);

    /// <summary>
    /// Resolves a reusable <see cref="SurfaceRole"/> into concrete colours: the surface background
    /// plus the optional alternate-record (zebra) colours. Table style recipes and other components
    /// use this so the same surface (e.g. <see cref="SurfaceRole.Panel"/>) looks consistent.
    /// </summary>
    SurfaceColors ResolveSurface(SurfaceRole role);
}
