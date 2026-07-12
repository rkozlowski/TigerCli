using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Selection;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineMultiColumnSelectWidgetTests : TestBase
{
    private static IReadOnlyList<SelectColumn> MenuColumns() =>
    [
        new SelectColumn(sizing: CliColumnSizing.Auto, alignment: CliTextAlignment.Left),
        new SelectColumn(sizing: CliColumnSizing.Star, alignment: CliTextAlignment.Left),
        new SelectColumn(sizing: CliColumnSizing.Auto, alignment: CliTextAlignment.Right, style: ThemeStyle.MutedText),
    ];

    private static IReadOnlyList<SelectRow> MenuRows() =>
    [
        new SelectRow(new SelectCell("import"), new SelectCell("Import files from a card."), new SelectCell("→ card import")),
        new SelectRow(new SelectCell("register-card"), new SelectCell("Register a card."), new SelectCell("→ card register")),
        new SelectRow(new SelectCell("card"), new SelectCell("Manage cards."), new SelectCell("›")),
        new SelectRow(new SelectCell("destination-group"), new SelectCell("Manage destination groups."), new SelectCell("›")),
    ];

    private static KeyEvent Key(ConsoleKey key) => new(key, ConsoleModifiers.None);

    [Fact]
    public void Columns_AlignAcrossRows_NameLeft_MarkerRight()
    {
        var shell = new TestShell(120, 24);
        var widget = new InlineMultiColumnSelectWidget(shell, MenuColumns(), MenuRows());

        var lines = TigerConsole.RenderGridToLines(widget.ToGrid());

        // Every row renders to the same width, so the three columns line up vertically across rows.
        Assert.Equal(4, lines.Count);
        Assert.Single(lines.Select(l => l.Length).Distinct());

        // First column is left-aligned (name starts each row), third column is right-aligned (marker
        // ends each row) — the star description column sits between and fills the remainder.
        Assert.StartsWith("import", lines[0].TrimStart());
        Assert.StartsWith("destination-group", lines[3].TrimStart());
        Assert.EndsWith("→ card import", lines[0].TrimEnd());
        Assert.EndsWith("→ card register", lines[1].TrimEnd());
        Assert.EndsWith("›", lines[2].TrimEnd());

        // The description is present and not shifted by the marker column.
        Assert.Contains("Import files from a card.", lines[0]);
        Assert.Contains("Manage cards.", lines[2]);
    }

    [Fact]
    public void SelectedRow_HighlightSpansEveryColumn()
    {
        var shell = new TestShell(120, 24);
        var widget = new InlineMultiColumnSelectWidget(shell, MenuColumns(), MenuRows()) { HasFocus = true };

        var grid = widget.ToGrid();
        _ = TigerConsole.RenderGridToLines(grid); // force measure
        var selectedBg = shell.Theme.Resolve(ThemeStyle.SelectedListItem).CharStyle?.Background;

        // The selected (default first) row shares the selection background across all three columns.
        for (int c = 0; c < 3; c++)
            Assert.Equal(selectedBg, grid.GetCellStyle(c, 0).CharStyle?.Background);

        // A non-selected row does not carry the selection background in any column.
        for (int c = 0; c < 3; c++)
            Assert.NotEqual(selectedBg, grid.GetCellStyle(c, 1).CharStyle?.Background);

        Assert.Equal(0, grid.ActivePoint!.Row);
    }

    [Fact]
    public void UnfocusedSelection_UsesInactiveHighlight()
    {
        var shell = new TestShell(120, 24);
        var widget = new InlineMultiColumnSelectWidget(shell, MenuColumns(), MenuRows()); // HasFocus = false

        var grid = widget.ToGrid();
        _ = TigerConsole.RenderGridToLines(grid);
        var inactiveBg = shell.Theme.Resolve(ThemeStyle.InactiveSelectedListItem).CharStyle?.Background;

        for (int c = 0; c < 3; c++)
            Assert.Equal(inactiveBg, grid.GetCellStyle(c, 0).CharStyle?.Background);
    }

    [Fact]
    public void NonSelectedCell_KeepsItsSemanticForeground()
    {
        var shell = new TestShell(120, 24);
        var columns = MenuColumns();
        // Row 1 is not selected by default; give its marker cell an explicit Accent style.
        var rows = new List<SelectRow>
        {
            new(new SelectCell("a"), new SelectCell("first")),
            new(new SelectCell("b"), new SelectCell("second"), new SelectCell("mark", style: ThemeStyle.Accent)),
        };
        var widget = new InlineMultiColumnSelectWidget(shell, columns, rows) { HasFocus = true };

        var grid = widget.ToGrid();
        _ = TigerConsole.RenderGridToLines(grid);

        var accentFg = shell.Theme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground;
        Assert.Equal(accentFg, grid.GetCellStyle(2, 1).CharStyle?.Foreground);
    }

    [Fact]
    public void LongContent_TruncatesWithIndicator()
    {
        var shell = new TestShell(120, 24);
        var widget = new InlineMultiColumnSelectWidget(shell, MenuColumns(), MenuRows()) { MaxWidth = 40 };

        var lines = TigerConsole.RenderGridToLines(widget.ToGrid());

        Assert.All(lines, l => Assert.Equal(40, l.Length));
        // The long first-row description is clipped to fit the narrowed frame.
        Assert.Contains("…", lines[0]);
        Assert.DoesNotContain("Import files from a card.", lines[0]);
    }

    [Fact]
    public void MissingTrailingCells_RenderBlank()
    {
        var shell = new TestShell(120, 24);
        var rows = new List<SelectRow> { new(new SelectCell("only-name")) };
        var widget = new InlineMultiColumnSelectWidget(shell, MenuColumns(), rows);

        var lines = TigerConsole.RenderGridToLines(widget.ToGrid());

        Assert.Single(lines);
        Assert.StartsWith("only-name", lines[0].TrimStart());
    }

    // ── Navigation ────────────────────────────────────────────────────

    [Fact]
    public void UpDownHomeEnd_MoveSelection()
    {
        var shell = new TestShell(120, 24);
        var widget = new InlineMultiColumnSelectWidget(shell, MenuColumns(), MenuRows());

        Assert.Equal(0, widget.SelectedIndex);
        Assert.True(widget.HandleKey(Key(ConsoleKey.DownArrow)).IsHandled);
        Assert.Equal(1, widget.SelectedIndex);
        Assert.True(widget.HandleKey(Key(ConsoleKey.End)).IsHandled);
        Assert.Equal(3, widget.SelectedIndex);
        Assert.True(widget.HandleKey(Key(ConsoleKey.UpArrow)).IsHandled);
        Assert.Equal(2, widget.SelectedIndex);
        Assert.True(widget.HandleKey(Key(ConsoleKey.Home)).IsHandled);
        Assert.Equal(0, widget.SelectedIndex);
    }

    [Fact]
    public void Navigation_ClampsAtBoundaries()
    {
        var shell = new TestShell(120, 24);
        var widget = new InlineMultiColumnSelectWidget(shell, MenuColumns(), MenuRows());

        widget.HandleKey(Key(ConsoleKey.UpArrow));
        Assert.Equal(0, widget.SelectedIndex);

        widget.HandleKey(Key(ConsoleKey.End));
        widget.HandleKey(Key(ConsoleKey.DownArrow));
        Assert.Equal(3, widget.SelectedIndex);
    }

    // ── Disabled rows ─────────────────────────────────────────────────

    [Fact]
    public void DisabledRows_AreSkippedByNavigationAndInitialSelection()
    {
        var shell = new TestShell(120, 24);
        var rows = new List<SelectRow>
        {
            new([new SelectCell("a"), new SelectCell("first")], isDisabled: true),
            new(new SelectCell("b"), new SelectCell("second")),
            new([new SelectCell("c"), new SelectCell("third")], isDisabled: true),
            new(new SelectCell("d"), new SelectCell("fourth")),
        };
        var widget = new InlineMultiColumnSelectWidget(shell, MenuColumns(), rows);

        // Initial selection skips the disabled first row.
        Assert.Equal(1, widget.SelectedIndex);

        // Down skips the disabled row 2 and lands on row 3.
        widget.HandleKey(Key(ConsoleKey.DownArrow));
        Assert.Equal(3, widget.SelectedIndex);

        // Up skips back over the disabled row to row 1.
        widget.HandleKey(Key(ConsoleKey.UpArrow));
        Assert.Equal(1, widget.SelectedIndex);
    }

    [Fact]
    public void AllRowsDisabled_HasNoSelection()
    {
        var shell = new TestShell(120, 24);
        var rows = new List<SelectRow>
        {
            new([new SelectCell("a")], isDisabled: true),
            new([new SelectCell("b")], isDisabled: true),
        };
        var widget = new InlineMultiColumnSelectWidget(shell, MenuColumns(), rows);

        Assert.Equal(-1, widget.SelectedIndex);
        Assert.False(widget.HandleKey(Key(ConsoleKey.DownArrow)).IsHandled);
    }

    [Fact]
    public void EmptyRows_ShowEmptyStateAndHaveNoActivePoint()
    {
        var shell = new TestShell(120, 24);
        var widget = new InlineMultiColumnSelectWidget(shell, MenuColumns(), []);

        var grid = widget.ToGrid();
        var text = string.Join("\n", TigerConsole.RenderGridToLines(grid));

        Assert.Equal(-1, widget.SelectedIndex);
        Assert.Null(grid.ActivePoint);
        Assert.Contains("No items available", text);
    }

    // ── Control wrapper ───────────────────────────────────────────────

    [Fact]
    public void Control_PayloadAndConfirm_TrackSelection()
    {
        var shell = new TestShell(120, 24);
        var control = new InlineMultiColumnSelect(shell, MenuColumns(), MenuRows());

        Assert.True(control.CanConfirm);
        Assert.Equal(0, control.Payload);

        Assert.True(control.HandleKey(Key(ConsoleKey.DownArrow)).IsHandled);
        Assert.Equal(1, control.Payload);
        Assert.Equal(1, control.ToGrid().ActivePoint!.Row);
    }

    [Fact]
    public void Control_GetWidgets_ReturnsFocusedInFrameScrollableWidget()
    {
        var shell = new TestShell(120, 24);
        var control = new InlineMultiColumnSelect(shell, MenuColumns(), MenuRows());

        var widget = Assert.Single(control.GetWidgets());

        Assert.True(widget.IsFocused);
        Assert.Equal(InlineDialogArea.InFrameScrollable, widget.Area);
        Assert.Equal(control.ControlDecoration, widget.Decoration);
        Assert.Equal(control.ScrollMode, widget.ScrollMode);
    }

    [Fact]
    public void Control_AllDisabled_IsNotConfirmable()
    {
        var shell = new TestShell(120, 24);
        var rows = new List<SelectRow> { new([new SelectCell("a")], isDisabled: true) };
        var control = new InlineMultiColumnSelect(shell, MenuColumns(), rows);

        Assert.False(control.CanConfirm);
        Assert.Null(control.Payload);
    }

    // ── TigerTui modal flow ───────────────────────────────────────────

    [Fact]
    public async Task MultiColumnSelectIndexAsync_ReturnsSelectedIndex()
    {
        var shell = new TestShell(120, 24);
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await ItTiger.TigerCli.Tui.TigerTui.MultiColumnSelectIndexAsync(
            shell, "Pick", MenuColumns(), MenuRows(), ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task MultiColumnSelectIndexAsync_EscapeReturnsNull()
    {
        var shell = new TestShell(120, 24);
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await ItTiger.TigerCli.Tui.TigerTui.MultiColumnSelectIndexAsync(
            shell, "Pick", MenuColumns(), MenuRows(), ct: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task MultiColumnSelectIndexResultAsync_PreservesTimeoutKind()
    {
        var shell = new TestShell(120, 24);

        var result = await ItTiger.TigerCli.Tui.TigerTui.MultiColumnSelectIndexResultAsync(
                shell, "Pick", MenuColumns(), MenuRows(),
                timeout: TimeSpan.FromMilliseconds(20), ct: TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(DialogResultKind.Timeout, result.ResultKind);
    }

    [Fact]
    public async Task MultiColumnSelectIndexAsync_RendersAlignedColumns()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(120, 24);
        var runTask = ItTiger.TigerCli.Tui.TigerTui.MultiColumnSelectIndexAsync(
            shell, "Pick", MenuColumns(), MenuRows(), ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        var text = shell.Terminal.LastRenderedText;
        Assert.Contains("import", text);
        Assert.Contains("Manage cards.", text);
        Assert.Contains("→ card import", text);
        Assert.Contains("›", text);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);
    }
}
