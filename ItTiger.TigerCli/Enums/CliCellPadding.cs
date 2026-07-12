namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Horizontal padding applied inside a rendered cell.
/// </summary>
public enum CliCellPadding
{
    /// <summary>No extra horizontal padding.</summary>
    None,
    /// <summary>Reserve one padding column on the left.</summary>
    Left,
    /// <summary>Reserve one padding column on the right.</summary>
    Right,
    /// <summary>Reserve one padding column on both the left and right.</summary>
    Both
}
