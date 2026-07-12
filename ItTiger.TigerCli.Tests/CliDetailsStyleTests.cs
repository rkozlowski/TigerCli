using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers the semantic value styling added to <see cref="CliDetails"/>: the optional <c>style</c>
/// parameter on Add/AddWhen/AddOptional and the AddKey/AddPath/AddOptionalPath convenience helpers.
/// The style applies to the <b>value</b> (the element/row data style), never the label, and is only
/// present when the field is actually rendered.
/// </summary>
public sealed class CliDetailsStyleTests : TestBase
{
    private static readonly ITheme Blue = new TigerBlueTheme();

    private static CliDetails NewDetails() =>
        new CliDetails().ApplyPreset(CliTableStylePreset.Details, Blue);

    private static CliColor? ValueForeground(CliTable table, int element) =>
        table.Header.Elements[element].DataStyle?.CharStyle?.Foreground;

    [Fact]
    public void Add_WithStyle_AppliesSemanticForegroundToValue()
    {
        var table = NewDetails().Add("Server:", "localhost", style: ThemeStyle.Key).ToTable();

        Assert.Equal(Blue.Resolve(ThemeStyle.Key).CharStyle?.Foreground, ValueForeground(table, 0));
    }

    [Fact]
    public void Add_WithoutStyle_LeavesValueCharStyleNull_SoPresetBodyWins()
    {
        var table = NewDetails().Add("Server:", "localhost").ToTable();

        Assert.Null(table.Header.Elements[0].DataStyle?.CharStyle);
    }

    [Fact]
    public void Add_WithStyle_DoesNotStyleTheLabel()
    {
        var table = NewDetails().Add("Server:", "localhost", style: ThemeStyle.Key).ToTable();

        // The header (label) keeps the preset's header styling, not the value's semantic foreground.
        Assert.NotEqual(
            Blue.Resolve(ThemeStyle.Key).CharStyle?.Foreground,
            table.Header.Elements[0].HeaderStyle?.CharStyle?.Foreground);
    }

    [Fact]
    public void AddWhen_True_AppliesStyle()
    {
        var table = NewDetails().AddWhen(true, "Group:", "g1", style: ThemeStyle.Key).ToTable();

        Assert.Single(table.Header.Elements);
        Assert.Equal(Blue.Resolve(ThemeStyle.Key).CharStyle?.Foreground, ValueForeground(table, 0));
    }

    [Fact]
    public void AddWhen_False_DoesNotAddStyledField()
    {
        var table = NewDetails()
            .Add("Name:", "prod")
            .AddWhen(false, "Group:", "g1", style: ThemeStyle.Key)
            .ToTable();

        Assert.Single(table.Header.Elements);
        Assert.Equal("Name:", table.Header.Elements[0].HeaderContent);
    }

    [Fact]
    public void AddOptional_Present_AppliesStyle()
    {
        var table = NewDetails().AddOptional("Path:", "/etc/app", style: ThemeStyle.Path).ToTable();

        Assert.Single(table.Header.Elements);
        Assert.Equal(Blue.Resolve(ThemeStyle.Path).CharStyle?.Foreground, ValueForeground(table, 0));
    }

    [Fact]
    public void AddOptional_Missing_OmitsStyledField()
    {
        var table = NewDetails()
            .Add("Name:", "prod")
            .AddOptional("Path:", null, style: ThemeStyle.Path)
            .ToTable();

        Assert.Single(table.Header.Elements);
    }

    [Fact]
    public void AddKey_MapsToKeyStyle()
    {
        var table = NewDetails().AddKey("Id:", "abc-123").ToTable();

        Assert.Equal(Blue.Resolve(ThemeStyle.Key).CharStyle?.Foreground, ValueForeground(table, 0));
    }

    [Fact]
    public void AddPath_MapsToPathStyle()
    {
        var table = NewDetails().AddPath("Config:", "/etc/app.conf").ToTable();

        Assert.Equal(Blue.Resolve(ThemeStyle.Path).CharStyle?.Foreground, ValueForeground(table, 0));
    }

    [Fact]
    public void AddOptionalPath_Present_MapsToPathStyle()
    {
        var table = NewDetails().AddOptionalPath("Config:", "/etc/app.conf").ToTable();

        Assert.Single(table.Header.Elements);
        Assert.Equal(Blue.Resolve(ThemeStyle.Path).CharStyle?.Foreground, ValueForeground(table, 0));
    }

    [Fact]
    public void AddOptionalPath_Missing_OmitsField()
    {
        var table = NewDetails()
            .Add("Name:", "prod")
            .AddOptionalPath("Config:", null)
            .ToTable();

        Assert.Single(table.Header.Elements);
    }

    [Fact]
    public void StyledMissingValue_StillUsesMissingDisplay()
    {
        // A styled field whose value is missing keeps the missing-display behaviour.
        var text = string.Join("\n", TigerConsole.RenderToLines(
            NewDetails().Add("Database:", null, missingDisplay: "(not selected)", style: ThemeStyle.Key)));

        Assert.Contains("(not selected)", text);
    }

    [Fact]
    public void StyledDetails_RenderThroughTablePipeline()
    {
        var text = string.Join("\n", TigerConsole.RenderToLines(NewDetails()
            .AddTitle("Device")
            .AddKey("Id:", "abc-123")
            .Add("Name:", "Front door")
            .AddPath("Config:", "/etc/app.conf")));

        Assert.Contains("abc-123", text);
        Assert.Contains("Front door", text);
        Assert.Contains("/etc/app.conf", text);
    }
}
