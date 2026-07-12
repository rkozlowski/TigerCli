using System.Globalization;
using System.Reflection;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Activity;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Text styling/alignment for the rich activity dialog: fluent <c>.Style(...)</c>/<c>.Align(...)</c> on a
/// text cell plus spec-level default cell style/alignment, and the precedence
/// (text element &gt; column &gt; spec default &gt; built-in fallback). Style/alignment are resolved
/// through the existing <c>CliGrid</c> cascade — verified via <see cref="CliGrid.GetCellStyle"/> for
/// semantics and rendered lines for layout — never by manual padding in activity code.
/// </summary>
public sealed class ActivityTextStylingTests
{
    private static TestShell NewShell() => new(culture: CultureInfo.GetCultureInfo("en-US"));

    // Builds the activity content grid the dialog would render (no modal run needed for static styling).
    private static CliGrid Grid(TestShell shell, ActivityDialogSpec spec) =>
        new InlineActivityControl<int>(shell, spec, (_, _) => Task.FromResult(0)).ToGrid();

    private static CliColor? Fg(TestShell shell, ThemeStyle style) =>
        shell.Theme.Resolve(style).CharStyle?.Foreground;

    // ── Builder/model ────────────────────────────────────────────────────────

    [Fact]
    public void TextBuilder_StoresStyleAndAlignment_OnElement()
    {
        var spec = ActivityDialogSpec.Create()
            .AddColumn()
            .AddRow(null, r => r.Cell(0).Text("Hi").Style(ThemeStyle.Accent).Align(CliTextAlignment.Center))
            .Build();

        var text = Assert.IsType<ActivityTextElement>(spec.Rows[0].Cells[0].Element);
        Assert.Equal(ThemeStyle.Accent, text.Style);
        Assert.Equal(CliTextAlignment.Center, text.Alignment);
    }

    [Fact]
    public void Spec_DefaultsStored_AndColumnAlignNullByDefault()
    {
        var spec = ActivityDialogSpec.Create()
            .SetDefaultCellStyle(ThemeStyle.MutedText)
            .SetDefaultCellAlignment(CliTextAlignment.Right)
            .AddColumn()
            .AddRow(null, r => r.Cell(0).Text("Hi"))
            .Build();

        Assert.Equal(ThemeStyle.MutedText, spec.DefaultCellStyle);
        Assert.Equal(CliTextAlignment.Right, spec.DefaultCellAlignment);
        Assert.Null(spec.Columns[0].Align); // unset column alignment defers to the spec default
    }

    [Fact]
    public void Text_SignatureRemainsTemplateOnly()
    {
        // Text(...) must take only the template — style/alignment are fluent, never overloaded onto Text.
        var methods = typeof(ActivityCellBuilder).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Text")
            .ToArray();

        var method = Assert.Single(methods);
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
    }

    // ── Style/alignment apply ─────────────────────────────────────────────────

    [Fact]
    public void TextStyle_AppliesRequestedThemeStyle()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 20)
            .AddRow(null, r => r.Cell(0).Text("Hi").Style(ThemeStyle.Accent))
            .Build();

        Assert.Equal(Fg(shell, ThemeStyle.Accent), Grid(shell, spec).GetCellStyle(0, 0).CharStyle?.Foreground);
    }

    [Fact]
    public void TextAlign_AppliesRequestedAlignment()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 20)
            .AddRow(null, r => r.Cell(0).Text("Hi").Align(CliTextAlignment.Right))
            .Build();

        Assert.Equal(CliTextAlignment.Right, Grid(shell, spec).GetCellStyle(0, 0).HorizontalAlignment);
    }

    // ── Spec defaults ─────────────────────────────────────────────────────────

    [Fact]
    public void SpecDefaultStyle_AppliesWhenTextHasNoExplicitStyle()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .SetDefaultCellStyle(ThemeStyle.Accent)
            .AddColumn(width: 20)
            .AddRow(null, r => r.Cell(0).Text("Hi"))
            .Build();

        Assert.Equal(Fg(shell, ThemeStyle.Accent), Grid(shell, spec).GetCellStyle(0, 0).CharStyle?.Foreground);
    }

    [Fact]
    public void SpecDefaultAlignment_AppliesWhenTextHasNoExplicitAlignment()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .SetDefaultCellAlignment(CliTextAlignment.Center)
            .AddColumn(width: 20)
            .AddRow(null, r => r.Cell(0).Text("Hi"))
            .Build();

        Assert.Equal(CliTextAlignment.Center, Grid(shell, spec).GetCellStyle(0, 0).HorizontalAlignment);
    }

    // ── Precedence: text element wins ─────────────────────────────────────────

    [Fact]
    public void TextStyle_OverridesSpecDefault()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .SetDefaultCellStyle(ThemeStyle.MutedText)
            .AddColumn(width: 20)
            .AddRow(null, r => r.Cell(0).Text("Hi").Style(ThemeStyle.Accent))
            .Build();

        var fg = Grid(shell, spec).GetCellStyle(0, 0).CharStyle?.Foreground;
        Assert.Equal(Fg(shell, ThemeStyle.Accent), fg);
        Assert.NotEqual(Fg(shell, ThemeStyle.MutedText), fg);
    }

    [Fact]
    public void TextAlignment_OverridesSpecDefault()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .SetDefaultCellAlignment(CliTextAlignment.Left)
            .AddColumn(width: 20)
            .AddRow(null, r => r.Cell(0).Text("Hi").Align(CliTextAlignment.Right))
            .Build();

        Assert.Equal(CliTextAlignment.Right, Grid(shell, spec).GetCellStyle(0, 0).HorizontalAlignment);
    }

    // ── Precedence: column beats spec default ─────────────────────────────────

    [Fact]
    public void ColumnDefault_BeatsSpecDefault_ForStyleAndAlignment()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .SetDefaultCellStyle(ThemeStyle.MutedText)
            .SetDefaultCellAlignment(CliTextAlignment.Left)
            .AddColumn(width: 20, align: CliTextAlignment.Right, style: ThemeStyle.Accent)
            .AddRow(null, r => r.Cell(0).Text("Hi"))
            .Build();

        var cell = Grid(shell, spec).GetCellStyle(0, 0);
        Assert.Equal(Fg(shell, ThemeStyle.Accent), cell.CharStyle?.Foreground);   // column style wins
        Assert.NotEqual(Fg(shell, ThemeStyle.MutedText), cell.CharStyle?.Foreground);
        Assert.Equal(CliTextAlignment.Right, cell.HorizontalAlignment);          // column alignment wins
    }

    [Fact]
    public void TextElement_BeatsColumnDefault()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 20, align: CliTextAlignment.Right, style: ThemeStyle.MutedText)
            .AddRow(null, r => r.Cell(0).Text("Hi").Style(ThemeStyle.Accent).Align(CliTextAlignment.Center))
            .Build();

        var cell = Grid(shell, spec).GetCellStyle(0, 0);
        Assert.Equal(Fg(shell, ThemeStyle.Accent), cell.CharStyle?.Foreground);
        Assert.Equal(CliTextAlignment.Center, cell.HorizontalAlignment);
    }

    // ── Safe markup + progress unaffected ─────────────────────────────────────

    [Fact]
    public void StyledText_StillEscapesPlaceholderValues()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 40)
            .AddRow("r", r => r.Cell(0).Text("File: [Accent]{0}[/]").Style(ThemeStyle.MutedText).Values("[Red]evil[/]"))
            .Build();

        var lines = TigerConsole.RenderGridToLines(Grid(shell, spec));
        // The injected markup in the value renders literally (escaped), not as interpreted markup.
        Assert.Contains(lines, l => l.Contains("[Red]evil[/]", StringComparison.Ordinal));
    }

    [Fact]
    public void ProgressRow_Unaffected_ByTextStyling()
    {
        var shell = NewShell();
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 8, align: CliTextAlignment.Right)
            .AddColumn(sizing: CliColumnSizing.Star)
            .AddRow("files", r => r
                .Cell(0).Text("Files:").Style(ThemeStyle.Heading)
                .Cell(1).ProgressBar(valueIndex: 0, maxValueIndex: 1)
                .Values(40, 100))
            .Build();

        // The progress element is untouched by the text-styling slice and still computes its fraction.
        var bar = Assert.IsType<ActivityProgressBarElement>(spec.Rows[0].Cells[1].Element);
        Assert.Equal(0.4, bar.Fraction(new object?[] { 40, 100 }), 5);

        // The styled text cell carries the requested style; building/rendering the grid does not throw.
        var grid = Grid(shell, spec);
        Assert.Equal(Fg(shell, ThemeStyle.Heading), grid.GetCellStyle(0, 0).CharStyle?.Foreground);
        _ = TigerConsole.RenderGridToLines(grid);
    }
}
