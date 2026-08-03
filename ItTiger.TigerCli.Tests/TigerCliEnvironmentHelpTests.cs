using System.Globalization;
using System.Resources;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliEnvironmentHelpTests
{
    private const string ExitHint = "For a list of exit codes, use --help-errors.";
    private const string EnvironmentHint = "For recognized environment variables, use --help-env.";

    private sealed class TrackingResources : ResourceManager
    {
        private readonly Dictionary<string, Dictionary<string, string>> resources;

        public TrackingResources(Dictionary<string, Dictionary<string, string>> resources)
            : base("TrackingResources", typeof(TrackingResources).Assembly)
        {
            this.resources = resources;
        }

        public List<(string Name, string Culture)> Lookups { get; } = new();

        public override string? GetString(string name, CultureInfo? culture)
        {
            culture ??= CultureInfo.InvariantCulture;
            Lookups.Add((name, culture.Name));
            return resources.TryGetValue(culture.Name, out var cultureResources)
                && cultureResources.TryGetValue(name, out var value)
                    ? value
                    : null;
        }
    }

    private sealed class EmptySettings : TigerCliSettings
    {
    }

    private sealed class PositionalSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "target")]
        public string Target { get; set; } = string.Empty;
    }

    private sealed class TrackingCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        private readonly Action execute;

        public TrackingCommand(Action execute)
        {
            this.execute = execute;
        }

        public override Task<int> ExecuteAsync(EmptySettings settings)
        {
            execute();
            return Task.FromResult(0);
        }
    }

    private sealed class PositionalTrackingCommand : TigerCliAsyncCommandHandler<PositionalSettings>
    {
        private readonly Action execute;

        public PositionalTrackingCommand(Action execute)
        {
            this.execute = execute;
        }

        public override Task<int> ExecuteAsync(PositionalSettings settings)
        {
            execute();
            return Task.FromResult(0);
        }
    }

    private enum TestExitCode
    {
        Ok = 0,
        Failed = 1
    }

    private sealed class EnvironmentContribution : ITigerCliAppContribution
    {
        public void Configure(TigerCliAppContributionBuilder builder)
        {
            builder.AddEnvironmentVariable(
                "LIBRARY_CACHE",
                "Selects the reusable library cache directory.");
        }
    }

    [Fact]
    public async Task RootHelpEnv_RendersFrameworkVariables_WithoutExecutingCommand()
    {
        var executed = false;
        var result = await RunAsync(CreateApp(() => executed = true), "--help-env");

        Assert.Equal(0, result.ExitCode);
        Assert.False(executed);
        AssertFrameworkEnvironmentVariables(result.StdOut);
        Assert.DoesNotContain(EnvironmentHint, result.StdOut);
    }

    [Fact]
    public async Task CommandHelpEnv_RendersFrameworkVariables_WithoutExecutingCommand()
    {
        var executed = false;
        var result = await RunAsync(CreateApp(() => executed = true), "run", "--help-env");

        Assert.Equal(0, result.ExitCode);
        Assert.False(executed);
        AssertFrameworkEnvironmentVariables(result.StdOut);
        Assert.DoesNotContain(EnvironmentHint, result.StdOut);
    }

    [Fact]
    public async Task RootHelpSections_ComposeWithoutRedundantHints()
    {
        var executed = false;
        var result = await RunAsync(
            CreateApp(() => executed = true), "--help", "--help-errors", "--help-env");

        Assert.Equal(0, result.ExitCode);
        Assert.False(executed);
        Assert.Contains("Exit codes:", result.StdOut);
        Assert.Contains("Environment variables:", result.StdOut);
        Assert.DoesNotContain(ExitHint, result.StdOut);
        Assert.DoesNotContain(EnvironmentHint, result.StdOut);
    }

    [Fact]
    public async Task CommandWithPositionals_HelpEnvAfterPositionals_IsAcceptedWithoutExecution()
    {
        var executed = false;
        var result = await RunAsync(
            CreateApp(() => executed = true), "process", "project", "--help-env");

        Assert.Equal(0, result.ExitCode);
        Assert.False(executed);
        Assert.Contains("Environment variables:", result.StdOut);
    }

    /// <summary>
    /// Execution options share the informational options' area, so composing them stays valid for
    /// both the root form and a named command, and neither form executes the handler.
    /// </summary>
    [Theory]
    [InlineData("--help", "--help-errors", "--help-env", "--theme", "light")]
    [InlineData("run", "--help", "--help-errors", "--help-env", "--theme", "light")]
    public async Task HelpSections_ComposeWithExecutionOptions(params string[] args)
    {
        var executed = false;
        var result = await RunAsync(CreateApp(() => executed = true), args);

        Assert.Equal(0, result.ExitCode);
        Assert.False(executed);
        Assert.Contains("Exit codes:", result.StdOut);
        Assert.Contains("Environment variables:", result.StdOut);
        Assert.DoesNotContain(ExitHint, result.StdOut);
        Assert.DoesNotContain(EnvironmentHint, result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Theory]
    [InlineData("--help-env", "run")]
    [InlineData("process", "--help-env", "project")]
    public async Task HelpEnv_BeforeCommandOrRequiredPositional_IsRejected(params string[] args)
    {
        var executed = false;
        var result = await RunAsync(CreateApp(() => executed = true), args);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(executed);
        Assert.Contains("Unknown option: '--help-env'", result.StdErr);
    }

    public static TheoryData<string, bool, bool, bool, bool> ComposedHelpCases => new()
    {
        { "run --help", false, false, true, true },
        { "run --help --help-errors", true, false, false, true },
        { "run --help --help-env", false, true, true, false },
        { "run --help --help-errors --help-env", true, true, false, false },
        { "run --help-errors", true, false, false, false },
        { "run --help-env", false, true, false, false },
        { "run --help-errors --help-env", true, true, false, false }
    };

    [Theory]
    [MemberData(nameof(ComposedHelpCases))]
    public async Task HelpSections_ComposeWithoutSelfReferentialHints(
        string commandLine,
        bool expectExitSection,
        bool expectEnvironmentSection,
        bool expectExitHint,
        bool expectEnvironmentHint)
    {
        var result = await RunAsync(CreateApp(() => Assert.Fail("Command must not execute.")),
            commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expectExitSection, result.StdOut.Contains("Exit codes:", StringComparison.Ordinal));
        Assert.Equal(expectEnvironmentSection, result.StdOut.Contains("Environment variables:", StringComparison.Ordinal));
        Assert.Equal(expectExitHint, result.StdOut.Contains(ExitHint, StringComparison.Ordinal));
        Assert.Equal(expectEnvironmentHint, result.StdOut.Contains(EnvironmentHint, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NormalHelp_DescribesAllFrameworkOptions()
    {
        var result = await RunAsync(CreateApp(() => Assert.Fail("Command must not execute.")),
            "run", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--help-env", result.StdOut);
        Assert.Contains("--help-errors", result.StdOut);
        Assert.Contains("--non-interactive", result.StdOut);
        Assert.Contains("--theme", result.StdOut);
        Assert.Contains("--color", result.StdOut);
        Assert.Contains("--no-color", result.StdOut);
        Assert.Contains("--culture", result.StdOut);
    }

    [Fact]
    public async Task RegisteredEnvironmentVariables_RenderAtTheirEffectiveScopes()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("env-test")
            .AddEnvironmentVariable("APP_ROOT", "App-wide variable.")
            .AddCommand(
                "outside",
                () => new TrackingCommand(() => Assert.Fail("Command must not execute.")),
                command => command.AddEnvironmentVariable("OUTSIDE_ONLY", "Outside command variable."))
            .AddCommandGroup("projects", projects =>
            {
                projects.AddEnvironmentVariable("PROJECT_ROOT", "Project-group variable.");
                projects.AddCommandGroup("stored", stored =>
                {
                    stored.AddEnvironmentVariable("STORED_ROOT", "Stored-project variable.");
                    stored.AddCommand(
                        "add",
                        () => new TrackingCommand(() => Assert.Fail("Command must not execute.")),
                        command => command.AddEnvironmentVariable("ADD_ONLY", "Add-command variable."));
                });
            })
            .Build();

        var root = await RunAsync(app, "--help-env");
        var group = await RunAsync(app, "projects", "--help-env");
        var nestedCommand = await RunAsync(app, "projects", "stored", "add", "--help-env");
        var outside = await RunAsync(app, "outside", "--help-env");

        Assert.Contains("APP_ROOT", root.StdOut);
        Assert.DoesNotContain("PROJECT_ROOT", root.StdOut);
        Assert.Contains("APP_ROOT", group.StdOut);
        Assert.Contains("PROJECT_ROOT", group.StdOut);
        Assert.DoesNotContain("STORED_ROOT", group.StdOut);
        Assert.Contains("APP_ROOT", nestedCommand.StdOut);
        Assert.Contains("PROJECT_ROOT", nestedCommand.StdOut);
        Assert.Contains("STORED_ROOT", nestedCommand.StdOut);
        Assert.Contains("ADD_ONLY", nestedCommand.StdOut);
        Assert.Contains("APP_ROOT", outside.StdOut);
        Assert.Contains("OUTSIDE_ONLY", outside.StdOut);
        Assert.DoesNotContain("PROJECT_ROOT", outside.StdOut);
        Assert.DoesNotContain("ADD_ONLY", outside.StdOut);
    }

    [Fact]
    public async Task ContributionEnvironmentVariable_AppearsInRootAndCommandHelpEnv()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("env-test")
            .AddContribution(new EnvironmentContribution())
            .AddCommand("run", () => new TrackingCommand(() => Assert.Fail("Command must not execute.")))
            .Build();

        var root = await RunAsync(app, "--help-env");
        var command = await RunAsync(app, "run", "--help-env");

        Assert.Contains("LIBRARY_CACHE", root.StdOut);
        Assert.Contains("Selects the reusable library cache directory.", root.StdOut);
        Assert.Contains("LIBRARY_CACHE", command.StdOut);
    }

    [Fact]
    public async Task ContributionEnvironmentVariable_DescriptionResolvesLateForActiveCulture()
    {
        var resources = new TrackingResources(new()
        {
            ["pl-PL"] = new()
            {
                ["Environment_LibraryCache_Description"] =
                    "[green]Wybiera katalog pamięci podręcznej biblioteki.[/]"
            }
        });
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("env-test")
            .SetSupportedCultures("en-US", "pl-PL")
            .UseAppResources(resources)
            .AddContribution(new Contribution(builder =>
                builder.AddEnvironmentVariable(
                    "LIBRARY_CACHE",
                    "Selects the reusable library cache directory.",
                    descriptionResourceKey: "Environment_LibraryCache_Description")))
            .AddCommand("run", () => new TrackingCommand(() => Assert.Fail("Command must not execute.")))
            .Build();

        Assert.Empty(resources.Lookups);

        var result = await RunAsync(app, "run", "--culture", "pl-PL", "--help-env");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("LIBRARY_CACHE", result.StdOut);
        Assert.Contains("Wybiera katalog pamięci podręcznej biblioteki.", result.StdOut);
        Assert.DoesNotContain("[green]", result.StdOut);
        Assert.DoesNotContain("Selects the reusable library cache directory.", result.StdOut);
        Assert.Equal([("Environment_LibraryCache_Description", "pl-PL")], resources.Lookups);
    }

    private sealed class Contribution : ITigerCliAppContribution
    {
        private readonly Action<TigerCliAppContributionBuilder> configure;

        public Contribution(Action<TigerCliAppContributionBuilder> configure)
        {
            this.configure = configure;
        }

        public void Configure(TigerCliAppContributionBuilder builder) => configure(builder);
    }

    [Fact]
    public void DuplicateEnvironmentVariable_InEffectiveScope_FailsAtBuild()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("env-test")
            .AddEnvironmentVariable("APP_MODE", "App definition.")
            .AddCommand(
                "run",
                () => new TrackingCommand(() => { }),
                command => command.AddEnvironmentVariable("APP_MODE", "Command definition."));

        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("APP_MODE", error.Message);
        Assert.Contains("effective scope", error.Message);
    }

    [Fact]
    public void FrameworkEnvironmentVariable_CannotBeRegisteredAgain()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("env-test")
            .AddEnvironmentVariable("TERM", "Duplicate framework variable.");

        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("TERM", error.Message);
        Assert.Contains("effective scope", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("APP MODE")]
    public void EnvironmentVariableName_MustBeNonEmptyAndContainNoWhitespace(string name)
    {
        var builder = TigerCliApp.CreateBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.AddEnvironmentVariable(name, "Description."));
    }

    [Fact]
    public async Task RegisteredEnvironmentVariable_DoesNotBecomeACommandOption()
    {
        var executed = false;
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("env-test")
            .AddEnvironmentVariable("APP_MODE", "Selects an app mode.")
            .AddCommand("run", () => new TrackingCommand(() => executed = true))
            .Build();

        var valid = await RunAsync(app, "run");
        Assert.Equal(0, valid.ExitCode);
        Assert.True(executed);

        executed = false;
        var invalid = await RunAsync(app, "run", "--APP_MODE", "test");

        Assert.NotEqual(0, invalid.ExitCode);
        Assert.False(executed);
        Assert.Contains("Unknown option: '--APP_MODE'", invalid.StdErr);
    }

    private static TigerCliApp CreateApp(Action execute) => TigerCliApp.CreateBuilder()
        .SetApplicationName("env-test")
        .UseExitCodes<TestExitCode>(TestExitCode.Ok, TestExitCode.Failed)
        .AddCommand("run", () => new TrackingCommand(execute))
        .AddCommand("process", () => new PositionalTrackingCommand(execute))
        .Build();

    private static void AssertFrameworkEnvironmentVariables(string output)
    {
        Assert.Contains("Environment variables:", output);
        Assert.Contains("TIGERCLI_THEME", output);
        Assert.Contains("FORCE_COLOR", output);
        Assert.Contains("CLICOLOR_FORCE", output);
        Assert.Contains("NO_COLOR", output);
        Assert.Contains("CLICOLOR", output);
        Assert.Contains("TERM", output);
    }

    private static Task<TigerCliAppRunResult> RunAsync(TigerCliApp app, params string[] args) =>
        TigerCliAppTestHost
            .For(app)
            .WithArgs(args)
            .RunAsync(TestContext.Current.CancellationToken);
}
