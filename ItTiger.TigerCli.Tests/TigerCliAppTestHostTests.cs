using System.Resources;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;
using ParserDefaultCommand = CommandParserTest.DefaultCommand;
using ParserProviderSmokeCommand = CommandParserTest.ProviderSmokeCommand;
using ParserProviderSmokeSettings = CommandParserTest.ProviderSmokeSettings;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliAppTestHostTests
{
    private static readonly ResourceManager ParserResources = new(
        "CommandParserTest.Resources.CommandParserTestStrings",
        typeof(ParserDefaultCommand).Assembly);

    private enum PromptColor
    {
        Red,
        Green,
        Blue
    }

    [Flags]
    private enum PromptFeatures
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4
    }

    private sealed class MessageSettings : TigerCliSettings
    {
        [TigerCliOption("--message", Required = true, Description = "Message")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class RequiredNameSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ColorSettings : TigerCliSettings
    {
        [TigerCliOption("--color", Required = true, Description = "Color")]
        public PromptColor Color { get; set; }
    }

    private sealed class ConfirmSettings : TigerCliSettings
    {
        [TigerCliOption("--enabled", Required = true, Description = "Enabled")]
        public bool? Enabled { get; set; }
    }

    private sealed class FlagsSettings : TigerCliSettings
    {
        [TigerCliOption("--features", Required = true, Description = "Features")]
        public PromptFeatures Features { get; set; }
    }

    private sealed class DuplicateArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "first")]
        public string First { get; set; } = string.Empty;

        [TigerCliArgument(0, Name = "second")]
        public string Second { get; set; } = string.Empty;
    }

    private sealed class MessageCommand : TigerCliAsyncCommandHandler<MessageSettings>
    {
        public override Task<int> ExecuteAsync(MessageSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Message));
            return Task.FromResult(23);
        }
    }

    private sealed class RequiredNameCommand : TigerCliAsyncCommandHandler<RequiredNameSettings>
    {
        public override Task<int> ExecuteAsync(RequiredNameSettings settings)
        {
            TigerConsole.MarkupLine($"name={CliMarkupParser.Escape(settings.Name)}");
            return Task.FromResult(0);
        }
    }

    private sealed class ColorCommand : TigerCliAsyncCommandHandler<ColorSettings>
    {
        public override Task<int> ExecuteAsync(ColorSettings settings)
        {
            TigerConsole.MarkupLine($"color={settings.Color}");
            return Task.FromResult(0);
        }
    }

    private sealed class ConfirmCommand : TigerCliAsyncCommandHandler<ConfirmSettings>
    {
        public override Task<int> ExecuteAsync(ConfirmSettings settings)
        {
            TigerConsole.MarkupLine($"enabled={settings.Enabled}");
            return Task.FromResult(0);
        }
    }

    private sealed class FlagsCommand : TigerCliAsyncCommandHandler<FlagsSettings>
    {
        public override Task<int> ExecuteAsync(FlagsSettings settings)
        {
            TigerConsole.MarkupLine($"features={settings.Features}");
            return Task.FromResult(0);
        }
    }

    private sealed class DuplicateArgumentCommand : TigerCliAsyncCommandHandler<DuplicateArgumentSettings>
    {
        public override Task<int> ExecuteAsync(DuplicateArgumentSettings settings)
        {
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task RunAsync_CapturesStdoutAndExitCode()
    {
        var app = App<MessageCommand>();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("--message", "hello")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(23, result.ExitCode);
        Assert.Contains("hello", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task RunAsync_CapturesStderrForFrameworkErrors()
    {
        var app = App<RequiredNameCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("--non-interactive")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(45, result.ExitCode);
        Assert.Empty(result.StdOut);
        Assert.Contains("Missing required option: --name", result.StdErr);
    }

    [Fact]
    public async Task RunAsync_RestoresConsoleWritersAfterSuccessfulRun()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var sentinelOut = new StringWriter();
        using var sentinelError = new StringWriter();

        try
        {
            Console.SetOut(sentinelOut);
            Console.SetError(sentinelError);

            var result = await TigerCliAppTestHost
                .For(App<MessageCommand>())
                .WithArgs("--message", "inside")
                .RunAsync(TestContext.Current.CancellationToken);

            Console.Out.Write("after-out");
            Console.Error.Write("after-error");

            Assert.Equal(23, result.ExitCode);
            Assert.Equal("after-out", sentinelOut.ToString());
            Assert.Equal("after-error", sentinelError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_RestoresConsoleWritersAfterException()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var sentinelOut = new StringWriter();
        using var sentinelError = new StringWriter();

        try
        {
            Console.SetOut(sentinelOut);
            Console.SetError(sentinelError);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await TigerCliAppTestHost
                    .For(App<DuplicateArgumentCommand>())
                    .RunAsync(TestContext.Current.CancellationToken);
            });

            Console.Out.Write("after-out");
            Console.Error.Write("after-error");

            Assert.Equal("after-out", sentinelOut.ToString());
            Assert.Equal("after-error", sentinelError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_CapturesHelpOutput()
    {
        var result = await TigerCliAppTestHost
            .For(App<MessageCommand>())
            .WithArgs("--help")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut);
        Assert.Contains("--message", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task RunAsync_CapturesLocalizedHelpOutput()
    {
        var result = await TigerCliAppTestHost
            .For(ParserApp())
            .WithArgs("--culture", "pl-PL", "--help")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Użycie:", result.StdOut);
        Assert.Contains("parser-test [opcje]", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task WithTextInput_AnswersParserDrivenStringPrompt()
    {
        var result = await TigerCliAppTestHost
            .For(App<RequiredNameCommand>())
            .WithTextInput("riley")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("name=riley", result.StdOut);
    }

    [Fact]
    public async Task WithSelectIndex_AnswersEnumPrompt()
    {
        var result = await TigerCliAppTestHost
            .For(App<ColorCommand>())
            .WithSelectIndex(2)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("color=Blue", result.StdOut);
    }

    [Fact]
    public async Task WithSelectIndex_AnswersProviderBackedPrompt()
    {
        var result = await TigerCliAppTestHost
            .For(ParserApp())
            .WithArgs("provider", "smoke")
            .WithSelectIndex(1)
            .WithSelectIndex(1)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("connection=demo; project=training", result.StdOut);
    }

    [Fact]
    public async Task WithConfirm_AnswersNullableBoolPrompt()
    {
        var result = await TigerCliAppTestHost
            .For(App<ConfirmCommand>())
            .WithConfirm(false)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("enabled=False", result.StdOut);
    }

    [Fact]
    public async Task WithMultiSelectIndexes_AnswersFlagsPrompt()
    {
        var result = await TigerCliAppTestHost
            .For(App<FlagsCommand>())
            .WithMultiSelectIndexes(1, 0, 1)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("features=Read, Write", result.StdOut);
    }

    [Fact]
    public async Task NonInteractive_DoesNotUsePromptAnswersAndFailsStrictly()
    {
        var app = App<RequiredNameCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("--non-interactive")
            .WithTextInput("unused")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(45, result.ExitCode);
        Assert.DoesNotContain("unused", result.StdOut);
        Assert.Contains("Missing required option: --name", result.StdErr);
    }

    [Fact]
    public async Task WithPromptTimeout_ConfiguresPromptTimeout()
    {
        var app = App<RequiredNameCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 46));

        var result = await TigerCliAppTestHost
            .For(app)
            .WithPromptTimeout(TimeSpan.FromMilliseconds(20))
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("Cancelled.", result.StdErr);
    }

    [Fact]
    public async Task RunAsync_CanOnlyBeCalledOnce()
    {
        var host = TigerCliAppTestHost
            .For(App<MessageCommand>())
            .WithArgs("--message", "first");

        var first = await host.RunAsync(TestContext.Current.CancellationToken);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await host.RunAsync(TestContext.Current.CancellationToken));

        Assert.Equal(23, first.ExitCode);
        Assert.Contains("single-use", ex.Message);
    }

    private static TigerCliApp App<TCommand>(Action<TigerCliAppBuilder>? configure = null)
        where TCommand : class, new()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("host-test")
            .SetDefaultCommand<TCommand>();

        configure?.Invoke(builder);
        return builder.Build();
    }

    private static TigerCliApp ParserApp()
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("parser-test")
            .SetDefaultCulture("en-US")
            .SetSupportedCultures("en-US", "pl-PL")
            .UseAppResources(ParserResources)
            .SetDefaultCommand<ParserDefaultCommand>()
            .AddCommandGroup("provider", group => group.AddCommand<ParserProviderSmokeCommand>("smoke"))
            .ConfigurePrompts<ParserProviderSmokeSettings>(prompts =>
            {
                prompts.For(settings => settings.ConnectionName).SelectFrom((_, ctx) =>
                [
                    new OptionItem<string>("local",
                        GetParserText("Provider_Connection_Local_Label", ctx)),
                    new OptionItem<string>("demo",
                        GetParserText("Provider_Connection_Demo_Label", ctx))
                ]);

                prompts.For(settings => settings.ProjectName).SelectFrom((settings, ctx) =>
                    settings.ConnectionName == "local"
                        ?
                        [
                            new OptionItem<string>("billing",
                                GetParserText("Provider_Project_Billing_Label", ctx)),
                            new OptionItem<string>("inventory",
                                GetParserText("Provider_Project_Inventory_Label", ctx))
                        ]
                        :
                        [
                            new OptionItem<string>("sandbox",
                                GetParserText("Provider_Project_Sandbox_Label", ctx)),
                            new OptionItem<string>("training",
                                GetParserText("Provider_Project_Training_Label", ctx))
                        ]);
            })
            .Build();
    }

    private static string GetParserText(string key, TigerCliPromptContext context)
    {
        return ParserResources.GetString(key, context.Culture) ?? key;
    }
}
