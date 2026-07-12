namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Determines whether the scrollbar thumb represents the physical scroll offset
/// or the logical active point (cursor) position.
/// </summary>
public enum CliScrollThumbMode
{
    /// <summary>Thumb represents the top/left visible offset.</summary>
    Offset,

    /// <summary>Thumb represents the active point (cursor/selection) position.</summary>
    ActivePoint
}
