using System.Resources;
using ItTiger.TigerCli.Commands;
using ParserDefaultCommand = CommandParserTest.DefaultCommand;
using ParserFailCommand = CommandParserTest.FailCommand;
using ParserModeCommand = CommandParserTest.ModeCommand;
using ParserProjectsSpAddCommand = CommandParserTest.ProjectsSpAddCommand;
using ParserPromptSmokeCommand = CommandParserTest.PromptSmokeCommand;
using ParserProviderSmokeCommand = CommandParserTest.ProviderSmokeCommand;
using ParserRawCommand = CommandParserTest.RawCommand;
using ParserThrowCommand = CommandParserTest.ThrowCommand;

namespace ItTiger.TigerCli.Tests;

public sealed class CommandParserTestLocalizationTests
{
    private static readonly ResourceManager ParserResources = new(
        "CommandParserTest.Resources.CommandParserTestStrings",
        typeof(ParserDefaultCommand).Assembly);

    [Fact]
    public async Task DefaultGreeting_EnglishDefault_RemainsUnchanged()
    {
        var app = CreateDefaultApp();

        var result = await RunCapturedAsync(app, ["--culture", "en-US"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello, World!", result.Stdout);
    }

    [Fact]
    public async Task DefaultGreeting_PolishDefault_IsLocalized()
    {
        var app = CreateDefaultApp();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Witaj, Świecie!", result.Stdout);
    }

    [Fact]
    public async Task DefaultGreeting_PolishUserName_IsPreserved()
    {
        var app = CreateDefaultApp();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL", "-n", "Riley"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Witaj, Riley!", result.Stdout);
    }

    [Fact]
    public async Task DefaultGreeting_PolishExplicitWorld_IsPreserved()
    {
        var app = CreateDefaultApp();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL", "-n", "World"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Witaj, World!", result.Stdout);
    }

    [Fact]
    public async Task DefaultGreeting_UserName_IsEscapedAndPreserved()
    {
        var app = CreateDefaultApp();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL", "-n", "[Riley]"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Witaj, [Riley]!", result.Stdout);
    }

    [Fact]
    public async Task ModeOutput_English_RemainsUnchanged()
    {
        var app = CreateModeApp();

        var result = await RunCapturedAsync(app, ["mode", "--culture", "en-US"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("interaction-mode=SemiInteractive", result.Stdout);
    }

    [Fact]
    public async Task ModeOutput_Polish_LocalizesLabelOnly()
    {
        var app = CreateModeApp();

        var result = await RunCapturedAsync(app, ["mode", "--culture", "pl-PL"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tryb-interakcji=SemiInteractive", result.Stdout);
    }

    [Fact]
    public async Task ModeOutput_PolishNonInteractive_LocalizesLabelOnly()
    {
        var app = CreateModeApp();

        var result = await RunCapturedAsync(app, ["mode", "--culture", "pl-PL", "--non-interactive"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tryb-interakcji=NonInteractive", result.Stdout);
    }

    [Fact]
    public async Task ProjectsSpAddOutput_English_RemainsEquivalent()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(
            app,
            ["projects", "sp-add", "--culture", "en-US", "local", "Billing", "--schema", "sales"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("projects sp-add connection=local project=Billing schema=sales", result.Stdout);
    }

    [Fact]
    public async Task ProjectsSpAddOutput_Polish_LocalizesLabels()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(
            app,
            ["projects", "sp-add", "--culture", "pl-PL", "local", "Billing", "--schema", "sales"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("projects sp-add połączenie=local projekt=Billing schemat=sales", result.Stdout);
    }

    [Fact]
    public async Task PromptSmokeOutput_English_RemainsEquivalent()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(
            app,
            ["prompt", "smoke", "--culture", "en-US", "Riley", "--mode", "Fast", "--features", "Logging"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("name=Riley; mode=Fast; features=Logging", result.Stdout);
    }

    [Fact]
    public async Task PromptSmokeOutput_Polish_LocalizesLabels()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(
            app,
            ["prompt", "smoke", "--culture", "pl-PL", "Riley", "--mode", "Fast", "--features", "Logging"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("nazwa=Riley; tryb=Fast; funkcje=Logging", result.Stdout);
    }

    [Fact]
    public async Task ProviderSmokeOutput_English_RemainsEquivalent()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(
            app,
            ["provider", "smoke", "--culture", "en-US", "local", "billing"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("connection=local; project=billing", result.Stdout);
    }

    [Fact]
    public async Task ProviderSmokeOutput_Polish_LocalizesLabels()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(
            app,
            ["provider", "smoke", "--culture", "pl-PL", "local", "billing"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("połączenie=local; projekt=billing", result.Stdout);
    }

    [Fact]
    public async Task FailOutput_English_RemainsEquivalent()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(app, ["fail", "--culture", "en-US"]);

        Assert.Equal(30, result.ExitCode);
        Assert.Contains("Intentional typed failure.", result.Stderr);
    }

    [Fact]
    public async Task FailOutput_Polish_LocalizesMessage()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(app, ["fail", "--culture", "pl-PL"]);

        Assert.Equal(30, result.ExitCode);
        Assert.Contains("Celowa typowana awaria.", result.Stderr);
    }

    [Fact]
    public async Task RawOutput_English_RemainsEquivalent()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(app, ["raw", "--culture", "en-US", "--code", "123"]);

        Assert.Equal(123, result.ExitCode);
        Assert.Contains("Returning raw code 123", result.Stdout);
    }

    [Fact]
    public async Task RawOutput_Polish_LocalizesMessage()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(app, ["raw", "--culture", "pl-PL", "--code", "123"]);

        Assert.Equal(123, result.ExitCode);
        Assert.Contains("Zwracanie surowego kodu 123", result.Stdout);
    }

    [Fact]
    public async Task ThrowOutput_English_ReportsRealExceptionMessage()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(app, ["throw", "--culture", "en-US"]);

        Assert.Equal(40, result.ExitCode);
        Assert.StartsWith("Error: ", result.Stderr);
        Assert.Contains("Intentional exception from parser test app.", result.Stderr);
        Assert.DoesNotContain("Exception has been thrown by the target of an invocation.", result.Stderr);
    }

    [Fact]
    public async Task ThrowOutput_Polish_ReportsRealExceptionMessage()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(app, ["throw", "--culture", "pl-PL"]);

        Assert.Equal(40, result.ExitCode);
        Assert.StartsWith("Błąd: ", result.Stderr);
        Assert.Contains("Intentional exception from parser test app.", result.Stderr);
        Assert.DoesNotContain("Exception has been thrown by the target of an invocation.", result.Stderr);
    }

    [Fact]
    public async Task HelpErrors_English_LocalizesSourceTextTigerTextEnum()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(app, ["--culture", "en-US", "--help-errors"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Parser test exit codes", result.Stdout);
        Assert.Contains("Invalid arguments", result.Stdout);
        Assert.Contains("Invalid command-line arguments.", result.Stdout);
    }

    [Fact]
    public async Task HelpErrors_Polish_LocalizesSourceTextTigerTextEnum()
    {
        var app = CreateRemainingCommandsApp();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL", "--help-errors"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Kody zakończenia testu parsera", result.Stdout);
        Assert.Contains("Nieprawidłowe argumenty", result.Stdout);
        Assert.Contains("Nieprawidłowe argumenty wiersza poleceń.", result.Stdout);
    }

    private static TigerCliApp CreateDefaultApp()
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("parser-test")
            .SetSupportedCultures("en-US", "pl-PL")
            .UseAppResources(ParserResources)
            .SetDefaultCommand<ParserDefaultCommand>()
            .Build();
    }

    private static TigerCliApp CreateModeApp()
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("parser-test")
            .SetSupportedCultures("en-US", "pl-PL")
            .UseAppResources(ParserResources)
            .AddCommand<ParserModeCommand>("mode")
            .Build();
    }

    private static TigerCliApp CreateRemainingCommandsApp()
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("parser-test")
            .SetSupportedCultures("en-US", "pl-PL")
            .UseAppResources(ParserResources)
            .UseExitCodes<global::CommandParserTest.ParserTestExitCode>(
                    global::CommandParserTest.ParserTestExitCode.Ok,
                    global::CommandParserTest.ParserTestExitCode.InternalError)
                .ExitKind(TigerCliExitKind.UnhandledException, global::CommandParserTest.ParserTestExitCode.UnhandledException)
            .AddCommandGroup("projects", group => group.AddCommand<ParserProjectsSpAddCommand>("sp-add"))
            .AddCommandGroup("prompt", group => group.AddCommand<ParserPromptSmokeCommand>("smoke"))
            .AddCommandGroup("provider", group => group.AddCommand<ParserProviderSmokeCommand>("smoke"))
            .AddCommand<ParserFailCommand>("fail")
            .AddCommand<ParserRawCommand>("raw")
            .AddCommand<ParserThrowCommand>("throw")
            .Build();
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app, string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
