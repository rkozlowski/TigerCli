using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliUsageHelpHintTests
{
    private const string EnglishHint = "For more info, run:";

    private sealed class EmptySettings : TigerCliSettings
    {
    }

    private sealed class PositionalSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class RequiredSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true)]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ValidatingSettings : TigerCliSettings
    {
        public override TigerCliValidationResult Validate() =>
            TigerCliValidationResult.Error("That value is not valid here.");
    }

    private sealed class SuccessfulCommand<TSettings> : TigerCliAsyncCommandHandler<TSettings>
        where TSettings : TigerCliSettings, new()
    {
        public override Task<int> ExecuteAsync(TSettings settings) => Task.FromResult(0);
    }

    private sealed class ThrowingCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings) =>
            Task.FromException<int>(new InvalidOperationException("handler failed"));
    }

    [Fact]
    public async Task RootParseError_AppendsRootHelpHint()
    {
        var result = await RunAsync(CreateGroupedApp(), "--unknown");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--unknown'", result.StdErr);
        AssertHint(result.StdErr, "hint-test --help");
    }

    [Fact]
    public async Task GroupParseError_AppendsGroupHelpHint()
    {
        var result = await RunAsync(CreateGroupedApp(), "projects", "--unknown");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--unknown'", result.StdErr);
        AssertHint(result.StdErr, "hint-test projects --help");
    }

    [Fact]
    public async Task CommandParseError_AppendsMostSpecificCommandHelpHint()
    {
        var result = await RunAsync(CreateGroupedApp(), "projects", "list", "--unknown");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--unknown'", result.StdErr);
        AssertHint(result.StdErr, "hint-test projects list --help");
    }

    [Fact]
    public async Task StrictOptionPlacementError_AppendsMostSpecificCommandHelpHint()
    {
        var result = await RunAsync(
            CreateGroupedApp(),
            "projects", "add", "--theme", "light", "sample");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unexpected positional argument after options: sample", result.StdErr);
        AssertHint(result.StdErr, "hint-test projects add --help");
    }

    [Fact]
    public async Task MissingRequiredOption_AppendsCommandHelpHint()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("hint-test")
            .SetInteractionMode(TigerCliInteractionMode.NonInteractive)
            .AddCommand<SuccessfulCommand<RequiredSettings>>("run")
            .Build();

        var result = await RunAsync(app, "run");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Missing required option: --name", result.StdErr);
        AssertHint(result.StdErr, "hint-test run --help");
    }

    [Fact]
    public async Task NoCommandAndBareGroup_AppendSafeHelpHints()
    {
        var app = CreateGroupedApp();

        var root = await RunAsync(app);
        var group = await RunAsync(app, "projects");

        Assert.NotEqual(0, root.ExitCode);
        AssertHint(root.StdErr, "hint-test --help");
        Assert.NotEqual(0, group.ExitCode);
        AssertHint(group.StdErr, "hint-test projects --help");
    }

    [Fact]
    public async Task UsageHelpHint_IsLocalized()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("narzedzie")
            .SetDefaultCulture("pl-PL")
            .AddCommand<SuccessfulCommand<EmptySettings>>("run")
            .Build();

        var result = await RunAsync(app, "run", "--unknown");

        Assert.Contains("Aby uzyskać więcej informacji, uruchom:", result.StdErr);
        Assert.Contains("    narzedzie run --help", result.StdErr);
        Assert.DoesNotContain(EnglishHint, result.StdErr);
    }

    [Fact]
    public async Task ValidationAndHandlerFailures_DoNotAppendHelpHint()
    {
        var validationApp = TigerCliApp.CreateBuilder()
            .SetApplicationName("hint-test")
            .SetDefaultCommand<SuccessfulCommand<ValidatingSettings>>()
            .Build();
        var handlerApp = TigerCliApp.CreateBuilder()
            .SetApplicationName("hint-test")
            .SetDefaultCommand<ThrowingCommand>()
            .Build();

        var validation = await RunAsync(validationApp);
        var handler = await RunAsync(handlerApp);

        Assert.Contains("Validation error: That value is not valid here.", validation.StdErr);
        Assert.DoesNotContain(EnglishHint, validation.StdErr);
        Assert.Contains("handler failed", handler.StdErr);
        Assert.DoesNotContain(EnglishHint, handler.StdErr);
    }

    private static TigerCliApp CreateGroupedApp() => TigerCliApp.CreateBuilder()
        .SetApplicationName("hint-test")
        .AddCommandGroup("projects", group =>
        {
            group.AddCommand<SuccessfulCommand<EmptySettings>>("list");
            group.AddCommand<SuccessfulCommand<PositionalSettings>>("add");
        })
        .Build();

    private static void AssertHint(string stderr, string helpCommand)
    {
        Assert.Contains(EnglishHint, stderr);
        Assert.Contains($"    {helpCommand}", stderr);
    }

    private static Task<TigerCliAppRunResult> RunAsync(TigerCliApp app, params string[] args) =>
        TigerCliAppTestHost
            .For(app)
            .WithArgs(args)
            .RunAsync(TestContext.Current.CancellationToken);
}
