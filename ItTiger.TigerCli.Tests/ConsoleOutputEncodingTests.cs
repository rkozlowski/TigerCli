using System.Text;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

// Guards for TigerCli's standard-output encoding contract that can run inside the test process. The
// half that needs a fresh process (a runtime-created writer is recognized and redirected output is
// UTF-8) is covered by CommandParserTest.Tests/RedirectedOutputEncodingTests.cs, and real consoles by
// CommandParserTest.Tests/ConsoleOutputEncodingTests.cs.
public sealed class ConsoleOutputEncodingTests
{
    // The runtime behavior PlatformStandardWriters depends on: assigning Console.OutputEncoding leaves
    // writers installed with SetOut/SetError in place. If a .NET upgrade breaks this, TigerCli could
    // replace an app's capture, so the probe must keep reporting installed writers as installed.
    [Fact]
    public void OwnershipProbe_ReportsInstalledWritersAsInstalledAndKeepsThem()
    {
        WithInstalledWriters((stdout, stderr) =>
        {
            var ownership = PlatformStandardWriters.SwitchToUnicode();

            Assert.False(ownership.OutIsRuntimeWriter);
            Assert.False(ownership.ErrorIsRuntimeWriter);
            Console.Out.Write("out");
            Console.Error.Write("err");
            Assert.Equal("out", stdout.ToString());
            Assert.Equal("err", stderr.ToString());
        });
    }

    [Fact]
    public void Contract_NeverReplacesInstalledWritersOfRedirectedStreams()
    {
        WithInstalledWriters((stdout, stderr) =>
        {
            ConsoleOutputEncoding.ApplyOnWindows(isOutputRedirected: true, isErrorRedirected: true);

            Console.Out.Write("Łódź");
            Console.Error.Write("€");
            Assert.Equal("Łódź", stdout.ToString());
            Assert.Equal("€", stderr.ToString());
        });
    }

    [Fact]
    public void RedirectedStreamWriter_WritesUtf8WithoutByteOrderMarkAndFlushesEachWrite()
    {
        using var stream = new MemoryStream();
        var writer = PlatformStandardWriters.CreateWriter(stream, Encoding.UTF8);

        writer.Write("Łódź €");

        Assert.Equal(Encoding.UTF8.GetBytes("Łódź €"), stream.ToArray());
    }

    [Fact]
    public void OutputEncoding_DefaultsToUtf8WithoutAnAppChoice()
    {
        Assert.Equal(65001, TigerConsole.OutputEncoding.CodePage);
        Assert.False(TigerConsole.IsOutputEncodingAppChosen);
    }

    // Installs capture writers, runs the check, and restores the process's writers and encoding.
    private static void WithInstalledWriters(Action<StringWriter, StringWriter> check)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalEncoding = Console.OutputEncoding;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            check(stdout, stderr);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Console.OutputEncoding = originalEncoding;
        }
    }
}
