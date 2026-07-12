using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// Describes a single table element (column in Vertical, row in Horizontal).
/// Styling is expressed via <see cref="CliCellStyle"/> and resolved by the grid's
/// style cascade (grid defaults → axis styles → section defaults → per-cell).
/// </summary>
public class CliTableElement
{
    /// <summary>
    /// Header caption content for this element (usually a string; may include TigerCli markup
    /// if the effective FormattingMode for the header band is Preformatted).
    /// </summary>
    public object? HeaderContent { get; set; }

    /// <summary>
    /// Element-axis defaults for data cells (width/min/max, wrapping, horizontal/vertical alignment,
    /// formatting mode, formatter, null display, char style).
    /// Layout properties are applied to the data band via axis styling; the char style (value ink,
    /// e.g. a Link underline) is applied per data cell at <see cref="CliTable.ToGrid"/> time so it
    /// never bleeds onto the header caption or frame cells crossing the axis. Not meant for
    /// per-cell overrides.
    /// </summary>
    public CliCellStyle? DataStyle { get; set; }

    /// <summary>
    /// Header-only overrides for the caption cell of this element (e.g., header alignment/char style).
    /// Typically uses Preformatted formatting mode for markup captions.
    /// </summary>
    public CliCellStyle? HeaderStyle { get; set; }

    /// <summary>
    /// When <c>true</c>, this element's <b>data</b> cells are marked as hyperlinks (a per-cell
    /// <see cref="CliCellStyle.IsHyperlink"/> is applied at <see cref="CliTable.ToGrid"/> time, so the
    /// flag never bleeds onto the header caption or frame cells). The render pipeline then derives each
    /// data cell's hyperlink target from its own full visible value. Header captions are never links.
    /// </summary>
    public bool DataIsHyperlink { get; set; }

    /// <summary>Creates an empty table element.</summary>
    public CliTableElement() { }

    /// <summary>Creates a table element with a preformatted header caption and data-cell style.</summary>
    /// <param name="headerContent">The header caption.</param>
    /// <param name="dataStyle">The default style for data cells.</param>
    /// <param name="dataAltStyle">
    /// Retained for source compatibility; this parameter does not currently affect rendering.
    /// </param>
    public CliTableElement(string headerContent, CliCellStyle? dataStyle, CliCellStyle? dataAltStyle = null)
    {
        HeaderContent = headerContent;
        HeaderStyle = new CliCellStyle
        {
            FormattingMode = CliFormattingMode.Preformatted
        };
        DataStyle = dataStyle;
    }

    /// <summary>Creates a table element with explicit header content and styles.</summary>
    /// <param name="headerContent">The header caption content.</param>
    /// <param name="headerStyle">The style applied to the header caption.</param>
    /// <param name="dataStyle">The default style for data cells.</param>
    /// <param name="dataAltStyle">
    /// Retained for source compatibility; this parameter does not currently affect rendering.
    /// </param>
    public CliTableElement(
        object? headerContent,
        CliCellStyle? headerStyle,
        CliCellStyle? dataStyle,        
        CliCellStyle? dataAltStyle = null)
    {
        HeaderContent = headerContent;
        DataStyle = dataStyle;
        HeaderStyle = headerStyle;        
    }
}
