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
    private const string GreetingLodz = "Hello, Łódź!";

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

        // A Unicode argument parsed into a Unicode enum value and written back.
        var city = interactive.GetProperty("UnicodeArgument");
        Assert.Equal(0, city.GetProperty("ExitCode").GetInt32());
        Assert.Equal(GreetingLodz, city.GetProperty("Screen").GetString());
        Assert.Equal(437, city.GetProperty("ConsoleCodePage").GetInt32());
    }

    // Redirected output is UTF-8 under code page 437, which has neither 'Ł' nor 'ź', and the code page
    // stays 437. Files are read as raw bytes; PowerShell, which decodes a program's output with
    // [Console]::OutputEncoding, reads it intact once the caller selects UTF-8.
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Hosts), MemberType = typeof(WindowsConsoleProbe))]
    public async Task Redirected_IsUtf8WithoutChangingTheCodePage(string host)
    {
        var redirected = (await RunAsync(host, constrained: false)).GetProperty("Redirected");

        var fileOutput = redirected.GetProperty("FileOutput");
        Assert.Equal(0, fileOutput.GetProperty("ExitCode").GetInt32());
        Assert.Equal(GreetingLodz, fileOutput.GetProperty("Stdout").GetString()!.TrimEnd());
        Assert.Equal(437, fileOutput.GetProperty("ConsoleCodePage").GetInt32());

        var fileError = redirected.GetProperty("FileError");
        Assert.NotEqual(0, fileError.GetProperty("ExitCode").GetInt32());
        Assert.StartsWith(PolishUnknownOption, fileError.GetProperty("Stderr").GetString());
        Assert.Equal(437, fileError.GetProperty("ConsoleCodePage").GetInt32());

        var powerShellOutput = redirected.GetProperty("PowerShellOutput");
        Assert.Equal(0, powerShellOutput.GetProperty("ExitCode").GetInt32());
        Assert.Equal(GreetingLodz, powerShellOutput.GetProperty("Text").GetString());
        Assert.Equal(437, powerShellOutput.GetProperty("ConsoleCodePage").GetInt32());

        var powerShellError = redirected.GetProperty("PowerShellError");
        Assert.NotEqual(0, powerShellError.GetProperty("ExitCode").GetInt32());
        Assert.StartsWith(PolishUnknownOption, powerShellError.GetProperty("Text").GetString());
    }

    // One stream redirected, the other on the console: the redirected one is UTF-8 and the one on the
    // console still renders every character.
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Hosts), MemberType = typeof(WindowsConsoleProbe))]
    public async Task Mixed_RedirectedStreamIsUtf8AndConsoleStreamRendersUnicode(string host)
    {
        var redirected = (await RunAsync(host, constrained: false)).GetProperty("Redirected");

        var errorOnConsole = redirected.GetProperty("ErrorOnConsoleOutputToFile");
        Assert.NotEqual(0, errorOnConsole.GetProperty("ExitCode").GetInt32());
        Assert.StartsWith(PolishUnknownOption, errorOnConsole.GetProperty("Screen").GetString());
        Assert.Equal(437, errorOnConsole.GetProperty("ConsoleCodePage").GetInt32());

        var outputOnConsole = redirected.GetProperty("OutputOnConsoleErrorToFile");
        Assert.Equal(0, outputOnConsole.GetProperty("ExitCode").GetInt32());
        Assert.Equal(GreetingLodz, outputOnConsole.GetProperty("Screen").GetString());
        Assert.Equal(437, outputOnConsole.GetProperty("ConsoleCodePage").GetInt32());
    }
}
