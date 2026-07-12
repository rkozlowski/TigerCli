using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Testing;

namespace DocSamples;

/// <summary>
/// TUI storyboard capture per <c>docs/design/doc-artifacts.md</c>: scripted <see cref="TestShell"/>
/// runs drive the real semi-interactive modal loop through the public <see cref="TigerTui"/> API,
/// and <see cref="TestTerminal.LastRenderedGrid"/> at each chosen moment is rendered to HTML and
/// to a PNG companion on the shared <see cref="DocTerminal"/> canvas.
/// The captured grids are already measured at the shell viewport, and <c>RenderGrid</c> never
/// re-measures a measured grid, so every frame is pixel-faithful to what the terminal showed —
/// which is why every shell here comes from <see cref="DocTerminal.CreateShell"/>.
/// Must run inside <see cref="DocExampleSet.GenerateAllAsync"/>'s pinned-theme region:
/// <see cref="TestShell"/> resolves its theme from <c>TigerConsole.CurrentTheme</c> at construction.
/// </summary>
internal static class TuiStoryboards
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

    public static async Task<IReadOnlyList<DocArtifact>> GenerateAsync()
    {
        var sections = new List<DocPage.Section>();
        var pngs = new List<DocArtifact>();
        sections.AddRange(await SelectFramesAsync(pngs));
        sections.Add(await ScrolledSelectFrameAsync(pngs));
        sections.AddRange(await MultiSelectFramesAsync(pngs));

        var page = DocArtifact.Text(
            "tui-storyboards.html",
            DocPage.BuildPage(
                "Semi-interactive storyboards",
                "Frames of the semi-interactive prompt dialogs, captured from scripted TestShell "
                + "runs of the real modal loop and rendered to HTML with the TigerBlue theme — "
                + "the storyboard form of a screenshot, drift-protected like every other artifact.",
                sections));

        return [page, .. pngs];
    }

    // ---- select ------------------------------------------------------------------------------

    private static async Task<IReadOnlyList<DocPage.Section>> SelectFramesAsync(List<DocArtifact> pngs)
    {
        var shell = DocTerminal.CreateShell();
        var task = TigerTui.SelectAsync(
            shell, "Choose environment", ["Local", "Demo", "Staging", "Production"]);

        await shell.Terminal.WaitForRenderCountAsync(1, StepTimeout);
        var initial = CaptureFrame(shell, pngs, "tui-select-initial", "Choose environment");

        shell.Terminal.EnqueueKeys(ConsoleKey.DownArrow, ConsoleKey.DownArrow);
        await shell.Terminal.WaitForInputDrainedAsync(StepTimeout);
        var navigated = CaptureFrame(shell, pngs, "tui-select-navigated", "Choose environment");

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await task.WaitAsync(StepTimeout);
        Require(result == "Staging", "select result", result);

        return
        [
            new(
                "Select prompt — initial state",
                "The dialog frames the list, the first row is active, and the hint row documents "
                + "the keys. Escape cancels and returns null.",
                "string? environment = await TigerTui.SelectAsync(\n"
                + "    \"Choose environment\",\n"
                + "    [\"Local\", \"Demo\", \"Staging\", \"Production\"]);",
                initial),
            new(
                "Select prompt — after ↓ ↓",
                "Arrow keys move the active row; the shell rerenders after every handled key. "
                + "Enter now confirms and the call returns \"Staging\".",
                null,
                navigated),
        ];
    }

    private static async Task<DocPage.Section> ScrolledSelectFrameAsync(List<DocArtifact> pngs)
    {
        // A viewport shorter than the list forces scrolling: the scrollbar is a post-layout
        // overlay strip, captured in the measured frame like everything else.
        var shell = DocTerminal.CreateShell(viewportHeight: 8);
        var labels = new List<string?>();
        for (int i = 1; i <= 12; i++)
            labels.Add($"cam-{i:000}");

        var task = TigerTui.SelectIndexAsync(shell, "Choose device", labels);

        await shell.Terminal.WaitForRenderCountAsync(1, StepTimeout);
        shell.Terminal.EnqueueKey(ConsoleKey.End);
        await shell.Terminal.WaitForInputDrainedAsync(StepTimeout);
        var scrolled = CaptureFrame(shell, pngs, "tui-select-scrolled", "Choose device");

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var result = await task.WaitAsync(StepTimeout);
        Require(result is null, "scrolled-select result", result);

        return new DocPage.Section(
            "Select prompt — a longer list scrolls",
            "Twelve items on an 8-row viewport, after End: the list scrolls to the last row and "
            + "the scrollbar overlay tracks the active position.",
            null,
            scrolled);
    }

    // ---- multi-select --------------------------------------------------------------------------

    private static async Task<IReadOnlyList<DocPage.Section>> MultiSelectFramesAsync(List<DocArtifact> pngs)
    {
        var shell = DocTerminal.CreateShell();
        var task = TigerTui.MultiSelectIndexesAsync(
            shell, "Choose features", ["Logging", "Metrics", "Trace", "Audit"],
            preselectedIndexes: [0]);

        await shell.Terminal.WaitForRenderCountAsync(1, StepTimeout);
        var initial = CaptureFrame(shell, pngs, "tui-multi-select-initial", "Choose features");

        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        await shell.Terminal.WaitForInputDrainedAsync(StepTimeout);
        var toggled = CaptureFrame(shell, pngs, "tui-multi-select-toggled", "Choose features");

        shell.Terminal.EnqueueKey(ConsoleKey.Add, keyChar: '+');
        await shell.Terminal.WaitForInputDrainedAsync(StepTimeout);
        var all = CaptureFrame(shell, pngs, "tui-multi-select-all", "Choose features");

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await task.WaitAsync(StepTimeout);
        Require(result is [0, 1, 2, 3], "multi-select result", result);

        return
        [
            new(
                "Multi-select prompt — initial state",
                "Each row carries a checkbox; \"Logging\" was preselected by the caller. The hint "
                + "row documents toggling and the bulk keys.",
                "int[]? indexes = await TigerTui.MultiSelectIndexesAsync(\n"
                + "    \"Choose features\",\n"
                + "    [\"Logging\", \"Metrics\", \"Trace\", \"Audit\"],\n"
                + "    preselectedIndexes: [0]);",
                initial),
            new(
                "Multi-select prompt — ↓ then Space",
                "Space toggles the active row, so \"Metrics\" is now checked too.",
                null,
                toggled),
            new(
                "Multi-select prompt — + selects all",
                "The bulk keys act on the whole list: + selects all, - clears, * inverts. Enter "
                + "now confirms and the call returns [0, 1, 2, 3].",
                null,
                all),
        ];
    }

    // ---- helpers -------------------------------------------------------------------------------

    // Captures the current frame both ways from the same measured grid: the HTML fragment for the
    // storyboard page, and the PNG companion on the documentation terminal canvas — the same width
    // every shell above was created at.
    private static string CaptureFrame(TestShell shell, List<DocArtifact> pngs, string pngName, string title)
    {
        var grid = shell.Terminal.LastRenderedGrid
            ?? throw new InvalidOperationException("TUI storyboard capture: no grid was rendered.");
        pngs.AddRange(PngCompanion.FromMeasuredGrid(
            pngName, grid, title: title));
        return TigerConsole.RenderGridToHtml(grid);
    }

    private static void Require(bool condition, string what, object? actual)
    {
        if (!condition)
            throw new InvalidOperationException(
                $"TUI storyboard capture: unexpected {what} ({actual ?? "<null>"}). "
                + "The scripted run no longer behaves as the storyboard describes.");
    }
}
