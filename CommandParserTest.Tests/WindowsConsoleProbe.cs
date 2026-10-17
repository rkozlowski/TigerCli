using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace CommandParserTest.Tests;

// Starts WindowsConsoleProbe.ps1 in a real PowerShell host and returns the JSON it wrote. Each host
// gets its own hidden console (CreateNoWindow) and keeps its standard handles on that console, so the
// app the probe starts writes to a genuine console and the code pages the probe sets cannot leak
// between runs or into the test runner. One probe run per host and language mode serves every test.
internal static class WindowsConsoleProbe
{
    private static readonly TimeSpan HostTimeout = TimeSpan.FromSeconds(120);
    private static readonly ConcurrentDictionary<(string Host, bool Constrained), Lazy<Task<JsonElement>>> Runs = new();

    // Windows PowerShell 5.1 ships with every Windows; PowerShell 7+ is tested when pwsh is on PATH.
    // Other platforms have no Windows console contract to check, so they get no test data.
    public static TheoryData<string> Hosts()
    {
        var hosts = new TheoryData<string>();
        if (!OperatingSystem.IsWindows())
            return hosts;

        hosts.Add("powershell.exe");
        if (FindOnPath("pwsh.exe") != null)
            hosts.Add("pwsh.exe");
        return hosts;
    }

    public static Task<JsonElement> RunAsync(string host, bool constrained) =>
        Runs.GetOrAdd((host, constrained), key => new Lazy<Task<JsonElement>>(() => StartAsync(key.Host, key.Constrained))).Value;

    public sealed record CompletionMatch(string Text, string Type, string ToolTip);

    public static IReadOnlyList<CompletionMatch> ReadMatches(JsonElement tab)
    {
        var matches = tab.GetProperty("Matches");
        // Windows PowerShell serializes a one-element array as a bare object and an empty one as {}.
        var items = matches.ValueKind == JsonValueKind.Array ? matches.EnumerateArray().ToArray() : [matches];
        return items
            .Where(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("Text", out _))
            .Select(item => new CompletionMatch(
                item.GetProperty("Text").GetString() ?? string.Empty,
                item.GetProperty("Type").GetString() ?? string.Empty,
                item.GetProperty("ToolTip").GetString() ?? string.Empty))
            .ToArray();
    }

    private static async Task<JsonElement> StartAsync(string host, bool constrained)
    {
        var binDir = AppContext.BaseDirectory;
        Assert.True(File.Exists(Path.Combine(binDir, "parser-test.exe")), "parser-test.exe was not copied to the test output.");

        var resultPath = Path.Combine(Path.GetTempPath(), $"tigercli-console-probe-{Guid.NewGuid():N}.json");
        var startInfo = new ProcessStartInfo(host)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = binDir
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(binDir, "WindowsConsoleProbe.ps1"), "-BinDir", binDir, "-ResultPath", resultPath })
            startInfo.ArgumentList.Add(argument);
        if (constrained)
            startInfo.ArgumentList.Add("-Constrained");

        try
        {
            using var process = Process.Start(startInfo)!;
            using var timeout = new CancellationTokenSource(HostTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail($"{host} did not finish the console probe within {HostTimeout}.");
            }

            Assert.True(File.Exists(resultPath), $"{host} probe wrote no result (exit {process.ExitCode}).");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
            var result = document.RootElement.Clone();
            if (result.TryGetProperty("ProbeError", out var probeError))
                Assert.Fail($"{host} probe failed: {probeError.GetString()}");
            Assert.Equal(0, process.ExitCode);
            return result;
        }
        finally
        {
            File.Delete(resultPath);
        }
    }

    private static string? FindOnPath(string fileName) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, fileName))
            .FirstOrDefault(File.Exists);
}
