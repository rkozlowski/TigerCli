using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers the builder-level wrapping/truncation added to <see cref="CliList{T}"/>:
/// <see cref="CliList{T}.DefaultWrapping"/>, per-column <see cref="CliList{T}.SetWrapping"/>, and
/// <see cref="CliList{T}.SetWidth"/>. Wrapping/truncation reuses the existing <see cref="CliTable"/>
/// pipeline (it is <see cref="CliCellStyle.Wrapping"/> plus a column width bound on the element axis),
/// so these tests assert both the resolved value style and the rendered layout. They also confirm no
/// regression to semantic <see cref="ThemeStyle"/> styling and to the unconfigured default.
/// </summary>
public sealed class CliListWrappingTests : TestBase
{
    private static readonly ITheme Blue = new TigerBlueTheme();

    private sealed record Row(string Name, string Description);

    private const string LongText = "The quick brown fox jumps over the lazy dog";

    private static readonly Row[] Rows =
    [
        new("alpha", LongText),
    ];

    // Parma is a frameless, no-padding vertical list preset, so rendered lines are the cell content
    // (columns separated by a single space) — the cleanest surface for asserting wrap/truncate layout.
    private static CliList<Row> NewList() =>
        new CliList<Row>().ApplyPreset(CliTableStylePreset.Parma, Blue);

    private static CliCellStyle? ValueStyle(CliTable table, int column) =>
        table.Header.Elements[column].DataStyle;

    [Fact]
    public void SetWrapping_WithWidth_WrapsLongValueAcrossLines()
    {
        var lines = TigerConsole.RenderToLines(NewList()
            .AddColumn("Name", r => r.Name)
            .AddColumn("Description", r => r.Description)
            .SetWrapping(CliWrapping.WordWrap)
            .SetWidth(maxWidth: 12)
            .Render(Rows));

        var text = string.Join("\n", lines);
        // The long value was broken up: its words survive but never appear together on one line.
        Assert.Contains("quick", text);
        Assert.DoesNotContain("quick brown fox", text);
    }

    [Fact]
    public void SetWidth_MaxWidth_BoundsRenderedColumnWidth()
    {
        var lines = TigerConsole.RenderToLines(NewList()
            .AddColumn("Description", r => r.Description)
            .SetWrapping(CliWrapping.WordWrap)
            .SetWidth(maxWidth: 12)
            .Render(Rows));

        // No rendered line exceeds the single column's 12-char cap.
        Assert.All(lines, l => Assert.True(l.Length <= 12, $"Overflow: '{l}' (len {l.Length}) > 12"));
    }

    [Fact]
    public void SetWrapping_SingleLineTruncate_AppendsIndicatorAndCuts()
    {
        var lines = TigerConsole.RenderToLines(NewList()
            .AddColumn("Description", r => r.Description)
            .SetWrapping(CliWrapping.SingleLineTruncate)
            .SetWidth(maxWidth: 10)
            .Render(Rows));

        var text = string.Join("\n", lines);
        Assert.Contains("…", text);                       // truncation indicator shown
        Assert.DoesNotContain("lazy dog", text);          // tail was cut
    }

    [Fact]
    public void DefaultWrapping_AppliesToColumnsWithoutOverride()
    {
        // Default wrapping reaches a column that only sets a width, with no per-column SetWrapping.
        var table = NewList()
            .DefaultWrapping(CliWrapping.WordWrap)
            .AddColumn("Description", r => r.Description)
            .SetWidth(maxWidth: 12)
            .Render(Rows);

        Assert.Equal(CliWrapMode.WordWrap, ValueStyle(table, 0)!.Wrapping!.Mode);

        var text = string.Join("\n", TigerConsole.RenderToLines(table));
        Assert.DoesNotContain("quick brown fox", text);
    }

    [Fact]
    public void SetWrapping_OverridesDefaultForThatColumn()
    {
        var table = NewList()
            .DefaultWrapping(CliWrapping.WordWrap)
            .AddColumn("Name", r => r.Name)
            .AddColumn("Description", r => r.Description)
            .SetWrapping(CliWrapping.CharWrap)
            .Render(Rows);

        // Column 0 inherits the default; column 1's override wins.
        Assert.Equal(CliWrapMode.WordWrap, ValueStyle(table, 0)!.Wrapping!.Mode);
        Assert.Equal(CliWrapMode.CharWrap, ValueStyle(table, 1)!.Wrapping!.Mode);
    }

    [Fact]
    public void Wrapping_PreservesSemanticThemeStyle()
    {
        // A styled column keeps its semantic foreground AND gains wrapping/width — layout only.
        var table = NewList()
            .AddKeyColumn("Id", r => r.Name)
            .SetWrapping(CliWrapping.WordWrapTruncate)
            .SetWidth(maxWidth: 8)
            .Render(Rows);

        var style = ValueStyle(table, 0)!;
        Assert.Equal(Blue.Resolve(ThemeStyle.Key).CharStyle?.Foreground, style.CharStyle?.Foreground);
        Assert.Equal(CliWrapMode.WordWrap, style.Wrapping!.Mode);
        Assert.True(style.Wrapping.AllowTruncation);
        Assert.Equal(8, style.MaxWidth);
    }

    [Fact]
    public void NoWrappingConfigured_LeavesValueStyleWrappingNull_AndKeepsValueOnOneLine()
    {
        var table = NewList()
            .AddColumn("Description", r => r.Description)
            .Render(Rows);

        // Regression: unconfigured columns carry no wrapping/width and are not forced to wrap.
        Assert.Null(table.Header.Elements[0].DataStyle?.Wrapping);

        var text = string.Join("\n", TigerConsole.RenderToLines(table));
        Assert.Contains(LongText, text);
    }

    [Fact]
    public void SetWrapping_BeforeAddColumn_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new CliList<Row>().SetWrapping(CliWrapping.WordWrap));
    }

    [Fact]
    public void SetWidth_BeforeAddColumn_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new CliList<Row>().SetWidth(maxWidth: 10));
    }

    [Fact]
    public void DefaultWrapping_NullArgument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NewList().DefaultWrapping(null!));
    }
}
