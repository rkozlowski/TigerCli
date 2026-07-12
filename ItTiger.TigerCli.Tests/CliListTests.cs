using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers <see cref="CliList{T}"/>: the list command builder. Verifies column projection, semantic
/// value styling (normal and Key/Path/Link), title/preset application, the empty-list default path,
/// and that it renders through the existing <see cref="CliTable"/> pipeline.
/// </summary>
public sealed class CliListTests : TestBase
{
    private static readonly ITheme Blue = new TigerBlueTheme();

    private sealed record Device(string Id, string Name, string Model, string GroupId, string ConfigPath);

    private static readonly Device[] Devices =
    [
        new("d-1", "Front door", "Cam-X", "g-100", "/etc/app/d1.conf"),
        new("d-2", "Garage", "Cam-Y", "g-200", "/etc/app/d2.conf"),
    ];

    private static CliList<Device> NewList() =>
        new CliList<Device>().ApplyPreset(CliTableStylePreset.Lucca, Blue);

    private static CliColor? ValueForeground(CliTable table, int column) =>
        table.Header.Elements[column].DataStyle?.CharStyle?.Foreground;

    [Fact]
    public void Render_ProjectsColumnsAndRecords()
    {
        var table = NewList()
            .AddColumn("Id", d => d.Id)
            .AddColumn("Name", d => d.Name)
            .Render(Devices);

        Assert.Equal(2, table.Header.Elements.Count);
        Assert.Equal("Id", table.Header.Elements[0].HeaderContent);
        Assert.Equal(2, table.Records.Count);
        Assert.Equal("d-1", table.Records[0][0]);
        Assert.Equal("Front door", table.Records[0][1]);
    }

    [Fact]
    public void AddColumn_WithStyle_AppliesSemanticForeground()
    {
        var table = NewList()
            .AddColumn("Group", d => d.GroupId, style: ThemeStyle.Key)
            .Render(Devices);

        Assert.Equal(Blue.Resolve(ThemeStyle.Key).CharStyle?.Foreground, ValueForeground(table, 0));
    }

    [Fact]
    public void AddColumn_WithoutStyle_LeavesValueCharStyleNull()
    {
        var table = NewList()
            .AddColumn("Name", d => d.Name)
            .Render(Devices);

        Assert.Null(table.Header.Elements[0].DataStyle?.CharStyle);
    }

    [Fact]
    public void AddKeyColumn_MapsToKeyStyle()
    {
        var table = NewList()
            .AddKeyColumn("Id", d => d.Id)
            .Render(Devices);

        Assert.Equal(Blue.Resolve(ThemeStyle.Key).CharStyle?.Foreground, ValueForeground(table, 0));
    }

    [Fact]
    public void AddPathColumn_MapsToPathStyle()
    {
        var table = NewList()
            .AddPathColumn("Config", d => d.ConfigPath)
            .Render(Devices);

        Assert.Equal(Blue.Resolve(ThemeStyle.Path).CharStyle?.Foreground, ValueForeground(table, 0));
    }

    [Fact]
    public void AddLinkColumn_MapsToLinkStyle()
    {
        var table = NewList()
            .AddLinkColumn("Url", d => d.ConfigPath)
            .Render(Devices);

        Assert.Equal(Blue.Resolve(ThemeStyle.Link).CharStyle?.Foreground, ValueForeground(table, 0));
        Assert.Equal(
            Blue.Resolve(ThemeStyle.Link).CharStyle?.Decorations,
            table.Header.Elements[0].DataStyle!.CharStyle!.Value.Decorations);
    }

    [Fact]
    public void Render_EmptyList_ProducesHeaderOnlyTable()
    {
        var table = NewList()
            .AddKeyColumn("Id", d => d.Id)
            .AddColumn("Name", d => d.Name)
            .Render([]);

        Assert.Empty(table.Records);
        Assert.Equal(2, table.Header.Elements.Count);

        // Header-only table still renders (the columns/title show), the consistent empty-state default.
        var lines = TigerConsole.RenderToLines(table);
        Assert.Contains(lines, l => l.Contains("Id"));
        Assert.Contains(lines, l => l.Contains("Name"));
    }

    [Fact]
    public void Render_ForcesVerticalOrientation_EvenForHorizontalOnlyPreset()
    {
        // Lucca is horizontal-only (a detail preset), but a list must render vertically
        // (records as rows). The preset contributes styling only.
        var table = NewList()
            .AddColumn("Id", d => d.Id)
            .AddColumn("Name", d => d.Name)
            .Render(Devices);

        Assert.Equal(CliTableOrientation.Vertical, table.Orientation);
    }

    [Fact]
    public void Render_VerticalLayout_HeaderCaptionsShareOneLine()
    {
        var lines = TigerConsole.RenderToLines(NewList()
            .AddColumn("Id", d => d.Id)
            .AddColumn("Name", d => d.Name)
            .Render(Devices));

        // Vertical orientation places both header captions on the same row.
        Assert.Contains(lines, l => l.Contains("Id") && l.Contains("Name"));
    }

    [Fact]
    public void Render_NoColumns_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new CliList<Device>().Render(Devices));
    }

    [Fact]
    public void AddTitle_IsRendered()
    {
        var lines = TigerConsole.RenderToLines(NewList()
            .AddTitle("Devices")
            .AddColumn("Id", d => d.Id)
            .Render(Devices));

        Assert.Contains(lines, l => l.Contains("Devices"));
    }

    [Fact]
    public void AddTitle_DefaultAlignment_IsCenter_Unchanged()
    {
        var table = NewList()
            .AddTitle("Devices")
            .AddColumn("Id", d => d.Id)
            .Render(Devices);

        Assert.NotNull(table.Title);
        Assert.Equal(CliTextAlignment.Center, table.Title!.Style.HorizontalAlignment);
    }

    [Theory]
    [InlineData(CliTextAlignment.Left)]
    [InlineData(CliTextAlignment.Center)]
    [InlineData(CliTextAlignment.Right)]
    public void AddTitle_WithAlignmentArgument_SetsTitleAlignment(CliTextAlignment alignment)
    {
        var table = NewList()
            .AddTitle("Devices", alignment)
            .AddColumn("Id", d => d.Id)
            .Render(Devices);

        Assert.Equal(alignment, table.Title!.Style.HorizontalAlignment);
    }

    [Theory]
    [InlineData(CliTextAlignment.Left)]
    [InlineData(CliTextAlignment.Right)]
    public void SetTitleAlignment_OverridesPresetDefault(CliTextAlignment alignment)
    {
        var table = NewList()
            .AddTitle("Devices")
            .SetTitleAlignment(alignment)
            .AddColumn("Id", d => d.Id)
            .Render(Devices);

        Assert.Equal(alignment, table.Title!.Style.HorizontalAlignment);
    }

    [Fact]
    public void AddTitle_LeftAligned_RendersFlushLeft_UnlikeCenteredDefault()
    {
        // A title wider than "Files written:" would be centered by default (leading padding); a
        // left override must render the title flush-left with no leading whitespace on its line.
        var left = TigerConsole.RenderToLines(NewList()
            .AddTitle("Files written:", CliTextAlignment.Left)
            .AddColumn("Path", d => d.ConfigPath)
            .Render(Devices));
        var centered = TigerConsole.RenderToLines(NewList()
            .AddTitle("Files written:")
            .AddColumn("Path", d => d.ConfigPath)
            .Render(Devices));

        var leftTitle = left.First(l => l.Contains("Files written:"));
        var centeredTitle = centered.First(l => l.Contains("Files written:"));

        Assert.StartsWith("Files written:", leftTitle);
        Assert.StartsWith(" ", centeredTitle);
    }

    [Fact]
    public void SetTitleAlignment_PreservesTitleStyle()
    {
        // Capture the preset's title ink/surface without an alignment override…
        var baseline = NewList().AddTitle("Devices").AddColumn("Id", d => d.Id).Render(Devices);
        var baselineCharStyle = baseline.Title!.Style.CharStyle;

        // …then confirm setting alignment changes only the alignment, not the semantic style.
        var table = NewList()
            .AddTitle("Devices", CliTextAlignment.Left)
            .AddColumn("Id", d => d.Id)
            .Render(Devices);

        Assert.Equal(CliTextAlignment.Left, table.Title!.Style.HorizontalAlignment);
        Assert.Equal(baselineCharStyle?.Foreground, table.Title.Style.CharStyle?.Foreground);
        Assert.Equal(baselineCharStyle?.Background, table.Title.Style.CharStyle?.Background);
    }

    [Fact]
    public void Render_StyledList_RendersValues()
    {
        var lines = TigerConsole.RenderToLines(NewList()
            .AddTitle("Devices")
            .AddKeyColumn("Id", d => d.Id)
            .AddColumn("Name", d => d.Name)
            .AddColumn("Model", d => d.Model)
            .AddKeyColumn("Group", d => d.GroupId)
            .Render(Devices));

        var text = string.Join("\n", lines);
        Assert.Contains("d-1", text);
        Assert.Contains("Garage", text);
        Assert.Contains("g-200", text);
    }

    [Fact]
    public void Render_NullItems_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NewList().AddColumn("Id", d => d.Id).Render(null!));
    }
}
