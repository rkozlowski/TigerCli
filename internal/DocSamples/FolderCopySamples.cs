using System.Diagnostics;
using System.Globalization;
using FolderCopy;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.PngSink;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Themes;
using ActivityContext = ItTiger.TigerCli.Tui.Activity.ActivityContext;

namespace DocSamples;

/// <summary>
/// Artifacts for <c>docs/examples/folder-copy.md</c>, captured from the Folder Copy sample app.
/// The activity dialog is the sample's own: <see cref="FolderCopyCommand.BuildActivitySpec"/> builds
/// the spec and <see cref="FolderCopyCommand.ReportProgress"/> maps progress onto it (both internal,
/// granted to this generator via <c>InternalsVisibleTo</c>), so the committed artifacts cannot drift
/// from what the sample actually renders. The copy itself is a scripted storyboard of
/// <see cref="CopyProgress"/> snapshots shaped exactly like <see cref="FolderCopyPlanner.ExecuteAsync"/>
/// reports them (before each file, after each 1&#160;MiB write, after each file) — no real filesystem,
/// no wall clock: a manual-clock <see cref="TestShell"/> advances 100&#160;ms of virtual time per
/// snapshot, so elapsed/ETA and the top-frame spinner overlay are deterministic.
/// <para>Two outputs share the storyboard runner:</para>
/// <list type="bullet">
/// <item><see cref="GenerateAsync"/> (inside <see cref="DocExampleSet.GenerateAllAsync"/>'s
/// pinned-theme region, drift-checked): <c>folder-copy.html</c> (generated help + a mid-copy dialog
/// frame) and the <c>png/folder-copy-activity.png</c> companion of that frame.</item>
/// <item><see cref="GenerateWebpAsync"/> (<c>dotnet run --project internal/DocSamples --
/// folder-copy</c>): every storyboard frame rendered to PNG and assembled by <c>img2webp.exe</c>
/// (<c>internal/tools/libwebp/</c>, not committed — see its README) into a looping
/// <c>docs/examples/folder-copy/folder-copy-activity.webp</c>. Like the spinner and progress-bar
/// showcases this is a separate mode, regenerated deliberately rather than drift-checked, because it
/// needs the external libwebp tool and WebP bytes carry encoder-version variance.</item>
/// </list>
/// <para>Determinism per step: the runner waits until one rendered frame shows the step's expected
/// content of all five dynamic rows (file, current, files, total size, elapsed/ETA) plus the expected
/// spinner overlay frame. Row updates are coalesced whole-row by <c>ActivityState</c>, so a frame
/// matching all five rows is pixel-identical to the fully-applied snapshot no matter how the update
/// burst interleaved with the modal loop's drains.</para>
/// </summary>
internal static class FolderCopySamples
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The culture the capture computes expected row contents with; must match the
    /// <see cref="TestShell"/> default culture the dialog formats values with.</summary>
    private static readonly CultureInfo ShellCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Virtual time per storyboard snapshot; also the per-frame WebP duration.</summary>
    private const int StepMs = 100;

    /// <summary>How long the finished (100%) frame is held before the animation loops.</summary>
    private const int HoldMs = 2000;

    /// <summary>Simulated write size — one storyboard snapshot per this many copied bytes.</summary>
    private const long ChunkBytes = 1024 * 1024;

    private const string SourcePath = @"C:\Trip";
    private const string DestinationPath = @"D:\Backup";
    private const string WindowTitle = "folder-copy";

    // The fixed sample copy: Windows-style display paths (pure strings — nothing touches the real
    // filesystem) with sizes that sum to exactly 56.0 MB so the headline and bytes row stay tidy.
    private static CopyPlan SamplePlan()
    {
        var items = new[]
        {
            Item(@"photos\2024\beach-day.jpg", 4_718_592),      // 4.5 MB
            Item(@"photos\2024\city-lights.jpg", 6_291_456),    // 6.0 MB
            Item(@"photos\2025\lake-sunrise.jpg", 8_388_608),   // 8.0 MB
            Item(@"music\road-trip-mix.mp3", 12_582_912),       // 12.0 MB
            Item(@"video\family-picnic.mp4", 26_214_400),       // 25.0 MB
            Item(@"notes\packing-list.txt", 524_288),           // 0.5 MB
        };
        return new CopyPlan(items, items.Sum(static i => i.Length));

        static CopyItem Item(string relative, long length) =>
            new($@"{SourcePath}\{relative}", relative, length);
    }

    // ---- drift-checked artifacts (part of DocExampleSet.GenerateAllAsync) ---------------------

    public static async Task<IReadOnlyList<DocArtifact>> GenerateAsync()
    {
        var sections = new List<DocPage.Section>();
        var pngs = new List<DocArtifact>();

        var help = await TigerCliAppTestHost.For(FolderCopyApp.Create())
            .WithArgs("--help")
            .WithHtmlCapture()
            .RunAsync();
        Require(help.ExitCode == 0, "--help exit code", help.ExitCode);
        sections.Add(new DocPage.Section(
            "Generated help (--help)",
            "Metadata-driven help for the single-default-command app: name and description come "
            + "from the project file via UseAssemblyMetadata, plus the two required folder options.",
            "folder-copy --help",
            help.StdOutHtml!));

        // The mid-copy frame: file 5 of 6 (the video) 48% done, so all three Dash bars sit at
        // visibly different fills. The storyboard runner validates every frame it yields.
        var targetIndex = MidCopyFrameIndex(BuildStoryboard(SamplePlan()));
        string? dialogHtml = null;
        await RunActivityStoryboardAsync((step, _, shell) =>
        {
            if (step != targetIndex)
                return;

            var grid = shell.Terminal.LastRenderedGrid
                ?? throw new InvalidOperationException("Folder Copy capture: no grid was rendered.");
            dialogHtml = TigerConsole.RenderGridToHtml(grid);
            pngs.AddRange(PngCompanion.FromMeasuredGrid(
                "folder-copy-activity", grid, title: WindowTitle));
        });
        Require(dialogHtml is not null, "mid-copy frame capture", targetIndex);

        sections.Add(new DocPage.Section(
            "Copy activity dialog (mid-copy)",
            "The rich copy activity dialog, captured mid-copy from the sample's own activity spec "
            + "on a scripted TestShell with a manual clock: the current file with its own progress "
            + "bar, the files-copied and total-size bars (all ProgressBarStyle.Dash), and the "
            + "elapsed/ETA line. The bracketed glyph on the top border is the activity spinner "
            + "overlay; Esc maps to the Cancel button.",
            $"folder-copy --source {SourcePath} --destination {DestinationPath}",
            dialogHtml!));

        var page = DocArtifact.Text(
            "folder-copy.html",
            DocPage.BuildPage(
                "Folder Copy sample",
                "The Folder Copy real-operation sample (docs/examples/folder-copy.md), captured "
                + "from real runs and real rendering with the TigerBlue theme: the generated help, "
                + "and the copy activity dialog driven through the sample's own spec and progress "
                + "mapping by a deterministic storyboard.",
                sections));

        return [page, .. pngs];
    }

    // The chunk snapshot with the video file 12 MB into its 25 MB: current 48.0%, files 4/6
    // (66.7%), bytes 42.5/56.0 MB (75.9%).
    private static int MidCopyFrameIndex(IReadOnlyList<CopyProgress> storyboard)
    {
        for (int i = 0; i < storyboard.Count; i++)
        {
            var p = storyboard[i];
            if (p.CurrentRelativePath == @"video\family-picnic.mp4" && p.CurrentFileBytes == 12 * ChunkBytes)
                return i;
        }

        throw new InvalidOperationException("Folder Copy capture: the mid-copy storyboard frame was not found.");
    }

    // ---- animated WebP (separate `folder-copy` generator mode, not drift-checked) -------------

    public static async Task<int> GenerateWebpAsync(string repoRoot)
    {
        var img2Webp = Path.Combine(repoRoot, "internal", "tools", "libwebp", "img2webp.exe");
        if (!File.Exists(img2Webp))
        {
            Console.Error.WriteLine(
                $"img2webp.exe not found at {img2Webp}. "
                + "See internal/tools/libwebp/README.md for how to fetch it.");
            return 1;
        }

        var outputDirectory = Path.Combine(repoRoot, "docs", "examples", "folder-copy");
        Directory.CreateDirectory(outputDirectory);

        // Pin the theme exactly like DocExampleSet.GenerateAllAsync: TestShell resolves its theme
        // from TigerConsole.CurrentTheme at construction.
        var originalTheme = TigerConsole.CurrentTheme;
        var originalThemeEnv = Environment.GetEnvironmentVariable("TIGERCLI_THEME");
        try
        {
            TigerConsole.CurrentTheme = new TigerBlueTheme();
            Environment.SetEnvironmentVariable("TIGERCLI_THEME", null);

            var frames = new List<byte[]>();
            int? canvasRows = null;
            await RunActivityStoryboardAsync((_, spinnerFrame, shell) =>
            {
                var grid = shell.Terminal.LastRenderedGrid
                    ?? throw new InvalidOperationException("Folder Copy capture: no grid was rendered.");
                var lines = TigerConsole.RenderGridToLines(grid);

                // Width is fixed by DocTerminal; the row count must also stay constant across the run —
                // a requirement for animation assembly — so the first frame pins it.
                int rows = DocTerminal.EnsureFits("Folder Copy capture", lines);
                canvasRows ??= rows;
                if (canvasRows.Value != rows)
                    throw new InvalidOperationException(
                        $"Folder Copy capture: frame {frames.Count} is {rows} rows tall, "
                        + $"but the run started at {canvasRows.Value} rows.");

                // Mirror the terminal title an activity uses in a real terminal: its current spinner
                // glyph prefixes the app title. The dialog grid already carries the bracketed overlay;
                // the PNG chrome needs this explicit per-frame title because it is rendered separately.
                frames.Add(PngRenderer.RenderGridToBytes(
                    grid,
                    DocTerminal.FrameOptions(rows, $"{spinnerFrame} {WindowTitle}")));
            });

            var fileName = "folder-copy-activity.webp";
            BuildWebp(img2Webp, frames, Path.Combine(outputDirectory, fileName));
            Console.WriteLine($"wrote docs/examples/folder-copy/{fileName} ({frames.Count} frames)");
            return 0;
        }
        finally
        {
            TigerConsole.CurrentTheme = originalTheme;
            Environment.SetEnvironmentVariable("TIGERCLI_THEME", originalThemeEnv);
        }
    }

    // ---- storyboard ----------------------------------------------------------------------------

    // One CopyProgress per simulated report, in exactly the shape FolderCopyPlanner.ExecuteAsync
    // reports: before each file, after each write (ChunkBytes per write), and after each file.
    private static IReadOnlyList<CopyProgress> BuildStoryboard(CopyPlan plan)
    {
        var steps = new List<CopyProgress>();
        long bytesCopied = 0;
        var filesCompleted = 0;

        foreach (var item in plan.Items)
        {
            steps.Add(new CopyProgress(
                filesCompleted, plan.Items.Count, bytesCopied, plan.TotalBytes, item.RelativePath, 0, item.Length));

            long currentFileBytes = 0;
            while (currentFileBytes < item.Length)
            {
                var written = Math.Min(ChunkBytes, item.Length - currentFileBytes);
                currentFileBytes += written;
                bytesCopied += written;
                steps.Add(new CopyProgress(
                    filesCompleted, plan.Items.Count, bytesCopied, plan.TotalBytes,
                    item.RelativePath, currentFileBytes, item.Length));
            }

            filesCompleted++;
            steps.Add(new CopyProgress(
                filesCompleted, plan.Items.Count, bytesCopied, plan.TotalBytes,
                item.RelativePath, item.Length, item.Length));
        }

        return steps;
    }

    /// <summary>
    /// Runs the sample's real activity dialog on a manual-clock <see cref="TestShell"/> and yields
    /// one validated frame per storyboard snapshot to <paramref name="onFrame"/>. Each step advances
    /// 100&#160;ms of virtual time (so the spinner overlay ticks at its authentic 500&#160;ms period),
    /// applies the snapshot through <see cref="FolderCopyCommand.ReportProgress"/>, and waits until a
    /// rendered frame shows the expected content of all five dynamic rows plus the expected spinner
    /// frame — see the class remarks for why that makes the captured frame deterministic.
    /// </summary>
    private static async Task RunActivityStoryboardAsync(Action<int, string, TestShell> onFrame)
    {
        var plan = SamplePlan();
        var storyboard = BuildStoryboard(plan);
        var spec = FolderCopyCommand.BuildActivitySpec(new FolderCopySettings(), plan, DestinationPath);

        var shell = DocTerminal.CreateShell(useManualClock: true);
        var operationDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contextReady = new TaskCompletionSource<ActivityContext>(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = TigerTui.RunActivityAsync(
            shell,
            title: null,
            spec,
            async (context, ct) =>
            {
                contextReady.SetResult(context);
                await operationDone.Task.WaitAsync(ct);
            });

        await shell.Terminal.WaitForRenderCountAsync(1, StepTimeout);
        var context = await contextReady.Task.WaitAsync(StepTimeout);

        var spinnerFrames = SpinnerTicker.Frames(SpinnerFrameSet.Default);
        int spinnerPeriodMs = (int)SpinnerTicker.DefaultInterval.TotalMilliseconds;

        for (int i = 0; i < storyboard.Count; i++)
        {
            var progress = storyboard[i];
            var elapsed = TimeSpan.FromMilliseconds((i + 1) * StepMs);

            shell.AdvanceTime(TimeSpan.FromMilliseconds(StepMs));
            FolderCopyCommand.ReportProgress(context, progress, elapsed);

            // The ticker advances once per elapsed spinner period, cycling through its frame set.
            string spinnerFrame = spinnerFrames[
                (int)(elapsed.TotalMilliseconds / spinnerPeriodMs) % spinnerFrames.Count];
            string spinnerNeedle = $"[{spinnerFrame}]";
            var rows = ExpectedRows(progress, elapsed);

            await WaitForFrameAsync(shell, i, lines =>
                lines.Any(l => l.Contains(spinnerNeedle, StringComparison.Ordinal))
                && rows.All(r => lines.Any(l =>
                    l.Contains(r.Label, StringComparison.Ordinal)
                    && l.Contains(r.Content, StringComparison.Ordinal))));

            onFrame(i, spinnerFrame, shell);
        }

        operationDone.SetResult();
        var result = await task.WaitAsync(StepTimeout);
        if (result.Outcome != ActivityOutcome.Completed)
            throw new InvalidOperationException(
                $"Folder Copy capture: activity ended with {result.Outcome}, expected Completed.");
    }

    // The expected rendered content of each dynamic dialog row for one snapshot, computed through
    // the same FolderCopyCommand helpers ReportProgress feeds the spec with (culture = the shell's).
    // The templates' "{n,5:F1}" alignment padding sits at the start of the value cell and is trimmed
    // away by the layout, so the needles use the plain value preceded by one space — the cell gap
    // that always precedes the value. That leading space anchors the needle to the value start:
    // " 4.0%" cannot match inside " 44.0%", and " 0.0%" cannot match inside " 100.0%".
    private static (string Label, string Content)[] ExpectedRows(CopyProgress p, TimeSpan elapsed) =>
    [
        ("File:", p.CurrentRelativePath),
        ("Current:", string.Format(ShellCulture, " {0:F1}%",
            FolderCopyCommand.Percent(p.CurrentFileBytes, p.CurrentFileLength))),
        ("Files:", string.Format(ShellCulture, " {0:F1}% {1}/{2}",
            FolderCopyCommand.Percent(p.FilesCompleted, p.TotalFiles), p.FilesCompleted, p.TotalFiles)),
        ("Total size:", string.Format(ShellCulture, " {0:F1}% {1:F1}/{2:F1}MB",
            FolderCopyCommand.Percent(p.BytesCopied, p.TotalBytes),
            p.BytesCopied / FolderCopyCommand.BytesPerMb,
            p.TotalBytes / FolderCopyCommand.BytesPerMb)),
        ("Elapsed:",
            $" {FolderCopyCommand.FormatDuration(elapsed)} • ETA {FolderCopyCommand.FormatEta(elapsed, p.BytesCopied, p.TotalBytes)}"),
    ];

    // Re-reads the last rendered frame after each render until it satisfies the step's expectation.
    // Progress is guaranteed: while any expected row value is still undrained the activity state
    // stays dirty (and a pending spinner tick is its own render), so another render always follows a
    // non-matching frame; the timeout turns a broken expectation into a loud failure.
    private static async Task WaitForFrameAsync(TestShell shell, int step, Func<IReadOnlyList<string>, bool> ready)
    {
        int target = Math.Max(1, shell.Terminal.RenderCount);
        while (true)
        {
            try
            {
                await shell.Terminal.WaitForRenderCountAsync(target, StepTimeout);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
            {
                var frame = shell.Terminal.LastRenderedGrid is { } grid
                    ? string.Join('\n', TigerConsole.RenderGridToLines(grid))
                    : "(no frame rendered)";
                throw new InvalidOperationException(
                    $"Folder Copy capture: step {step} never rendered its expected frame. Last frame:\n{frame}");
            }

            var lines = shell.Terminal.LastRenderedGrid is { } rendered
                ? TigerConsole.RenderGridToLines(rendered)
                : [];
            if (lines.Count > 0 && ready(lines))
                return;

            target++;
        }
    }

    private static void Require(bool condition, string what, object? actual)
    {
        if (!condition)
            throw new InvalidOperationException(
                $"Folder Copy capture: unexpected {what} ({actual ?? "<null>"}). "
                + "The scripted run no longer behaves as the artifact describes.");
    }

    // Assembles the captured PNGs into a looping WebP via img2webp: StepMs per frame, with the final
    // (100%) frame held for HoldMs before the loop restarts. img2webp frame options (-d) apply to the
    // frames listed after them, so the duration is switched once before the last frame.
    private static void BuildWebp(string img2Webp, IReadOnlyList<byte[]> pngFrames, string outputPath)
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"tigercli-folder-copy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = img2Webp,
                WorkingDirectory = workDir,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            psi.ArgumentList.Add("-loop");
            psi.ArgumentList.Add("0");
            psi.ArgumentList.Add("-lossless");
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(StepMs.ToString());
            for (int i = 0; i < pngFrames.Count; i++)
            {
                var framePath = Path.Combine(workDir, $"frame-{i:000}.png");
                File.WriteAllBytes(framePath, pngFrames[i]);
                if (i == pngFrames.Count - 1)
                {
                    psi.ArgumentList.Add("-d");
                    psi.ArgumentList.Add(HoldMs.ToString());
                }

                psi.ArgumentList.Add(framePath);
            }

            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(outputPath);

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("img2webp did not start.");
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"img2webp failed with exit code {process.ExitCode} for {Path.GetFileName(outputPath)}:\n{stderr}");
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }
}
