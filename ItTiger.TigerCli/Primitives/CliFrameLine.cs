using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// A frame line scheduled inside a frame area.
/// </summary>
public class CliFrameLine(CliFrameLineType type, int column, int row, int length, CliFrameSegment style, CliCharStyle? charStyle = null)
{
    /// <summary>Logical line type or edge.</summary>
    public CliFrameLineType Type { get; init; } = type;

    /// <summary>Start column.</summary>
    public int Column { get; init; } = column;

    /// <summary>Start row.</summary>
    public int Row { get; init; } = row;

    /// <summary>Line length in grid cells.</summary>
    public int Length { get; init; } = length;

    /// <summary>Frame segment style.</summary>
    public CliFrameSegment Style { get; init; } = style;

    /// <summary>Optional character style for the frame glyphs.</summary>
    public CliCharStyle? CharStyle { get; init; } = charStyle;
}
