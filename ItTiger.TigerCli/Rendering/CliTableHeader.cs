using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// Represents the header of a CLI table, including visibility and structural elements.
/// </summary>
public class CliTableHeader
{
    /// <summary>
    /// Gets or sets a value indicating whether the header should be rendered.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Style applied to the header band before per-element header styles.
    /// </summary>
    public CliCellStyle? HeaderStyle { get; set; }

    /// <summary>
    /// Gets or sets the list of header elements (e.g., column or row names).
    /// </summary>
    public IList<CliTableElement> Elements { get; set; } = [];
}
