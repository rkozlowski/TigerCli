using System.Globalization;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Activity;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Multi-colour activity progress bars: two-colour (done / not-done) and three-colour (done / not-done /
/// complete). The bar uses one glyph from the chosen <see cref="ProgressBarStyle"/> for every cell and
/// distinguishes the parts by colour, drawn from semantic theme styles resolved in the activity control.
/// The "complete" colour applies only at exactly 100%. Single-colour bars are asserted to stay uniform.
/// </summary>
public sealed class ActivityMultiColorProgressBarTests : TestBase
{
    private static TestShell NewShell() => new(culture: CultureInfo.GetCultureInfo("en-US"));

    private static ActivityDialogSpec BarSpec(
        double value, double max, ProgressBarColorMode mode,
        ProgressBarStyle style = ProgressBarStyle.Default) =>
        ActivityDialogSpec.Create()
            .AddColumn(sizing: CliColumnSizing.Star)
            .AddRow("p", r => r.Cell(0)
                .ProgressBar(valueIndex: 0, maxValue: max, style: style, colorMode: mode)
                .Values(value))
            .Build();

    // Renders the control's content grid (which carries the bar overlay) to per-character (glyph, style)
    // pairs. The overlay reads the spec's seeded values, so the bar reflects `value/max` at this fraction.
    private static List<(char ch, CliCharStyle style)> RenderBarCells(TestShell shell, ActivityDialogSpec spec)
    {
        var control = new InlineActivityControl<int>(shell, spec, (_, _) => Task.FromResult(0));
        var grid = control.ToGrid();
        var sink = new TextSegmentLinesSink();
        var lines = TigerConsole.RenderGridToSegmentedLines(grid, sink);

        var cells = new List<(char, CliCharStyle)>();
        foreach (var line in lines)
            foreach (var seg in line)
                foreach (var c in seg.Text)
                    cells.Add((c, seg.Style));
        return cells;
    }

    private static CliColor? Fg(TestShell shell, ThemeStyle style) =>
        shell.Theme.Resolve(style).CharStyle?.Foreground;

    // -------------------- two-colour --------------------

    [Fact]
    public void TwoColor_RendersDoneAndRemaining_WithDistinctThemeColours()
    {
        var shell = NewShell();
        var done = Fg(shell, ThemeStyle.ProgressBarDone);
        var remaining = Fg(shell, ThemeStyle.ProgressBarRemaining);
        Assert.NotEqual(done, remaining); // the theme distinguishes the two parts by colour

        var cells = RenderBarCells(shell, BarSpec(50, 100, ProgressBarColorMode.TwoColor));

        // Every cell uses the same glyph (Default family → █); the parts differ only by colour.
        Assert.All(cells, c => Assert.Equal(ConsoleSymbol.FullBlock, c.ch));
        Assert.Contains(cells, c => c.style.Foreground == done);
        Assert.Contains(cells, c => c.style.Foreground == remaining);
        // No "complete" colour appears in two-colour mode, even though half is filled.
        Assert.DoesNotContain(cells, c => c.style.Foreground == Fg(shell, ThemeStyle.ProgressBarComplete)
            && Fg(shell, ThemeStyle.ProgressBarComplete) != done);
    }

    // -------------------- three-colour --------------------

    [Fact]
    public void ThreeColor_AtFull_UsesCompleteColour()
    {
        var shell = NewShell();
        var complete = Fg(shell, ThemeStyle.ProgressBarComplete);

        var cells = RenderBarCells(shell, BarSpec(100, 100, ProgressBarColorMode.ThreeColor));

        Assert.NotEmpty(cells);
        Assert.All(cells, c =>
        {
            Assert.Equal(ConsoleSymbol.FullBlock, c.ch);
            Assert.Equal(complete, c.style.Foreground); // whole filled bar recoloured to complete
        });
    }

    [Fact]
    public void ThreeColor_BelowFull_DoesNotUseCompleteColour()
    {
        var shell = NewShell();
        var done = Fg(shell, ThemeStyle.ProgressBarDone);
        var complete = Fg(shell, ThemeStyle.ProgressBarComplete);

        // 99% is below 100%, so complete must never appear — even where rounding fills cells.
        var cells = RenderBarCells(shell, BarSpec(99, 100, ProgressBarColorMode.ThreeColor));

        Assert.Contains(cells, c => c.style.Foreground == done);
        Assert.DoesNotContain(cells, c => c.style.Foreground == complete && complete != done);
    }

    // -------------------- single-colour stays uniform --------------------

    [Fact]
    public void Single_StaysUniform_FilledAndTrackShareOneColour()
    {
        var shell = NewShell();
        var accent = Fg(shell, ThemeStyle.Accent);

        var cells = RenderBarCells(shell, BarSpec(50, 100, ProgressBarColorMode.Single));

        // Default single bar: distinct filled/track glyphs, but one uniform colour across the whole strip.
        Assert.Contains(cells, c => c.ch == ConsoleSymbol.FullBlock);
        Assert.Contains(cells, c => c.ch == ConsoleSymbol.ShadeLight);
        Assert.All(cells, c => Assert.Equal(accent, c.style.Foreground));
    }

    [Fact]
    public void MultiColor_GlyphFamily_UsesSameSolidGlyphForEveryCell()
    {
        var shell = NewShell();

        // A non-default family (Line → ━) repeats its solid glyph across every cell in multi-colour mode.
        var cells = RenderBarCells(shell, BarSpec(50, 100, ProgressBarColorMode.TwoColor, ProgressBarStyle.Line));

        Assert.NotEmpty(cells);
        Assert.All(cells, c => Assert.Equal(ConsoleSymbol.HeavyHorizontal, c.ch));
    }

    [Theory]
    [InlineData(ProgressBarColorMode.Single)]
    [InlineData(ProgressBarColorMode.TwoColor)]
    [InlineData(ProgressBarColorMode.ThreeColor)]
    public void ForegroundOnlyBarStyles_PreserveTigerBlueDialogSurface(ProgressBarColorMode mode)
    {
        var original = TigerConsole.CurrentTheme;
        try
        {
            TigerConsole.CurrentTheme = new TigerBlueTheme();
            var shell = NewShell();
            var dialogBackground = shell.Theme.Resolve(ThemeStyle.DialogSurface).CharStyle?.Background;

            var cells = RenderBarCells(shell, BarSpec(50, 100, mode));

            Assert.IsType<TigerBlueTheme>(shell.Theme);
            Assert.Equal(CliColor.Navy, dialogBackground);
            Assert.NotEmpty(cells);
            Assert.All(cells, c => Assert.Equal(dialogBackground, c.style.Background));
        }
        finally
        {
            TigerConsole.CurrentTheme = original;
        }
    }

    // -------------------- built-in theme defaults --------------------

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("tiger-blue")]
    public void BuiltInThemes_ResolveSensibleProgressBarInks(string themeName)
    {
        ITheme theme = themeName switch
        {
            "dark" => new DarkTheme(),
            "light" => new LightTheme(),
            _ => new TigerBlueTheme(),
        };

        var done = theme.Resolve(ThemeStyle.ProgressBarDone).CharStyle?.Foreground;
        var remaining = theme.Resolve(ThemeStyle.ProgressBarRemaining).CharStyle?.Foreground;
        var complete = theme.Resolve(ThemeStyle.ProgressBarComplete).CharStyle?.Foreground;

        Assert.NotNull(done);
        Assert.NotNull(remaining);
        Assert.NotNull(complete);
        // Done falls back to the accent; the complete state is a distinct (green) colour on every theme.
        Assert.Equal(theme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground, done);
        Assert.NotEqual(done, complete);
        Assert.NotEqual(remaining, complete);
    }
}
