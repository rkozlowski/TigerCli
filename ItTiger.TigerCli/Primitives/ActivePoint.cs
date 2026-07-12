namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// A logical position within a grid: column, row, and character offset inside the cell content.
/// </summary>
public sealed class ActivePoint(int column, int row, int offset)
{
    /// <summary>Active cell column.</summary>
    public int Column { get; } = column;

    /// <summary>Active cell row.</summary>
    public int Row { get; } = row;

    /// <summary>Character offset inside the active cell content.</summary>
    public int Offset { get; } = offset;
}
