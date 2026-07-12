using System.Globalization;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Activity;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Cell/column padding for the rich activity dialog: fluent <c>.Padding(...)</c> on a column (after
/// <c>AddColumn</c>) and on a text cell (after <c>Text</c>), resolved through the existing
/// <c>CliGrid</c> cell-style cascade so a cell's padding overrides its column's. Text stays content-only;
/// padding is the framework's <see cref="CliCellPadding"/>, never manual spaces.
/// </summary>
public sealed class ActivityCellPaddingTests
{
    private static TestShell NewShell() => new(culture: CultureInfo.GetCultureInfo("en-US"));

    private static CliGrid Grid(TestShell shell, ActivityDialogSpec spec) =>
        new InlineActivityControl<int>(shell, spec, (_, _) => Task.FromResult(0)).ToGrid();

    // ── Builder/model ────────────────────────────────────────────────────────

    [Fact]
    public void ColumnPadding_StoredOnColumnSpec()
    {
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 8, align: CliTextAlignment.Right).Padding(CliCellPadding.Right)
            .AddRow(null, r => r.Cell(0).Text("Hi"))
            .Build();

        Assert.Equal(CliCellPadding.Right, spec.Columns[0].Padding);
        // Padding(...) reconstructs the column without disturbing its other settings.
        Assert.Equal(8, spec.Columns[0].Width);
        Assert.Equal(CliTextAlignment.Right, spec.Columns[0].Align);
    }

    [Fact]
    public void TextCellPadding_StoredOnElement()
    {
        var spec = ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow(null, r => r.Cell(0).Text("Files:").Padding(CliCellPadding.Both))
            .Build();

        var text = Assert.IsType<ActivityTextElement>(spec.Rows[0].Cells[0].Element);
        Assert.Equal(CliCellPadding.Both, text.Padding);
    }

    [Fact]
    public void Padding_BeforeAnyColumn_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ActivityDialogSpec.Create().Padding(CliCellPadding.Both));
    }

    // ── Resolution through the grid cascade ───────────────────────────────────

    [Fact]
    public void ColumnPadding_AppliesToTextCell()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 10).Padding(CliCellPadding.Right)
            .AddRow(null, r => r.Cell(0).Text("Hi"))
            .Build();

        Assert.Equal(CliCellPadding.Right, Grid(shell, spec).GetCellStyle(0, 0).Padding);
    }

    [Fact]
    public void CellPadding_Applies()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 10)
            .AddRow(null, r => r.Cell(0).Text("Hi").Padding(CliCellPadding.Both))
            .Build();

        Assert.Equal(CliCellPadding.Both, Grid(shell, spec).GetCellStyle(0, 0).Padding);
    }

    [Fact]
    public void CellPadding_OverridesColumnPadding()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 12).Padding(CliCellPadding.Left)
            .AddRow(null, r => r.Cell(0).Text("Hi").Padding(CliCellPadding.Both))
            .Build();

        Assert.Equal(CliCellPadding.Both, Grid(shell, spec).GetCellStyle(0, 0).Padding);
    }

    [Fact]
    public void NoPadding_LeavesCellPaddingUnset()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 10)
            .AddRow(null, r => r.Cell(0).Text("Hi"))
            .Build();

        Assert.Null(Grid(shell, spec).GetCellStyle(0, 0).Padding);
    }

    // ── Padding is real grid padding (content stays content-only) ──────────────

    [Fact]
    public void RightPadding_ReservesAColumnAndKeepsTextContentOnly()
    {
        var shell = NewShell();
        var noPad = ActivityDialogSpec.Create()
            .AddColumn(width: 6, align: CliTextAlignment.Left)
            .AddRow(null, r => r.Cell(0).Text("AB"))
            .Build();
        var withPad = ActivityDialogSpec.Create()
            .AddColumn(width: 6, align: CliTextAlignment.Left).Padding(CliCellPadding.Right)
            .AddRow(null, r => r.Cell(0).Text("AB"))
            .Build();

        var plain = TigerConsole.RenderGridToLines(Grid(shell, noPad))[0];
        var padded = TigerConsole.RenderGridToLines(Grid(shell, withPad))[0];

        // Both columns resolve to width 6 and carry the same content text — the grid (not the template)
        // reserves the right padding column, so the rendered line still begins with the literal content.
        Assert.Equal(6, plain.Length);
        Assert.Equal(6, padded.Length);
        Assert.StartsWith("AB", plain);
        Assert.StartsWith("AB", padded);
        Assert.EndsWith(" ", padded); // reserved right padding cell
    }
}
