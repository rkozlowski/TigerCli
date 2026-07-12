

namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Controls how crossing frame segments choose junction glyphs.
/// </summary>
public enum CliFrameJoinStyle
{
    /// <summary>Prefer full box-drawing accuracy, including double-line junctions.</summary>
    PreferDoubleJunctions,   // Use full box-drawing accuracy

    /// <summary>Use fallback-safe simplified junctions.</summary>
    SimplifiedCompatible     // Use fallback-safe characters
}
