
namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Base layout constraints shared by renderable components and grids.
/// </summary>
public abstract class CliLayoutComponent
{
    /// <summary>Default style applied before row, column, and cell styles.</summary>
    public CliCellStyle? DefaultCellStyle { get; set; }

    /// <summary>Whether the component participates in interactive rendering.</summary>
    public bool IsInteractive { get; set; } = false;

    /// <summary>Exact width constraint.</summary>
    public int? Width { get; set; }              // Exact width
    /// <summary>Hard lower width bound.</summary>
    public int? MinWidth { get; set; }           // Hard lower bound
    /// <summary>Soft upper width target used for best-effort fitting.</summary>
    public int? SoftMaxWidth { get; set; }       // Soft upper bound
    /// <summary>Hard upper width bound.</summary>
    public int? MaxWidth { get; set; }           // Hard upper bound

    /// <summary>Exact height constraint.</summary>
    public int? Height { get; set; }              // Exact height
    /// <summary>Hard lower height bound.</summary>
    public int? MinHeight { get; set; }           // Hard lower bound
    /// <summary>Soft upper height target used for best-effort fitting.</summary>
    public int? SoftMaxHeight { get; set; }       // Target, best effort
    /// <summary>Hard upper height bound.</summary>
    public int? MaxHeight { get; set; }           // Hard upper bound

}
