using System.Diagnostics;
using System.Text.Json;

namespace CommandParserTest.Tests;

// These tests run the generated PowerShell completer for the real parser-test executable inside real
// PowerShell hosts: Windows PowerShell 5.1, which every Windows machine has, and PowerShell 7+ when
// pwsh is on PATH. PowerShellCompletionProbe.ps1 presses Tab through TabExpansion2 and records what
// it saw; the assertions here are the completer's session contract. Each host gets its own hidden
// console, so the console code page the probe sets cannot leak between runs or into the test runner.
// Other platforms have no Windows PowerShell console contract to check, so they get no test data.
public sealed class PowerShellCompletionTests
{
    private const string PolishHelpDescription = "Wyświetl pomoc";
    private static readonly TimeSpan HostTimeout = TimeSpan.FromSeconds(90);

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

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Hosts))]
    public async Task FullLanguage_CompletesWithDescriptionsAndLeavesSessionUnchanged(string host)
    {
        var result = await RunProbeAsync(host, constrained: false);

        Assert.Equal("FullLanguage", result.GetProperty("LanguageMode").GetString());
        foreach (var tab in result.GetProperty("Tabs").EnumerateArray())
        {
            Assert.Equal(7, tab.GetProperty("LastExitCode").GetInt32());
            Assert.Equal(0, tab.GetProperty("ErrorsAdded").GetInt32());
            Assert.Equal(852, tab.GetProperty("ConsoleCodePage").GetInt32());
        }

        var echo = Assert.Single(Matches(result, 0), match => match.Text == "echo");
        Assert.Equal("ParameterValue", echo.Type);

        // The Polish description has non-ASCII letters that an OEM console decodes wrongly unless
        // the completer reads the app's answer as UTF-8.
        var help = Assert.Single(Matches(result, 1), match => match.Text == "--help");
        Assert.Equal(PolishHelpDescription, help.ToolTip);
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Hosts))]
    public async Task ConstrainedLanguage_CompletesWithPlainCandidates(string host)
    {
        var result = await RunProbeAsync(host, constrained: true);

        Assert.Equal("ConstrainedLanguage", result.GetProperty("LanguageMode").GetString());
        foreach (var tab in result.GetProperty("Tabs").EnumerateArray())
        {
            Assert.Equal(7, tab.GetProperty("LastExitCode").GetInt32());
            Assert.Equal(0, tab.GetProperty("ErrorsAdded").GetInt32());
        }

        var echo = Assert.Single(Matches(result, 0), match => match.Text == "echo");
        Assert.Equal("Text", echo.Type);
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Hosts))]
    public async Task UninstalledExecutable_CompletesNothingAndRecordsNoError(string host)
    {
        var result = await RunProbeAsync(host, constrained: false);

        var uninstalled = result.GetProperty("Uninstalled");
        Assert.Equal(7, uninstalled.GetProperty("LastExitCode").GetInt32());
        Assert.Equal(0, uninstalled.GetProperty("ErrorsAdded").GetInt32());
        Assert.DoesNotContain(ReadMatches(uninstalled), match => match.Text == "echo");
    }

    private sealed record CompletionMatch(string Text, string Type, string ToolTip);

    private static IReadOnlyList<CompletionMatch> Matches(JsonElement result, int tabIndex) =>
        ReadMatches(result.GetProperty("Tabs")[tabIndex]);

    private static IReadOnlyList<CompletionMatch> ReadMatches(JsonElement tab)
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

    private static async Task<JsonElement> RunProbeAsync(string host, bool constrained)
    {
        var binDir = AppContext.BaseDirectory;
        Assert.True(File.Exists(Path.Combine(binDir, "parser-test.exe")), "parser-test.exe was not copied to the test output.");

        var resultPath = Path.Combine(Path.GetTempPath(), $"tigercli-pwsh-completion-{Guid.NewGuid():N}.json");
        var startInfo = new ProcessStartInfo(host)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = binDir
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(binDir, "PowerShellCompletionProbe.ps1"), "-BinDir", binDir, "-ResultPath", resultPath })
            startInfo.ArgumentList.Add(argument);
        if (constrained)
            startInfo.ArgumentList.Add("-Constrained");

        try
        {
            using var process = Process.Start(startInfo)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(HostTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail($"{host} did not finish the completion probe within {HostTimeout}.");
            }

            Assert.True(
                process.ExitCode == 0 && File.Exists(resultPath),
                $"{host} probe failed (exit {process.ExitCode}).{Environment.NewLine}{await stdout}{await stderr}");

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
            return document.RootElement.Clone();
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
