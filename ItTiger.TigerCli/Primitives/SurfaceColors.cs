using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// The concrete colours a theme resolves for a <see cref="SurfaceRole"/>: the surface
/// <see cref="Background"/> plus the optional alternate-record (zebra) colours used for data cells.
/// When <see cref="AltBackground"/> is <c>null</c> the surface has no zebra striping.
/// <see cref="AltForeground"/> may be <c>null</c> to inherit the body foreground.
/// </summary>
public readonly record struct SurfaceColors(
    CliColor? Background,
    CliColor? AltBackground = null,
    CliColor? AltForeground = null);
