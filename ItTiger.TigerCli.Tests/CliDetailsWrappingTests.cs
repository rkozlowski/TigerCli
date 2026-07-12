using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers the builder-level wrapping/truncation added to <see cref="CliDetails"/>:
/// <see cref="CliDetails.DefaultWrapping"/>, per-field <see cref="CliDetails.SetWrapping"/>, and the
/// shared-value-column <see cref="CliDetails.SetValueWidth"/>. A detail view is horizontal, so per-field
/// wrapping is a per-cell style while width is a single value-column bound on the record axis. Reuses
/// the existing <see cref="CliTable"/> pipeline; tests assert both the resolved styles and the rendered
/// layout, and confirm no regression to semantic <see cref="ThemeStyle"/> styling or the plain default.
/// </summary>
public sealed class CliDetailsWrappingTests : TestBase
{
    private static readonly ITheme Blue = new TigerBlueTheme();

    private const string LongText = "The quick brown fox jumps over the lazy dog";

    private static CliDetails NewDetails() =>
        new CliDetails().ApplyPreset(CliTableStylePreset.Verona, Blue);

    private static CliCellStyle? ValueStyle(CliTable table, int element) =>
        table.Header.Elements[element].DataStyle;

    [Fact]
    public void SetValueWidth_WithWrapping_WrapsLongValueAcrossLines()
    {
        var lines = TigerConsole.RenderToLines(NewDetails()
            .Add("Description:", LongText)
            .SetWrapping(CliWrapping.WordWrap)
            .SetValueWidth(maxWidth: 20)
            .ToTable());

        var text = string.Join("\n", lines);
        Assert.Contains("quick", text);
        Assert.DoesNotContain("quick brown fox jumps over the lazy", text);
    }

    [Fact]
    public void SetValueWidth_SetsRecordAxisWidth()
    {
        var table = NewDetails()
            .Add("Description:", LongText)
            .SetValueWidth(maxWidth: 20)
            .ToTable();

        // The single value column is the record axis, whose style is the table's DataStyle.
        Assert.Equal(20, table.DataStyle?.MaxWidth);
    }

    [Fact]
    public void DefaultWrapping_AppliesToFieldsWithoutOverride()
    {
        var table = NewDetails()
            .DefaultWrapping(CliWrapping.WordWrap)
            .Add("Description:", LongText)
            .ToTable();

        Assert.Equal(CliWrapMode.WordWrap, ValueStyle(table, 0)!.Wrapping!.Mode);
    }

    [Fact]
    public void SetWrapping_OverridesDefaultForThatField()
    {
        var table = NewDetails()
            .DefaultWrapping(CliWrapping.WordWrap)
            .Add("Name:", "alpha")
            .Add("Description:", LongText)
            .SetWrapping(CliWrapping.CharWrap)
            .ToTable();

        Assert.Equal(CliWrapMode.WordWrap, ValueStyle(table, 0)!.Wrapping!.Mode);
        Assert.Equal(CliWrapMode.CharWrap, ValueStyle(table, 1)!.Wrapping!.Mode);
    }

    [Fact]
    public void SetWrapping_SingleLineTruncate_AppendsIndicatorAndCuts()
    {
        var lines = TigerConsole.RenderToLines(NewDetails()
            .Add("Description:", LongText)
            .SetWrapping(CliWrapping.SingleLineTruncate)
            .SetValueWidth(maxWidth: 12)
            .ToTable());

        var text = string.Join("\n", lines);
        Assert.Contains("…", text);
        Assert.DoesNotContain("lazy dog", text);
    }

    [Fact]
    public void SetWrapping_AfterSkippedOptional_IsNoOp()
    {
        // The optional field is missing, so SetWrapping has no field to configure and must NOT
        // reconfigure the earlier field.
        var table = NewDetails()
            .Add("Name:", "alpha")
            .AddOptional("Path:", null, style: ThemeStyle.Path)
            .SetWrapping(CliWrapping.WordWrap)
            .ToTable();

        Assert.Single(table.Header.Elements);
        Assert.Null(ValueStyle(table, 0)?.Wrapping);
    }

    [Fact]
    public void SetWrapping_AfterAddedOptional_ConfiguresThatField()
    {
        var table = NewDetails()
            .Add("Name:", "alpha")
            .AddOptional("Description:", LongText)
            .SetWrapping(CliWrapping.WordWrap)
            .ToTable();

        Assert.Equal(2, table.Header.Elements.Count);
        Assert.Null(ValueStyle(table, 0)?.Wrapping);                          // untouched
        Assert.Equal(CliWrapMode.WordWrap, ValueStyle(table, 1)!.Wrapping!.Mode);
    }

    [Fact]
    public void Wrapping_PreservesSemanticThemeStyle()
    {
        var table = NewDetails()
            .AddKey("Id:", "abc-123")
            .SetWrapping(CliWrapping.WordWrapTruncate)
            .ToTable();

        var style = ValueStyle(table, 0)!;
        Assert.Equal(Blue.Resolve(ThemeStyle.Key).CharStyle?.Foreground, style.CharStyle?.Foreground);
        Assert.Equal(CliWrapMode.WordWrap, style.Wrapping!.Mode);
        Assert.True(style.Wrapping.AllowTruncation);
    }

    [Fact]
    public void NoWrappingConfigured_LeavesStylesUnchanged_AndKeepsValueOnOneLine()
    {
        var table = NewDetails()
            .Add("Description:", LongText)
            .ToTable();

        // Regression: no wrapping style on the field, no width on the value column.
        Assert.Null(table.Header.Elements[0].DataStyle?.Wrapping);
        Assert.Null(table.DataStyle?.MaxWidth);

        var text = string.Join("\n", TigerConsole.RenderToLines(table));
        Assert.Contains(LongText, text);
    }

    [Fact]
    public void SetWrapping_NoFieldYet_IsNoOp()
    {
        // No field added yet: SetWrapping is a graceful no-op (mirrors the skipped-optional case).
        var details = NewDetails().SetWrapping(CliWrapping.WordWrap);
        var table = details.Add("Name:", "alpha").ToTable();

        Assert.Null(ValueStyle(table, 0)?.Wrapping);
    }

    [Fact]
    public void DefaultWrapping_NullArgument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NewDetails().DefaultWrapping(null!));
    }
}
