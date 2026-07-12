namespace ItTiger.TigerCli.Tui.Activity;

/// <summary>
/// Immutable placement of one <see cref="ActivityElement"/> within a row: the anchor
/// <see cref="Column"/> and how many columns it <see cref="Span"/>s. Spanning belongs to the cell, not
/// the row.
/// </summary>
public sealed class ActivityCellSpec
{
    internal ActivityCellSpec(int column, int span, ActivityElement element)
    {
        Column = column;
        Span = span;
        Element = element;
    }

    /// <summary>Anchor column index.</summary>
    public int Column { get; }

    /// <summary>Number of columns the cell spans (>= 1).</summary>
    public int Span { get; }

    /// <summary>The element rendered in this cell.</summary>
    public ActivityElement Element { get; }
}
