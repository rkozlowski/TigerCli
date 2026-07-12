using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using ItTiger.TigerCli.PngSink;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace DocSamples;

/// <summary>
/// Builds the PNG companion of a curated documentation artifact, per
/// <c>docs/design/doc-artifacts.md</c>: managed artifacts are generated as HTML + PNG pairs.
/// HTML is the inspectable/diffable truth; the PNG is the embeddable visual rendered through
/// <c>PngSink</c> from the same measured grid. The canvas is always the shared documentation
/// terminal (<see cref="DocTerminal"/>); height derives from the rendered content, and content
/// wider than the canvas fails loudly (<c>PngOverflowMode.Throw</c> remains the sink-level
/// backstop) — nothing is clipped silently.
/// <para>Every PNG carries a <c>.png.txt</c> sidecar recording the generation environment and the
/// visible text. The check mode in <c>Program.cs</c> compares sidecars exactly apart from the
/// volatile <c>generated-os:</c>/<c>dotnet:</c> lines, and compares PNG bytes only on the
/// canonical generation platform (<see cref="CanonicalOs"/>).</para>
/// </summary>
internal static class PngCompanion
{
    public const string CanonicalOs = "Windows 11";

    /// <summary>Whether this process runs on the canonical PNG generation platform
    /// (Windows 11 — build 22000+). Only there are committed PNG bytes authoritative.</summary>
    public static bool IsCanonicalPlatform => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    /// <summary>Measures an unmeasured grid at the documentation terminal width, the same way the
    /// HTML artifacts pin width (a sink-reported SoftMaxWidth). No-op if already measured.</summary>
    public static void MeasureAt(CliGrid grid)
    {
        if (grid.IsMeasured)
            return;
        _ = TigerConsole.RenderGridToHtml(grid, DocTerminal.Html());
    }

    /// <summary>Measures an unmeasured grid at the documentation terminal width and returns its
    /// PNG + sidecar pair.</summary>
    public static IReadOnlyList<DocArtifact> FromGrid(string name, CliGrid grid, string? title = null)
    {
        MeasureAt(grid);
        return FromMeasuredGrid(name, grid, title);
    }

    /// <summary>
    /// Renders an already-measured grid (e.g. a TestShell frame, or a grid the paired HTML render
    /// just measured) to <c>png/&lt;name&gt;.png</c> plus its <c>.png.txt</c> sidecar, on the
    /// shared documentation terminal canvas.
    /// </summary>
    public static IReadOnlyList<DocArtifact> FromMeasuredGrid(string name, CliGrid grid, string? title = null)
    {
        if (!grid.IsMeasured)
            throw new InvalidOperationException(
                $"PNG artifact '{name}': the grid must be measured first (render its HTML pair or call {nameof(MeasureAt)}).");

        // Same measured layout as the paired HTML: RenderGrid never re-measures a measured grid.
        var lines = TigerConsole.RenderGridToLines(grid);
        int rows = DocTerminal.EnsureFits($"PNG artifact '{name}'", lines);
        var options = DocTerminal.FrameOptions(rows, title ?? name);

        var bytes = PngRenderer.RenderGridToBytes(grid, options);
        return
        [
            DocArtifact.Png($"png/{name}.png", bytes),
            DocArtifact.PngSidecar($"png/{name}.png.txt", BuildSidecar($"{name}.png", options, lines)),
        ];
    }

    // Sidecar layout: a fixed metadata block, then the visible text under a divider. Visible-text
    // lines are TrimEnd'ed (committed files must not carry trailing whitespace); the raw padded
    // width is still recorded via "columns:". LF-only, like every text artifact.
    private static string BuildSidecar(string artifactFileName, PngSinkOptions options, IReadOnlyList<string> lines)
    {
        var sb = new StringBuilder();
        sb.Append("artifact: ").Append(artifactFileName).Append('\n');
        sb.Append("canonical-os: ").Append(CanonicalOs).Append('\n');
        sb.Append("generated-os: ").Append(RuntimeInformation.OSDescription.Trim()).Append('\n');
        sb.Append("dotnet: ").Append(RuntimeInformation.FrameworkDescription.Trim()).Append('\n');
        sb.Append("skiasharp: ").Append(SkiaSharpVersion).Append('\n');
        sb.Append("terminal-font: ").Append(options.TerminalFont.DisplayName).Append('\n');
        sb.Append("title-font: ").Append(options.TitleFont.DisplayName).Append('\n');
        sb.Append("columns: ").Append(options.Columns).Append('\n');
        sb.Append("rows: ").Append(options.Rows).Append('\n');
        sb.Append("chrome: ").Append(options.Chrome).Append('\n');
        sb.Append("title: ").Append(options.Title ?? "(none)").Append('\n');
        sb.Append("--- visible text ---\n");
        foreach (var line in lines)
            sb.Append(line.TrimEnd()).Append('\n');
        return sb.ToString();
    }

    // The NuGet package version (informational version without build metadata), falling back to
    // the assembly version. Pinned by the PngSink csproj, so it is deterministic per repo state —
    // bumping SkiaSharp intentionally drifts every sidecar, forcing PNG regeneration.
    private static readonly string SkiaSharpVersion = GetSkiaSharpVersion();

    private static string GetSkiaSharpVersion()
    {
        var assembly = typeof(SkiaSharp.SKBitmap).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            int metadata = informational.IndexOf('+');
            return metadata >= 0 ? informational[..metadata] : informational;
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
