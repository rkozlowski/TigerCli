namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// The result of mapping an <see cref="ActivePoint"/> through the measurement pipeline.
/// Contains the owning cell coordinates and the final position within the measured lines.
/// </summary>
public sealed class MeasuredActivePoint(int column, int row, int lineIndex, int offsetInLine)
{
    /// <summary>Column of the owning (anchor) cell.</summary>
    public int Column { get; } = column;

    /// <summary>Row of the owning (anchor) cell.</summary>
    public int Row { get; } = row;

    /// <summary>Line index within the measured cell's lines.</summary>
    public int LineIndex { get; internal set; } = lineIndex;

    /// <summary>Character offset within that line.</summary>
    public int OffsetInLine { get; internal set; } = offsetInLine;
}
