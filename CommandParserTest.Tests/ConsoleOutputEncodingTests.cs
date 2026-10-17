using static CommandParserTest.Tests.WindowsConsoleProbe;

namespace CommandParserTest.Tests;

// Normal execution of the real parser-test executable under an OEM console code page, observed by
// WindowsConsoleProbe inside real Windows PowerShell 5.1 and PowerShell 7+ hosts. TigerCli must render
// non-ASCII text correctly without changing the console code page the app shares with the shell.
public sealed class ConsoleOutputEncodingTests
{
    private const string PolishGreeting = "Witaj, Świecie!";
    private const string PolishUnknownOption = "Błąd: Nieznana opcja: '--bogus'";
    private const string PolishHelpDescription = "Wyświetl pomoc";

    // Code page 437 has neither 'Ś' nor 'ł', so they reach the screen only when written as Unicode.
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Hosts), MemberType = typeof(WindowsConsoleProbe))]
    public async Task Interactive_RendersNonAsciiOutputAndErrorsWithoutChangingTheCodePage(string host)
    {
        var interactive = (await RunAsync(host, constrained: false)).GetProperty("Interactive");
        Assert.True(interactive.GetProperty("ProbeOnConsole").GetBoolean(), "The probe's standard handles are not on its console.");

        var output = interactive.GetProperty("Output");
        Assert.Equal(0, output.GetProperty("ExitCode").GetInt32());
        Assert.Equal(PolishGreeting, output.GetProperty("Screen").GetString());
        Assert.Equal(437, output.GetProperty("ConsoleCodePage").GetInt32());

        var error = interactive.GetProperty("Error");
        Assert.NotEqual(0, error.GetProperty("ExitCode").GetInt32());
        Assert.StartsWith(PolishUnknownOption, error.GetProperty("Screen").GetString());
        Assert.Equal(437, error.GetProperty("ConsoleCodePage").GetInt32());

        // Help is a themed grid rendered with ANSI styling: the console interprets the escape
        // sequences, so only the text reaches the screen.
        var help = interactive.GetProperty("Help");
        Assert.Equal(0, help.GetProperty("ExitCode").GetInt32());
        var screen = help.GetProperty("Screen").GetString()!;
        Assert.Contains(PolishHelpDescription, screen);
        Assert.DoesNotContain((char)0x1B, screen);
        Assert.Equal(437, help.GetProperty("ConsoleCodePage").GetInt32());
    }

    // PowerShell decodes a program's redirected output with the console code page (852 here, which
    // has the Polish letters); TigerCli writes in that code page instead of switching it to UTF-8.
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Hosts), MemberType = typeof(WindowsConsoleProbe))]
    public async Task Redirected_WritesInTheConsoleCodePageWithoutChangingIt(string host)
    {
        var redirected = (await RunAsync(host, constrained: false)).GetProperty("Redirected");

        var output = redirected.GetProperty("Output");
        Assert.Equal(0, output.GetProperty("ExitCode").GetInt32());
        Assert.Equal(PolishGreeting, output.GetProperty("Text").GetString());
        Assert.Equal(852, output.GetProperty("ConsoleCodePage").GetInt32());

        var error = redirected.GetProperty("Error");
        Assert.NotEqual(0, error.GetProperty("ExitCode").GetInt32());
        Assert.StartsWith(PolishUnknownOption, error.GetProperty("Text").GetString());
        Assert.Equal(852, error.GetProperty("ConsoleCodePage").GetInt32());
    }
}
