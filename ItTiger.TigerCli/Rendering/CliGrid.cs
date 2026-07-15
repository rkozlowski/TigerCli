using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static ItTiger.TigerCli.Rendering.CliFrameArea;

namespace ItTiger.TigerCli.Rendering;
/// <summary>
/// Lower-level grid layout and rendering building block used by tables, detail/list builders, and
/// TUI widgets. Most command output should start with <see cref="CliList{T}"/>,
/// <see cref="CliDetails"/>, or <see cref="CliTable"/>; create a <see cref="CliGrid"/> directly
/// when building custom renderables, composite layouts, or widgets.
/// </summary>
/// <remarks>
/// Coordinates are zero-based and use <c>(column, row)</c> order. Cells hold formatted content or a
/// nested subgrid, row and column definitions supply axis styles, and <see cref="Measure"/> resolves
/// wrapping, sizing, spans, alignment, frames, overlays, and scroll information before rendering.
/// </remarks>
/// <param name="columnCount">Number of columns in the grid. Valid cell columns are
/// <c>0</c> through <c>columnCount - 1</c>.</param>
/// <param name="rowCount">Number of rows in the grid. Valid cell rows are
/// <c>0</c> through <c>rowCount - 1</c>.</param>
public partial class CliGrid(int columnCount, int rowCount) : CliLayoutComponent
{
    static readonly CliCharStyle DefaultCharStyle = new() { Background = CliColor.Black, Foreground = CliColor.Gray };

    // The framework-global fallback char style the cascade injects when no style layer sets
    // colours. Exposed so document-style renderers (help) can recognize — and neutralize — the
    // fallback colours on otherwise-unstyled text.
    internal static CliCharStyle GlobalDefaultCharStyle => DefaultCharStyle;

    static readonly CliCellStyle GlobalDefaultStyle = new()
    {
        HorizontalAlignment = CliTextAlignment.Left,
        VerticalAlignment = CliVerticalAlignment.Top,
        Wrapping = CliWrapping.WordWrap,
        FormattingMode = CliFormattingMode.Raw,
        NullDisplayValue = string.Empty,
        CharStyle = DefaultCharStyle,
        MinWidth = 1,
        MinHeight = 1
    };

    /// <summary>Horizontal scroll offset used by scrollable host cells.</summary>
    public int OffsetX { get; set; }

    /// <summary>Vertical scroll offset used by scrollable host cells.</summary>
    public int OffsetY { get; set; }

    /// <summary>Number of columns in the grid.</summary>
    public int ColumnCount { get; init; } = columnCount;

    /// <summary>Number of rows in the grid.</summary>
    public int RowCount { get; init; } = rowCount;

    private readonly CliGridCell?[,] cells = new CliGridCell?[rowCount, columnCount];
    
    private MeasuredCell?[,]? measuredCells;
    private int[]? measuredColumnWidths;
    private int[]? measuredRowHeights;
    private int? measuredWidth = null;
    private int? measuredHeight = null;


    /// <summary>Gets a cell's measured representation, or <c>null</c> before measurement.</summary>
    /// <param name="col">The zero-based column.</param>
    /// <param name="row">The zero-based row.</param>
    /// <returns>The measured cell, or <c>null</c> when the grid has not been measured.</returns>
    protected internal MeasuredCell? GetMeasuredCell(int col, int row) => measuredCells?[row, col];

    /// <summary>Gets a column's measured width, or <c>null</c> before measurement.</summary>
    /// <param name="col">The zero-based column.</param>
    /// <returns>The measured width, or <c>null</c> when the grid has not been measured.</returns>
    protected internal int? GetMeasuredColumnWidth(int col) => measuredColumnWidths?[col];

    /// <summary>Gets a row's measured height, or <c>null</c> before measurement.</summary>
    /// <param name="row">The zero-based row.</param>
    /// <returns>The measured height, or <c>null</c> when the grid has not been measured.</returns>
    protected internal int? GetMeasuredRowHeight(int row) => measuredRowHeights?[row];

    /// <summary>
    /// Returns the measured top-left rendered origin of a cell, or <c>null</c> when the grid has not
    /// been measured or the coordinate is outside the grid.
    /// </summary>
    public CliPoint? GetMeasuredCellOrigin(int column, int row)
    {
        if (measuredColumnWidths is null || measuredRowHeights is null)
            return null;
        if (column < 0 || column >= ColumnCount || row < 0 || row >= RowCount)
            return null;

        int x = 0;
        for (int c = 0; c < column; c++)
            x += measuredColumnWidths[c];

        int y = 0;
        for (int r = 0; r < row; r++)
            y += measuredRowHeights[r];

        return new CliPoint(x, y);
    }

    /// <summary>Gets the scroll mode assigned to a grid cell.</summary>
    /// <param name="col">The zero-based column.</param>
    /// <param name="row">The zero-based row.</param>
    /// <returns>The cell's scroll mode, or <see cref="CliScrollMode.None"/> when none is assigned.</returns>
    protected internal CliScrollMode GetScrollMode(int col, int row)
    {
        return _scrollCells.TryGetValue((col, row), out var sc) ? sc.Mode : CliScrollMode.None;
    }

    /// <summary>
    /// The scrollable cell currently under the grid's resolved active point, or <c>null</c>
    /// when no active point lands on a scrollable cell. This is the single "active" scrollable
    /// region: only it runs active-point-follow and feeds the parameterless scroll-info methods.
    /// </summary>
    private CliScrollableCell? ActiveScrollCell()
    {
        if (MeasuredActivePoint is { } ap && _scrollCells.TryGetValue((ap.Column, ap.Row), out var sc))
            return sc;
        return null;
    }

    /// <summary>
    /// Gets vertical scroll information for the grid's scrollable cell.
    /// Returns null if no vertical scrollable cell is defined.
    /// </summary>
    /// <remarks>
    /// The returned values are normalized so that scrollbar rendering can stay
    /// mode-agnostic: <c>offset</c> is the value the scrollbar thumb tracks, and
    /// <c>maxOffset</c> is the largest valid value for <c>offset</c>. Per thumb mode:
    /// <list type="bullet">
    ///   <item><description><see cref="CliScrollThumbMode.Offset"/>: <c>offset</c> is
    ///     the viewport's first-line index in [0, <c>total - viewport</c>];
    ///     <c>maxOffset = total - viewport</c>.</description></item>
    ///   <item><description><see cref="CliScrollThumbMode.ActivePoint"/>: <c>offset</c>
    ///     is the absolute active line index in [0, <c>total - 1</c>];
    ///     <c>maxOffset = total - 1</c>.</description></item>
    /// </list>
    /// All values are clamped: <c>viewport &gt;= 1</c>, <c>total &gt;= viewport</c>,
    /// <c>maxOffset &gt;= 0</c>, and <c>offset</c> in <c>[0, maxOffset]</c>.
    /// </remarks>
    public (bool visible, int offset, int viewport, int total, int maxOffset)? GetVerticalScrollInfo()
        => GetVerticalScrollInfoCore(ActiveScrollCell());

    /// <summary>
    /// Vertical scroll info for a specific scrollable cell, regardless of whether it is active.
    /// Returns null when the addressed cell is not a vertically scrollable cell.
    /// </summary>
    public (bool visible, int offset, int viewport, int total, int maxOffset)? GetVerticalScrollInfo(int column, int row)
        => GetVerticalScrollInfoCore(_scrollCells.TryGetValue((column, row), out var sc) ? sc : null);

    private (bool visible, int offset, int viewport, int total, int maxOffset)? GetVerticalScrollInfoCore(CliScrollableCell? scrollCell)
    {
        if (scrollCell == null)
            return null;
        if (scrollCell.Mode != CliScrollMode.Vertical && scrollCell.Mode != CliScrollMode.Both)
            return null;

        var m = GetMeasuredCell(scrollCell.Column, scrollCell.Row);
        if (m == null)
            return null;

        int viewport = Math.Max(1, m.Lines.Count);
        int total = Math.Max(viewport, m.TotalLinesCount);
        int scrollOffsetY = scrollCell.ScrollOffsetY;

        int offset;
        int maxOffset;

        if (scrollCell.ThumbMode == CliScrollThumbMode.ActivePoint)
        {
            int activeIndex = scrollOffsetY; // fallback when no MeasuredActivePoint
            if (MeasuredActivePoint != null
                && MeasuredActivePoint.Row == scrollCell.Row
                && MeasuredActivePoint.Column == scrollCell.Column)
            {
                activeIndex = MeasuredActivePoint.LineIndex + scrollOffsetY;
            }
            offset = activeIndex;
            maxOffset = Math.Max(0, total - 1);
        }
        else
        {
            offset = scrollOffsetY;
            maxOffset = Math.Max(0, total - viewport);
        }

        if (offset < 0) offset = 0;
        if (offset > maxOffset) offset = maxOffset;

        bool visible = scrollCell.Mode != CliScrollMode.None && total > viewport;

        TigerConsole.Logger?.LogTrace(
            "[GetVerticalScrollInfo] Visible: {Visible}, Offset: {Offset}, Viewport: {Viewport}, Total: {Total}, MaxOffset: {MaxOffset}",
            visible, offset, viewport, total, maxOffset);

        return (visible, offset, viewport, total, maxOffset);
    }

    /// <summary>
    /// Gets horizontal scroll information for the active scrollable cell, or <c>null</c> when there
    /// is no active horizontal scrollable cell.
    /// </summary>
    /// <remarks>
    /// The returned values are clamped and normalized for scrollbar rendering. <c>visible</c>
    /// indicates whether the content is wider than the viewport; <c>offset</c> is the horizontal
    /// viewport offset; <c>viewport</c> is the visible width; <c>total</c> is the total content width;
    /// and <c>maxOffset</c> is the largest valid offset.
    /// </remarks>
    public (bool visible, int offset, int viewport, int total, int maxOffset)? GetHorizontalScrollInfo()
        => GetHorizontalScrollInfoCore(ActiveScrollCell());

    /// <summary>
    /// Horizontal scroll info for a specific scrollable cell, regardless of whether it is active.
    /// Returns null when the addressed cell is not a horizontally scrollable cell.
    /// </summary>
    public (bool visible, int offset, int viewport, int total, int maxOffset)? GetHorizontalScrollInfo(int column, int row)
        => GetHorizontalScrollInfoCore(_scrollCells.TryGetValue((column, row), out var sc) ? sc : null);

    private (bool visible, int offset, int viewport, int total, int maxOffset)? GetHorizontalScrollInfoCore(CliScrollableCell? scrollCell)
    {
        if (scrollCell == null) return null;
        if (scrollCell.Mode != CliScrollMode.Horizontal && scrollCell.Mode != CliScrollMode.Both) return null;

        var m = GetMeasuredCell(scrollCell.Column, scrollCell.Row);
        if (m == null) return null;

        int viewport = 0;
        for (int c = 0; c < m.ColSpan; c++)
            viewport += GetMeasuredColumnWidth(scrollCell.Column + c) ?? 0;
        viewport = Math.Max(1, viewport);

        CliGrid? subgrid = null;
        MeasuredActivePoint? subgridActivePoint = null;

        if (scrollCell.ThumbMode == CliScrollThumbMode.ActivePoint)
        {
            var gridCell = cells[scrollCell.Row, scrollCell.Column];
            if (gridCell?.HasSubgrid == true)
            {
                subgrid = gridCell.Subgrid!;
                if (subgrid.MeasuredActivePoint is { } ap)
                {
                    subgridActivePoint = ap;
                }
            }
        }

        int total = Math.Max(viewport, HorizontalScrollableTotalWidth(m, scrollCell, subgrid, subgridActivePoint));

        int maxOffset = Math.Max(0, total - viewport);
        int offset = scrollCell.ScrollOffsetX;

        if (offset < 0) offset = 0;
        if (offset > maxOffset) offset = maxOffset;

        bool visible = scrollCell.Mode != CliScrollMode.None && total > viewport;

        return (visible, offset, viewport, total, maxOffset);
    }
    /// <summary>Total measured width after <see cref="Measure"/>; <c>null</c> before measurement.</summary>
    public int? MeasuredWidth => measuredWidth;

    /// <summary>Total measured height after <see cref="Measure"/>; <c>null</c> before measurement.</summary>
    public int? MeasuredHeight => measuredHeight;

    private readonly CliGridRowDefinition?[] rows = new CliGridRowDefinition?[rowCount];
    private readonly CliGridColumnDefinition?[] columns = new CliGridColumnDefinition?[columnCount];
    

    /// <summary>Whether <see cref="Measure"/> has completed without subsequent invalidation.</summary>
    public bool IsMeasured { get; private set; } = false;

    /// <summary>Whether frame areas have been expanded into structural grid cells.</summary>
    public bool IsFrameLayoutInitialized { get; private set; } = false;

    /// <summary>
    /// Controls whether row styles or column styles win when both define the same cell property.
    /// Cell-level style always has final precedence.
    /// </summary>
    public CliStylePrecedence StylePrecedence { get; set; } = CliStylePrecedence.ColumnOverRow;

    private readonly List<CliFrameArea> frameAreas = [];

    // Scrollable subgrid cells, keyed by their (column, row) coordinate. A grid may host several
    // scrollable cells, but only the one under the resolved active point (see ActiveScrollCell)
    // is "active": only it runs active-point-follow. Inactive cells keep their own scroll offsets
    // and still slice/clamp using them, so focus moving away and back never resets their position.
    private readonly Dictionary<(int Column, int Row), CliScrollableCell> _scrollCells = new();

    private readonly List<CliOverlay> _overlays = [];

    /// <summary>
    /// Registers an overlay to be applied after grid measurement.
    /// No two overlays may start at the same cell, and overlays must not overlap.
    /// </summary>
    public void AddOverlay(CliOverlay overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        if (overlay.Start.Column < 0 || overlay.Start.Column >= ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(overlay), $"Overlay start column {overlay.Start.Column} is outside the grid.");
        if (overlay.Start.Row < 0 || overlay.Start.Row >= RowCount)
            throw new ArgumentOutOfRangeException(nameof(overlay), $"Overlay start row {overlay.Start.Row} is outside the grid.");

        // Build the set of (col, row) cells occupied by the new overlay.
        var newCells = new HashSet<(int col, int row)>(overlay.LogicalLength);
        for (int i = 0; i < overlay.LogicalLength; i++)
        {
            int col = overlay.Orientation == Enums.CliOrientation.Horizontal
                ? overlay.Start.Column + i
                : overlay.Start.Column;
            int row = overlay.Orientation == Enums.CliOrientation.Vertical
                ? overlay.Start.Row + i
                : overlay.Start.Row;
            newCells.Add((col, row));
        }

        // Check duplicate start cell and cell-level overlap with existing overlays.
        foreach (var existing in _overlays)
        {
            if (existing.Start == overlay.Start)
                throw new TigerCliException(
                    $"An overlay already starts at ({overlay.Start.Column}, {overlay.Start.Row}).",
                    TigerCliRenderStage.InvalidUsage);

            for (int i = 0; i < existing.LogicalLength; i++)
            {
                int col = existing.Orientation == Enums.CliOrientation.Horizontal
                    ? existing.Start.Column + i
                    : existing.Start.Column;
                int row = existing.Orientation == Enums.CliOrientation.Vertical
                    ? existing.Start.Row + i
                    : existing.Start.Row;

                if (newCells.Contains((col, row)))
                    throw new TigerCliException(
                        $"Overlay starting at ({overlay.Start.Column}, {overlay.Start.Row}) overlaps an existing overlay at cell ({col}, {row}).",
                        TigerCliRenderStage.InvalidUsage);
            }
        }

        _overlays.Add(overlay);
        InvalidateLayout();
    }

    private CliGrid? parent = null;
    /// <summary>Parent grid when this grid is hosted as a subgrid; set automatically by <see cref="SetSubgrid"/>.</summary>
    public CliGrid? ParentGrid { get => parent; internal set => parent = value; }

    /// <summary>Logical cursor/selection anchor position within the grid.</summary>
    public ActivePoint? ActivePoint { get; set; }

    /// <summary>Cursor visibility mode.</summary>
    public CursorMode CursorMode { get; set; } = CursorMode.Hidden;

    /// <summary>Result of mapping <see cref="ActivePoint"/> through the measurement pipeline.</summary>
    public MeasuredActivePoint? MeasuredActivePoint { get; private set; }

    /// <summary>
    /// Per-grid override for treating <see cref="DBNull.Value"/> as <c>null</c>.
    /// When <c>null</c>, falls back to <see cref="Terminal.TigerConsole.TreatDbNullAsNull"/>.
    /// </summary>
    public bool? TreatDbNullAsNull { get; set; }

    // Total padding width contribution for a cell (1 for Left/Right, 2 for Both).
    // Returns 0 for frame cells and width-locked columns — these cannot reserve padding space.
    // Centralizes the exclusion rule so InitializeMeasuredCells, ApplyAlignmentAndFill,
    // and the wrapping pass agree on which cells participate in padding.
    private int GetCellPaddingWidth(int column, int row, CliCellStyle style)
    {
        if (cells[row, column]?.IsFrameCell ?? false) return 0;
        if (columns[column]?.IsWidthLocked ?? false) return 0;
        return style.Padding switch
        {
            CliCellPadding.Left or CliCellPadding.Right => 1,
            CliCellPadding.Both => 2,
            _ => 0
        };
    }

    /// <summary>
    /// Returns the effective style for a cell after default, row, column, and cell styles are merged.
    /// </summary>
    public CliCellStyle GetCellStyle(int column, int row)
    {
        CliCellStyle style = GlobalDefaultStyle.MergeWith(DefaultCellStyle);
        CliCellStyle? columnStyle = columns[column]?.Style;
        CliCellStyle? rowStyle = rows[row]?.Style;
        CliCellStyle? cellStyle = cells[row, column]?.Style;

        style = StylePrecedence switch
        {
            CliStylePrecedence.RowOverColumn =>
                style.MergeWith(columnStyle)
                     .MergeWith(rowStyle),

            CliStylePrecedence.ColumnOverRow =>
                style.MergeWith(rowStyle)
                     .MergeWith(columnStyle),

            _ => style
        };

        style = style.MergeWith(cellStyle);

        // Frame cells are structural: they take colours from the cascade (banded backgrounds must
        // stay continuous across separators) but never content decorations or hyperlink targets
        // from axis or default styles — decorations merge additively, so e.g. an underline on a
        // data axis would otherwise leak onto every border/separator glyph crossing it. A frame
        // glyph renders with its configured frame style only.
        if ((cells[row, column]?.IsFrameCell ?? false) && style.CharStyle is { } mergedCharStyle)
        {
            var frameCharStyle = cellStyle?.CharStyle;
            style.CharStyle = new CliCharStyle(
                mergedCharStyle.Foreground,
                mergedCharStyle.Background,
                frameCharStyle?.Decorations ?? CliTextDecoration.None)
            {
                HyperlinkTarget = frameCharStyle?.HyperlinkTarget
            };
        }

        return style;
    }



    /// <summary>
    /// Clears measurement state for this grid and any parent grid that depends on it.
    /// </summary>
    public void InvalidateLayout()
    {
        IsMeasured = false;
        measuredCells = null;
        measuredColumnWidths = null;
        measuredRowHeights = null;
        measuredWidth = null;
        measuredHeight = null;
        MeasuredActivePoint = null;

        if (ParentGrid != null)
        {
            ParentGrid.InvalidateLayout();
        }
    }

    /// <summary>
    /// Sets the content and optional style/span at a cell coordinate.
    /// </summary>
    /// <remarks>
    /// Content is formatted by the cell's effective <see cref="CliCellStyle"/> during measurement.
    /// Use <see cref="SetSubgrid"/> when the content is another <see cref="CliGrid"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The coordinate or span is outside the grid, or
    /// a span is less than one.</exception>
    /// <exception cref="TigerCliException">The target conflicts with an existing covered, frame, or
    /// differently-spanned cell.</exception>
    public void Set(
        int column,
        int row,
        object? content,
        CliCellStyle? style = null,
        int colSpan = 1,
        int rowSpan = 1)
    {
        Set(column, row, content, false, null, style, colSpan, rowSpan);
    }

    /// <summary>
    /// Hosts another grid in a cell, optionally making that host cell scrollable.
    /// </summary>
    /// <remarks>
    /// The hosted grid's <see cref="ParentGrid"/> is assigned automatically. A grid can host multiple
    /// scrollable cells; each cell keeps its own offsets. Re-setting the same coordinate with a
    /// different scroll mode or thumb mode is invalid.
    /// </remarks>
    public void SetSubgrid(int column, int row, CliGrid subgrid, CliScrollMode scrollMode = CliScrollMode.None, CliScrollThumbMode thumbMode = CliScrollThumbMode.Offset)
    {
        Set(column, row, null, false, subgrid, null, 1, 1, false, false, scrollMode, thumbMode);        
    }

    internal void Set(
        int column,
        int row,
        object? content,
        bool isFrameCell,
        CliGrid? subgrid = null,
        CliCellStyle? style = null,        
        int colSpan = 1,
        int rowSpan = 1,
        bool fillHorizontal = false,
        bool fillVertical = false, 
        CliScrollMode scrollMode = CliScrollMode.None,
        CliScrollThumbMode thumbMode = CliScrollThumbMode.Offset)
    {
        if (TreatDbNullAsNull ?? Terminal.TigerConsole.TreatDbNullAsNull)
        {
            if (content is DBNull)
                content = null;
        }

        if (column < 0 || column >= ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(column), $"Column index must be in [0, {ColumnCount - 1}].");

        if (row < 0 || row >= RowCount)
            throw new ArgumentOutOfRangeException(nameof(row), $"Row index must be in [0, {RowCount - 1}].");

        if (colSpan < 1)
            throw new ArgumentOutOfRangeException(nameof(colSpan), "ColSpan must be at least 1.");

        if (rowSpan < 1)
            throw new ArgumentOutOfRangeException(nameof(rowSpan), "RowSpan must be at least 1.");

        if (column + colSpan > ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(colSpan), "Cell exceeds grid column bounds.");

        if (row + rowSpan > RowCount)
            throw new ArgumentOutOfRangeException(nameof(rowSpan), "Cell exceeds grid row bounds.");
        if (fillHorizontal && fillVertical)
            throw new ArgumentException("Cannot set both fillHorizontal and fillVertical");
        if ((fillHorizontal || fillVertical) && !isFrameCell)
            throw new ArgumentException("FillHorizontal and fillVertical could only be set if isFrameCell is set");

        var current = cells[row, column];
        if (current != null)
        {
            if (current.IsCovered)
                throw new TigerCliException("Cell is covered by a spanning cell.", TigerCliRenderStage.InvalidUsage);
            if (current.IsFrameCell && !isFrameCell)
                throw new TigerCliException("Target anchor already contains a frame cell.", TigerCliRenderStage.InvalidUsage);
            if (!current.IsFrameCell && isFrameCell)
                throw new TigerCliException("Target anchor already contains a non-frame cell.", TigerCliRenderStage.InvalidUsage);
            if (current.RowSpan != rowSpan || current.ColSpan != colSpan)
                throw new TigerCliException("Cannot change the span of an already set cell", TigerCliRenderStage.InvalidUsage);
        }
        if (scrollMode != CliScrollMode.None)
        {
            if (_scrollCells.TryGetValue((column, row), out var existing))
            {
                if (existing.Mode != scrollMode)
                    throw new TigerCliException("Cannot change scroll mode of an already set scrollable subgrid.", TigerCliRenderStage.InvalidUsage);
                if (existing.ThumbMode != thumbMode)
                    throw new TigerCliException("Cannot change thumb mode of an already set scrollable subgrid.", TigerCliRenderStage.InvalidUsage);
            }
            else
            {
                _scrollCells[(column, row)] = new CliScrollableCell(column, row, scrollMode, thumbMode);
            }
        }
        CliGridCell cell;
        if (subgrid != null)
        {
            cell = new CliGridCell(subgrid, style, colSpan, rowSpan);
            subgrid.ParentGrid = this;
        }
        else
        {
            cell = new CliGridCell(
            content: content,
            style: CliCellStyle.Clone(style),
            colSpan: colSpan,
            rowSpan: rowSpan
        )
            {
                FillHorizontal = fillHorizontal,
                FillVertical = fillVertical,
                IsFrameCell = isFrameCell
            };
        }
        

        cells[row, column] = cell;

        if (colSpan > 1 || rowSpan > 1)
        {
            for (var r = 0; r < rowSpan; r++)
            {
                for (var c = 0; c < colSpan; c++)
                {
                    if ((r + c) > 0)
                    {
                        CliGridCell? offsetCell = cells[row + r, column + c];
                        if (offsetCell != null)
                        {
                            if ((offsetCell.RowOffset != r) || (offsetCell.ColOffset != c))
                            {
                                throw new TigerCliException("Cannot place span: target slot already populated.", TigerCliRenderStage.InvalidUsage);
                            }
                        }
                        offsetCell = new CliGridCell
                        (
                            content: null,
                            style: null,
                            colSpan: 1,
                            rowSpan: 1
                        )
                        {
                            RowOffset = r,
                            ColOffset = c
                        };
                        cells[row + r, column + c] = offsetCell;
                    }
                }
            }
        }

        InvalidateLayout();
    }

    /// <summary>
    /// Sets the style definition for a row.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="row"/> is outside the grid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <c>null</c>.</exception>
    /// <exception cref="TigerCliException">The row height was locked by frame layout and the new
    /// definition is incompatible with the locked height.</exception>
    public void SetRow(int row, CliGridRowDefinition definition)
    {
        if (row < 0 || row >= RowCount)
            throw new ArgumentOutOfRangeException(nameof(row), $"Row index must be in [0, {RowCount - 1}].");
        ArgumentNullException.ThrowIfNull(definition);

        var existing = rows[row];
        bool wasLocked = existing?.IsHeightLocked == true;
        int? lockedHeight = wasLocked ? existing!.Style?.Height : null;

        // If the row was previously locked, ensure the new style is compatible with the locked height.
        if (wasLocked && lockedHeight.HasValue && definition.Style is not null)
        {
            if (!definition.Style.IsHeightCompatible(lockedHeight.Value))
                throw new TigerCliException($"Row {row} height is locked to {lockedHeight} and the new style is incompatible.",
                    TigerCliRenderStage.InvalidUsage);
        }

        // Clone incoming definition (deep copy of Style)
        var cloned = CliGridRowDefinition.Clone(definition);

        // If it was locked before, keep it locked and re-apply the locked Height
        if (wasLocked)
        {
            cloned.IsHeightLocked = true;
            cloned.Style ??= new CliCellStyle();
            // Re-apply the locked value to ensure nothing downstream can change it
            if (lockedHeight.HasValue)
                cloned.Style.Height = lockedHeight.Value;
        }

        rows[row] = cloned;
        InvalidateLayout();
    }

    /// <summary>
    /// Sets the style and sizing definition for a column.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="column"/> is outside the grid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <c>null</c>.</exception>
    /// <exception cref="TigerCliException">The column width was locked by frame layout and the new
    /// definition is incompatible with the locked width.</exception>
    public void SetColumn(int column, CliGridColumnDefinition definition)
    {
        if (column < 0 || column >= ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(column), $"Column index must be in [0, {ColumnCount - 1}].");
        ArgumentNullException.ThrowIfNull(definition);

        var existing = columns[column];
        bool wasLocked = existing?.IsWidthLocked == true;   // use IsWidhtLocked if that's your current name
        int? lockedWidth = wasLocked ? existing!.Style?.Width : null;

        // If the column was previously locked, ensure the new style is compatible with the locked width.
        if (wasLocked && lockedWidth.HasValue && definition.Style is not null)
        {
            if (!definition.Style.IsWidthCompatible(lockedWidth.Value))
                throw new TigerCliException($"Column {column} width is locked to {lockedWidth} and the new style is incompatible.",
                    TigerCliRenderStage.InvalidUsage);
        }

        // Clone incoming definition (deep copy of Style)
        var cloned = CliGridColumnDefinition.Clone(definition);

        // If it was locked before, keep it locked and re-apply the locked Width
        if (wasLocked)
        {
            cloned.IsWidthLocked = true; // or IsWidhtLocked
            cloned.Style ??= new CliCellStyle();
            if (lockedWidth.HasValue)
                cloned.Style.Width = lockedWidth.Value;
        }

        columns[column] = cloned;
        InvalidateLayout();
    }


    /// <summary>
    /// Sets a row or column definition selected by <paramref name="axis"/>. For
    /// <see cref="CliGridAxis.Row"/>, <paramref name="row"/> is used; for
    /// <see cref="CliGridAxis.Column"/>, <paramref name="column"/> is used.
    /// </summary>
    public void SetAxis(int column, int row, CliGridAxis axis, CliGridAxisDefinition definition)
    {
        switch (axis)
        {
            case CliGridAxis.Row:
                SetRow(row, (CliGridRowDefinition)definition);
                break;
            case CliGridAxis.Column:
                SetColumn(column, (CliGridColumnDefinition)definition);
                break;
        }
    }


    internal void LockColumnWidth(int column, int width = 1)
    {
        if (column < 0 || column >= ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(column), $"Column must be in [0, {ColumnCount - 1}].");
        if (width < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be >= 1.");
        if (IsFrameLayoutInitialized)
            throw new TigerCliException("Cannot lock column width after frame layout has been initialized.",
                TigerCliRenderStage.InvalidUsage);

        var def = columns[column];
        if (def is not null)
        {
            var style = def.Style;
            if (style is not null && !style.IsWidthCompatible(width))
                throw new TigerCliException("Column width already constrained incompatibly.",
                    TigerCliRenderStage.InvalidUsage);

            if (def.IsWidthLocked)
                return; // idempotent (already locked to a compatible value)
        }
        else
        {
            def = new CliGridColumnDefinition();
            columns[column] = def;
        }

        def.Style ??= new CliCellStyle();
        def.Style.Width = width;

        def.IsWidthLocked = true;
        InvalidateLayout();
    }

    internal void LockRowHeight(int row, int height = 1)
    {
        if (row < 0 || row >= RowCount)
            throw new ArgumentOutOfRangeException(nameof(row), $"Row must be in [0, {RowCount - 1}].");
        if (height < 1)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be >= 1.");
        if (IsFrameLayoutInitialized)
            throw new TigerCliException("Cannot lock row height after frame layout has been initialized.",
                TigerCliRenderStage.InvalidUsage);

        var def = rows[row];
        if (def is not null)
        {
            var style = def.Style;
            if (style is not null && !style.IsHeightCompatible(height))
                throw new TigerCliException("Row height already constrained incompatibly.",
                    TigerCliRenderStage.InvalidUsage);

            if (def.IsHeightLocked)
                return; // idempotent (already locked to a compatible value)
        }
        else
        {
            def = new CliGridRowDefinition();
            rows[row] = def;
        }

        def.Style ??= new CliCellStyle();
        def.Style.Height = height;

        def.IsHeightLocked = true;
        InvalidateLayout();
    }


    /// <summary>
    /// Adds a rectangular frame area to be expanded into frame cells during measurement.
    /// </summary>
    /// <exception cref="TigerCliException">Frame layout has already been initialized.</exception>
    public CliFrameArea AddFrameArea(CliFrameJoinStyle joinStyle, int firstColumn, int firstRow, int lastColumn, int lastRow,
        CliCharStyle? charStyle = null)
    {
        if (IsFrameLayoutInitialized)
            throw new TigerCliException("Cannot add frame area after layout has been initialized.",
                    TigerCliRenderStage.InvalidUsage);
        CliFrameArea area = new(this, joinStyle, firstColumn, firstRow, lastColumn, lastRow, charStyle);
        frameAreas.Add(area);
        return area;
    }

    internal void InitializeFrameLayout()
    {
        if (IsFrameLayoutInitialized)
            throw new TigerCliException("Frame layout has already been initialized.",
                    TigerCliRenderStage.Unknown);
        foreach (var area in frameAreas)
            area.InitializeFrameLayout();
        IsFrameLayoutInitialized = true;
    }

    /// <summary>
    /// Runs the layout pipeline for this grid against the provided render sink.
    /// </summary>
    /// <remarks>
    /// Measurement expands frames, initializes cells, applies wrapping/truncation, resolves axis
    /// sizes, aligns and fills content, applies overlays, and records measured dimensions. Subgrids
    /// contribute to parent sizing on non-scrolling axes; scrollable axes are sized by the parent
    /// constraints and expose scroll information through the scroll-info methods.
    /// </remarks>
    public void Measure(Terminal.ICliRenderSink sink)
    {
        // Pass 1: Frame layout.
        if (!IsFrameLayoutInitialized)
        {
            InitializeFrameLayout();
        }
        var output = new MeasuredCell[RowCount, ColumnCount];
        var columnWidths = new int[ColumnCount];
        var rowHeights = new int[RowCount];

        // Pass 2: Initialize 
        InitializeMeasuredCells(output, columnWidths, rowHeights, sink);

        
        
        // After InitializeMeasuredCells(output, columnWidths, rowHeights);
        var softMaxW = SoftMaxWidth ?? sink.SoftMaxWidth;
        var softMaxH = SoftMaxHeight ?? sink.SoftMaxHeight;

        // Hard maximums: grid-level takes precedence; fall back to sink if positive
        int? maxW = this.MaxWidth ?? (sink.MaxWidth is > 0 ? sink.MaxWidth : null);
        int? maxH = this.MaxHeight ?? (sink.MaxHeight is > 0 ? sink.MaxHeight : null);

        ApplyWrappingAndResizing(output, columnWidths, rowHeights, softMaxW, softMaxH, maxW, maxH);

        // If this grid has no ActivePoint of its own but a hosted subgrid does,
        // adopt the host cell as the parent ActivePoint so the rest of the
        // pipeline (scroll offset, scrollbar thumb mode, etc.) can rely on it.
        TryAutoPropagateActivePointFromSubgrid();

        // Map ActivePoint through content measurement (wrapping/truncation)
        MeasuredActivePoint = MapActivePointAfterWrapping(output);

        // Final pass - H/V alignments & frame fills
        ApplyAlignmentAndFill(output, columnWidths, rowHeights);

        // Adjust ActivePoint for alignment shifts
        AdjustActivePointForAlignment(output, columnWidths, rowHeights);

        // Clamp ActivePoint after clipping
        ClampActivePoint(output);

        measuredCells = output;
        measuredColumnWidths = columnWidths;
        measuredRowHeights = rowHeights;
        var w = 0;
        var h = 0;
        for (var r = 0; r < RowCount; r++)
            h += measuredRowHeights[r];
        for (var c = 0; c < ColumnCount; c++)
            w += measuredColumnWidths[c];
        measuredHeight = h;
        measuredWidth = w;

        // Overlay pass — applied after all layout, before finalising measuredCells.
        ApplyOverlays(output, columnWidths, rowHeights);

        IsMeasured = true;
    }

    private void ApplyOverlays(MeasuredCell[,] measured, int[] columnWidths, int[] rowHeights)
    {
        if (_overlays.Count == 0)
            return;

        // Build physical-coordinate prefix sums: colX[c] = screen X of the left edge of grid column c,
        // rowY[r] = screen Y of the top edge of grid row r.
        var colX = new int[ColumnCount];
        var rowY = new int[RowCount];
        for (int c = 1; c < ColumnCount; c++)
            colX[c] = colX[c - 1] + columnWidths[c - 1];
        for (int r = 1; r < RowCount; r++)
            rowY[r] = rowY[r - 1] + rowHeights[r - 1];

        foreach (var overlay in _overlays)
        {
            int startCol = overlay.Start.Column;
            int startRow = overlay.Start.Row;

            // --- Physical renderLength: sum of screen rows or columns covered ---
            int renderLength = 0;
            if (overlay.Orientation == Enums.CliOrientation.Vertical)
            {
                for (int i = 0; i < overlay.LogicalLength && startRow + i < RowCount; i++)
                    renderLength += rowHeights[startRow + i];
            }
            else
            {
                for (int i = 0; i < overlay.LogicalLength && startCol + i < ColumnCount; i++)
                    renderLength += columnWidths[startCol + i];
            }

            // Overlays apply through a single styled path: a plain CliOverlayRenderer is adapted into a
            // glyph stream whose per-glyph style is null, so it resolves to the overlay's base Style and
            // single-style overlays render exactly as before.
            var (visible, content) = overlay.StyledRenderer(this, renderLength);
            if (!visible)
                continue;

            if (content is null || content.Length == 0)
                continue;

            if (content.Length > renderLength)
                throw new TigerCliException(
                    $"Overlay renderer returned {content.Length} characters but physical renderLength is {renderLength}.",
                    TigerCliRenderStage.Render);

            if (overlay.Orientation == Enums.CliOrientation.Vertical)
            {
                // The overlay occupies a single screen column (physX = left edge of startCol, char offset 0).
                // Each content[i] maps to a successive screen row starting at rowY[startRow].
                int physYBase = rowY[startRow];

                for (int i = 0; i < content.Length; i++)
                {
                    int physY = physYBase + i;

                    // Map physY → grid row + line index within that cell.
                    int gridRow = startRow;
                    int lineIdx = physY - rowY[startRow];
                    while (gridRow < RowCount - 1 && lineIdx >= rowHeights[gridRow])
                    {
                        lineIdx -= rowHeights[gridRow];
                        gridRow++;
                    }

                    // Character offset within the cell column is always 0 (leftmost char of the column).
                    OverwriteCharInCell(measured[gridRow, startCol], lineIdx, 0,
                        content[i].Char, content[i].Style ?? overlay.Style);
                }
            }
            else // Horizontal
            {
                // The overlay occupies a single screen row (line 0 of the startRow cells).
                // Each content[i] maps to a successive screen column starting at colX[startCol].
                int physXBase = colX[startCol];

                for (int i = 0; i < content.Length; i++)
                {
                    int physX = physXBase + i;

                    // Map physX → grid column + char offset within that cell.
                    int gridCol = startCol;
                    int charOffset = physX - colX[startCol];
                    while (gridCol < ColumnCount - 1 && charOffset >= columnWidths[gridCol])
                    {
                        charOffset -= columnWidths[gridCol];
                        gridCol++;
                    }

                    // Line index within the cell row is always 0 (topmost line of the row).
                    OverwriteCharInCell(measured[startRow, gridCol], 0, charOffset,
                        content[i].Char, content[i].Style ?? overlay.Style);
                }
            }
        }
    }

    /// <summary>
    /// Overwrites a single character at <paramref name="charOffset"/> on <paramref name="lineIdx"/>
    /// inside <paramref name="cell"/>'s segment list, applying <paramref name="style"/>.
    /// </summary>
    private static void OverwriteCharInCell(
        MeasuredCell? cell, int lineIdx, int charOffset, char ch, CliCharStyle style)
    {
        if (cell is null || lineIdx >= cell.Lines.Count)
            return;

        var newLines = new List<List<CliTextSegment>>(cell.Lines.Count);
        for (int l = 0; l < cell.Lines.Count; l++)
        {
            newLines.Add(l == lineIdx
                ? OverwriteCharInLine(cell.Lines[l], charOffset, ch, style)
                : cell.Lines[l]);
        }
        cell.UpdateLines(newLines);
    }

    /// <summary>
    /// Returns a new line with the character at <paramref name="charOffset"/> replaced by
    /// <paramref name="ch"/> styled with <paramref name="style"/>.
    /// Segments outside the target offset are preserved unchanged.
    /// </summary>
    private static List<CliTextSegment> OverwriteCharInLine(
        List<CliTextSegment> line, int charOffset, char ch, CliCharStyle style)
    {
        var result = new List<CliTextSegment>(line.Count + 2);
        int pos = 0;
        bool written = false;

        foreach (var seg in line)
        {
            int segLen = seg.Text.Length;
            int segEnd = pos + segLen;

            if (!written && charOffset >= pos && charOffset < segEnd)
            {
                int local = charOffset - pos;
                var composedStyle = style;
                if (composedStyle.Background is null)
                    composedStyle.Background = seg.Style.Background;

                if (local > 0)
                    result.Add(new CliTextSegment(seg.Text[..local], seg.Style));
                result.Add(new CliTextSegment(ch.ToString(), composedStyle));
                if (local + 1 < segLen)
                    result.Add(new CliTextSegment(seg.Text[(local + 1)..], seg.Style));
                written = true;
            }
            else
            {
                result.Add(seg);
            }

            pos = segEnd;
        }

        return result;
    }

    private void ApplyAlignmentAndFill(MeasuredCell[,] measured, int[] columnWidths, int[] rowHeights)
    {
        for (int row = 0; row < RowCount; row++)
        {
            for (int col = 0; col < ColumnCount; col++)
            {
                var m = measured[row, col];
                if (m == null)
                {
                    //TigerConsole.Logger?.LogTrace("Cell ({Col},{Row}); Measured is null!", col, row);
                    continue;
                }
                if (m.IsCovered)
                {
                    //TigerConsole.Logger?.LogTrace("Cell ({Col},{Row}): IsCovered", col, row);
                    continue;
                }
                var gridCell = cells[row, col];
                string fillStr = " ";
                if (gridCell != null && gridCell.IsFrameCell && (gridCell.FillHorizontal || gridCell.FillVertical))
                    fillStr = gridCell.Content?.ToString() ?? " ";
                int targetW = columnWidths[col];
                int targetH = rowHeights[row];
                if (targetW < 1 || targetH < 1)
                {
                    //TigerConsole.Logger?.LogTrace("Cell ({Col},{Row}): TargetW: {TargetW}, TargetH: {TargetH}", col, row, targetW, targetH);
                }
                int colSpan = gridCell?.ColSpan ?? 1;
                int rowSpan = gridCell?.RowSpan ?? 1;
                if (colSpan > 1)
                {
                    for (int c = 1; c < colSpan; c++)
                        targetW += columnWidths[col + c];
                }
                if (rowSpan > 1)
                {
                    for (int r = 1; r < rowSpan; r++)
                        targetH += rowHeights[row + r];
                }

                var style = m.Style;
                var charStyle = style.CharStyle ?? DefaultCharStyle;
                var lines = MeasuredCell.CloneLines(m.Lines); // work on a copy

                m.TotalLinesCount = m.Lines.Count;

                // Resolve scroll offsets for scrollable hosted subgrids. A grid may host several
                // scrollable cells; only the cell under the grid's active point runs active-point-
                // follow. Every scrollable cell (active or not) clamps its stored offset to the
                // current content range and slices its rendered lines by that offset, so an inactive
                // region keeps its position and never jumps when focus moves away and back.
                if (_scrollCells.TryGetValue((col, row), out var scrollCell) && gridCell?.HasSubgrid == true)
                {
                    var subgrid = gridCell.Subgrid!;
                    var ap = subgrid.MeasuredActivePoint;
                    var scrollMode = scrollCell.Mode;

                    bool isActive = MeasuredActivePoint != null
                                    && MeasuredActivePoint.Column == col
                                    && MeasuredActivePoint.Row == row;

                    if (isActive && ap != null)
                    {
                        if (scrollMode.HasFlag(CliScrollMode.Vertical))
                        {
                            int rowStartLine = 0;
                            for (int r = 0; r < ap.Row; r++)
                                rowStartLine += subgrid.GetMeasuredRowHeight(r) ?? 0;

                            int rowHeight = subgrid.GetMeasuredRowHeight(ap.Row) ?? 1;
                            int absLineIdx = rowStartLine + ap.LineIndex; // Specific line the cursor is on

                            if (rowHeight <= targetH)
                            {
                                // Row fits in viewport -> bring entire row into view if any part is out
                                if (rowStartLine < scrollCell.ScrollOffsetY)
                                {
                                    scrollCell.ScrollOffsetY = rowStartLine;
                                }
                                else if (rowStartLine + rowHeight > scrollCell.ScrollOffsetY + targetH)
                                {
                                    scrollCell.ScrollOffsetY = rowStartLine + rowHeight - targetH;
                                }
                            }
                            else
                            {
                                // Row is taller than viewport -> bring cursor line into view
                                if (absLineIdx < scrollCell.ScrollOffsetY)
                                {
                                    scrollCell.ScrollOffsetY = absLineIdx;
                                }
                                else if (absLineIdx >= scrollCell.ScrollOffsetY + targetH)
                                {
                                    scrollCell.ScrollOffsetY = absLineIdx - targetH + 1;
                                }
                            }

                            // Clamp to valid content range
                            int maxOffY = Math.Max(0, m.Lines.Count - targetH);
                            scrollCell.ScrollOffsetY = Math.Clamp(scrollCell.ScrollOffsetY, 0, maxOffY);
                        }

                        if (scrollMode.HasFlag(CliScrollMode.Horizontal))
                        {
                            int absColIdx = GetAbsoluteSubgridColumn(subgrid, ap);
                            int totalWidth = HorizontalScrollableTotalWidth(m, scrollCell, subgrid, ap);

                            if (absColIdx < scrollCell.ScrollOffsetX)
                            {
                                scrollCell.ScrollOffsetX = absColIdx;
                            }
                            else if (absColIdx >= scrollCell.ScrollOffsetX + targetW)
                            {
                                scrollCell.ScrollOffsetX = absColIdx - targetW + 1;
                            }

                            int maxOffX = Math.Max(0, totalWidth - targetW);
                            scrollCell.ScrollOffsetX = Math.Clamp(scrollCell.ScrollOffsetX, 0, maxOffX);
                        }
                    }
                    else
                    {
                        // Inactive cell (or active cell whose subgrid produced no active point):
                        // do not follow, but clamp stale offsets so shrunk content cannot leave the
                        // viewport scrolled past its end.
                        if (scrollMode.HasFlag(CliScrollMode.Vertical))
                        {
                            int maxOffY = Math.Max(0, m.Lines.Count - targetH);
                            scrollCell.ScrollOffsetY = Math.Clamp(scrollCell.ScrollOffsetY, 0, maxOffY);
                        }
                        if (scrollMode.HasFlag(CliScrollMode.Horizontal))
                        {
                            int totalWidth = HorizontalScrollableTotalWidth(m, scrollCell, subgrid, ap);
                            int maxOffX = Math.Max(0, totalWidth - targetW);
                            scrollCell.ScrollOffsetX = Math.Clamp(scrollCell.ScrollOffsetX, 0, maxOffX);
                        }
                    }

                    // Perform Viewport Slicing (Vertical) — always uses the cell's stored offset.
                    if (scrollMode.HasFlag(CliScrollMode.Vertical))
                    {
                        int offY = scrollCell.ScrollOffsetY;
                        if (offY > 0 || m.Lines.Count > targetH)
                        {
                            m.TotalLinesCount = m.Lines.Count;
                            lines = lines.Skip(offY).Take(targetH).ToList();
                        }
                    }

                    // Perform Viewport Slicing (Horizontal) — always uses the cell's stored offset.
                    if (scrollMode.HasFlag(CliScrollMode.Horizontal))
                    {
                        int offX = scrollCell.ScrollOffsetX;
                        if (offX > 0 || lines.Any(line => CliTextSegment.Length(line) > targetW))
                        {
                            var slicedLines = new List<List<CliTextSegment>>(lines.Count);
                            foreach (var line in lines)
                                slicedLines.Add(SliceLine(line, offX, targetW));
                            lines = slicedLines;
                        }
                    }
                }

                var addRows = Math.Max(0, targetH - lines.Count);
                int addRowsBefore = 0;
                var fillVertical = gridCell?.FillVertical ?? false;
                var fillHorizontal = gridCell?.FillHorizontal ?? false;

                // Padding and alignment fill are whitespace, not content: they keep the cell's
                // colours but must not carry content decorations (an underlined link value would
                // otherwise underline its own padding) or hyperlink targets. Frame cells fill with
                // their frame glyph and keep their configured frame style untouched.
                var fillCharStyle = (gridCell?.IsFrameCell ?? false)
                    ? charStyle
                    : new CliCharStyle(charStyle.Foreground, charStyle.Background);

                List<CliTextSegment> fillLineContent = [new CliTextSegment(fillVertical ? fillStr : " ", fillCharStyle)];

                var alignedLines = new List<List<CliTextSegment>>(targetH);
                if (!fillVertical)
                {
                    switch (style.VerticalAlignment)
                    {
                        case CliVerticalAlignment.Bottom:
                            addRowsBefore = addRows;
                            break;
                        case CliVerticalAlignment.Center:
                            addRowsBefore = addRows / 2;
                            break;
                    }
                }
                // Padding reserves 1 space at left/right (or both) of the cell content.
                // GetCellPaddingWidth handles the frame/locked exclusion; resolve left/right split here.
                int padLeft = 0, padRight = 0;
                if (GetCellPaddingWidth(col, row, style) > 0 && style.Padding is CliCellPadding pad)
                {
                    if (pad == CliCellPadding.Left || pad == CliCellPadding.Both) padLeft = 1;
                    if (pad == CliCellPadding.Right || pad == CliCellPadding.Both) padRight = 1;
                }
                int alignTargetW = Math.Max(0, targetW - padLeft - padRight);

                var align = fillHorizontal ? CliTextAlignment.Left : (style.HorizontalAlignment ?? CliTextAlignment.Left);
                for (int h = 0; h < targetH; h++)
                {
                    int src = h - addRowsBefore;
                    var srcLine = (src >= 0 && src < lines.Count) ? lines[src] : fillLineContent;
                    var line = MeasuredCell.CloneLine(srcLine);
                    AlignHorizontally(line, alignTargetW, align, fillStr, fillCharStyle);
                    if (padLeft > 0)
                        line.Insert(0, new CliTextSegment(" ", fillCharStyle));
                    if (padRight > 0)
                        line.Add(new CliTextSegment(" ", fillCharStyle));
                    CompactLine(line);
                    alignedLines.Add(line);
                }

                // Carry color context across line boundaries.
                // Each line after the first receives a zero-length leader segment whose
                // FG/BG match whatever was effective at the end of the preceding line.
                // This preserves color state for the render sink without any changes
                // to the render pass — the sink sets colors and writes "".
                {
                    CliColor? effectiveFg = charStyle.Foreground;
                    CliColor? effectiveBg = charStyle.Background;
                    for (int li = 0; li < alignedLines.Count; li++)
                    {
                        var aline = alignedLines[li];
                        
                        if (effectiveFg.HasValue && effectiveBg.HasValue)
                        {
                            aline.Insert(0, new CliTextSegment(string.Empty, new CliCharStyle(effectiveFg, effectiveBg)));
                        }                        
                        
                        foreach (var seg in aline)
                        {
                            if (seg.Style.Foreground.HasValue) effectiveFg = seg.Style.Foreground;
                            if (seg.Style.Background.HasValue) effectiveBg = seg.Style.Background;
                        }
                    }
                }

                m.UpdateLines(alignedLines);
                if (rowSpan > 1)
                {
                    var offset = rowHeights[row];
                    for (int r = 1; r < rowSpan; r++)
                    {
                        var h = rowHeights[row + r];
                        var cm = measured[row + r, col];
                        var coveredLines = new List<List<CliTextSegment>>();
                        for (int i = 0; i < h; i++)
                            coveredLines.Add(alignedLines[offset + i]);
                        cm.UpdateLines(coveredLines);
                        offset += h;
                    }
                }
            }
        }
    }

    private static int MaxLineWidth(IReadOnlyList<List<CliTextSegment>> lines)
    {
        int width = 0;
        for (int i = 0; i < lines.Count; i++)
            width = Math.Max(width, CliTextSegment.Length(lines[i]));
        return width;
    }

    private static int HorizontalScrollableTotalWidth(
        MeasuredCell cell,
        CliScrollableCell scrollCell,
        CliGrid? subgrid,
        MeasuredActivePoint? subgridActivePoint)
    {
        int contentWidth = MaxLineWidth(cell.InitialLines);
        int totalWidth = contentWidth;

        if (scrollCell.ThumbMode == CliScrollThumbMode.ActivePoint)
            totalWidth = Math.Max(totalWidth, contentWidth + 1);

        if (subgrid is not null && subgridActivePoint is not null)
            totalWidth = Math.Max(totalWidth, GetAbsoluteSubgridColumn(subgrid, subgridActivePoint) + 1);

        return totalWidth;
    }

    private static int GetAbsoluteSubgridColumn(CliGrid subgrid, MeasuredActivePoint ap)
    {
        int absCol = ap.OffsetInLine;
        for (int c = 0; c < ap.Column; c++)
            absCol += subgrid.GetMeasuredColumnWidth(c) ?? 0;
        return absCol;
    }

    private static List<CliTextSegment> SliceLine(List<CliTextSegment> line, int offset, int width)
    {
        if (width <= 0)
            return [];

        var result = new List<CliTextSegment>();
        int pos = 0;
        int end = offset + width;

        foreach (var seg in line)
        {
            int segStart = pos;
            int segEnd = pos + seg.Text.Length;
            pos = segEnd;

            if (segEnd <= offset)
                continue;
            if (segStart >= end)
                break;

            int localStart = Math.Max(offset, segStart) - segStart;
            int localEnd = Math.Min(end, segEnd) - segStart;
            if (localEnd > localStart)
                result.Add(new CliTextSegment(seg.Text[localStart..localEnd], seg.Style));
        }

        if (result.Count == 0)
        {
            var style = line.Count > 0 ? line[^1].Style : DefaultCharStyle;
            result.Add(new CliTextSegment(string.Empty, style));
        }

        CompactLine(result);
        return result;
    }

    private static string RepeatTo(string text, int len)
    {
        var sb = new StringBuilder(len);
        while (sb.Length < len)
            sb.Append(text);
        sb.Length = len;
        return sb.ToString();
    }

    private static void AlignHorizontally(List<CliTextSegment> line, int targetW, CliTextAlignment align, string fillStr, CliCharStyle charStyle)
    {
        var width = CliTextSegment.Length(line);
        if (width >= targetW)
            return;
        var pad = targetW - width;
        var left = align == CliTextAlignment.Left ? 0 : align == CliTextAlignment.Center ? pad / 2 : pad;
        var right = pad - left;
        if (left > 0)
            line.Insert(0, new CliTextSegment(RepeatTo(fillStr, left), charStyle));
        if (right > 0)
            line.Add(new CliTextSegment(RepeatTo(fillStr, right), charStyle));
        CompactLine(line);
    }

    // Derives a hyperlink target from the full visible text of a cell's segments and stamps it onto
    // every segment that has no explicit target yet. The target is computed once from the whole cell
    // (not per wrapped/split segment) so a wrapped or truncated link still points at the full value.
    // Whitespace-only content yields no link (e.g. an empty optional value).
    private static List<CliTextSegment> ApplyDerivedHyperlinkTarget(List<CliTextSegment> segments)
    {
        var sb = new StringBuilder();
        foreach (var seg in segments)
            sb.Append(seg.Text);
        var target = sb.ToString();

        if (string.IsNullOrWhiteSpace(target))
            return segments;

        var result = new List<CliTextSegment>(segments.Count);
        foreach (var seg in segments)
        {
            if (seg.Style.HyperlinkTarget is not null)
            {
                result.Add(seg); // explicit per-segment target wins
                continue;
            }

            var charStyle = seg.Style;
            charStyle.HyperlinkTarget = target;
            result.Add(new CliTextSegment(seg.Text, charStyle));
        }

        return result;
    }

    private void InitializeMeasuredCells(MeasuredCell[,] output, int[] columnWidths, int[] rowHeights, ICliRenderSink parentSink)
    {
        // Built once per measure pass so markup inside rendered labels can use semantic theme tokens
        // and app custom styles; raw colour aliases come from the active alias registry.
        var markupStyles = TigerConsole.CreateMarkupStyleResolver();
        var markupColorAliases = TigerConsole.ColorAliases;

        for (int col = 0; col < ColumnCount; col++)
            columnWidths[col] = 1;
        for (int row = 0; row < RowCount; row++)
        {
            rowHeights[row] = 1;
            for (int col = 0; col < ColumnCount; col++)
            {
                var gridCell = cells[row, col];
                
                // Resolve effective style for this slot (always inherit a style)
                var style = GetCellStyle(col, row); // column-major precedence already handled
                MeasuredCell cell;
                if (gridCell != null && gridCell.IsCovered)
                {
                    cell = new MeasuredCell(new List<List<CliTextSegment>>(), style);
                    cell.IsCovered = true;
                    var anchorCell = cells[row - gridCell.RowOffset, col - gridCell.ColOffset];
                    if (gridCell.ColOffset == 0)
                        cell.ColSpan = anchorCell?.ColSpan ?? 1;
                }
                else if (gridCell != null && gridCell.HasSubgrid)
                {
                    var sink = new TextSegmentLinesSink();

                    // Propagate constraints from the parent sink, narrowed by cell style limits.
                    // EffectiveMaxWidth/Height returns int.MaxValue when unconstrained, so treat that as "no limit".
                    int styleMaxW = style.EffectiveMaxWidth;
                    int styleMaxH = style.EffectiveMaxHeight;
                    int? cellMaxW = styleMaxW < int.MaxValue ? styleMaxW : null;
                    int? cellMaxH = styleMaxH < int.MaxValue ? styleMaxH : null;

                    // The resolved cell style for a *spanned* cell carries the anchor column's width
                    // (e.g. a frame-interior column locked to Width=1), which is meaningless as a cap
                    // on a cell that spans several columns: applying it would collapse the whole subgrid
                    // to the first column's width. A multi-column cell's real width comes from the span,
                    // resolved later in RemeasureSubgridCell, so drop the per-cell width cap here.
                    if (gridCell.ColSpan > 1)
                        cellMaxW = null;

                    // Soft WIDTH limit: this is the *natural* sizing pass whose only job is to learn
                    // the subgrid's content-driven width so the parent can size the hosting column/span.
                    // A soft-max width here is a fill ceiling, not a limit: a subgrid that contains a
                    // Star/fill column would grow to the inherited soft-max (e.g. the viewport) during
                    // this pass, corrupting its natural width and forcing the parent column far too wide.
                    // So the natural pass is left width-unconstrained (matching how plain text cells and
                    // horizontally-scrolling subgrids already measure); the real available width is
                    // applied later, span-aware, in RemeasureSubgridCell. The hard MaxWidth below still
                    // bounds genuinely runaway content.
                    sink.SoftMaxWidth = null;

                    // Soft HEIGHT limit: inherit from parent, then narrow by cell style max.
                    int? inheritedSoftH = parentSink.SoftMaxHeight;
                    if (cellMaxH.HasValue)
                        sink.SoftMaxHeight = inheritedSoftH.HasValue ? Math.Min(inheritedSoftH.Value, cellMaxH.Value) : cellMaxH;
                    else
                        sink.SoftMaxHeight = inheritedSoftH;

                    // Hard limits: inherit from parent, then narrow by cell style max
                    int? inheritedMaxW = parentSink.MaxWidth is > 0 ? parentSink.MaxWidth : null;
                    int? inheritedMaxH = parentSink.MaxHeight is > 0 ? parentSink.MaxHeight : null;
                    if (cellMaxW.HasValue)
                        sink.MaxWidth = inheritedMaxW.HasValue ? Math.Min(inheritedMaxW.Value, cellMaxW.Value) : cellMaxW;
                    else
                        sink.MaxWidth = inheritedMaxW;

                    if (cellMaxH.HasValue)
                        sink.MaxHeight = inheritedMaxH.HasValue ? Math.Min(inheritedMaxH.Value, cellMaxH.Value) : cellMaxH;
                    else
                        sink.MaxHeight = inheritedMaxH;

                    // Scrollable cells can overflow in the scroll direction, so relax
                    // soft and hard limits along the corresponding axis.
                    var scrollMode = GetScrollMode(col, row);
                    if (scrollMode.HasFlag(CliScrollMode.Horizontal))
                    {
                        sink.SoftMaxWidth = null;
                        sink.MaxWidth = null;
                    }
                    if (scrollMode.HasFlag(CliScrollMode.Vertical))
                    {
                        sink.SoftMaxHeight = null;
                        sink.MaxHeight = null;
                    }

                    var subgrid = gridCell.Subgrid!;
                    subgrid.InvalidateLayout();
                    subgrid.Measure(sink);
                    var lines = TigerConsole.RenderGridToSegmentedLines(subgrid, sink);

                    cell = new MeasuredCell(lines, style);
                    cell.ColSpan = gridCell?.ColSpan ?? 1;
                    cell.RowSpan = gridCell?.RowSpan ?? 1;
                    cell.Sink = sink;

                }
                else
                {
                    // Determine the text to render for this slot
                    string text = FormatCellContent(gridCell, style);

                    // Apply pass-2 wrap mode policy (no width-constrained wrapping yet)
                    text = NormalizeNewlines(text);

                    var segments = CliMarkupParser.Parse(text, style.CharStyle, markupStyles, markupColorAliases);

                    // Cell-level hyperlink: derive the target once from the full visible cell text and
                    // stamp it onto every segment that does not already carry an explicit target
                    // (explicit per-segment target always wins). Done here, before wrapping/splitting,
                    // so the full target survives wrap/truncate/reassembly via the carried CharStyle.
                    if (style.IsHyperlink == true)
                        segments = ApplyDerivedHyperlinkTarget(segments);

                    bool trimLineEnds = ShouldTrimLineEnds(style);
                    var lines = SplitSegmentsByNewlines(segments, trimLineEnds);
                    if (style.Wrapping?.Mode == CliWrapMode.SingleLine)
                    {
                        var singleLine = JoinSegmentsIntoSingleLine(lines, trimLineEnds);
                        lines = [singleLine];
                    }
                    cell = new MeasuredCell(lines, style);
                    cell.ColSpan = gridCell?.ColSpan ?? 1;
                    cell.RowSpan = gridCell?.RowSpan ?? 1;
                }
                output[row, col] = cell;

                // Scroll-aware sizing: subgrid cells contribute to parent column/row only
                // on non-scrolling axes. Scrolling axes are sized by parent constraints
                // (grown to fill SoftMax in ApplyWrappingAndResizing).
                var cellScroll = GetScrollMode(col, row);

                // Padding reserves column space around the content (1 for Left/Right, 2 for Both).
                int paddingW = GetCellPaddingWidth(col, row, style);
                int paddedCellWidth = Math.Max(cell.Width + paddingW, cell.Style.EffectiveMinWidth);

                if (cell.ColSpan == 1 && !cellScroll.HasFlag(CliScrollMode.Horizontal) && paddedCellWidth > columnWidths[col])
                    columnWidths[col] = paddedCellWidth;
                if (cell.RowSpan == 1 && !cellScroll.HasFlag(CliScrollMode.Vertical) && cell.Height > rowHeights[row])
                    rowHeights[row] = cell.Height;
            }
        }
    }
    private static string NormalizeNewlines(string s)
    {
        // Normalize CRLF/CR to LF so splitting is consistent
        if (s.Contains('\r'))
            s = s.Replace("\r\n", "\n").Replace('\r', '\n');
        return s;
    }

    private static string FormatCellContent(CliGridCell? cell, CliCellStyle style)
    {
        if (cell == null || cell.Content == null)
            return style.NullDisplayValue ?? string.Empty;

        if (style.FormattingMode == CliFormattingMode.Preformatted)
            return cell.Content.ToString() ?? string.Empty;

        string content;
        if (style.Formatter != null)
            content = style.Formatter.Format(cell.Content); // From delegate or format string
        else
            content = cell.Content.ToString() ?? string.Empty;
        return CliMarkupParser.Escape(content);
    }

    private static bool ShouldTrimLineEnds(CliCellStyle style)
        => !(style.FormattingMode == CliFormattingMode.Raw && style.Wrapping?.Mode == CliWrapMode.SingleLine);

    private static List<List<CliTextSegment>> SplitSegmentsByNewlines(IReadOnlyList<CliTextSegment> segments, bool trimLineEnds = true)
    {
        var lines = new List<List<CliTextSegment>>();
        var current = new List<CliTextSegment>();

        foreach (var seg in segments)
        {
            var text = seg.Text;
            int start = 0;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    if (i > start)
                        current.Add(new CliTextSegment(text[start..i], seg.Style));

                    if (trimLineEnds)
                        TrimLineEnds(current);
                    CompactLine(current);                    
                    lines.Add(current);

                    current = [];
                    start = i + 1;
                }
            }

            if (start < text.Length)
                current.Add(new CliTextSegment(text[start..], seg.Style));
        }

        if (trimLineEnds)
            TrimLineEnds(current);
        CompactLine(current);        
        lines.Add(current);

        return lines;
    }

    private static void TrimLineEnds(List<CliTextSegment> line)
    {
        if (line.Count == 0) return;

        // Trim start
        var first = line[0];
        var t0 = first.Text.TrimStart();
        if (!ReferenceEquals(t0, first.Text) && t0.Length != first.Text.Length)
            line[0] = new CliTextSegment(t0, first.Style);

        // Trim end
        var lastIdx = line.Count - 1;
        var last = line[lastIdx];
        var tN = last.Text.TrimEnd();
        if (!ReferenceEquals(tN, last.Text) && tN.Length != last.Text.Length)
            line[lastIdx] = new CliTextSegment(tN, last.Style);
    }

    private static void CompactLine(List<CliTextSegment> line)
    {
        if (line.Count == 0) return;

        // Remember the original first style for fallback
        var originalFirstStyle = line[0].Style;

        var compact = new List<CliTextSegment>(line.Count);
        foreach (var seg in line)
        {
            if (seg.Text.Length == 0) continue; // drop empty

            if (compact.Count > 0)
            {
                var prev = compact[^1];
                // Only merge runs whose full render-relevant identity matches (foreground, background,
                // decorations, hyperlink target). Comparing a subset would silently drop a difference
                // such as bold/underline or a link target when the runs are concatenated.
                if (prev.Style.HasSameRenderingAs(seg.Style))
                {
                    // merge with previous
                    compact[^1] =
                        new CliTextSegment(prev.Text + seg.Text, prev.Style);
                    continue;
                }
            }
            compact.Add(seg);
        }

        // If everything got trimmed away, keep one empty segment using the original style
        if (compact.Count == 0)
            compact.Add(new CliTextSegment(string.Empty, originalFirstStyle));

        line.Clear();
        line.AddRange(compact);
    }

    // Join lines into a single line for CliWrapMode.SingleLine.
    // Adds a space to the last segment of the current result before appending the next line.
    // Merges adjacent segments with the same style via CompactLine at the end.
    /// <summary>
    /// If this grid has a scrollable cell whose hosted subgrid has a
    /// <see cref="MeasuredActivePoint"/>, adopt the scrollable cell as this grid's
    /// <see cref="ActivePoint"/>. No-op when this grid already has its own
    /// ActivePoint, has no scrollable cell, or the scrollable cell's subgrid
    /// is inactive. Non-scrollable subgrids never trigger propagation.
    /// </summary>
    private void TryAutoPropagateActivePointFromSubgrid()
    {
        // (1) An explicitly set active point always wins — composite controls set it themselves.
        if (ActivePoint != null) return;
        if (_scrollCells.Count == 0) return;

        // (2) Adopt the active point from a scrollable subgrid cell that produced a measured
        // active point. Scan in deterministic (row, col) order so the choice is stable when more
        // than one scrollable cell qualifies. Non-scrollable subgrids are intentionally not
        // adopted here: that broader generalization belongs with the composite-control work.
        for (int row = 0; row < RowCount; row++)
        {
            for (int col = 0; col < ColumnCount; col++)
            {
                if (!_scrollCells.ContainsKey((col, row)))
                    continue;

                var gridCell = cells[row, col];
                if (gridCell == null || gridCell.IsCovered || !gridCell.HasSubgrid)
                    continue;

                if (gridCell.Subgrid!.MeasuredActivePoint == null)
                    continue;

                ActivePoint = new ActivePoint(col, row, 0);
                return;
            }
        }
    }

    /// <summary>
    /// After wrapping/truncation, map the original ActivePoint offset to (lineIndex, offsetInLine).
    /// </summary>
    private MeasuredActivePoint? MapActivePointAfterWrapping(MeasuredCell[,] output)
    {
        if (ActivePoint is null)
            return null;

        int col = ActivePoint.Column;
        int row = ActivePoint.Row;

        if (col < 0 || col >= ColumnCount || row < 0 || row >= RowCount)
            return null;

        // Only anchor cells own the ActivePoint
        var gridCell = cells[row, col];
        if (gridCell == null || gridCell.IsCovered)
            return null;

        // Host cell containing a subgrid: derive the parent's MeasuredActivePoint
        // from the subgrid's, translated into absolute (line, column) within the
        // rendered subgrid output. AdjustActivePointForAlignment will then subtract
        // the scroll offset so the line index becomes viewport-relative; consumers
        // (e.g. GetVerticalScrollInfo) add the scroll offset back when they need
        // the absolute index again.
        if (gridCell.HasSubgrid)
        {
            var sub = gridCell.Subgrid!;
            var subMap = sub.MeasuredActivePoint;
            if (subMap == null)
                return null;

            int absLine = 0;
            for (int r = 0; r < subMap.Row; r++)
                absLine += sub.GetMeasuredRowHeight(r) ?? 0;
            absLine += subMap.LineIndex;

            int absCol = 0;
            for (int c = 0; c < subMap.Column; c++)
                absCol += sub.GetMeasuredColumnWidth(c) ?? 0;
            absCol += subMap.OffsetInLine;

            return new MeasuredActivePoint(col, row, absLine, absCol);
        }

        var mc = output[row, col];
        if (mc == null)
            return null;

        // Map the original character offset into a (lineIndex, offsetInLine)
        int remaining = ActivePoint.Offset;
        int lineIndex = 0;
        int offsetInLine = 0;

        for (int li = 0; li < mc.Lines.Count; li++)
        {
            int lineLen = CliTextSegment.Length(mc.Lines[li]);
            if (remaining <= lineLen || li == mc.Lines.Count - 1)
            {
                lineIndex = li;
                offsetInLine = Math.Min(remaining, lineLen);
                break;
            }
            remaining -= lineLen;
        }

        return new MeasuredActivePoint(col, row, lineIndex, offsetInLine);
    }

    /// <summary>
    /// Adjust the MeasuredActivePoint for vertical and horizontal alignment shifts.
    /// Must be called after ApplyAlignmentAndFill.
    /// </summary>
    private void AdjustActivePointForAlignment(MeasuredCell[,] output, int[] columnWidths, int[] rowHeights)
    {
        if (MeasuredActivePoint is null) return;

        int col = MeasuredActivePoint.Column;
        int row = MeasuredActivePoint.Row;
        var mc = output[row, col];
        if (mc == null) return;

        var gridCell = cells[row, col];
        var style = mc.Style;

        // Compute vertical shift (same logic as ApplyAlignmentAndFill)
        int targetH = rowHeights[row];
        int rowSpan = gridCell?.RowSpan ?? 1;
        if (rowSpan > 1)
            for (int r = 1; r < rowSpan; r++)
                targetH += rowHeights[row + r];

        int contentLines = mc.InitialLines.Count;
        int addRows = Math.Max(0, targetH - contentLines);
        bool fillVertical = gridCell?.FillVertical ?? false;
        int addRowsBefore = 0;
        if (!fillVertical)
        {
            switch (style.VerticalAlignment)
            {
                case CliVerticalAlignment.Bottom:
                    addRowsBefore = addRows;
                    break;
                case CliVerticalAlignment.Center:
                    addRowsBefore = addRows / 2;
                    break;
            }
        }
        
        int scrollOffY = 0;
        int scrollOffX = 0;
        // The active point only ever lands on the active scrollable cell, so a direct lookup at
        // its coordinate resolves the right offsets.
        if (_scrollCells.TryGetValue((col, row), out var scrollCell))
        {
            scrollOffY = scrollCell.ScrollOffsetY;
            scrollOffX = scrollCell.ScrollOffsetX;
        }

        MeasuredActivePoint.LineIndex += addRowsBefore - scrollOffY;
        MeasuredActivePoint.OffsetInLine -= scrollOffX;

        // Compute horizontal shift (same logic as AlignHorizontally)
        int targetW = columnWidths[col];
        int colSpan = gridCell?.ColSpan ?? 1;
        if (colSpan > 1)
            for (int c = 1; c < colSpan; c++)
                targetW += columnWidths[col + c];

        bool fillHorizontal = gridCell?.FillHorizontal ?? false;
        var align = fillHorizontal ? CliTextAlignment.Left : (style.HorizontalAlignment ?? CliTextAlignment.Left);

        // Find the content width of the line that the active point is on (before alignment)
        // We use InitialLines to get the pre-alignment width at the mapped lineIndex
        int origLineIdx = MeasuredActivePoint.LineIndex - addRowsBefore;
        int contentWidth = 0;
        if (origLineIdx >= 0 && origLineIdx < mc.InitialLines.Count)
            contentWidth = CliTextSegment.Length(mc.InitialLines[origLineIdx]);
        // But after wrapping, lines changed - use the wrapped lines count from before alignment
        // Actually, the lines on mc are already post-alignment. Let's compute the pad from align logic.
        if (contentWidth < targetW)
        {
            int pad = targetW - contentWidth;
            int left = align == CliTextAlignment.Left ? 0 : align == CliTextAlignment.Center ? pad / 2 : pad;
            MeasuredActivePoint.OffsetInLine += left;
        }
    }

    /// <summary>
    /// Clamp MeasuredActivePoint to valid range within the measured cell's final lines.
    /// </summary>
    private void ClampActivePoint(MeasuredCell[,] output)
    {
        if (MeasuredActivePoint is null) return;

        int col = MeasuredActivePoint.Column;
        int row = MeasuredActivePoint.Row;
        var mc = output[row, col];
        if (mc == null || mc.Lines.Count == 0)
        {
            MeasuredActivePoint = null;
            return;
        }

        // Clamp line index
        if (MeasuredActivePoint.LineIndex < 0)
            MeasuredActivePoint.LineIndex = 0;
        if (MeasuredActivePoint.LineIndex >= mc.Lines.Count)
            MeasuredActivePoint.LineIndex = mc.Lines.Count - 1;

        // Clamp offset within line
        int lineLen = CliTextSegment.Length(mc.Lines[MeasuredActivePoint.LineIndex]);
        if (MeasuredActivePoint.OffsetInLine < 0)
            MeasuredActivePoint.OffsetInLine = 0;
        if (MeasuredActivePoint.OffsetInLine > lineLen)
            MeasuredActivePoint.OffsetInLine = lineLen;
    }

    private static List<CliTextSegment> JoinSegmentsIntoSingleLine(List<List<CliTextSegment>> lines, bool trimLineEnds = true)
    {
        var result = new List<CliTextSegment>();
        if (lines.Count == 0)
            return result;

        // seed with first line (reuse segment instances; we only replace result's last when adding space)
        result.AddRange(lines[0]);

        for (int li = 1; li < lines.Count; li++)
        {
            if (result.Count > 0)
            {
                var last = result[^1];
                result[^1] = new CliTextSegment(last.Text + " ", last.Style);
            }
            result.AddRange(lines[li]);
        }

        if (trimLineEnds)
            TrimLineEnds(result);
        // merge adjacent segments with identical style; also ensures at least one segment remains
        CompactLine(result);
        return result;
    }    
}
