using ItTiger.TigerCli.Terminal;
using Choice = ItTiger.TigerCli.Terminal.ConsoleOutputEncoding.Choice;

namespace ItTiger.TigerCli.Tests;

// The pure decision behind TigerCli's console output encoding. Real-console behavior is covered by
// CommandParserTest.Tests/ConsoleOutputEncodingTests.cs, which runs the app in real PowerShell hosts.
public sealed class ConsoleOutputEncodingPolicyTests
{
    // UTF-16 is the only encoding .NET can use on Windows without calling SetConsoleOutputCP, and it
    // is correct only when every standard stream is the interactive console (WriteConsoleW).
    [Fact]
    public void Windows_InteractiveConsole_UsesUnicode() =>
        Assert.Equal(Choice.ConsoleUnicode, ConsoleOutputEncoding.Select(isWindows: true, isOutputRedirected: false, isErrorRedirected: false));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Windows_AnyRedirectedStream_KeepsTheConsoleCodePage(bool isOutputRedirected, bool isErrorRedirected) =>
        Assert.Equal(Choice.KeepPlatformDefault, ConsoleOutputEncoding.Select(isWindows: true, isOutputRedirected, isErrorRedirected));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void OtherPlatforms_UseUtf8(bool isOutputRedirected, bool isErrorRedirected) =>
        Assert.Equal(Choice.Utf8, ConsoleOutputEncoding.Select(isWindows: false, isOutputRedirected, isErrorRedirected));

    [Fact]
    public void TigerConsole_OutputEncoding_DefaultsToTigerCliChoosing() =>
        Assert.Null(TigerConsole.OutputEncoding);
}
