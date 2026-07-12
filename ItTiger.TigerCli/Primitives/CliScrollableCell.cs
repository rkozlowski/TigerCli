using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Scroll state for a grid cell that hosts a scrollable subgrid.
/// </summary>
public sealed class CliScrollableCell
{
    /// <summary>Host cell row.</summary>
    public int Row { get; }

    /// <summary>Host cell column.</summary>
    public int Column { get; }

    /// <summary>Scrollable axes.</summary>
    public CliScrollMode Mode { get; }

    /// <summary>How scrollbar thumbs derive their position.</summary>
    public CliScrollThumbMode ThumbMode { get; }

    /// <summary>Horizontal viewport offset.</summary>
    public int ScrollOffsetX { get; set; } = 0;

    /// <summary>Vertical viewport offset.</summary>
    public int ScrollOffsetY { get; set; } = 0;


    /// <summary>
    /// Creates scroll state for a hosted subgrid cell.
    /// </summary>
    public CliScrollableCell(
        int column,
        int row,
        CliScrollMode mode,
        CliScrollThumbMode thumbMode = CliScrollThumbMode.Offset)
    {
        Column = column;
        Row = row;
        Mode = mode;
        ThumbMode = thumbMode;
    }
    }

