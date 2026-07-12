using System.Text;
using DocSamples;

// DocSamples — regenerates (or checks) the committed documentation artifacts under docs/examples/.
//
// Usage:
//   dotnet run --project internal/DocSamples             # (re)write the artifacts
//   dotnet run --project internal/DocSamples -- check    # exit 1 if committed files are out of date
//   dotnet run --project internal/DocSamples -- spinners # animated spinner previews (needs img2webp;
//                                                        # separate mode, not drift-checked)
//   dotnet run --project internal/DocSamples -- progress-bars # animated progress-bar previews
//                                                        # (needs img2webp; separate mode, not drift-checked)
//   dotnet run --project internal/DocSamples -- folder-copy # animated Folder Copy activity dialog
//                                                        # (needs img2webp; separate mode, not drift-checked)
//   dotnet run --project internal/DocSamples -- api-map  # regenerate docs/reference/api-map.md from
//                                                        # DocFX metadata (assumes `dotnet docfx` has
//                                                        # already been run; `api-map check` for drift)
//   dotnet run --project internal/DocSamples -- themes [name] # ANSI theme-style showcase in the
//                                                        # current console (all built-in themes, or one
//                                                        # by name); the committed HTML pages
//                                                        # (theme-*.html) regenerate with the default run
//
// Output is deterministic (pinned theme, fixed data, LF-only). The drift test in
// ItTiger.TigerCli.Tests runs the `check` mode in a child process: app-run capture artifacts
// depend on process-global state (e.g. registered theme names in --help), so the comparison must
// run in a pristine process — the same definition of truth as the generator itself.
//
// Every artifact — HTML, PNG, and the animated WebPs of the separate modes above — is captured from
// the one documentation terminal defined in DocTerminal.cs: 120 columns, window chrome and title bar.
//
// Curated artifacts are HTML + PNG pairs (docs/design/doc-artifacts.md). Check-mode rules per kind:
//   Text        — newline-normalized, otherwise byte-exact.
//   PngSidecar  — like Text, but the volatile environment lines (generated-os:, dotnet:) are
//                 masked: they record where the committed PNG was generated, which legitimately
//                 differs on other machines.
//   Png         — existence everywhere; bytes only on the canonical generation platform
//                 (Windows 11), because glyph rasterization is platform-backed and PNG output is
//                 not byte-identical across operating systems.

var check = args.Length > 0 && string.Equals(args[0], "check", StringComparison.OrdinalIgnoreCase);
var root = RepoRoot.Locate();

if (args.Length > 0 && string.Equals(args[0], "spinners", StringComparison.OrdinalIgnoreCase))
    return await SpinnerShowcase.GenerateAsync(root);

if (args.Length > 0 && string.Equals(args[0], "progress-bars", StringComparison.OrdinalIgnoreCase))
    return await ProgressBarShowcase.GenerateAsync(root);

if (args.Length > 0 && string.Equals(args[0], "folder-copy", StringComparison.OrdinalIgnoreCase))
    return await FolderCopySamples.GenerateWebpAsync(root);

if (args.Length > 0 && string.Equals(args[0], "themes", StringComparison.OrdinalIgnoreCase))
    return ThemeShowcase.RenderToConsole(args.Length > 1 ? args[1] : null);

if (args.Length > 0 && string.Equals(args[0], "api-map", StringComparison.OrdinalIgnoreCase))
{
    var apiMapCheck = args.Length > 1 && string.Equals(args[1], "check", StringComparison.OrdinalIgnoreCase);
    if (apiMapCheck)
        return DocApiMap.Check(root) ? 0 : 1;

    DocApiMap.Write(root);
    return 0;
}

var outputDirectory = Path.Combine(root, "docs", "examples");
var artifacts = await DocExampleSet.GenerateAllAsync();

if (check)
{
    var failures = 0;
    foreach (var artifact in artifacts)
    {
        var path = Path.Combine(outputDirectory, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"MISSING docs/examples/{artifact.RelativePath}");
            failures++;
            continue;
        }

        switch (artifact.Kind)
        {
            case DocArtifactKind.Text:
            case DocArtifactKind.PngSidecar:
            {
                // Committed files may be checked out with CRLF depending on git settings; compare
                // newline-normalized. The generator itself always writes LF.
                var committed = File.ReadAllText(path).Replace("\r\n", "\n");
                var generated = artifact.Content!;
                if (artifact.Kind == DocArtifactKind.PngSidecar)
                {
                    committed = MaskVolatileSidecarLines(committed);
                    generated = MaskVolatileSidecarLines(generated);
                }

                if (!string.Equals(committed, generated, StringComparison.Ordinal))
                {
                    Console.Error.WriteLine($"DRIFT   docs/examples/{artifact.RelativePath}");
                    failures++;
                }
                else
                {
                    Console.WriteLine($"ok      docs/examples/{artifact.RelativePath}");
                }

                break;
            }

            case DocArtifactKind.Png:
            {
                if (!PngCompanion.IsCanonicalPlatform)
                {
                    Console.WriteLine(
                        $"ok      docs/examples/{artifact.RelativePath} (exists; byte check skipped: not on canonical {PngCompanion.CanonicalOs})");
                    break;
                }

                var committedBytes = File.ReadAllBytes(path);
                if (!committedBytes.AsSpan().SequenceEqual(artifact.Bytes!))
                {
                    Console.Error.WriteLine($"DRIFT   docs/examples/{artifact.RelativePath} (PNG bytes)");
                    failures++;
                }
                else
                {
                    Console.WriteLine($"ok      docs/examples/{artifact.RelativePath}");
                }

                break;
            }

            default:
                throw new InvalidOperationException($"Unhandled artifact kind: {artifact.Kind}");
        }
    }

    if (failures > 0)
    {
        Console.Error.WriteLine(
            $"{failures} artifact(s) missing or out of date. Regenerate with: dotnet run --project internal/DocSamples");
        return 1;
    }

    return 0;
}

var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
foreach (var artifact in artifacts)
{
    var path = Path.Combine(outputDirectory, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    if (artifact.Kind == DocArtifactKind.Png)
        File.WriteAllBytes(path, artifact.Bytes!);
    else
        File.WriteAllText(path, artifact.Content!, utf8NoBom);
    Console.WriteLine($"wrote docs/examples/{artifact.RelativePath}");
}

return 0;

// The sidecar records where the committed PNG was actually generated; those lines are
// environment-dependent by design and must not fail the check on other machines.
static string MaskVolatileSidecarLines(string sidecar)
{
    var lines = sidecar.Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        if (lines[i].StartsWith("generated-os:", StringComparison.Ordinal))
            lines[i] = "generated-os: <masked>";
        else if (lines[i].StartsWith("dotnet:", StringComparison.Ordinal))
            lines[i] = "dotnet: <masked>";
    }

    return string.Join('\n', lines);
}
