using System.Diagnostics;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Guards the committed documentation artifacts under <c>docs/examples/</c> against drift, per
/// <c>docs/design/doc-artifacts.md</c>: the DocSamples generator's <c>check</c> mode regenerates
/// every artifact and compares it to the committed file, failing until the artifacts are
/// regenerated with <c>dotnet run --project internal/DocSamples</c>.
/// <para>The check runs in a <b>child process</b>, not in-process: app-run capture artifacts embed
/// process-global state (the <c>--help</c> theme list reflects <c>TigerConsole</c> theme
/// registrations, which other tests in this suite add to), so regenerating inside the shared test
/// process would be test-order-dependent. A pristine generator process is the definition of truth.
/// The generator binaries are copied into this project's output by the DocSamples project
/// reference, so the spawn needs no extra build.</para>
/// </summary>
public sealed class DocExamplesDriftTests
{
    [Fact]
    public void CommittedArtifacts_MatchGeneratorCheck()
    {
        var generatorDll = Path.Combine(AppContext.BaseDirectory, "DocSamples.dll");
        Assert.True(File.Exists(generatorDll), $"DocSamples.dll not found beside the tests: {generatorDll}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(generatorDll);
        startInfo.ArgumentList.Add("check");

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(120_000), "DocSamples check timed out after 120s.");

        Assert.True(
            process.ExitCode == 0,
            "docs/examples artifacts are missing or out of date. "
            + "Regenerate with: dotnet run --project internal/DocSamples\n"
            + $"--- check stdout ---\n{stdout}\n--- check stderr ---\n{stderr}");
    }
}
