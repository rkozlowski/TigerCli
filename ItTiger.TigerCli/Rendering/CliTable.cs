using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// Medium-level table builder for custom structured output. Use <see cref="CliList{T}"/> for
/// conventional list command output and <see cref="CliDetails"/> for single-record detail output;
/// use <see cref="CliTable"/> directly when a command needs explicit control over headers,
/// records, orientation, frames, styles, or table presets.
/// </summary>
/// <remarks>
/// A table materializes to a <see cref="CliGrid"/> through <see cref="ToGrid"/>. Vertical
/// orientation renders header elements as columns and records as rows; horizontal orientation
/// renders header elements as row labels and records as value columns. The instance is mutable and
/// the fluent helpers return this table for chaining.
/// </remarks>
public partial class CliTable : CliRenderableComponent
{
    private CliTableOrientation _orientation = CliTableOrientation.Vertical;
    private CliTableStyleOrientationSupport _orientationSupport = CliTableStyleOrientationSupport.Both;
    private CliTextAlignment? _verticalHeaderHorizontalAlignment;
    private bool _styleApplied;

    /// <summary>
    /// Gets or sets the orientation of the table (vertical or horizontal).
    /// Default is <see cref="CliTableOrientation.Vertical"/>.
    /// </summary>
    public CliTableOrientation Orientation
    {
        get => _orientation;
        set => _orientation = ClampOrientation(value);
    }

    /// <summary>
    /// Optional table title rendered above the table body.
    /// </summary>
    public CliTableTitle? Title { get; set; }

    /// <summary>
    /// The current default title style, set by <see cref="ApplyStyle"/> / <see cref="ApplyPreset"/>
    /// from the applied <see cref="CliTableStyle.TitleStyle"/>. <see cref="AddTitle(string)"/> clones
    /// this when creating the <see cref="Title"/>, so callers do not pass a style explicitly.
    /// </summary>
    public CliCellStyle? TitleStyle { get; set; }

    /// <summary>
    /// Default style for each record axis. In vertical orientation this applies to data rows; in
    /// horizontal orientation this applies to value columns.
    /// </summary>
    public CliCellStyle? DataStyle { get; set; }

    /// <summary>
    /// Alternate record style used when <see cref="AlternateRecordsEnabled"/> is enabled.
    /// </summary>
    public CliCellStyle? DataAltStyle { get; set; }

    /// <summary>
    /// Enables use of <see cref="DataAltStyle"/> for alternate records during rendering.
    /// Default is <c>false</c>.
    /// </summary>
    public bool AlternateRecordsEnabled { get; set; }

    /// <summary>
    /// Frame and separator configuration used when the table is converted to a grid.
    /// </summary>
    public CliTableFrameConfig FrameConfig { get; set; } = new CliTableFrameConfig();

    /// <summary>
    /// Per-table override for treating <see cref="DBNull.Value"/> as <c>null</c>.
    /// When <c>null</c>, falls back to <see cref="Terminal.TigerConsole.TreatDbNullAsNull"/>.
    /// </summary>
    public bool? TreatDbNullAsNull { get; set; }

    /// <summary>
    /// Gets the table header definition.
    /// </summary>
    public CliTableHeader Header { get; init; } = new CliTableHeader();

    /// <summary>
    /// Gets or sets the list of record objects to be rendered in the table.
    /// </summary>
    public IList<IList<object?>> Records { get; set; } = [];

    static CliGridAxisDefinition CreateAxisDefinition(CliGridAxis axis)
    {
        return (axis == CliGridAxis.Row) ? new CliGridRowDefinition() : new CliGridColumnDefinition();
    }

    /// <summary>
    /// Materializes this table as a <see cref="CliGrid"/> for rendering.
    /// </summary>
    /// <returns>A grid containing the title, frame, header elements, and records.</returns>
    /// <exception cref="TigerCliException">The table has no header elements, or a record has a
    /// different value count than the header element count.</exception>
    public override CliGrid ToGrid()
    {
        using var defaultOutputPresetScope = ApplyDefaultOutputPresetIfNeeded();

        int columnCount = 0;
        int rowCount = 0;        
        int firstRecordRow = 0;
        int firstRecordColumn = 0;
        int firstElementRow = 0 ;
        int firstElementColumn = 0;
        int headerRow = 0;
        int headerColumn = 0;
        int recordStepCol = 0;
        int recordStepRow = 0;
        int elementStepCol = 0;
        int elementStepRow = 0;
        bool hasTitle = false;        
        bool hasOuterFrame = false;
        bool hasInternalFrame = false;
        bool hasHeaderFrame = false;
        bool hasElementFrame = false;
        bool hasRecordFrame = false;
        
        int frameFirstRow = -1;
        int frameLastRow = -1;
        int frameFirstColumn = -1;
        int frameLastColumn = -1;
        
        if (Header.Elements.Count == 0)
        {
            throw new TigerCliException("Table must define at least one field", TigerCliRenderStage.ToGrid);
        }

        if (Title != null)
        {
            rowCount++;
            firstRecordRow++;
            firstElementRow++;
            headerRow++;      
            hasTitle = true;
        }

        if (FrameConfig.OuterFrame.Style != CliFrameSegmentStyle.None)
        {
            rowCount += 2;
            columnCount += 2;
            firstRecordRow++;
            firstElementRow++;
            firstRecordColumn++;
            firstElementColumn++;
            headerRow++;
            headerColumn++;
            hasOuterFrame = true;
        }
        int recordRowDelta = 0;
        int recordColumnDelta = 0;
        int elementRowDelta = 0;
        int elementColumnDelta = 0;

        if (Orientation == CliTableOrientation.Vertical)
        {
            recordRowDelta = 1;
            elementColumnDelta = 1;
        }
        else
        {
            recordColumnDelta = 1;
            elementRowDelta = 1;
        }

            
        if (Header.IsVisible)
        {            
            rowCount += recordRowDelta;
            columnCount += recordColumnDelta;
            firstRecordRow += recordRowDelta;
            firstRecordColumn += recordColumnDelta;
            if (FrameConfig.AfterHeader.Style != CliFrameSegmentStyle.None)
            {
                if (Records.Count > 0)
                {
                    rowCount += recordRowDelta;
                    columnCount += recordColumnDelta;
                    hasInternalFrame = true;
                    hasHeaderFrame = true;
                    firstRecordRow += recordRowDelta;
                    firstRecordColumn += recordColumnDelta;
                }                
            }
        }
        recordStepRow += recordRowDelta;
        recordStepCol += recordColumnDelta;
        if (FrameConfig.BetweenRecords.Style != CliFrameSegmentStyle.None)
        {
            recordStepRow += recordRowDelta;
            recordStepCol += recordColumnDelta;
            if (Records.Count > 1)
            {
                hasInternalFrame = true;
                hasRecordFrame = true;
            }
        }
        elementStepCol += elementColumnDelta;
        elementStepRow += elementRowDelta;
        if (FrameConfig.BetweenElements.Style != CliFrameSegmentStyle.None)
        {
            elementStepCol += elementColumnDelta;
            elementStepRow += elementRowDelta;
            if (Header.Elements.Count > 1 && (Header.IsVisible || Records.Count > 0))
            {
                hasInternalFrame = true;
                hasElementFrame = true;
            }
        }
        rowCount += Records.Count * recordRowDelta;
        rowCount += Header.Elements.Count * elementRowDelta;
        columnCount += Records.Count * recordColumnDelta;
        columnCount += Header.Elements.Count * elementColumnDelta;
        if (FrameConfig.BetweenRecords.Style != CliFrameSegmentStyle.None && Records.Count > 1)
        {
            rowCount += (Records.Count - 1) * recordRowDelta;
            columnCount += (Records.Count - 1) * recordColumnDelta;
        }
        if (FrameConfig.BetweenElements.Style != CliFrameSegmentStyle.None && Header.Elements.Count > 1)
        {
            columnCount += (Header.Elements.Count - 1) * elementColumnDelta;
            rowCount += (Header.Elements.Count - 1) * elementRowDelta;
        }
        
        
        var grid = ToGrid(columnCount, rowCount);

        grid.TreatDbNullAsNull = this.TreatDbNullAsNull;

        grid.StylePrecedence = (Orientation == CliTableOrientation.Vertical) ? CliStylePrecedence.RowOverColumn : CliStylePrecedence.ColumnOverRow;

        bool hasFrame = hasOuterFrame | hasInternalFrame;
        if (hasFrame)
        {
            if (hasOuterFrame)
            {
                frameFirstRow = hasTitle ? 1 : 0;
                frameLastRow = rowCount - 1;
                frameFirstColumn = 0;
                frameLastColumn = columnCount - 1;
            }
            else
            {
                int minRow = int.MaxValue;
                int maxRow = int.MinValue;
                int minColumn = int.MaxValue;
                int maxColumn = int.MinValue;
                if (hasHeaderFrame)
                {
                    minRow = firstRecordRow - recordRowDelta;
                    minColumn = firstRecordColumn - recordColumnDelta;
                    maxRow = minRow + (rowCount - (hasTitle ? 2 : 1)) * elementRowDelta;
                    maxColumn = minColumn + (columnCount - 1) * elementColumnDelta;
                }
                frameFirstRow = minRow;
                frameLastRow = maxRow;
                frameFirstColumn = minColumn;
                frameLastColumn = maxColumn;
                if (hasRecordFrame)
                {
                    minRow = firstRecordRow + recordRowDelta;
                    minColumn = firstRecordColumn + recordColumnDelta;                    
                    maxRow = rowCount - 1 - recordRowDelta;
                    maxColumn = columnCount - 1 - recordColumnDelta;
                }
                frameFirstRow = Math.Min(frameFirstRow, minRow);
                frameLastRow = Math.Max(frameLastRow, maxRow);
                frameFirstColumn = Math.Min(frameFirstColumn, minColumn);
                frameLastColumn = Math.Max(frameLastColumn, maxColumn);
                
                if (hasElementFrame)
                {
                    minRow = (hasTitle ? 1 : 0) + elementRowDelta;
                    minColumn = elementColumnDelta;
                    maxRow = rowCount - 1 - elementRowDelta;
                    maxColumn = columnCount - 1 - elementColumnDelta;                                            
                }
                frameFirstRow = Math.Min(frameFirstRow, minRow);
                frameLastRow = Math.Max(frameLastRow, maxRow);
                frameFirstColumn = Math.Min(frameFirstColumn, minColumn);
                frameLastColumn = Math.Max(frameLastColumn, maxColumn);
            }
            var frameArea = grid.AddFrameArea(FrameConfig.JoinStyle, frameFirstColumn, frameFirstRow, frameLastColumn, frameLastRow, FrameConfig.CharStyle);
            if (hasOuterFrame)
            {                
                frameArea.AddOuterFrame(FrameConfig.OuterFrame);
            }
            int row;
            int column;
            int len;
            if (Orientation == CliTableOrientation.Vertical)
            {                
                if (hasHeaderFrame)
                {
                    row = hasOuterFrame ? 2 : 1;                    
                    column = hasOuterFrame ? 1 : 0;
                    len = columnCount;
                    if (hasTitle)
                    {
                        row++;                        
                    }
                    if (hasOuterFrame)
                        len -= 2;
                    frameArea.AddHorizontalFrame(column, row, len, FrameConfig.AfterHeader);
                }
                if (hasRecordFrame)
                {
                    row = firstRecordRow + 1; 
                    column = firstRecordColumn;
                    len = columnCount;
                    if (hasOuterFrame)
                        len -= 2;
                    for (int i = 0; i < (Records.Count - 1); i++)
                    {
                        frameArea.AddHorizontalFrame(column, row, len, FrameConfig.BetweenRecords);
                        row += 2;
                    }
                }
                if (hasElementFrame)
                {
                    row = hasOuterFrame ? 1 : 0;
                    len = rowCount;
                    if (hasTitle)
                    { 
                        row++;
                        len--;
                    }
                    if (hasOuterFrame)
                        len -= 2;
                    column = hasOuterFrame ? 2 : 1;
                    for (int i = 0; i < (Header.Elements.Count - 1); i++)
                    {
                        frameArea.AddVerticalFrame(column, row, len, FrameConfig.BetweenElements);
                        column += 2;
                    }
                }
            }
            else
            {
                // Horizontal orientation (records = columns, elements = rows)

                if (hasHeaderFrame)
                {
                    column = hasOuterFrame ? 2 : 1;
                    row = hasOuterFrame ? 1 : 0;
                    len = rowCount;
                    if (hasTitle)
                    {
                        row++;
                        len--;
                    }
                    if (hasOuterFrame)
                        len -= 2;

                    frameArea.AddVerticalFrame(column, row, len, FrameConfig.AfterHeader);
                }

                if (hasRecordFrame)
                {
                    column = firstRecordColumn + 1;
                    row = firstRecordRow;
                    len = rowCount;
                    if (hasTitle)
                    {                        
                        len--;
                    }
                    if (hasOuterFrame)
                        len -= 2;

                    for (int i = 0; i < (Records.Count - 1); i++)
                    {
                        frameArea.AddVerticalFrame(column, row, len, FrameConfig.BetweenRecords);
                        column += 2;
                    }
                }

                if (hasElementFrame)
                {
                    row = hasOuterFrame ? 2 : 1;
                    if (hasTitle)
                    {
                        row++;
                    }
                    column = hasOuterFrame ? 1 : 0;
                    len = columnCount;
                    if (hasOuterFrame)
                        len -= 2;

                    for (int i = 0; i < (Header.Elements.Count - 1); i++)
                    {
                        frameArea.AddHorizontalFrame(column, row, len, FrameConfig.BetweenElements);
                        row += 2;
                    }
                }

            }

        }

        if (Title != null)
        {            
            grid.Set(0, 0, Title.Content, Title.Style, columnCount);
        }
        var recordAxis = Orientation == CliTableOrientation.Vertical ? CliGridAxis.Row : CliGridAxis.Column;
        var elementAxis = Orientation == CliTableOrientation.Vertical ? CliGridAxis.Column : CliGridAxis.Row;
        if (Header.IsVisible)
        {
            CliGridAxisDefinition def = CreateAxisDefinition(recordAxis);
            def.Style = Header.HeaderStyle;
            grid.SetAxis(headerColumn, headerRow, recordAxis, def);
            for (int i = 0; i < Header.Elements.Count; i++)
            {
                grid.Set(headerColumn + elementStepCol * i, headerRow + elementStepRow * i,
                    Header.Elements[i].HeaderContent, Header.Elements[i].HeaderStyle);
            }
        }

        for (int i = 0; i < Header.Elements.Count; i++)
        {
            CliGridAxisDefinition def = CreateAxisDefinition(elementAxis);

            // The element axis carries only layout defaults (width bounds, wrapping, alignment,
            // formatting): the axis also crosses the header caption and frame cells, and
            // decorations merge additively in the style cascade, so a value ink placed here would
            // leak (e.g. a Link underline onto borders and captions). The element's CharStyle is
            // applied per data cell in the record loop below instead.
            var axisStyle = CliCellStyle.Clone(Header.Elements[i].DataStyle);
            if (axisStyle is not null)
                axisStyle.CharStyle = null;
            def.Style = axisStyle;
            var row = firstRecordRow;
            var column = firstRecordColumn;
            row += elementStepRow * i;
            column += elementStepCol * i;
            grid.SetAxis(column, row, elementAxis, def);
        }

        for (int rec = 0; rec < Records.Count; rec++)
        {            
            var record = Records[rec];

            if (record.Count != Header.Elements.Count)
                throw new TigerCliException($"Invalid number of elements in record #{rec + 1}", TigerCliRenderStage.ToGrid);

            var row = firstRecordRow;
            var column = firstRecordColumn;
            row += recordStepRow * rec;
            column += recordStepCol * rec;

            CliGridAxisDefinition def = CreateAxisDefinition(recordAxis);
            def.Style = DataStyle;
            if (AlternateRecordsEnabled && DataAltStyle != null && ((rec & 1) == 1))
                def.Style = DataAltStyle;
            grid.SetAxis(column, row, recordAxis, def);

            for (int elem = 0; elem < record.Count; elem++)
            {
                // Hyperlink marking and the element's value ink (CharStyle) are applied per data
                // cell (not on the element/record axis) so they can never bleed onto the header
                // caption or frame cells in the same band. The render pipeline derives the
                // hyperlink target from each cell's own visible value.
                var element = Header.Elements[elem];
                CliCellStyle? cellStyle = null;
                if (element.DataStyle?.CharStyle is not null || element.DataIsHyperlink)
                {
                    cellStyle = new CliCellStyle
                    {
                        CharStyle = CliCharStyle.Clone(element.DataStyle?.CharStyle),
                        IsHyperlink = element.DataIsHyperlink ? true : null,
                    };
                }
                grid.Set(column, row, record[elem], cellStyle);
                row += elementStepRow;
                column += elementStepCol;
            }
        }

        return grid;        
    }

    private IDisposable? ApplyDefaultOutputPresetIfNeeded()
    {
        var preset = CliOutputPresetContext.Current?.Table;
        if (_styleApplied || preset is null)
            return null;

        var scope = new DefaultOutputPresetScope(this);
        ApplyStyleCore(CliTableStyles.Create(preset.Value));
        return scope;
    }

    private sealed class DefaultOutputPresetScope : IDisposable
    {
        private readonly CliTable _table;
        private readonly CliTableOrientation _orientation;
        private readonly CliTableStyleOrientationSupport _orientationSupport;
        private readonly CliTextAlignment? _verticalHeaderHorizontalAlignment;
        private readonly bool _styleApplied;
        private readonly CliCellStyle? _defaultCellStyle;
        private readonly CliCellStyle? _dataStyle;
        private readonly CliCellStyle? _dataAltStyle;
        private readonly bool _alternateRecordsEnabled;
        private readonly CliTableFrameConfig _frameConfig;
        private readonly CliCellStyle? _headerStyle;
        private readonly CliCellStyle? _titleStyle;
        private bool _disposed;

        public DefaultOutputPresetScope(CliTable table)
        {
            _table = table;
            _orientation = table._orientation;
            _orientationSupport = table._orientationSupport;
            _verticalHeaderHorizontalAlignment = table._verticalHeaderHorizontalAlignment;
            _styleApplied = table._styleApplied;
            _defaultCellStyle = CliCellStyle.Clone(table.DefaultCellStyle);
            _dataStyle = CliCellStyle.Clone(table.DataStyle);
            _dataAltStyle = CliCellStyle.Clone(table.DataAltStyle);
            _alternateRecordsEnabled = table.AlternateRecordsEnabled;
            _frameConfig = CloneFrameConfig(table.FrameConfig);
            _headerStyle = CliCellStyle.Clone(table.Header.HeaderStyle);
            _titleStyle = CliCellStyle.Clone(table.TitleStyle);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _table._orientation = _orientation;
            _table._orientationSupport = _orientationSupport;
            _table._verticalHeaderHorizontalAlignment = _verticalHeaderHorizontalAlignment;
            _table._styleApplied = _styleApplied;
            _table.DefaultCellStyle = CliCellStyle.Clone(_defaultCellStyle);
            _table.DataStyle = CliCellStyle.Clone(_dataStyle);
            _table.DataAltStyle = CliCellStyle.Clone(_dataAltStyle);
            _table.AlternateRecordsEnabled = _alternateRecordsEnabled;
            _table.FrameConfig = CloneFrameConfig(_frameConfig);
            _table.Header.HeaderStyle = CliCellStyle.Clone(_headerStyle);
            _table.TitleStyle = CliCellStyle.Clone(_titleStyle);
            _disposed = true;
        }
    }

    private CliTableOrientation ClampOrientation(CliTableOrientation orientation) => _orientationSupport switch
    {
        CliTableStyleOrientationSupport.VerticalOnly => CliTableOrientation.Vertical,
        CliTableStyleOrientationSupport.HorizontalOnly => CliTableOrientation.Horizontal,
        _ => orientation
    };
}
