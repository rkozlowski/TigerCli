using System.Diagnostics;
using System.Globalization;
using System.Text;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.PngSink;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Activity;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Themes;
using ActivityContext = ItTiger.TigerCli.Tui.Activity.ActivityContext;

namespace DocSamples;

/// <summary>
/// Generates animated progress-bar previews under <c>docs/examples/progress-bars/</c>: one looping
/// <c>.webp</c> per (<see cref="ProgressBarStyle"/> × <see cref="ProgressBarCaps"/> ×
/// <see cref="ProgressBarColorMode"/>) combination plus a <c>progress-bars.md</c> index page. The
/// colour modes shown are <see cref="ProgressBarColorMode.Single"/> and
/// <see cref="ProgressBarColorMode.ThreeColor"/>; <c>TwoColor</c> is <c>ThreeColor</c> without the
/// distinct 100% recolour, so it adds no frame the three-colour animation doesn't already show.
/// <para>Every frame is captured through the real semi-interactive activity path: a scripted
/// <see cref="TestShell"/> with a manual clock runs <see cref="TigerTui.RunActivityAsync"/> with a
/// progress-bar <see cref="ActivityDialogSpec"/>, and the idle operation's <see cref="ActivityContext"/>
/// steps the bar 1% at a time (each <c>SetValue</c> is drained by the modal loop into exactly one
/// re-render). The shell clock also advances 100&#160;ms of virtual time per step, so the dialog's
/// top-frame spinner animates at its authentic 500&#160;ms period relative to the bar. Each rendered
/// frame becomes a PNG on the shared <see cref="DocTerminal"/> canvas, and <c>img2webp.exe</c>
/// (<c>internal/tools/libwebp/</c>, not committed — see its README) assembles them into a looping
/// animation: 100&#160;ms per 1% step, with the 100% frame held for 2&#160;seconds before the loop
/// restarts. The animation therefore shows the same framed terminal window as every static PNG
/// artifact.</para>
/// <para>This is a separate generator mode (<c>dotnet run --project internal/DocSamples --
/// progress-bars</c>), not part of <see cref="DocExampleSet.GenerateAllAsync"/>: it needs the external
/// libwebp tool and WebP bytes carry encoder-version variance, so the artifacts are regenerated
/// deliberately rather than drift-checked.</para>
/// </summary>
internal static class ProgressBarShowcase
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Virtual time per 1% step; also the per-frame WebP duration.</summary>
    private const int StepMs = 100;

    /// <summary>How long the finished (100%) frame is held before the animation loops.</summary>
    private const int HoldMs = 2000;

    // The colour modes the showcase demonstrates (see the class remarks for why TwoColor is omitted).
    private static readonly ProgressBarColorMode[] ColorModes =
    [
        ProgressBarColorMode.Single,
        ProgressBarColorMode.ThreeColor,
    ];

    public static async Task<int> GenerateAsync(string repoRoot)
    {
        var img2Webp = Path.Combine(repoRoot, "internal", "tools", "libwebp", "img2webp.exe");
        if (!File.Exists(img2Webp))
        {
            Console.Error.WriteLine(
                $"img2webp.exe not found at {img2Webp}. "
                + "See internal/tools/libwebp/README.md for how to fetch it.");
            return 1;
        }

        var outputDirectory = Path.Combine(repoRoot, "docs", "examples", "progress-bars");
        Directory.CreateDirectory(outputDirectory);

        // Pin the theme exactly like DocExampleSet.GenerateAllAsync: TestShell resolves its theme
        // from TigerConsole.CurrentTheme at construction.
        var originalTheme = TigerConsole.CurrentTheme;
        var originalThemeEnv = Environment.GetEnvironmentVariable("TIGERCLI_THEME");
        try
        {
            TigerConsole.CurrentTheme = new TigerBlueTheme();
            Environment.SetEnvironmentVariable("TIGERCLI_THEME", null);

            var sections = new StringBuilder();
            foreach (var style in Enum.GetValues<ProgressBarStyle>())
            {
                sections.Append($"## ProgressBarStyle.{style}\n\n");
                foreach (var caps in Enum.GetValues<ProgressBarCaps>())
                {
                    foreach (var colorMode in ColorModes)
                    {
                        var fileName = FileName(style, caps, colorMode);
                        var pngFrames = await CaptureFramesAsync(style, caps, colorMode);
                        BuildWebp(img2Webp, pngFrames, Path.Combine(outputDirectory, fileName));
                        Console.WriteLine(
                            $"wrote docs/examples/progress-bars/{fileName} ({pngFrames.Count} frames)");

                        sections.Append($"### ProgressBarCaps.{caps} · ProgressBarColorMode.{colorMode}\n\n");
                        sections.Append(
                            $"![ProgressBarStyle.{style}, ProgressBarCaps.{caps}, ProgressBarColorMode.{colorMode}]({fileName})\n\n");
                    }
                }
            }

            var page =
                "# Progress bar styles\n\n"
                + "<!-- GENERATED FILE — do not edit. Regenerate with: "
                + "dotnet run --project internal/DocSamples -- progress-bars -->\n\n"
                + "Animated previews of every `ProgressBarStyle` × `ProgressBarCaps` combination in the\n"
                + "`Single` and `ThreeColor` colour modes, captured from the real `TigerTui.RunActivityAsync`\n"
                + "dialog on a scripted `TestShell` with a manual clock (`TwoColor` is `ThreeColor` without\n"
                + "the distinct 100% recolour, so it is not shown separately). The bar sits in a star-sized\n"
                + "column with a right-aligned percentage cell; the operation steps progress from 0% to 100%\n"
                + "in 1% increments. Each rendered frame becomes a PNG through `PngSink` and the frames are\n"
                + "assembled into a looping WebP: 100 ms per step, with the 100% frame held for 2 seconds\n"
                + "before the loop restarts. In `ThreeColor` mode note the bar recolouring at exactly 100%.\n"
                + "The title bar shows the raw-frame spinner prefix an app with terminal title management\n"
                + "gets on its real window/tab title; the bracketed frame on the dialog's top border is the\n"
                + "activity overlay.\n\n"
                + sections;

            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            File.WriteAllText(Path.Combine(outputDirectory, "progress-bars.md"), page, utf8NoBom);
            Console.WriteLine("wrote docs/examples/progress-bars/progress-bars.md");
            return 0;
        }
        finally
        {
            TigerConsole.CurrentTheme = originalTheme;
            Environment.SetEnvironmentVariable("TIGERCLI_THEME", originalThemeEnv);
        }
    }

    private static string FileName(ProgressBarStyle style, ProgressBarCaps caps, ProgressBarColorMode colorMode) =>
        "progress-"
        + $"{style.ToString().ToLowerInvariant()}-"
        + $"{caps.ToString().ToLowerInvariant()}-"
        + $"{colorMode.ToString().ToLowerInvariant()}.webp";

    // Runs a real activity dialog whose operation idles on a TaskCompletionSource, and captures one PNG
    // per 1% progress step (101 frames, 0–100%). Determinism comes from strict sequencing: the modal
    // loop drains each ActivityContext.SetValue into exactly one re-render, awaited via the terminal's
    // render count before the next step. The manual clock additionally advances 100 ms of virtual time
    // per step so the top-frame spinner ticks once per SpinnerTicker period (a spinner frame change is
    // one extra awaited render), keeping the dialog's own animation authentic in the capture.
    private static async Task<IReadOnlyList<byte[]>> CaptureFramesAsync(
        ProgressBarStyle style, ProgressBarCaps caps, ProgressBarColorMode colorMode)
    {
        var shell = DocTerminal.CreateShell(useManualClock: true);
        var operationDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contextReady = new TaskCompletionSource<ActivityContext>(TaskCreationOptions.RunContinuationsAsynchronously);

        // The bar fills a star column; the fixed trailing column shows the bound percentage so every
        // frame is self-describing (and gives the capture a needle to validate against).
        var spec = ActivityDialogSpec.Create()
            .AddColumn(sizing: CliColumnSizing.Star)
            .AddColumn(width: 5, align: CliTextAlignment.Right)
            .AddRow("p", r => r
                .Cell(0).ProgressBar(valueIndex: 0, maxValue: 100, style: style, caps: caps, colorMode: colorMode)
                .Cell(1).Text("{0,3:F0}%")
                .Values(0.0))
            .Build();

        var task = TigerTui.RunActivityAsync(
            shell,
            title: null,
            spec,
            async (context, ct) =>
            {
                contextReady.SetResult(context);
                await operationDone.Task.WaitAsync(ct);
            });

        string comboTitle = $"{style} · {caps} · {colorMode}";
        int spinnerPeriodMs = (int)SpinnerTicker.DefaultInterval.TotalMilliseconds;
        var spinnerFrames = SpinnerTicker.Frames(SpinnerFrameSet.Default);
        var captured = new List<byte[]>(101);
        int? canvasRows = null;

        int renderTarget = 1;
        await shell.Terminal.WaitForRenderCountAsync(renderTarget, StepTimeout);
        var context = await contextReady.Task.WaitAsync(StepTimeout);
        captured.Add(CapturePng(shell, comboTitle, percent: 0, spinnerFrames[0], ref canvasRows));

        for (int percent = 1; percent <= 100; percent++)
        {
            // Advance virtual time first; when the accumulated time crosses a spinner period the ticker
            // changes frame, which is its own awaited render (StepMs divides the period, so the
            // crossing is exact and the render deterministic).
            shell.AdvanceTime(TimeSpan.FromMilliseconds(StepMs));
            if (percent * StepMs % spinnerPeriodMs == 0)
            {
                renderTarget++;
                await shell.Terminal.WaitForRenderCountAsync(renderTarget, StepTimeout);
            }

            context.SetValue("p", 0, (double)percent);
            renderTarget++;
            await shell.Terminal.WaitForRenderCountAsync(renderTarget, StepTimeout);

            // The ticker has advanced once per elapsed spinner period, cycling through its frame set;
            // the capture asserts this expectation against the rendered overlay.
            string expectedFrame = spinnerFrames[percent * StepMs / spinnerPeriodMs % spinnerFrames.Count];
            captured.Add(CapturePng(shell, comboTitle, percent, expectedFrame, ref canvasRows));
        }

        operationDone.SetResult();
        var result = await task.WaitAsync(StepTimeout);
        if (result.Outcome != ActivityOutcome.Completed)
            throw new InvalidOperationException(
                $"Progress capture ({comboTitle}): activity ended with {result.Outcome}, expected Completed.");

        return captured;
    }

    // Renders the shell's last measured frame to PNG bytes on the documentation terminal canvas, like
    // the spinner showcase and the static PNG artifacts.
    // The chrome title mirrors what an app with terminal title management shows in the real window/tab
    // title: the raw spinner frame prefixing the base title (TerminalTitleSession composes
    // "{prefix} {title}"). The expected frame is asserted against the rendered bracketed overlay, so
    // title and overlay provably show the same frame. The rendered percentage cell is likewise asserted
    // against the step's expected value ("{0,3:F0}%" pads to a fixed width, so the needle is
    // unambiguous — "  1%" never matches inside " 21%"), proving the captured frame is the one the step
    // produced. Width is fixed by DocTerminal; the row count must also stay constant across a run — a
    // requirement for animation assembly — so the first frame pins it.
    private static byte[] CapturePng(
        TestShell shell, string comboTitle, int percent, string expectedFrame, ref int? canvasRows)
    {
        var grid = shell.Terminal.LastRenderedGrid
            ?? throw new InvalidOperationException($"Progress capture ({comboTitle}): no grid was rendered.");
        var lines = TigerConsole.RenderGridToLines(grid);

        if (!lines.Any(l => l.Contains($"[{expectedFrame}]", StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"Progress capture ({comboTitle}): expected overlay frame \"[{expectedFrame}]\" at {percent}% "
                + "not found in the rendered output:\n" + string.Join('\n', lines));

        var needle = string.Format(CultureInfo.InvariantCulture, "{0,3:F0}%", percent);
        if (!lines.Any(l => l.Contains(needle, StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"Progress capture ({comboTitle}): expected \"{needle}\" not found in the rendered output:\n"
                + string.Join('\n', lines));

        int rows = DocTerminal.EnsureFits($"Progress capture ({comboTitle})", lines);
        canvasRows ??= rows;
        if (canvasRows.Value != rows)
            throw new InvalidOperationException(
                $"Progress capture ({comboTitle}): frame at {percent}% is {rows} rows tall, "
                + $"but the run started at {canvasRows.Value} rows.");

        var options = DocTerminal.FrameOptions(rows, $"{expectedFrame} {comboTitle}");
        return PngRenderer.RenderGridToBytes(grid, options);
    }

    // Assembles the captured PNGs into a looping WebP via img2webp: StepMs per frame, with the final
    // (100%) frame held for HoldMs before the loop restarts. img2webp frame options (-d) apply to the
    // frames listed after them, so the duration is switched once before the last frame.
    private static void BuildWebp(string img2Webp, IReadOnlyList<byte[]> pngFrames, string outputPath)
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"tigercli-progress-{Guid.NewGuid():N}");
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
