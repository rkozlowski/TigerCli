using System.Diagnostics;
using System.Text;

namespace CommandParserTest.Tests;

// Redirected output of the real parser-test process is UTF-8, whatever the console code page. On
// Windows the child gets a hidden console with the system's OEM code page, and none of the legacy OEM
// code pages has '€', '╭', '✓', CJK, or characters outside the BMP, so exact UTF-8 bytes prove TigerCli
// did not fall back to that code page.
// These tests are also the guard for PlatformStandardWriters: if a runtime upgrade stopped .NET
// recreating its own writers when Console.OutputEncoding is assigned, TigerCli would fall back to the
// console code page and these assertions would fail.
public sealed class RedirectedOutputEncodingTests
{
    private const string BeyondLegacyCodePages = "€ ╭─╮ ✓ 東京 😀";

    [Fact]
    public async Task Stdout_KeepsCharactersNoLegacyCodePageHas()
    {
        var run = await RunAsync("echo", "-m", BeyondLegacyCodePages);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(BeyondLegacyCodePages + Environment.NewLine, Utf8(run.Stdout));
    }

    [Fact]
    public async Task Stdout_IsUtf8WithoutByteOrderMark()
    {
        var run = await RunAsync("--city", "Łódź");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("Hello, Łódź!" + Environment.NewLine, Utf8(run.Stdout));
        Assert.False(run.Stdout.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()), "Redirected stdout starts with a byte-order mark.");
    }

    [Fact]
    public async Task Stderr_IsUtf8()
    {
        var run = await RunAsync("--culture", "pl-PL", "--bogus");

        Assert.NotEqual(0, run.ExitCode);
        Assert.StartsWith("Błąd: Nieznana opcja: '--bogus'", Utf8(run.Stderr));
    }

    // Bash, Zsh, and Fish read completion answers as UTF-8.
    [Fact]
    public async Task BashCompletionAnswer_IsUtf8()
    {
        var run = await RunAsync("--tigercli-complete", "bash", "_--city", "_");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("Kraków\nŁódź\nGdańsk\n:none\n", Utf8(run.Stdout));
    }

    private sealed record Run(int ExitCode, byte[] Stdout, byte[] Stderr);

    private static string Utf8(byte[] bytes) => new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);

    private static async Task<Run> RunAsync(params string[] arguments)
    {
        var executable = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "parser-test.exe" : "parser-test");
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await Task.WhenAll(
            process.StandardOutput.BaseStream.CopyToAsync(stdout, timeout.Token),
            process.StandardError.BaseStream.CopyToAsync(stderr, timeout.Token),
            process.WaitForExitAsync(timeout.Token));
        return new Run(process.ExitCode, stdout.ToArray(), stderr.ToArray());
    }
}
