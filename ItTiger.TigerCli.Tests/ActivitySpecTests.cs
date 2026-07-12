using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Tui.Activity;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Spec/model validation for the rich activity dialog: variable columns, fixed/star sizing, static vs
/// dynamic rows, unique names, fixed-length value arrays, spans, and progress/text element rules. Plus
/// the public progress-fraction calculation and the generic progress-bar overlay renderer.
/// </summary>
public sealed class ActivitySpecTests
{
    // ── Columns ──────────────────────────────────────────────────────────────

    [Fact]
    public void Build_VariableColumns_FixedAutoAndStar()
    {
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 10)
            .AddColumn(sizing: CliColumnSizing.Star)
            .AddColumn() // auto
            .AddRow(null, r => r.Cell(0, span: 3).Text("hi"))
            .Build();

        Assert.Equal(3, spec.Columns.Count);
        Assert.Equal(10, spec.Columns[0].Width);
        Assert.Equal(CliColumnSizing.Star, spec.Columns[1].Sizing);
        Assert.Null(spec.Columns[2].Width);
        Assert.Equal(CliColumnSizing.Auto, spec.Columns[2].Sizing);
    }

    [Fact]
    public void AddColumn_FixedWidthAndStar_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ActivityDialogSpec.Create().AddColumn(width: 10, sizing: CliColumnSizing.Star));
    }

    [Fact]
    public void AddColumn_NonPositiveWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivityDialogSpec.Create().AddColumn(width: 0));
    }

    // ── Rows: static vs dynamic ──────────────────────────────────────────────

    [Fact]
    public void StaticUnnamedRow_NoValues_Ok()
    {
        var spec = ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow(null, r => r.Cell(0).Text("Loading..."))
            .Build();

        Assert.Single(spec.Rows);
        Assert.False(spec.Rows[0].IsDynamic);
        Assert.Equal(0, spec.Rows[0].ValueCount);
    }

    [Fact]
    public void DynamicNamedRow_FixedValueCount()
    {
        var spec = ActivityDialogSpec.Create()
            .AddColumn().AddColumn()
            .AddRow("files", r => r.Cell(0).Text("Files:").Cell(1).Text("{0}/{1}").Values(0, 0))
            .Build();

        var row = spec.GetRow("files");
        Assert.NotNull(row);
        Assert.True(row!.IsDynamic);
        Assert.Equal(2, row.ValueCount);
    }

    [Fact]
    public void DuplicateDynamicRowName_Throws()
    {
        var builder = ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("dup", r => r.Cell(0).Text("{0}").Values(1));

        Assert.Throws<ArgumentException>(() =>
            builder.AddRow("dup", r => r.Cell(0).Text("{0}").Values(2)));
    }

    [Fact]
    public void NamedRowWithoutValues_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ActivityDialogSpec.Create()
                .AddColumn()
                .AddRow("x", r => r.Cell(0).Text("label"))
                .Build());
    }

    [Fact]
    public void StaticRowWithValues_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ActivityDialogSpec.Create()
                .AddColumn()
                .AddRow(null, r => r.Cell(0).Text("label").Values(1))
                .Build());
    }

    [Fact]
    public void StaticRowText_WithPlaceholder_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ActivityDialogSpec.Create()
                .AddColumn()
                .AddRow(null, r => r.Cell(0).Text("File: {0}"))
                .Build());
    }

    [Fact]
    public void TextTemplate_ReferencingMissingValue_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ActivityDialogSpec.Create()
                .AddColumn()
                .AddRow("r", r => r.Cell(0).Text("{0} {5}").Values(1))
                .Build());
    }

    // ── Cells / spans ────────────────────────────────────────────────────────

    [Fact]
    public void Cell_Span_Recorded()
    {
        var spec = ActivityDialogSpec.Create()
            .AddColumn().AddColumn().AddColumn()
            .AddRow(null, r => r.Cell(0, span: 3).Text("wide"))
            .Build();

        var cell = spec.Rows[0].Cells[0];
        Assert.Equal(0, cell.Column);
        Assert.Equal(3, cell.Span);
    }

    [Fact]
    public void Cell_OutOfRangeColumn_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivityDialogSpec.Create()
                .AddColumn()
                .AddRow(null, r => r.Cell(5).Text("x")));
    }

    [Fact]
    public void Cell_SpanBeyondColumns_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivityDialogSpec.Create()
                .AddColumn().AddColumn()
                .AddRow(null, r => r.Cell(0, span: 3).Text("x")));
    }

    [Fact]
    public void Cell_OverlappingColumns_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ActivityDialogSpec.Create()
                .AddColumn().AddColumn()
                .AddRow(null, r => r.Cell(0, span: 2).Text("a").Cell(1).Text("b")));
    }

    [Fact]
    public void EmptySpec_NoColumns_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ActivityDialogSpec.Create().Build());
    }

    [Fact]
    public void Rows_BeforeColumns_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ActivityDialogSpec.Create().AddRow(null, r => r.Cell(0).Text("x")));
    }

    // ── Progress element ─────────────────────────────────────────────────────

    [Fact]
    public void ProgressBar_OnStaticRow_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ActivityDialogSpec.Create()
                .AddColumn()
                .AddRow(null, r => r.Cell(0).ProgressBar(valueIndex: 0))
                .Build());
    }

    [Fact]
    public void ProgressBar_ValueIndexOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivityDialogSpec.Create()
                .AddColumn()
                .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 3).Values(0))
                .Build());
    }

    [Fact]
    public void ProgressBar_MaxValueIndexOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivityDialogSpec.Create()
                .AddColumn()
                .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0, maxValueIndex: 9).Values(0))
                .Build());
    }

    [Fact]
    public void ProgressFraction_DefaultMaxIs100()
    {
        var bar = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0).Values(50.0))
            .Build());

        Assert.Equal(0.5, bar.Fraction(new object?[] { 50.0 }), 5);
    }

    [Fact]
    public void ProgressFraction_DynamicMaxByIndex()
    {
        var bar = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0, maxValueIndex: 1).Values(0, 0))
            .Build());

        Assert.Equal(0.25, bar.Fraction(new object?[] { 10, 40 }), 5);
    }

    [Fact]
    public void ProgressFraction_Clamps()
    {
        var bar = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0, maxValue: 10).Values(0))
            .Build());

        Assert.Equal(0, bar.Fraction(new object?[] { -5 }), 5);
        Assert.Equal(1, bar.Fraction(new object?[] { 999 }), 5);
    }

    [Fact]
    public void ProgressFraction_NonPositiveMax_IsZero()
    {
        var bar = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0, maxValueIndex: 1).Values(5, 0))
            .Build());

        Assert.Equal(0, bar.Fraction(new object?[] { 5, 0 }), 5);   // max == 0, no divide-by-zero
        Assert.Equal(0, bar.Fraction(new object?[] { 5, -3 }), 5);  // negative max
    }

    [Fact]
    public void ProgressBar_Style_DefaultsToDefault()
    {
        var bar = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0).Values(0))
            .Build());

        Assert.Equal(ProgressBarStyle.Default, bar.Style);
    }

    [Fact]
    public void ProgressBar_Style_RecordedFromBuilder_BothOverloads()
    {
        var fixedMax = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0, style: ProgressBarStyle.Square).Values(0))
            .Build());
        Assert.Equal(ProgressBarStyle.Square, fixedMax.Style);

        var dynamicMax = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0, maxValueIndex: 1, style: ProgressBarStyle.Dash).Values(0, 0))
            .Build());
        Assert.Equal(ProgressBarStyle.Dash, dynamicMax.Style);
    }

    [Fact]
    public void ProgressBar_Caps_DefaultsToNone()
    {
        var bar = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0).Values(0))
            .Build());

        Assert.Equal(ProgressBarCaps.None, bar.Caps);
    }

    [Fact]
    public void ProgressBar_Caps_RecordedFromBuilder_BothOverloads()
    {
        var fixedMax = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0, caps: ProgressBarCaps.Brackets).Values(0))
            .Build());
        Assert.Equal(ProgressBarCaps.Brackets, fixedMax.Caps);

        var dynamicMax = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0, maxValueIndex: 1, caps: ProgressBarCaps.Brackets).Values(0, 0))
            .Build());
        Assert.Equal(ProgressBarCaps.Brackets, dynamicMax.Caps);
    }

    [Fact]
    public void ProgressBar_Caps_ComposeWithAnyStyle()
    {
        // Caps are orthogonal to the glyph style: a non-default style still records its caps independently.
        var bar = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0)
                .ProgressBar(valueIndex: 0, style: ProgressBarStyle.Square, caps: ProgressBarCaps.Brackets)
                .Values(0))
            .Build());

        Assert.Equal(ProgressBarStyle.Square, bar.Style);
        Assert.Equal(ProgressBarCaps.Brackets, bar.Caps);
    }

    [Fact]
    public void ProgressBar_ColorMode_DefaultsToSingle()
    {
        var bar = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0).Values(0))
            .Build());

        Assert.Equal(ProgressBarColorMode.Single, bar.ColorMode);
    }

    [Fact]
    public void ProgressBar_ColorMode_RecordedFromBuilder_BothOverloads()
    {
        var fixedMax = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0, colorMode: ProgressBarColorMode.TwoColor).Values(0))
            .Build());
        Assert.Equal(ProgressBarColorMode.TwoColor, fixedMax.ColorMode);

        var dynamicMax = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0).ProgressBar(valueIndex: 0, maxValueIndex: 1, colorMode: ProgressBarColorMode.ThreeColor).Values(0, 0))
            .Build());
        Assert.Equal(ProgressBarColorMode.ThreeColor, dynamicMax.ColorMode);
    }

    [Fact]
    public void ProgressBar_ColorMode_ComposesWithStyleAndCaps()
    {
        // Colour mode, glyph family and caps are independent axes that all record on the same element.
        var bar = Bar(ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow("r", r => r.Cell(0)
                .ProgressBar(valueIndex: 0, style: ProgressBarStyle.Line, caps: ProgressBarCaps.Brackets,
                    colorMode: ProgressBarColorMode.ThreeColor)
                .Values(0))
            .Build());

        Assert.Equal(ProgressBarStyle.Line, bar.Style);
        Assert.Equal(ProgressBarCaps.Brackets, bar.Caps);
        Assert.Equal(ProgressBarColorMode.ThreeColor, bar.ColorMode);
    }

    // ── Overlay renderer ─────────────────────────────────────────────────────

    [Fact]
    public void ProgressBarOverlay_FillsRenderLength()
    {
        var renderer = CliOverlayRenderers.ProgressBar(() => 0.5);
        var (visible, content) = renderer(new CliGrid(1, 1), 10);

        Assert.True(visible);
        Assert.Equal(10, content.Length);
        Assert.Equal(5, content.Count(ch => ch == ConsoleSymbol.FullBlock));
        Assert.Equal(5, content.Count(ch => ch == ConsoleSymbol.ShadeLight));
    }

    [Fact]
    public void ProgressBarOverlay_FullAndEmpty()
    {
        var full = CliOverlayRenderers.ProgressBar(() => 1.0)(new CliGrid(1, 1), 8);
        Assert.All(full.content, ch => Assert.Equal(ConsoleSymbol.FullBlock, ch));

        var empty = CliOverlayRenderers.ProgressBar(() => 0.0)(new CliGrid(1, 1), 8);
        Assert.All(empty.content, ch => Assert.Equal(ConsoleSymbol.ShadeLight, ch));
    }

    [Fact]
    public void ProgressBarOverlay_ClampsOutOfRangeFraction()
    {
        var over = CliOverlayRenderers.ProgressBar(() => 2.0)(new CliGrid(1, 1), 6);
        Assert.Equal(6, over.content.Count(ch => ch == ConsoleSymbol.FullBlock));

        var under = CliOverlayRenderers.ProgressBar(() => -1.0)(new CliGrid(1, 1), 6);
        Assert.Equal(6, under.content.Count(ch => ch == ConsoleSymbol.ShadeLight));
    }

    [Fact]
    public void ProgressBarOverlay_CustomGlyphs_AreUsed()
    {
        var (_, content) = CliOverlayRenderers.ProgressBar(
            () => 1.0, ConsoleSymbol.Square, ConsoleSymbol.WhiteSquare)(new CliGrid(1, 1), 5);

        Assert.All(content, ch => Assert.Equal(ConsoleSymbol.Square, ch));
    }

    [Fact]
    public void ProgressBarOverlay_Caps_ReserveEndsAndFillInterior()
    {
        var (visible, content) = CliOverlayRenderers.ProgressBar(
            () => 0.5, leftCap: '[', rightCap: ']')(new CliGrid(1, 1), 10);

        Assert.True(visible);
        Assert.Equal(10, content.Length);
        Assert.Equal('[', content[0]);
        Assert.Equal(']', content[9]);
        // 8 interior cells, half filled / half track.
        Assert.Equal(4, content.Count(ch => ch == ConsoleSymbol.FullBlock));
        Assert.Equal(4, content.Count(ch => ch == ConsoleSymbol.ShadeLight));
    }

    [Fact]
    public void ProgressBarOverlay_Caps_DroppedWhenStripTooShort()
    {
        // renderLength 2 cannot hold both caps plus an interior cell, so caps are dropped and the bar fills.
        var (visible, content) = CliOverlayRenderers.ProgressBar(
            () => 1.0, leftCap: '[', rightCap: ']')(new CliGrid(1, 1), 2);

        Assert.True(visible);
        Assert.DoesNotContain('[', content);
        Assert.DoesNotContain(']', content);
        Assert.All(content, ch => Assert.Equal(ConsoleSymbol.FullBlock, ch));
    }

    private static ActivityProgressBarElement Bar(ActivityDialogSpec spec) =>
        (ActivityProgressBarElement)spec.Rows[0].Cells[0].Element;
}
