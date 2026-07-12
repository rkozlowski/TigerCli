using System.Globalization;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Activity;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Regression coverage for activity dialog <see cref="CliColumnSizing.Star"/> column sizing. A Star
/// column in the hosted activity content grid must expand so its content (text or a progress bar) has
/// usable width, while the dialog stays a correctly-framed rectangle and is sized to content — it must
/// neither overflow the frame on a narrow terminal nor balloon to the whole screen on a wide one. The
/// failure mode lives in the activity grid's Star/total-width handling, not the progress overlay (a
/// fixed-width bar column always rendered correctly).
/// </summary>
public sealed class ActivityStarColumnSizingTests
{
    private static TestShell NewShell(int width) =>
        new(viewportWidth: width, culture: CultureInfo.GetCultureInfo("en-US"));

    // fixed(8) + Star + fixed(16), matching the reported repro shape; the caller fills the Star cell.
    private static ActivityDialogSpec StarSpec(Action<ActivityRowBuilder> filesRow) =>
        ActivityDialogSpec.Create()
            .AddColumn(width: 8, align: CliTextAlignment.Right).Padding(CliCellPadding.Right)
            .AddColumn(sizing: CliColumnSizing.Star)
            .AddColumn(width: 16, align: CliTextAlignment.Left).Padding(CliCellPadding.Left)
            .AddRow(null, r => r.Cell(0, span: 3).Text("Working...").Align(CliTextAlignment.Center))
            .AddRow("files", filesRow)
            .Build();

    private static ActivityDialogSpec ProgressInStar() =>
        StarSpec(r => r
            .Cell(0).Text("Files:")
            .Cell(1).ProgressBar(valueIndex: 0, maxValueIndex: 1)
            .Cell(2).Text("[Blue]{2,5:F1}%[/] [Green]{0}/{1}[/]")
            .Values(1, 5, 20.0));

    private static ActivityDialogSpec TextInStar() =>
        StarSpec(r => r
            .Cell(0).Text("Files:")
            .Cell(1).Text("[Blue]MIDSTAR[/]").Align(CliTextAlignment.Center)
            .Cell(2).Text("[Green]{0}/{1}[/]")
            .Values(1, 5));

    private static async Task<string[]> RenderModalAsync(TestShell shell, ActivityDialogSpec spec, CancellationToken ct)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = TigerTui.RunActivityAsync(shell, "Activity", spec,
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; }, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(2), ct);
        var lines = shell.Terminal.LastRenderedText
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .ToArray();

        gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        return lines;
    }

    // Rightmost frame border column on a line (╗/╝/║), or -1 for a non-framed line.
    private static int RightBorderColumn(string line)
    {
        int idx = -1;
        foreach (char ch in new[] { '╗', '╝', '║' })
            idx = Math.Max(idx, line.LastIndexOf(ch));
        return idx;
    }

    private static int[] FrameRightBorders(string[] lines) =>
        lines.Select(RightBorderColumn).Where(i => i >= 0).ToArray();

    // Length of the longest contiguous run of progress-bar glyphs (filled or track) on any line.
    private static int LongestBarRun(string[] lines)
    {
        int best = 0;
        foreach (var line in lines)
        {
            int run = 0;
            foreach (char ch in line)
            {
                if (ch == ConsoleSymbol.FullBlock || ch == ConsoleSymbol.ShadeLight)
                    best = Math.Max(best, ++run);
                else
                    run = 0;
            }
        }
        return best;
    }

    private static void AssertFramedRectangle(string[] lines)
    {
        var borders = FrameRightBorders(lines);
        Assert.NotEmpty(borders);
        // Every framed row (top, content rows, bottom) must close at the same column.
        Assert.True(borders.Distinct().Count() == 1,
            $"Frame is not a rectangle; right borders at columns [{string.Join(",", borders)}]:\n{string.Join("\n", lines)}");
    }

    // ── 1. Text in the Star column ────────────────────────────────────────────

    [Fact]
    public async Task TextInStarColumn_Expands_AndFramesCleanly()
    {
        var ct = TestContext.Current.CancellationToken;
        var lines = await RenderModalAsync(NewShell(80), TextInStar(), ct);

        AssertFramedRectangle(lines);

        var midLine = lines.Single(l => l.Contains("MIDSTAR", StringComparison.Ordinal));
        // The Star column expanded, so the centered text sits well away from the left "Files:" label
        // rather than being squeezed against it.
        int filesEnd = midLine.IndexOf("Files:", StringComparison.Ordinal) + "Files:".Length;
        int midStart = midLine.IndexOf("MIDSTAR", StringComparison.Ordinal);
        Assert.True(midStart - filesEnd >= 8,
            $"Star text not given width (gap {midStart - filesEnd}):\n{string.Join("\n", lines)}");
    }

    // ── 2. ProgressBar in the Star column ─────────────────────────────────────

    [Fact]
    public async Task ProgressBarInStarColumn_FillsWideRegion_AndFramesCleanly()
    {
        var ct = TestContext.Current.CancellationToken;
        var lines = await RenderModalAsync(NewShell(80), ProgressInStar(), ct);

        AssertFramedRectangle(lines);

        var text = string.Join("\n", lines);
        Assert.Contains(ConsoleSymbol.FullBlock, text);   // filled portion (~20%)
        Assert.Contains(ConsoleSymbol.ShadeLight, text);  // track portion
        Assert.True(LongestBarRun(lines) >= 30,
            $"Progress bar did not fill the Star column (longest run {LongestBarRun(lines)}):\n{text}");
    }

    // ── 3. Wide terminal: sized to content, does not balloon ──────────────────

    [Fact]
    public async Task StarColumn_WideViewport_DoesNotBalloon()
    {
        var ct = TestContext.Current.CancellationToken;
        var lines = await RenderModalAsync(NewShell(200), ProgressInStar(), ct);

        AssertFramedRectangle(lines);
        int frameWidth = FrameRightBorders(lines).First() + 1;
        // A small progress dialog must stay content-sized on a 200-column terminal, not span it.
        Assert.True(frameWidth <= 90,
            $"Star activity dialog ballooned to {frameWidth} columns on a 200-wide viewport:\n{string.Join("\n", lines)}");
    }

    // ── 4. Narrow terminal: stays within the frame (no overflow) ──────────────

    [Fact]
    public async Task StarColumn_NarrowViewport_DoesNotOverflowFrame()
    {
        var ct = TestContext.Current.CancellationToken;
        var lines = await RenderModalAsync(NewShell(30), ProgressInStar(), ct);

        // Before the fix the content row was forced wider than the frame and the rectangle broke.
        AssertFramedRectangle(lines);
        int frameWidth = FrameRightBorders(lines).First() + 1;
        Assert.True(frameWidth <= 30,
            $"Frame width {frameWidth} exceeds the 30-column viewport:\n{string.Join("\n", lines)}");
    }

    // ── 5. Fixed-width middle column still renders correctly (control) ─────────

    [Fact]
    public async Task FixedWidthColumn_StillFramesCleanly()
    {
        var ct = TestContext.Current.CancellationToken;
        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 8, align: CliTextAlignment.Right).Padding(CliCellPadding.Right)
            .AddColumn(width: 40)
            .AddColumn(width: 16, align: CliTextAlignment.Left).Padding(CliCellPadding.Left)
            .AddRow(null, r => r.Cell(0, span: 3).Text("Working...").Align(CliTextAlignment.Center))
            .AddRow("files", r => r
                .Cell(0).Text("Files:")
                .Cell(1).ProgressBar(valueIndex: 0, maxValueIndex: 1)
                .Cell(2).Text("[Blue]{2,5:F1}%[/] [Green]{0}/{1}[/]")
                .Values(1, 5, 20.0))
            .Build();

        var lines = await RenderModalAsync(NewShell(80), spec, ct);

        AssertFramedRectangle(lines);
        Assert.True(LongestBarRun(lines) >= 30,
            $"Fixed-width progress bar did not fill its column:\n{string.Join("\n", lines)}");
    }
}
