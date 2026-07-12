using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Primitives;
using System.Globalization;
using System.Net.Http.Headers;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// Describes a rectangular frame region owned by a <see cref="CliGrid"/>.
/// </summary>
/// <remarks>
/// App code normally obtains a frame area through <see cref="CliGrid.AddFrameArea"/> and then adds
/// horizontal, vertical, or outer frame segments before the grid is measured/rendered. Frame segments
/// cannot be added after the grid's frame layout has been initialized.
/// </remarks>
public class CliFrameArea
{
    /// <summary>The grid that owns this frame area.</summary>
    public CliGrid Grid { get; init; }
    
    /// <summary>How mixed single/double junctions are resolved.</summary>
    public CliFrameJoinStyle JoinStyle { get; init; }

    /// <summary>The first grid column included in this area.</summary>
    public int FirstColumn { get; init; }

    /// <summary>The first grid row included in this area.</summary>
    public int FirstRow { get; init; }

    /// <summary>The last grid column included in this area.</summary>
    public int LastColumn { get; init; }

    /// <summary>The last grid row included in this area.</summary>
    public int LastRow { get; init; }

    /// <summary>The number of columns in this area.</summary>
    public int ColumnCount => LastColumn - FirstColumn + 1;

    /// <summary>The number of rows in this area.</summary>
    public int RowCount => LastRow - FirstRow + 1;

    /// <summary>The frame line segments that will be applied during frame layout initialization.</summary>
    public List<CliFrameLine> Lines { get; init; } = [];

    /// <summary>Optional default character style for frame segments that do not specify one.</summary>
    public CliCharStyle? CharStyle { get; init; } = null;

    internal IReadOnlyDictionary<(int Col, int Row), (char Glyph, CliCharStyle? Style)> JunctionGlyphs => junctionGlyphs;
    private readonly Dictionary<(int Col, int Row), (char Glyph, CliCharStyle? Style)> junctionGlyphs = [];

   



    internal CliFrameArea(CliGrid grid, CliFrameJoinStyle joinStyle, int firstColumn, int firstRow, int lastColumn, int lastRow, CliCharStyle? charStyle = null)
    {
        if (firstColumn < 0 || lastColumn < firstColumn || lastColumn >= grid.ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(firstColumn), 
                $"Frame column range [{firstColumn}..{lastColumn}] is outside the grid columns (0..{grid.ColumnCount - 1}).");

        if (firstRow < 0 || lastRow < firstRow || lastRow >= grid.RowCount)
            throw new ArgumentOutOfRangeException(nameof(firstRow), 
                $"Frame row range [{firstRow}..{lastRow}] is outside the grid rows (0..{grid.RowCount - 1}).");

        Grid = grid;
        JoinStyle = joinStyle;
        FirstColumn = firstColumn;
        FirstRow = firstRow;
        LastColumn = lastColumn;
        LastRow = lastRow;
        CharStyle = charStyle;
    }

    /// <summary>Adds top, left, right, and bottom frame segments around the full area.</summary>
    public void AddOuterFrame(CliFrameSegment segmentStyle, CliCharStyle? charStyle = null)
    {
        AddTopFrame(segmentStyle, charStyle);        
        AddLeftFrame(segmentStyle, charStyle);
        AddRightFrame(segmentStyle, charStyle);
        AddBottomFrame(segmentStyle, charStyle);
    }

    /// <summary>Adds a top horizontal frame segment spanning the full area width.</summary>
    public void AddTopFrame(CliFrameSegment segmentStyle, CliCharStyle? charStyle = null)
    {
        AddFrameSegment(CliFrameLineType.Top, FirstColumn, FirstRow, LastColumn - FirstColumn + 1, segmentStyle, charStyle);
    }

    /// <summary>Adds a bottom horizontal frame segment spanning the full area width.</summary>
    public void AddBottomFrame(CliFrameSegment segmentStyle, CliCharStyle? charStyle = null)
    {
        AddFrameSegment(CliFrameLineType.Bottom, FirstColumn, LastRow, LastColumn - FirstColumn + 1, segmentStyle, charStyle);
    }

    /// <summary>Adds a left vertical frame segment spanning the full area height.</summary>
    public void AddLeftFrame(CliFrameSegment segmentStyle, CliCharStyle? charStyle = null)
    {
        AddFrameSegment(CliFrameLineType.Left, FirstColumn, FirstRow, LastRow - FirstRow + 1, segmentStyle, charStyle);
    }

    /// <summary>Adds a right vertical frame segment spanning the full area height.</summary>
    public void AddRightFrame(CliFrameSegment segmentStyle, CliCharStyle? charStyle = null)
    {
        AddFrameSegment(CliFrameLineType.Right, LastColumn, FirstRow, LastRow - FirstRow + 1, segmentStyle, charStyle);
    }

    /// <summary>Adds a horizontal frame segment inside this area.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The segment is outside this frame area or has a non-positive length.</exception>
    /// <exception cref="TigerCliException">The frame layout has already been initialized.</exception>
    public void AddHorizontalFrame(int column, int row, int length, CliFrameSegment segmentStyle, CliCharStyle? charStyle = null)
    {
        AddFrameSegment(CliFrameLineType.Horizontal, column, row, length, segmentStyle, charStyle);
    }

    /// <summary>Adds a vertical frame segment inside this area.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The segment is outside this frame area or has a non-positive length.</exception>
    /// <exception cref="TigerCliException">The frame layout has already been initialized.</exception>
    public void AddVerticalFrame(int column, int row, int length, CliFrameSegment segmentStyle, CliCharStyle? charStyle = null)
    {
        AddFrameSegment(CliFrameLineType.Vertical, column, row, length, segmentStyle, charStyle);
    }

    internal void AddFrameSegment(CliFrameLineType type, int column, int row, int length, CliFrameSegment segmentStyle, CliCharStyle? charStyle)
    {
        if (length < 1)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be at least 1.");
        if (Grid.IsFrameLayoutInitialized)
        {
            throw new TigerCliException("Cannot add frame segment after layout has been initialized.",
                    TigerCliRenderStage.InvalidUsage);
        }

        switch (type)
        {
            case CliFrameLineType.Top:
            case CliFrameLineType.Bottom:
            case CliFrameLineType.Horizontal:
                if (column < FirstColumn || column + length - 1 > LastColumn)
                    throw new ArgumentOutOfRangeException(nameof(column), "Column range is outside the frame area.");
                if (row < FirstRow || row > LastRow)
                    throw new ArgumentOutOfRangeException(nameof(row), "Row is outside the frame area.");
                break;

            case CliFrameLineType.Left:
            case CliFrameLineType.Right:
            case CliFrameLineType.Vertical:
                if (row < FirstRow || row + length - 1 > LastRow)
                    throw new ArgumentOutOfRangeException(nameof(row), "Row range is outside the frame area.");
                if (column < FirstColumn || column > LastColumn)
                    throw new ArgumentOutOfRangeException(nameof(column), "Column is outside the frame area.");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), "Unsupported frame line type.");
        }

        Lines.Add(new CliFrameLine(type, column, row, length, segmentStyle, charStyle ?? CharStyle));
    }

    internal record FrameCell
    (
        CliFrameLineType LineType,
        CliFrameSegmentStyle SegmentStyle,
        CliCellStyle? Style
    )
    {
        internal string? Text { get; set; }
        internal bool FillH { get; set; } = false;
        internal bool FillV { get; set; } = false;        
    };

    internal static int IsSingleOrDoubleJunction(bool acceptHorizontal, bool acceptVertical, FrameCell? cell)
    {
        var junction = 0;
        if (cell == null) 
            return junction;
        var style = cell.SegmentStyle;
        if (!(style is (CliFrameSegmentStyle.SingleFrame or CliFrameSegmentStyle.DoubleFrame)))
            return junction;
        int junctionType = style == CliFrameSegmentStyle.SingleFrame ? 1 : 2;
        var type = cell.LineType;
        if (acceptHorizontal && type == CliFrameLineType.Horizontal)
            junction = junctionType;
        if (acceptVertical && type == CliFrameLineType.Vertical)
            junction = junctionType;
        return junction;
    }
    internal void InitializeFrameLayout()
    {
        if (Grid.IsFrameLayoutInitialized)
            throw new TigerCliException("Frame layout has already been initialized.",
                TigerCliRenderStage.Unknown);
        
        FrameCell?[,] cells = new FrameCell?[RowCount + 2, ColumnCount + 2];

        var rowsToLock = new Dictionary<int, int>(); // row, height
        var colsToLock = new Dictionary<int, int>(); // column, width

        foreach (var line in Lines)
        {
            var firstRow = line.Row - FirstRow + 1;
            var firstCol = line.Column - FirstColumn + 1;
            CliFrameLineType type;
            CliCellStyle? cellStyle = null;
            var charStyle = line.CharStyle ?? CharStyle;
            cellStyle = new CliCellStyle
            {
                    CharStyle = charStyle,
                    FormattingMode = CliFormattingMode.Raw,
                    Formatter = CliFormatter.NoOpFormatter
            };
            int dRow = 0;
            int dCol = 0;
            
            switch (line.Type)
            {
                case CliFrameLineType.Top:
                case CliFrameLineType.Bottom:
                case CliFrameLineType.Horizontal:
                    type = CliFrameLineType.Horizontal;
                    dCol = 1;
                    break;
                case CliFrameLineType.Left:
                case CliFrameLineType.Right:
                case CliFrameLineType.Vertical:
                    type = CliFrameLineType.Vertical;
                    dRow = 1;
                    break;
                default:
                    throw new TigerCliException($"Unknown frame line type: {line.Type}", TigerCliRenderStage.Unknown);
            }
            string content = string.Empty;
            switch (line.Style.Style)
            {
                case CliFrameSegmentStyle.Space:
                    content = " ";
                    break;
                case CliFrameSegmentStyle.Custom:
                    content = line.Style.Custom ?? String.Empty;
                    break;
                case CliFrameSegmentStyle.DoubleFrame:
                    content = (type == CliFrameLineType.Horizontal ? ConsoleSymbol.DoubleH : ConsoleSymbol.DoubleV).ToString();
                    break;
                case CliFrameSegmentStyle.SingleFrame:
                    content = (type == CliFrameLineType.Horizontal ? ConsoleSymbol.SingleH : ConsoleSymbol.SingleV).ToString();
                    break;
            }
            int w = Math.Max(content.Length, 1);
            int h = 1;
            if (type == CliFrameLineType.Horizontal)
                rowsToLock[line.Row] = h;
            else // Vertical
                colsToLock[line.Column] = w;

            for (int i = 0; i < line.Length; i++)
            {
                int r = firstRow + i * dRow;
                int c = firstCol + i * dCol;
                if (cells[r, c] != null)
                    continue;
                bool fillH = type == CliFrameLineType.Horizontal;
                bool fillV = type == CliFrameLineType.Vertical;
                cells[r, c] = new FrameCell(type, line.Style.Style, cellStyle) { Text = content, FillH = fillH, FillV = fillV };
            }
        }

        foreach (var r in rowsToLock)
            Grid.LockRowHeight(r.Key, r.Value);
        foreach (var c in colsToLock)
            Grid.LockColumnWidth(c.Key, c.Value);

        for (int row = 1; row <= RowCount; row++)
        {
            for (int col = 1; col <= ColumnCount; col++)
            {
                var cell = cells[row, col];
                if (cell == null)
                    continue;
                char? fc = ResolveJunctionChar(cells, row, col, cell);
                if (fc != null)
                    cell.Text = fc.ToString();                
                Grid.Set(FirstColumn + col - 1, FirstRow + row - 1, cell.Text, true, null, cell.Style, 1, 1, cell.FillH, cell.FillV);
            }
        }
    }

    private char? ResolveJunctionChar(FrameCell?[,] cells, int row, int col, FrameCell cell)
    {
        char? fc = null;
        var segmentStyle = cell.SegmentStyle;
        if (!(segmentStyle is (CliFrameSegmentStyle.SingleFrame or CliFrameSegmentStyle.DoubleFrame)))
            return fc;
        var cs = segmentStyle == CliFrameSegmentStyle.SingleFrame ? 1 : 2;
        var isH = cell.LineType == CliFrameLineType.Horizontal;
        var isV = cell.LineType == CliFrameLineType.Vertical;
        var ncs = IsSingleOrDoubleJunction(isV, true, cells[row - 1, col]);
        var scs = IsSingleOrDoubleJunction(isV, true, cells[row + 1, col]);
        var ecs = IsSingleOrDoubleJunction(true, isH, cells[row, col + 1]);
        var wcs = IsSingleOrDoubleJunction(true, isH, cells[row, col - 1]);
        var isN = ncs > 0;
        var isS = scs > 0;
        var isE = ecs > 0;
        var isW = wcs > 0;
        var simpleJs = JoinStyle == CliFrameJoinStyle.SimplifiedCompatible;
        if (isH && !isN && !isS)
            return fc;
        if (isV && !isW && !isE)
            return fc;
        
        int sum = cs + ncs + scs + wcs + ecs;
        if (isS && isE && !isN && !isW)
        {
            fc = sum > 3 ? ConsoleSymbol.DoubleTopLeft : ConsoleSymbol.SingleTopLeft;
        }
        if (isS && isW && !isN && !isE)
        {
            fc = sum > 3 ? ConsoleSymbol.DoubleTopRight : ConsoleSymbol.SingleTopRight;
        }
        if (isN && isE && !isS && !isW)
        {
            fc = sum > 3 ? ConsoleSymbol.DoubleBottomLeft : ConsoleSymbol.SingleBottomLeft;
        }
        if (isN && isW && !isS && !isE)
        {
            fc = sum > 3 ? ConsoleSymbol.DoubleBottomRight : ConsoleSymbol.SingleBottomRight;
        }
        if (isN && isS && isE && !isW)
        {
            if (sum == 4 || (sum == 5 && !simpleJs))
            {
                fc = ConsoleSymbol.SingleTRight;
            }
            else if (sum == 8 || !simpleJs)
            {
                fc = ConsoleSymbol.DoubleTRight;
            }
            else if (simpleJs)
            {
                if (ncs + scs == 4)
                    fc = ConsoleSymbol.DoubleV;
                else if (ncs + ecs == 4)
                    fc = ConsoleSymbol.DoubleBottomLeft;
                else if (scs + ecs == 4)
                    fc = ConsoleSymbol.DoubleTopLeft;
                else
                    fc = ConsoleSymbol.SingleV;
            }
        }
        if (isN && isS && isW && !isE)
        {
            if (sum == 4 || (sum == 5 && !simpleJs))
            {
                fc = ConsoleSymbol.SingleTLeft;
            }
            else if (sum == 8 || !simpleJs)
            {
                fc = ConsoleSymbol.DoubleTLeft;
            }
            else if (simpleJs)
            {
                if (ncs + scs == 4)
                    fc = ConsoleSymbol.DoubleV;
                else if (ncs + wcs == 4)
                    fc = ConsoleSymbol.DoubleBottomRight;
                else if (scs + wcs == 4)
                    fc = ConsoleSymbol.DoubleTopRight;
                else
                    fc = ConsoleSymbol.SingleV;
            }
        }
        if (isS && isW && isE && !isN)
        {
            if (sum == 4 || (sum == 5 && !simpleJs))
            {
                fc = ConsoleSymbol.SingleTDown;
            }
            else if (sum == 8 || !simpleJs)
            {
                fc = ConsoleSymbol.DoubleTDown;
            }
            else if (simpleJs)
            {
                if (wcs + ecs == 4)
                    fc = ConsoleSymbol.DoubleH;
                else if (scs + wcs == 4)
                    fc = ConsoleSymbol.DoubleTopRight;
                else if (scs + ecs == 4)
                    fc = ConsoleSymbol.DoubleTopLeft;
                else
                    fc = ConsoleSymbol.SingleH;
            }
        }
        if (isN && isW && isE && !isS)
        {
            if (sum == 4 || (sum == 5 && !simpleJs))
            {
                fc = ConsoleSymbol.SingleTUp;
            }
            else if (sum == 8 || !simpleJs)
            {
                fc = ConsoleSymbol.DoubleTUp;
            }
            else if (simpleJs)
            {
                if (wcs + ecs == 4)
                    fc = ConsoleSymbol.DoubleH;
                else if (ncs + wcs == 4)
                    fc = ConsoleSymbol.DoubleBottomRight;
                else if (ncs + ecs == 4)
                    fc = ConsoleSymbol.DoubleBottomLeft;
                else
                    fc = ConsoleSymbol.SingleH;
            }
        }
        if (isN && isW && isE && isS)
        {
            if (sum == 5 || (sum == 6 && !simpleJs))
            {
                fc = ConsoleSymbol.SingleCross;
            }
            else if (sum == 10 || (sum == 9 && cs == 1) || !simpleJs)
            {
                fc = ConsoleSymbol.DoubleCross;
            }
            else if (simpleJs)
            {
                if (ncs + scs + ecs == 6)
                    fc = ConsoleSymbol.DoubleTRight;
                else if (ncs + scs + wcs == 6)
                    fc = ConsoleSymbol.DoubleTLeft;
                else if (wcs + ecs + scs == 6)
                    fc = ConsoleSymbol.DoubleTDown;
                else if (wcs + ecs + ncs == 6)
                    fc = ConsoleSymbol.DoubleTUp;
                else if (scs + ecs == 4)
                    fc = ConsoleSymbol.DoubleTopLeft;
                else if (scs + wcs == 4)
                    fc = ConsoleSymbol.DoubleTopRight;
                else if (ncs + ecs == 4)
                    fc = ConsoleSymbol.DoubleBottomLeft;
                else if (ncs + wcs == 4)
                    fc = ConsoleSymbol.DoubleBottomRight;
                if (ncs + scs + ecs == 3)
                    fc = ConsoleSymbol.SingleTRight;
                else if (ncs + scs + wcs == 3)
                    fc = ConsoleSymbol.SingleTLeft;
                else if (wcs + ecs + scs == 3)
                    fc = ConsoleSymbol.SingleTDown;
                else if (wcs + ecs + ncs == 3)
                    fc = ConsoleSymbol.SingleTUp;
                else if (ncs + scs == 4)
                    fc = ConsoleSymbol.DoubleV;
                else if (ecs + wcs == 4)
                    fc = ConsoleSymbol.DoubleH;
                else if (ncs + scs == 2)
                    fc = ConsoleSymbol.SingleV;
                else if (ecs + wcs == 2)
                    fc = ConsoleSymbol.SingleH;
            }

        }

        return fc;
    }

    

}
