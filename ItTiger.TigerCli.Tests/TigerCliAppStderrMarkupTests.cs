using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliAppStderrMarkupTests
{
    private sealed class EchoSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class EchoCommand : TigerCliAsyncCommandHandler<EchoSettings>
    {
        public override Task<int> ExecuteAsync(EchoSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class BracketArgSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "[item]", Description = "Item")]
        public string Item { get; set; } = string.Empty;
    }

    private sealed class BracketArgCommand : TigerCliAsyncCommandHandler<BracketArgSettings>
    {
        public override Task<int> ExecuteAsync(BracketArgSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Item));
            return Task.FromResult(0);
        }
    }

    private sealed class ValidatingSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Description = "Name")]
        public string Name { get; set; } = string.Empty;

        public override TigerCliValidationResult Validate()
        {
            return TigerCliValidationResult.Error("bad [value] supplied");
        }
    }

    private sealed class ValidatingCommand : TigerCliAsyncCommandHandler<ValidatingSettings>
    {
        public override Task<int> ExecuteAsync(ValidatingSettings settings)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class ThrowingSettings : TigerCliSettings
    {
    }

    private sealed class ThrowingCommand : TigerCliAsyncCommandHandler<ThrowingSettings>
    {
        public override Task<int> ExecuteAsync(ThrowingSettings settings)
        {
            return Task.FromException<int>(new InvalidOperationException("boom [bracketed]"));
        }
    }

    private sealed class ReflectionWrappedThrowingCommand : TigerCliAsyncCommandHandler<ThrowingSettings>
    {
        public override Task<int> ExecuteAsync(ThrowingSettings settings)
        {
            throw new InvalidOperationException("inner command failure");
        }
    }

    private sealed class MarkupLikeThrowingCommand : TigerCliAsyncCommandHandler<ThrowingSettings>
    {
        public override Task<int> ExecuteAsync(ThrowingSettings settings)
        {
            throw new InvalidOperationException("bad [red]value[/]");
        }
    }

    private sealed class SemanticMarkupLikeThrowingCommand : TigerCliAsyncCommandHandler<ThrowingSettings>
    {
        public override Task<int> ExecuteAsync(ThrowingSettings settings)
        {
            // A semantic-looking token in user/app data must NOT be treated as trusted markup.
            throw new InvalidOperationException("[Alert]boom[/]");
        }
    }

    [Fact]
    public async Task ParseError_WritesToStderr_NotStdout()
    {
        var app = App<EchoCommand>();

        var result = await RunCapturedAsync(app, ["--name", "n", "--unknown"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("Unknown option: '--unknown'", result.Stderr);
    }

    [Fact]
    public async Task ParseError_AddsFrameworkErrorPrefix_AndPreservesMessageText()
    {
        var app = App<EchoCommand>();

        var result = await RunCapturedAsync(app, ["--name", "n", "--unknown"]);

        Assert.Equal("Error: Unknown option: '--unknown'" + Environment.NewLine, result.Stderr);
    }

    [Fact]
    public async Task MissingRequiredArgument_EscapesDisplayName_WithBracketsInName()
    {
        var app = App<BracketArgCommand>();

        var result = await RunCapturedAsync(app, ["--non-interactive"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("Missing required argument: <[item]>", result.Stderr);
    }

    [Fact]
    public async Task MissingRequiredOption_WritesToStderr()
    {
        var app = App<EchoCommand>();

        var result = await RunCapturedAsync(app, ["--non-interactive"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("Missing required option: --name", result.Stderr);
    }

    [Fact]
    public async Task UserValidationError_EscapesUserSuppliedMessage()
    {
        var app = App<ValidatingCommand>();

        var result = await RunCapturedAsync(app, ["--name", "x"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("Validation error: bad [value] supplied", result.Stderr);
    }

    [Fact]
    public async Task DirectHandlerException_MapsToUnhandledException_AndReportsDirectMessage()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("stderr-test")
            .SetDefaultCommand<ThrowingCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.UnhandledException, 46)
            .Build();

        var result = await RunCapturedAsync(app, []);

        Assert.Equal(46, result.ExitCode);
        Assert.StartsWith("Error: ", result.Stderr);
        Assert.Contains("boom [bracketed]", result.Stderr);
    }

    [Fact]
    public async Task HandlerException_UsesPolishFrameworkErrorPrefix()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("stderr-test")
            .SetSupportedCultures("en-US", "pl-PL")
            .SetDefaultCommand<ThrowingCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.StartsWith("Błąd: ", result.Stderr);
        Assert.Contains("boom [bracketed]", result.Stderr);
    }

    [Fact]
    public async Task ReflectionWrappedHandlerException_ReportsInnerExceptionMessage()
    {
        var app = App<ReflectionWrappedThrowingCommand>();

        var result = await RunCapturedAsync(app, []);

        Assert.NotEqual(0, result.ExitCode);
        Assert.StartsWith("Error: ", result.Stderr);
        Assert.Contains("inner command failure", result.Stderr);
        Assert.DoesNotContain("Exception has been thrown by the target of an invocation.", result.Stderr);
    }

    [Fact]
    public async Task UnhandledException_EscapesMarkupLikeMessage_AndWritesToStderr()
    {
        var app = App<MarkupLikeThrowingCommand>();

        var result = await RunCapturedAsync(app, []);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("Error: bad [red]value[/]" + Environment.NewLine, result.Stderr);
    }

    [Fact]
    public async Task NonInteractiveAgainstFullInteractive_WritesToStderr()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("stderr-test")
            .SetDefaultCommand<EchoCommand>()
            .SetInteractionMode(TigerCliInteractionMode.FullInteractive)
            .Build();

        var result = await RunCapturedAsync(app, ["--non-interactive"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("--non-interactive cannot be used with FullInteractive commands.", result.Stderr);
    }

    [Fact]
    public async Task UnhandledException_EscapesSemanticMarkupLikeMessage_RendersLiterally()
    {
        // After the Phase 2 migration the error PREFIX uses the semantic [Error] token, but the
        // app-supplied message is still escaped, so a semantic-looking value renders literally and
        // cannot inject styling.
        var app = App<SemanticMarkupLikeThrowingCommand>();

        var result = await RunCapturedAsync(app, []);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("Error: [Alert]boom[/]" + Environment.NewLine, result.Stderr);
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("tiger-blue")]
    public async Task FrameworkErrorPrefix_ResolvesSemanticErrorToken_UnderEachTheme(string themeName)
    {
        // The migrated [Error] prefix must resolve through every framework theme (no unknown-tag
        // throw), and dynamic message text stays escaped. Output capture strips styling, so the
        // structural text is identical across themes.
        var original = TigerConsole.CurrentTheme;
        try
        {
            TigerConsole.CurrentTheme = TigerConsole.GetTheme(themeName);
            var result = await RunCapturedAsync(App<MarkupLikeThrowingCommand>(), []);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal("Error: bad [red]value[/]" + Environment.NewLine, result.Stderr);
        }
        finally
        {
            TigerConsole.CurrentTheme = original;
        }
    }

    private static TigerCliApp App<TCommand>()
        where TCommand : class, new()
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("stderr-test")
            .SetDefaultCommand<TCommand>()
            .Build();
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args)
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
