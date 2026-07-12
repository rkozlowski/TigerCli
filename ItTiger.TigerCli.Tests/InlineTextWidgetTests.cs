using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers <see cref="InlineTextWidget"/>: it renders text as a single-cell grid and delegates all
/// text handling (end-of-line splitting, wrapping, formatting, measuring, truncation) to
/// <see cref="ItTiger.TigerCli.Rendering.CliGrid"/> — it never splits text into multiple rows.
/// </summary>
public sealed class InlineTextWidgetTests : TestBase
{
    private static KeyEvent Key(ConsoleKey key) => new(key, ConsoleModifiers.None);

    [Fact]
    public void ToGrid_ReturnsSingleCellGrid()
    {
        var shell = new TestShell();
        var widget = new InlineTextWidget(shell, "Hello");

        var grid = widget.ToGrid();

        Assert.Equal(1, grid.ColumnCount);
        Assert.Equal(1, grid.RowCount);
    }

    [Fact]
    public void ToGrid_StoresFullTextInOneCell()
    {
        var shell = new TestShell();
        var widget = new InlineTextWidget(shell, "Hello world");

        var grid = widget.ToGrid();

        // A single 1x1 cell holds the whole text; rendering shows it on one line.
        Assert.Equal(1, grid.ColumnCount);
        Assert.Equal(1, grid.RowCount);
        var lines = TigerConsole.RenderGridToLines(grid);
        Assert.Single(lines);
        Assert.Contains("Hello world", lines[0]);
    }

    [Fact]
    public void MultilineText_IsNotSplitIntoMultipleRows()
    {
        var shell = new TestShell();
        var widget = new InlineTextWidget(shell, "First line.\nSecond line.\nThird line.");

        var grid = widget.ToGrid();

        // The widget keeps the full text in a single grid row/cell regardless of embedded newlines;
        // it is CliGrid (next test) that turns them into multiple rendered lines.
        Assert.Equal(1, grid.ColumnCount);
        Assert.Equal(1, grid.RowCount);
    }

    [Fact]
    public void EmbeddedNewlines_AreRenderedAsSeparateLines_ByCliGrid()
    {
        var shell = new TestShell();
        var widget = new InlineTextWidget(shell, "First line.\nSecond line.\nThird line.");

        var lines = TigerConsole.RenderGridToLines(widget.ToGrid());

        // CliGrid — not the widget — splits the single cell's content at end-of-line characters.
        Assert.Equal(3, lines.Count);
        Assert.Contains("First line.", lines[0]);
        Assert.Contains("Second line.", lines[1]);
        Assert.Contains("Third line.", lines[2]);
    }

    [Fact]
    public void Defaults_AreWordWrap_Raw_TextStyle()
    {
        var shell = new TestShell();
        var widget = new InlineTextWidget(shell, "x");

        Assert.Equal(CliWrapMode.WordWrap, widget.Wrapping.Mode);
        Assert.Equal(CliFormattingMode.Raw, widget.FormattingMode);
        Assert.Equal(ThemeStyle.Text, widget.Style);
    }

    [Fact]
    public void WordWrapDefault_WrapsLongTextWhenColumnIsConstrained()
    {
        var shell = new TestShell();
        var widget = new InlineTextWidget(shell, "alpha beta gamma delta epsilon");

        var grid = widget.ToGrid();
        // Constrain the single column so the cell must wrap; CliGrid performs the wrapping.
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = 12, MaxWidth = 12 }));

        var lines = TigerConsole.RenderGridToLines(grid);

        Assert.True(lines.Count > 1, "word-wrap default should break long text across multiple lines");
    }

    [Fact]
    public void ConfiguredFormattingMode_IsApplied()
    {
        var shell = new TestShell();
        var widget = new InlineTextWidget(shell, "[Accent]hi[/]")
        {
            FormattingMode = CliFormattingMode.Preformatted,
        };

        // Preformatted markup is parsed; the markup tags are not shown literally.
        var text = string.Join("\n", TigerConsole.RenderGridToLines(widget.ToGrid()));

        Assert.Contains("hi", text);
        Assert.DoesNotContain("[Accent]", text);
    }

    [Fact]
    public void ConfiguredTruncation_ViaWrapping_TruncatesWithIndicator()
    {
        var shell = new TestShell();
        var widget = new InlineTextWidget(shell, "alpha beta gamma delta")
        {
            Wrapping = CliWrapping.SingleLineTruncate,
        };

        var grid = widget.ToGrid();
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = 10, MaxWidth = 10 }));

        var lines = TigerConsole.RenderGridToLines(grid);

        Assert.Single(lines);
        Assert.Contains("…", lines[0]); // truncation indicator carried by the CliWrapping value
    }

    [Fact]
    public void IsReadOnly_NotFocusable_AndDoesNotConsumeKeys()
    {
        var shell = new TestShell();
        var widget = new InlineTextWidget(shell, "x");

        Assert.False(widget.Focusable);
        Assert.False(widget.HandleKey(Key(ConsoleKey.Enter)).IsHandled);
        Assert.False(widget.HandleKey(Key(ConsoleKey.DownArrow)).IsHandled);
    }

    [Fact]
    public void CanBeUsedAsMessageWidget_InMessageBox()
    {
        var shell = new TestShell();
        var control = new InlineMessageBoxControl(shell, "Saved.", MessageBoxButtons.Ok);

        var widgets = control.GetWidgets();
        var messageGrid = widgets[0].Grid;

        // The message widget is a single-cell grid produced through InlineTextWidget.
        Assert.Equal(InlineDialogArea.InFrameScrollable, widgets[0].Area);
        Assert.Equal(1, messageGrid.ColumnCount);
        Assert.Equal(1, messageGrid.RowCount);
        Assert.Contains("Saved.", string.Join("\n", TigerConsole.RenderGridToLines(messageGrid)));
    }
}
