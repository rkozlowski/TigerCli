namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Rendering pipeline stage associated with a <see cref="Exceptions.TigerCliException"/>.
/// </summary>
public enum TigerCliRenderStage
{
    /// <summary>Unknown or uncategorized stage.</summary>
    Unknown = 0,
    /// <summary>Invalid public API usage before rendering can proceed.</summary>
    InvalidUsage,
    /// <summary>Conversion from component to grid.</summary>
    ToGrid,
    /// <summary>Grid measurement.</summary>
    Measure,
    /// <summary>Rendering measured output.</summary>
    Render
}
