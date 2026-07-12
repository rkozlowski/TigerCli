
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// A single grid cell definition, holding either scalar content or a hosted subgrid plus optional
/// style and span information.
/// </summary>
public sealed class CliGridCell
{
    /// <summary>Scalar content rendered through the effective cell style; <c>null</c> uses the null display value.</summary>
    public object? Content { get; }

    /// <summary>Hosted subgrid content, when this cell wraps another grid.</summary>
    public CliGrid? Subgrid { get; }

    /// <summary>Cell-level style applied after row and column styles.</summary>
    public CliCellStyle? Style { get; }

    /// <summary>Number of columns covered by this cell.</summary>
    public int ColSpan { get; }

    /// <summary>Number of rows covered by this cell.</summary>
    public int RowSpan { get; }    

    /// <summary>Column offset from the anchor cell for covered span cells.</summary>
    public int ColOffset { get; internal set; } = 0;

    /// <summary>Row offset from the anchor cell for covered span cells.</summary>
    public int RowOffset { get; internal set; } = 0;    

    /// <summary>Whether this slot is covered by a spanning cell whose anchor is elsewhere.</summary>
    public bool IsCovered => (ColOffset > 0) || (RowOffset > 0);

    /// <summary>Whether this slot is the anchor cell for its content/span.</summary>
    public bool IsAnchor => !IsCovered;

    /// <summary>Whether frame content fills this cell horizontally.</summary>
    public bool FillHorizontal { get; internal set; } = false;

    /// <summary>Whether frame content fills this cell vertically.</summary>
    public bool FillVertical { get; internal set; } = false;

    /// <summary>Whether this cell is structural frame content.</summary>
    public bool IsFrameCell { get; internal set; } = false;


    /// <summary>Whether this cell hosts a subgrid.</summary>
    public bool HasSubgrid => Subgrid is not null;


    /// <summary>
    /// Creates a scalar-content grid cell.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="content"/> is a <see cref="CliGrid"/>; use the subgrid constructor instead.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A span is less than one.</exception>
    public CliGridCell(object? content, CliCellStyle? style = null, int colSpan = 1, int rowSpan = 1)
    {
        if (colSpan < 1) throw new ArgumentOutOfRangeException(nameof(colSpan));
        if (rowSpan < 1) throw new ArgumentOutOfRangeException(nameof(rowSpan));

        if (content is CliGrid)
            throw new ArgumentException("Use the subgrid constructor for grid content.", nameof(content));

        Content = content;
        Style = style;
        ColSpan = colSpan;
        RowSpan = rowSpan;
    }

    /// <summary>
    /// Creates a subgrid cell. The optional style applies to the host cell area.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="subgrid"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A span is less than one.</exception>
    public CliGridCell(CliGrid subgrid, CliCellStyle? style = null, int colSpan = 1, int rowSpan = 1)
    {
        if (subgrid is null) throw new ArgumentNullException(nameof(subgrid));
        if (colSpan < 1) throw new ArgumentOutOfRangeException(nameof(colSpan));
        if (rowSpan < 1) throw new ArgumentOutOfRangeException(nameof(rowSpan));

        Subgrid = subgrid;
        Style = CliCellStyle.Clone(style);
        ColSpan = colSpan;
        RowSpan = rowSpan;
    }
}
