using ItTiger.TigerCli.Testing;

namespace CommandParserTest.Tests;

// These tests exercise the real CommandParserTest app the way an app developer
// would: construct the app, run it through TigerCliAppTestHost, and assert the
// durable app-level behavior.
public sealed class CommandParserTestAppTests
{
    [Fact]
    public async Task DefaultCommand_EnglishDefault_WritesGreeting()
    {
        var result = await RunAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello, World!", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task DefaultCommand_PolishDefault_WritesLocalizedGreeting()
    {
        var result = await RunAsync("--culture", "pl-PL");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Witaj, Świecie!", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task DefaultCommand_PolishExplicitName_WritesLocalizedGreetingWithName()
    {
        var result = await RunAsync("--culture", "pl-PL", "-n", "Riley");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Witaj, Riley!", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Help_Polish_WritesLocalizedFrameworkAndAppText()
    {
        var result = await RunAsync("--help", "--culture", "pl-PL");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Użycie:", result.StdOut);
        Assert.Contains("Polecenia:", result.StdOut);
        Assert.Contains("Opcje:", result.StdOut);
        Assert.Contains("Aplikacja testowa parsera poleceń TigerCli.", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task HelpErrors_Polish_WritesLocalizedExitCodeText()
    {
        var result = await RunAsync("--help-errors", "--culture", "pl-PL");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Kody zakończenia", result.StdOut);
        Assert.Contains("Nieprawidłowe argumenty wiersza poleceń.", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task ModeCommand_PolishNonInteractive_WritesEffectiveMode()
    {
        var result = await RunAsync("mode", "--culture", "pl-PL", "--non-interactive");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tryb-interakcji=NonInteractive", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task RawCommand_MissingRequiredCode_PolishFailsWithValidationErrorCode()
    {
        var result = await RunAsync("raw", "--culture", "pl-PL", "--non-interactive");

        Assert.Equal((int)ParserTestExitCode.ValidationError, result.ExitCode);
        Assert.Contains("Błąd:", result.StdErr);
        Assert.Contains("Brak wymaganej opcji: --code", result.StdErr);
        Assert.Empty(result.StdOut);
    }

    [Fact]
    public async Task RawCommand_ExplicitCode_PolishReturnsRawCode()
    {
        var result = await RunAsync("raw", "--culture", "pl-PL", "--code", "123");

        Assert.Equal(123, result.ExitCode);
        Assert.Contains("Zwracanie surowego kodu 123", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task ThrowCommand_PolishFailsWithUnhandledExceptionCode()
    {
        var result = await RunAsync("throw", "--culture", "pl-PL");

        Assert.Equal((int)ParserTestExitCode.UnhandledException, result.ExitCode);
        Assert.Contains("Błąd:", result.StdErr);
        Assert.Contains("Intentional exception from parser test app.", result.StdErr);
        Assert.Empty(result.StdOut);
    }

    [Fact]
    public async Task PromptSmoke_PolishAnswersMissingValues()
    {
        var result = await TigerCliAppTestHost
            .For(CommandParserTestApp.Create())
            .WithArgs("prompt", "smoke", "--culture", "pl-PL")
            .WithTextInput("Riley")
            .WithSelectIndex(1)
            .WithMultiSelectIndexes(0, 2)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("nazwa=Riley", result.StdOut);
        Assert.Contains("tryb=Normal", result.StdOut);
        Assert.Contains("funkcje=Logging, Trace", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task ProviderSmoke_PolishSelectsLanguageNeutralKeys()
    {
        var result = await TigerCliAppTestHost
            .For(CommandParserTestApp.Create())
            .WithArgs("provider", "smoke", "--culture", "pl-PL")
            .WithSelectIndex(1)
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("połączenie=demo", result.StdOut);
        Assert.Contains("projekt=sandbox", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task PromptSmoke_PolishNonInteractiveFailsWithoutUsingPromptAnswers()
    {
        var result = await TigerCliAppTestHost
            .For(CommandParserTestApp.Create())
            .WithArgs("prompt", "smoke", "--culture", "pl-PL", "--non-interactive")
            .WithTextInput("unused")
            .WithSelectIndex(2)
            .WithMultiSelectIndexes(1)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Błąd:", result.StdErr);
        Assert.DoesNotContain("unused", result.StdOut);
    }

    private static Task<TigerCliAppRunResult> RunAsync(params string[] args)
    {
        return TigerCliAppTestHost
            .For(CommandParserTestApp.Create())
            .WithArgs(args)
            .RunAsync(TestContext.Current.CancellationToken);
    }
}
