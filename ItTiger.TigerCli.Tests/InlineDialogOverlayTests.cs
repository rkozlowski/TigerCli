using System.Reflection;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Stage 3: overlays / scroll indicators for the rewritten <see cref="InlineDialog"/>. Overlays are
/// created once as structural grid elements (driven by each widget's <see cref="InlineDialogAreaDefinition"/>
/// indicator/scrollbar columns and its <see cref="CliControlDecoration"/>); their visibility stays
/// dynamic, driven by the active (focused) scroll cell through <c>GetVerticalScrollInfo()</c> /
/// <c>GetHorizontalScrollInfo()</c>, which Stage 1's focus-following <see cref="CliGrid.ActivePoint"/>
/// selects. Focus changes never rebuild the cached grid (the structural signature is focus-free).
/// </summary>
public sealed class InlineDialogOverlayTests : TestBase
{
    // A composite control: a horizontally-scrolling text input above the frame (horizontal indicators)
    // and a vertically-scrolling list in the frame (vertical scrollbar), in distinct areas.
    private sealed class OverlayProbeControl : InlineMultiControl
    {
        public readonly InlineTextInputWidget Input;
        public readonly InlineSelectWidget List;
        public readonly int InputIndex;
        public readonly int ListIndex;

        public OverlayProbeControl(ICliAppShell shell, int listItems = 20)
            : base(shell)
        {
            Input = new InlineTextInputWidget(shell, "a path value that overflows", width: 8);
            var labels = Enumerable.Range(0, listItems).Select(i => $"Item {i}").ToArray();
            List = new InlineSelectWidget(shell, labels, preselectIndex: 0);

            InputIndex = AddWidget(Input, InlineDialogArea.AboveFrameWithIndicators,
                CliControlDecoration.HorizontalIndicators, CliScrollMode.Horizontal, CliScrollThumbMode.ActivePoint);
            ListIndex = AddWidget(List, InlineDialogArea.InFrameScrollable,
                CliControlDecoration.VerticalScrollBar, CliScrollMode.Vertical, CliScrollThumbMode.ActivePoint);

            SetFocusedWidgetIndex(ListIndex);
        }

        public override object? Payload => List.SelectedValue;

        public void FocusInput() => SetFocusedWidgetIndex(InputIndex);
        public void FocusList() => SetFocusedWidgetIndex(ListIndex);
    }

    private static IReadOnlyList<CliOverlay> Overlays(CliGrid grid)
    {
        var field = typeof(CliGrid).GetField("_overlays", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CliGrid._overlays not found.");
        return ((IEnumerable<CliOverlay>)field.GetValue(grid)!).ToList();
    }

    private static string Render(CliGrid grid, int? softMaxHeight = null)
    {
        if (softMaxHeight is int h)
            grid.SoftMaxHeight = h; // force the tall list to overflow its viewport so the scrollbar shows
        return string.Join("\n", TigerConsole.RenderGridToLines(grid));
    }

    // ------------------------------------------------------------------
    // 1. In-frame scrollable widget gets a vertical scrollbar overlay at the scrollbar column
    // ------------------------------------------------------------------

    [Fact]
    public void InFrameScrollableWidget_GetsVerticalScrollbarOverlay_AtScrollbarColumn()
    {
        var shell = new TestShell();
        var select = new InlineSelect(shell, ["Red", "Green", "Blue"]); // InFrameScrollable + VerticalScrollBar
        var dialog = new InlineDialog(shell, title: null, select);

        var scrollbar = Assert.Single(Overlays(dialog.ToGrid()), o => o.Orientation == CliOrientation.Vertical);
        Assert.Equal(4, scrollbar.Start.Column); // InFrameScrollable area definition's ScrollbarColumn
    }

    // ------------------------------------------------------------------
    // 2. Above-frame horizontal-indicator widget gets left/right overlays at the indicator columns
    // ------------------------------------------------------------------

    [Fact]
    public void AboveFrameIndicatorWidget_GetsLeftRightOverlays_AtIndicatorColumns()
    {
        var shell = new TestShell();
        var control = new OverlayProbeControl(shell);
        var dialog = new InlineDialog(shell, title: null, control);

        var horizontal = Overlays(dialog.ToGrid())
            .Where(o => o.Orientation == CliOrientation.Horizontal)
            .OrderBy(o => o.Start.Column)
            .ToList();

        // AboveFrameWithIndicators area definition: left indicator column 0, right indicator column 6.
        Assert.Equal(new[] { 0, 6 }, horizontal.Select(o => o.Start.Column));
    }

    // ------------------------------------------------------------------
    // 3. Overlay visibility follows focus via ActivePoint (rendered glyphs)
    // ------------------------------------------------------------------

    [Fact]
    public void VerticalScrollbar_VisibleOnlyWhenScrollableWidgetIsFocused()
    {
        var shell = new TestShell();
        var control = new OverlayProbeControl(shell, listItems: 20);
        var dialog = new InlineDialog(shell, title: null, control);

        // Focused list overflows its viewport → the scrollbar arrows render.
        control.FocusList();
        var listFocused = Render(dialog.ToGrid(), softMaxHeight: 12);
        Assert.Contains(ConsoleSymbol.TriangleUp, listFocused);
        Assert.Contains(ConsoleSymbol.TriangleDown, listFocused);

        // Focus moves to the (non-scrollable-vertically) input: the active scroll cell is no longer the
        // list, so GetVerticalScrollInfo() reports nothing and the scrollbar disappears.
        control.FocusInput();
        var inputFocused = Render(dialog.ToGrid(), softMaxHeight: 12);
        Assert.DoesNotContain(ConsoleSymbol.TriangleUp, inputFocused);
        Assert.DoesNotContain(ConsoleSymbol.TriangleDown, inputFocused);
    }

    // ------------------------------------------------------------------
    // 4. Focus change does not rebuild the cached grid (structural signature unchanged)
    // ------------------------------------------------------------------

    [Fact]
    public void FocusChange_DoesNotRebuildGrid_NorDuplicateOverlays()
    {
        var shell = new TestShell();
        var control = new OverlayProbeControl(shell);
        var dialog = new InlineDialog(shell, title: null, control);

        control.FocusList();
        var first = dialog.ToGrid();
        int overlayCount = Overlays(first).Count;

        control.FocusInput();
        var second = dialog.ToGrid();

        // Overlays are structural and focus is not in the signature: same cached grid, same overlays
        // (never re-added on a focus change).
        Assert.Same(first, second);
        Assert.Equal(overlayCount, Overlays(second).Count);
    }

    // ------------------------------------------------------------------
    // 5. A non-focused scrollable widget does not show its overlay
    // ------------------------------------------------------------------

    [Fact]
    public void NonFocusedScrollableWidget_DoesNotShowScrollbar()
    {
        var shell = new TestShell();
        var control = new OverlayProbeControl(shell, listItems: 20);
        var dialog = new InlineDialog(shell, title: null, control);

        // The list overflows but is NOT focused (the input is): its scrollbar overlay stays hidden.
        control.FocusInput();
        var text = Render(dialog.ToGrid(), softMaxHeight: 12);

        Assert.DoesNotContain(ConsoleSymbol.TriangleUp, text);
        Assert.DoesNotContain(ConsoleSymbol.FullBlock, text);
    }
}
