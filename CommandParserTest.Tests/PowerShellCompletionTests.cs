using System.Text.Json;
using static CommandParserTest.Tests.WindowsConsoleProbe;

namespace CommandParserTest.Tests;

// The generated PowerShell completer for the real parser-test executable, driven through
// TabExpansion2 inside real Windows PowerShell 5.1 and PowerShell 7+ hosts by WindowsConsoleProbe.
// The assertions are the completer's session contract under an OEM console code page.
public sealed class PowerShellCompletionTests
{
    private const string PolishHelpDescription = "Wyświetl pomoc";

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Hosts), MemberType = typeof(WindowsConsoleProbe))]
    public async Task FullLanguage_CompletesWithDescriptionsAndLeavesSessionUnchanged(string host)
    {
        var result = await RunAsync(host, constrained: false);

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
        // the completer selects UTF-8 for the app's answer.
        var help = Assert.Single(Matches(result, 1), match => match.Text == "--help");
        Assert.Equal(PolishHelpDescription, help.ToolTip);
    }

    // A completion request is answered without console setup: in the console code page the invoking
    // shell chose (852 here, which has the Polish letters), which stays as it was.
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Hosts), MemberType = typeof(WindowsConsoleProbe))]
    public async Task CompletionRequest_AnswersInTheConsoleCodePageWithoutChangingIt(string host)
    {
        var request = (await RunAsync(host, constrained: false)).GetProperty("CompletionRequest");

        Assert.Equal(0, request.GetProperty("ExitCode").GetInt32());
        Assert.Equal(852, request.GetProperty("ConsoleCodePage").GetInt32());
        var lines = request.GetProperty("Lines").EnumerateArray().Select(line => line.GetString()).ToArray();
        Assert.Contains($"--help\t{PolishHelpDescription}", lines);
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Hosts), MemberType = typeof(WindowsConsoleProbe))]
    public async Task ConstrainedLanguage_CompletesWithPlainCandidates(string host)
    {
        var result = await RunAsync(host, constrained: true);

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
    [MemberData(nameof(Hosts), MemberType = typeof(WindowsConsoleProbe))]
    public async Task UninstalledExecutable_CompletesNothingAndRecordsNoError(string host)
    {
        var result = await RunAsync(host, constrained: false);

        var uninstalled = result.GetProperty("Uninstalled");
        Assert.Equal(7, uninstalled.GetProperty("LastExitCode").GetInt32());
        Assert.Equal(0, uninstalled.GetProperty("ErrorsAdded").GetInt32());
        Assert.DoesNotContain(ReadMatches(uninstalled), match => match.Text == "echo");
    }

    private static IReadOnlyList<CompletionMatch> Matches(JsonElement result, int tabIndex) =>
        ReadMatches(result.GetProperty("Tabs")[tabIndex]);
}
