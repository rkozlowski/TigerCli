namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Controls how a column's width is determined after content-driven sizing.
/// Auto: size to content (default).
/// Star: take remaining space after all Auto columns are resolved.
///       Multiple Star columns share the remainder equally.
/// </summary>
public enum CliColumnSizing
{
    /// <summary>Size the column to its content and style constraints.</summary>
    Auto,

    /// <summary>Take a share of remaining width after auto columns are resolved.</summary>
    Star
}
