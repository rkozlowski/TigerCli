using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers the dialog/widget-area composition contract: <see cref="InlineDialog"/> builds its layout
/// from <see cref="InlineControlBase.GetWidgets"/> rather than assuming a single content subgrid, and
/// existing single-widget controls keep mapping to the same areas/geometry.
/// </summary>
public sealed class InlineDialogWidgetCompositionTests : TestBase
{
    // A control that exposes an arbitrary set of top-level widgets across dialog areas, so the
    // dialog's placement/area behavior can be exercised independently of the real controls.
    private sealed class StubControl : InlineControlBase
    {
        private readonly List<InlineDialogWidget> _widgets = new();

        public StubControl(ICliAppShell shell) : base(shell) { }

        public string? LabelOverride { get; set; }
        public string? HintOverride { get; set; }

        public override string? ContentLabel => LabelOverride;
        public override string? Hint => HintOverride;

        public InlineDialogWidget Add(
            InlineDialogArea area,
            string text,
            bool focused = false,
            CliControlDecoration decoration = CliControlDecoration.None,
            CliScrollMode scroll = CliScrollMode.None,
            CliScrollThumbMode thumb = CliScrollThumbMode.Offset)
        {
            var grid = ToGrid(1, 1);
            grid.DefaultCellStyle = Shell.Theme.Resolve(ThemeStyle.DialogSurface);
            grid.Set(0, 0, text);

            var widget = new InlineDialogWidget
            {
                Area = area,
                Grid = grid,
                IsFocused = focused,
                Decoration = decoration,
                ScrollMode = scroll,
                ThumbMode = thumb,
            };
            _widgets.Add(widget);
            return widget;
        }

        public override IReadOnlyList<InlineDialogWidget> GetWidgets() => _widgets;
        public override InlineKeyResult HandleKey(KeyEvent key) => InlineKeyResult.NotHandled;
        public override object? Payload => null;
        public override CliGrid ToGrid() => _widgets.Count > 0 ? _widgets[0].Grid : ToGrid(1, 1);
    }

    private static int LineIndexContaining(IReadOnlyList<string> lines, string needle)
    {
        for (int i = 0; i < lines.Count; i++)
            if (lines[i].Contains(needle))
                return i;
        return -1;
    }

    // ------------------------------------------------------------------
    // Area mapping for existing single-widget controls
    // ------------------------------------------------------------------

    [Fact]
    public void TextInput_MapsTo_InFrameWithIndicators()
    {
        var shell = new TestShell();
        Assert.Equal(InlineDialogArea.InFrameWithIndicators, new InlineTextInput(shell).DialogArea);
    }

    [Fact]
    public void Select_MultiSelect_FolderSelect_MapTo_InFrameScrollable()
    {
        var shell = new TestShell();
        Assert.Equal(InlineDialogArea.InFrameScrollable, new InlineSelect(shell, ["a"]).DialogArea);
        Assert.Equal(InlineDialogArea.InFrameScrollable, new InlineMultiSelect(shell, ["a"]).DialogArea);
        Assert.Equal(InlineDialogArea.InFrameScrollable, new InlineFolderSelect(shell, new FileSystemFolderBrowser()).DialogArea);
    }

    [Fact]
    public void SelectGetWidgets_ReturnsSingleFocusedWidget_FromControlMetadata()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"]);

        var widgets = select.GetWidgets();

        var widget = Assert.Single(widgets);
        Assert.True(widget.IsFocused);
        Assert.Equal(InlineDialogArea.InFrameScrollable, widget.Area);
        Assert.Equal(select.ControlDecoration, widget.Decoration);
        Assert.Equal(select.ScrollMode, widget.ScrollMode);
        Assert.Equal(select.ThumbMode, widget.ThumbMode);
        Assert.Same(select.ToGrid(), widget.Grid);
    }

    // ------------------------------------------------------------------
    // Existing single-widget rendering is unchanged
    // ------------------------------------------------------------------

    [Fact]
    public void Select_Dialog_Renders_TitleFrameAndSelection()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"], preselectIndex: 0);
        var dialog = new InlineDialog(shell, "Pick one", select);

        var lines = TigerConsole.RenderGridToLines(dialog.ToGrid());
        var text = string.Join("\n", lines);

        Assert.Contains("Pick one", text);
        Assert.Contains("Red", text);                    // selected row in the scrollable viewport
        Assert.Contains(ConsoleSymbol.DoubleV, text);    // dialog frame border
        // Dialogs now use the unified seven-column area-definition layout.
        Assert.Equal(7, dialog.ToGrid().ColumnCount);
    }

    [Fact]
    public void TextInput_Dialog_ReservesIndicatorColumns()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell, "server");
        var dialog = new InlineDialog(shell, title: null, input, "Name");

        var grid = dialog.ToGrid();
        TigerConsole.RenderGridToLines(grid);

        // InFrameWithIndicators uses the unified seven-column layout with indicator overlays.
        Assert.Equal(7, grid.ColumnCount);
        Assert.Equal(new[] { 1, 3 }, Overlays(grid)
            .Where(o => o.Orientation == CliOrientation.Horizontal)
            .OrderBy(o => o.Start.Column)
            .Select(o => o.Start.Column));
    }

    [Fact]
    public void NonIndicatorControl_StillUsesUnifiedSevenColumnGrid()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red"]);
        var dialog = new InlineDialog(shell, title: null, select);

        Assert.Equal(7, dialog.ToGrid().ColumnCount);
    }

    // ------------------------------------------------------------------
    // Multiple widgets across areas
    // ------------------------------------------------------------------

    [Fact]
    public void Widgets_AboveInFrameBelow_RenderInExpectedPositions()
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        control.Add(InlineDialogArea.AboveFrame, "ABOVEROW");
        control.Add(InlineDialogArea.InFrame, "INFRAMEROW", focused: true);
        control.Add(InlineDialogArea.BelowFrame, "BELOWROW");
        var dialog = new InlineDialog(shell, title: null, control);

        var lines = TigerConsole.RenderGridToLines(dialog.ToGrid());

        int above = LineIndexContaining(lines, "ABOVEROW");
        int inFrame = LineIndexContaining(lines, "INFRAMEROW");
        int below = LineIndexContaining(lines, "BELOWROW");

        Assert.True(above >= 0 && inFrame >= 0 && below >= 0);
        Assert.True(above < inFrame, "above-frame widget must render above the in-frame widget");
        Assert.True(inFrame < below, "below-frame widget must render below the in-frame widget");

        // Only the in-frame row carries the dialog frame border.
        Assert.Contains(ConsoleSymbol.DoubleV, lines[inFrame]);
        Assert.DoesNotContain(ConsoleSymbol.DoubleV, lines[above]);
        Assert.DoesNotContain(ConsoleSymbol.DoubleV, lines[below]);
    }

    [Fact]
    public void DuplicateWidgetArea_Throws()
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        control.Add(InlineDialogArea.InFrame, "FIRSTROW", focused: true);
        control.Add(InlineDialogArea.InFrame, "SECONDROW");
        var dialog = new InlineDialog(shell, title: null, control);

        var ex = Assert.Throws<TigerCliException>(() => dialog.ToGrid());
        Assert.Contains("Cannot define more than one widget in the same area", ex.Message);
    }

    // ------------------------------------------------------------------
    // Control-level metadata stays dialog-owned
    // ------------------------------------------------------------------

    [Fact]
    public void ContentLabel_StillRendersInsideFrameAboveContent()
    {
        var shell = new TestShell();
        var control = new StubControl(shell) { LabelOverride = "MYLABEL" };
        control.Add(InlineDialogArea.InFrame, "CONTENTROW", focused: true);
        var dialog = new InlineDialog(shell, title: null, control);

        var lines = TigerConsole.RenderGridToLines(dialog.ToGrid());

        int label = LineIndexContaining(lines, "MYLABEL");
        int content = LineIndexContaining(lines, "CONTENTROW");

        Assert.True(label >= 0 && content > label);
        Assert.Contains(ConsoleSymbol.DoubleV, lines[label]); // inside frame
    }

    [Fact]
    public void Hint_IsDialogOwned_AndRendersBelowFrame()
    {
        var shell = new TestShell();
        var control = new StubControl(shell) { HintOverride = "MYHINT" };
        control.Add(InlineDialogArea.InFrame, "CONTENTROW", focused: true);
        var dialog = new InlineDialog(shell, title: null, control);

        var lines = TigerConsole.RenderGridToLines(dialog.ToGrid());

        int hint = LineIndexContaining(lines, "MYHINT");
        int content = LineIndexContaining(lines, "CONTENTROW");

        Assert.True(hint > content); // status/hint bar is below the frame content
        Assert.DoesNotContain(ConsoleSymbol.DoubleV, lines[hint]); // outside the frame
    }

    // ------------------------------------------------------------------
    // Widget-level decoration / scroll mode / focus
    // ------------------------------------------------------------------

    [Fact]
    public void WidgetLevelScrollMode_CreatesScrollableHostCell()
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        // Control decoration/scroll defaults are None; the widget carries the scroll mode.
        control.Add(InlineDialogArea.InFrameScrollable, "LISTROW", focused: true, scroll: CliScrollMode.Vertical);
        var dialog = new InlineDialog(shell, title: null, control);

        var grid = dialog.ToGrid();
        TigerConsole.RenderGridToLines(grid);

        // No title/label/hint: frame top = row 0, the single in-frame widget = row 1, content
        // column = 1. The widget's host cell is a vertical scroll cell from the widget's ScrollMode.
        Assert.NotNull(grid.GetVerticalScrollInfo(1, 1));
    }

    [Fact]
    public void IndicatorWidget_ReservesIndicatorColumns_ForGeometry()
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        control.Add(InlineDialogArea.InFrameWithIndicators, "PATHROW", focused: true,
            decoration: CliControlDecoration.HorizontalIndicators, scroll: CliScrollMode.Horizontal);
        var dialog = new InlineDialog(shell, title: null, control);

        Assert.Equal(7, dialog.ToGrid().ColumnCount);
    }

    [Fact]
    public void FocusedWidgetIdentity_DrivesActivePoint()
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        // Two scrollable widgets in distinct areas; only the second is focused/active.
        control.Add(InlineDialogArea.AboveFrameWithIndicators, "FIRSTROW", focused: false, scroll: CliScrollMode.Horizontal);
        control.Add(InlineDialogArea.InFrameScrollable, "SECONDROW", focused: true, scroll: CliScrollMode.Vertical);
        var dialog = new InlineDialog(shell, title: null, control);

        var grid = dialog.ToGrid();
        TigerConsole.RenderGridToLines(grid);

        // No title/label/hint: above-frame row = 0, frame top = row 1, in-frame row = 2,
        // content column is 1. The active point must be the focused (second) widget.
        Assert.NotNull(grid.ActivePoint);
        Assert.Equal(1, grid.ActivePoint!.Column);
        Assert.Equal(2, grid.ActivePoint.Row);
    }

    [Fact]
    public void MultiWidgetIndicatorLayout_GivesScrollableListUsableContentWidth()
    {
        var shell = new TestShell(viewportWidth: 60);
        var control = new StubControl(shell);
        control.Add(InlineDialogArea.AboveFrameWithIndicators, @"R:\$RECYCLE.BIN",
            decoration: CliControlDecoration.HorizontalIndicators, scroll: CliScrollMode.Horizontal);
        control.Add(InlineDialogArea.InFrameScrollable, "$RECYCLE.BIN",
            focused: true, decoration: CliControlDecoration.VerticalScrollBar, scroll: CliScrollMode.Vertical);
        control.Add(InlineDialogArea.BelowFrame, "[ OK ] [ Cancel ]");
        var dialog = new InlineDialog(shell, "Select secondary destination folder", control);

        var grid = dialog.ToGrid();
        var lines = TigerConsole.RenderGridToLines(grid);
        var text = string.Join("\n", lines);

        Assert.Contains("$RECYCLE.BIN", text);

        // The list row is the in-frame widget after the title, one above-frame row, and frame top.
        Assert.NotNull(grid.GetVerticalScrollInfo(1, 3));

        // The list item renders horizontally (one line), not wrapped to a one-character column.
        int listLine = LineIndexContaining(lines, "$RECYCLE.BIN");
        Assert.True(listLine >= 0);
        Assert.Contains("$RECYCLE.BIN", lines[listLine]);
    }

    // ------------------------------------------------------------------
    // Definition-driven composite layout contract (observable host cells / overlays)
    // ------------------------------------------------------------------

    private static CliFrameArea SingleFrameArea(CliGrid grid)
    {
        var field = typeof(CliGrid).GetField("frameAreas",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("CliGrid.frameAreas not found.");

        var areas = (IEnumerable<CliFrameArea>)field.GetValue(grid)!;
        return Assert.Single(areas);
    }

    private static IReadOnlyList<CliOverlay> Overlays(CliGrid grid)
    {
        var field = typeof(CliGrid).GetField("_overlays",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("CliGrid._overlays not found.");

        return ((IEnumerable<CliOverlay>)field.GetValue(grid)!).ToList();
    }

    [Theory]
    [InlineData(InlineDialogArea.AboveFrame, 0, 7)]
    [InlineData(InlineDialogArea.AboveFrameWithIndicators, 1, 5)]
    [InlineData(InlineDialogArea.InFrameWithIndicators, 2, 1)]
    [InlineData(InlineDialogArea.InFrameScrollable, 1, 3)]
    [InlineData(InlineDialogArea.InFrame, 1, 3)]
    [InlineData(InlineDialogArea.BelowFrameWithIndicators, 1, 5)]
    [InlineData(InlineDialogArea.BelowFrame, 0, 7)]
    public void AreaPlacement_MatchesDefinitionDrivenHostCell(InlineDialogArea area, int column, int _)
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        control.Add(area, "FOCUS", focused: true);
        AddRequiredDistinctInFrameWidget(control, area);
        var dialog = new InlineDialog(shell, title: null, control);

        var grid = dialog.ToGrid();
        TigerConsole.RenderGridToLines(grid);

        Assert.NotNull(grid.ActivePoint);
        Assert.Equal(column, grid.ActivePoint!.Column);
    }

    [Theory]
    [InlineData(InlineDialogArea.AboveFrameWithIndicators, 0, 6)]
    [InlineData(InlineDialogArea.InFrameWithIndicators, 1, 3)]
    [InlineData(InlineDialogArea.BelowFrameWithIndicators, 0, 6)]
    public void IndicatorArea_MarkerOverlayColumns_MatchDefinition(InlineDialogArea area, int left, int right)
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        control.Add(area, "PATH", focused: true,
            decoration: CliControlDecoration.HorizontalIndicators, scroll: CliScrollMode.Horizontal);
        AddRequiredDistinctInFrameWidget(control, area);
        var dialog = new InlineDialog(shell, title: null, control);

        var columns = Overlays(dialog.ToGrid())
            .Where(o => o.Orientation == CliOrientation.Horizontal)
            .OrderBy(o => o.Start.Column)
            .Select(o => o.Start.Column)
            .ToArray();

        Assert.Equal(new[] { left, right }, columns);
    }

    [Fact]
    public void CompositeDialog_UsesFixedSevenColumnGrid()
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        control.Add(InlineDialogArea.AboveFrame, "ABOVE");
        control.Add(InlineDialogArea.InFrame, "INFRAME", focused: true);
        var dialog = new InlineDialog(shell, title: null, control);

        Assert.Equal(7, dialog.ToGrid().ColumnCount);
    }

    [Fact]
    public void CompositeDialog_FrameSpansColumnsZeroThroughFour()
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        control.Add(InlineDialogArea.AboveFrameWithIndicators, "PATH");
        control.Add(InlineDialogArea.InFrameScrollable, "LIST", focused: true,
            decoration: CliControlDecoration.VerticalScrollBar, scroll: CliScrollMode.Vertical);
        control.Add(InlineDialogArea.BelowFrameWithIndicators, "BUTTONS");
        var dialog = new InlineDialog(shell, title: null, control);

        var frame = SingleFrameArea(dialog.ToGrid());

        Assert.Equal(0, frame.FirstColumn);
        Assert.Equal(4, frame.LastColumn);
        Assert.Equal(5, frame.ColumnCount);
    }

    [Fact]
    public void CompositeDialog_VerticalScrollbarOverlaysFrameRightColumn()
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        control.Add(InlineDialogArea.AboveFrame, "ABOVE");
        control.Add(InlineDialogArea.InFrameScrollable, "LIST", focused: true,
            decoration: CliControlDecoration.VerticalScrollBar, scroll: CliScrollMode.Vertical);
        control.Add(InlineDialogArea.BelowFrame, "BELOW");
        var dialog = new InlineDialog(shell, title: null, control);

        var scrollBar = Assert.Single(Overlays(dialog.ToGrid()),
            o => o.Orientation == CliOrientation.Vertical);

        Assert.Equal(4, scrollBar.Start.Column);
    }

    [Fact]
    public void CompositeDialog_AboveIndicatorOverlays_UseContractIndicatorColumns()
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        control.Add(InlineDialogArea.AboveFrameWithIndicators, "PATH", focused: true,
            decoration: CliControlDecoration.HorizontalIndicators, scroll: CliScrollMode.Horizontal);
        control.Add(InlineDialogArea.InFrame, "BODY");
        control.Add(InlineDialogArea.BelowFrameWithIndicators, "BUTTONS");
        var dialog = new InlineDialog(shell, title: null, control);

        var horizontalOverlays = Overlays(dialog.ToGrid())
            .Where(o => o.Orientation == CliOrientation.Horizontal)
            .OrderBy(o => o.Start.Column)
            .ToList();

        // The AboveFrameWithIndicators row carries the only HorizontalIndicators decoration; its
        // left/right marker overlays sit on the area definition's indicator columns (0 and 6).
        // (The BelowFrameWithIndicators "BUTTONS" widget has no decoration, so it adds no overlays.)
        Assert.Equal(new[] { 0, 6 }, horizontalOverlays.Select(o => o.Start.Column));
    }

    [Fact]
    public void SingleWidgetDialog_UsesUnifiedFrameAndScrollbarColumns()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green"]);
        var dialog = new InlineDialog(shell, title: null, select);

        var grid = dialog.ToGrid();
        var frame = SingleFrameArea(grid);
        var scrollBar = Assert.Single(Overlays(grid),
            o => o.Orientation == CliOrientation.Vertical);

        Assert.Equal(7, grid.ColumnCount);
        Assert.Equal(0, frame.FirstColumn);
        Assert.Equal(4, frame.LastColumn);
        Assert.Equal(4, scrollBar.Start.Column);
    }

    // Verifies the placement table is actually wired into the composite layout: the focused widget's
    // host cell lands in the contract column (read back through the grid's active point).
    [Theory]
    [InlineData(InlineDialogArea.AboveFrame, 0)]
    [InlineData(InlineDialogArea.AboveFrameWithIndicators, 1)]
    [InlineData(InlineDialogArea.InFrameWithIndicators, 2)]
    [InlineData(InlineDialogArea.InFrame, 1)]
    [InlineData(InlineDialogArea.InFrameScrollable, 1)]
    [InlineData(InlineDialogArea.BelowFrame, 0)]
    public void CompositeLayout_PlacesFocusedWidget_AtContractColumn(InlineDialogArea area, int expectedColumn)
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        control.Add(area, "FOCUS", focused: true);
        AddRequiredDistinctInFrameWidget(control, area);
        var dialog = new InlineDialog(shell, title: null, control);

        var grid = dialog.ToGrid();
        TigerConsole.RenderGridToLines(grid);

        Assert.NotNull(grid.ActivePoint);
        Assert.Equal(expectedColumn, grid.ActivePoint!.Column);
    }

    private static void AddRequiredDistinctInFrameWidget(StubControl control, InlineDialogArea occupiedArea)
    {
        if (occupiedArea is InlineDialogArea.InFrame or InlineDialogArea.InFrameScrollable or InlineDialogArea.InFrameWithIndicators)
            control.Add(InlineDialogArea.AboveFrame, "OTHER");
        else
            control.Add(InlineDialogArea.InFrame, "OTHER");
    }

    [Fact]
    public void CompositeFullWidthRows_SpanWholeDialog_AndTitleRendersOutsideFrame()
    {
        var shell = new TestShell();
        var control = new StubControl(shell);
        // A wide in-frame body sizes the frame so the full-width title/below rows fit on one line.
        control.Add(InlineDialogArea.InFrame, "WIDEINFRAMEBODYCONTENT", focused: true);
        control.Add(InlineDialogArea.BelowFrame, "FULLWIDTHROW");
        var dialog = new InlineDialog(shell, "TITLEROW", control);

        var lines = TigerConsole.RenderGridToLines(dialog.ToGrid());

        int title = LineIndexContaining(lines, "TITLEROW");
        int body = LineIndexContaining(lines, "WIDEINFRAMEBODYCONTENT");
        int below = LineIndexContaining(lines, "FULLWIDTHROW");

        Assert.True(title >= 0 && body > title && below > body);
        // Title and the below-frame full-width row sit outside the frame (no frame border glyph),
        // while the body row carries it.
        Assert.DoesNotContain(ConsoleSymbol.DoubleV, lines[title]);
        Assert.DoesNotContain(ConsoleSymbol.DoubleV, lines[below]);
        Assert.Contains(ConsoleSymbol.DoubleV, lines[body]);
    }
}
