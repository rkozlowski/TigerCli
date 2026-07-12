using System.Diagnostics;
using System.Text;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.PngSink;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Activity;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Themes;

namespace DocSamples;

/// <summary>
/// Generates animated spinner previews under <c>docs/examples/spinners/</c>: one looping
/// <c>.webp</c> per <see cref="SpinnerFrameSet"/> plus a <c>spinners.md</c> index page.
/// <para>Every frame is captured through the real semi-interactive activity path: a scripted
/// <see cref="TestShell"/> with a manual clock runs <see cref="TigerTui.RunActivityAsync"/>, and
/// <see cref="TestShell.AdvanceTime"/> steps the spinner ticker exactly one 500&#160;ms period at a
/// time (the modal loop advances animations on the shell clock and re-renders on each frame
/// change). Each rendered frame becomes a PNG on the shared <see cref="DocTerminal"/> canvas, and
/// <c>img2webp.exe</c> (<c>internal/tools/libwebp/</c>, not committed — see its README) assembles
/// them into a looping animation at the spinner's default 500&#160;ms frame period. The animation
/// therefore shows the same framed terminal window as every static PNG artifact.</para>
/// <para>This is a separate generator mode (<c>dotnet run --project internal/DocSamples --
/// spinners</c>), not part of <see cref="DocExampleSet.GenerateAllAsync"/>: it needs the external
/// libwebp tool and WebP bytes carry encoder-version variance, so the artifacts are regenerated
/// deliberately rather than drift-checked.</para>
/// </summary>
internal static class SpinnerShowcase
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

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

        var outputDirectory = Path.Combine(repoRoot, "docs", "examples", "spinners");
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
            foreach (var frameSet in Enum.GetValues<SpinnerFrameSet>())
            {
                var fileName = $"spinner-{frameSet.ToString().ToLowerInvariant()}.webp";
                var pngFrames = await CaptureFramesAsync(frameSet);
                BuildWebp(img2Webp, pngFrames, Path.Combine(outputDirectory, fileName));
                Console.WriteLine($"wrote docs/examples/spinners/{fileName} ({pngFrames.Count} frames)");

                var frameGlyphs = string.Join(" ", SpinnerTicker.Frames(frameSet).Select(f => $"`{f}`"));
                sections.Append($"## SpinnerFrameSet.{frameSet}\n\n");
                sections.Append($"Frames ({pngFrames.Count}): {frameGlyphs}\n\n");
                sections.Append($"![SpinnerFrameSet.{frameSet}]({fileName})\n\n");
            }

            var page =
                "# Spinner frame sets\n\n"
                + "<!-- GENERATED FILE — do not edit. Regenerate with: "
                + "dotnet run --project internal/DocSamples -- spinners -->\n\n"
                + "Animated previews of every `SpinnerFrameSet`, captured from the real\n"
                + "`TigerTui.RunActivityAsync` dialog on a scripted `TestShell` with a manual clock.\n"
                + "Each spinner frame is rendered to PNG through `PngSink` and the frames are\n"
                + "assembled into a looping WebP at the default 500 ms frame period.\n"
                + "The title bar shows the raw-frame spinner prefix an app with terminal title\n"
                + "management gets on its real window/tab title; the bracketed frame on the dialog's\n"
                + "top border is the activity overlay.\n\n"
                + sections;

            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            File.WriteAllText(Path.Combine(outputDirectory, "spinners.md"), page, utf8NoBom);
            Console.WriteLine("wrote docs/examples/spinners/spinners.md");
            return 0;
        }
        finally
        {
            TigerConsole.CurrentTheme = originalTheme;
            Environment.SetEnvironmentVariable("TIGERCLI_THEME", originalThemeEnv);
        }
    }

    // Runs a real activity dialog whose operation idles on a TaskCompletionSource, and captures one
    // PNG per spinner frame. The manual clock makes this deterministic: the modal loop advances
    // ticker animations on the shell clock, so each AdvanceTime(500 ms) produces exactly one frame
    // change and one re-render — WaitForRenderCountAsync orders the capture after it.
    private static async Task<IReadOnlyList<byte[]>> CaptureFramesAsync(SpinnerFrameSet frameSet)
    {
        var frames = SpinnerTicker.Frames(frameSet);

        var shell = DocTerminal.CreateShell(useManualClock: true);
        var operationDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = TigerTui.RunActivityAsync(
            shell,
            title: null,
            $"Demonstrating SpinnerFrameSet.{frameSet}",
            async (_, ct) => await operationDone.Task.WaitAsync(ct),
            spinner: ActivitySpinnerSpec.FromFrameSet(frameSet));

        var captured = new List<byte[]>(frames.Count);
        await shell.Terminal.WaitForRenderCountAsync(1, StepTimeout);
        captured.Add(CapturePng(shell, frameSet, frames[0]));

        for (int i = 1; i < frames.Count; i++)
        {
            shell.AdvanceTime(SpinnerTicker.DefaultInterval);
            await shell.Terminal.WaitForRenderCountAsync(i + 1, StepTimeout);
            captured.Add(CapturePng(shell, frameSet, frames[i]));
        }

        operationDone.SetResult();
        var result = await task.WaitAsync(StepTimeout);
        if (result.Outcome != ActivityOutcome.Completed)
            throw new InvalidOperationException(
                $"Spinner capture ({frameSet}): activity ended with {result.Outcome}, expected Completed.");

        return captured;
    }

    // Renders the shell's last measured frame to PNG bytes on the documentation terminal canvas,
    // exactly like the static PNG artifacts. Width is fixed by DocTerminal and all frames of one run
    // share the same measured grid instance, so the canvas dimensions are identical across frames —
    // a requirement for animation assembly.
    // The chrome title mirrors what an app with terminal title management shows in the real
    // window/tab title: the raw spinner frame prefixing the base title (TerminalTitleSession
    // composes "{prefix} {title}"; the dialog's rendered title row intentionally carries no
    // spinner — only the top-frame overlay and the window title do). The expected frame is
    // asserted against the rendered overlay, so title and overlay provably show the same frame.
    private static byte[] CapturePng(TestShell shell, SpinnerFrameSet frameSet, string expectedFrame)
    {
        var grid = shell.Terminal.LastRenderedGrid
            ?? throw new InvalidOperationException($"Spinner capture ({frameSet}): no grid was rendered.");
        var lines = TigerConsole.RenderGridToLines(grid);

        if (!lines.Any(l => l.Contains($"[{expectedFrame}]", StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"Spinner capture ({frameSet}): expected overlay frame \"[{expectedFrame}]\" not found in the rendered output.");

        int rows = DocTerminal.EnsureFits($"Spinner capture ({frameSet})", lines);
        var options = DocTerminal.FrameOptions(rows, $"{expectedFrame} SpinnerFrameSet.{frameSet}");
        return PngRenderer.RenderGridToBytes(grid, options);
    }

    // Assembles the captured PNGs into a looping WebP (500 ms per frame, lossless) via img2webp.
    private static void BuildWebp(string img2Webp, IReadOnlyList<byte[]> pngFrames, string outputPath)
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"tigercli-spinner-{Guid.NewGuid():N}");
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
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(((int)SpinnerTicker.DefaultInterval.TotalMilliseconds).ToString());
            psi.ArgumentList.Add("-lossless");
            for (int i = 0; i < pngFrames.Count; i++)
            {
                var framePath = Path.Combine(workDir, $"frame-{i:00}.png");
                File.WriteAllBytes(framePath, pngFrames[i]);
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
