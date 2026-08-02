using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliAppContributionTests
{
    private const string OptionName = "--library-config";

    private sealed class EmptySettings : TigerCliSettings
    {
    }

    private sealed class ConflictingSettings : TigerCliSettings
    {
        [TigerCliOption(OptionName)]
        public string? Config { get; set; }
    }

    private sealed class PositionalSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "target")]
        public string Target { get; set; } = string.Empty;
    }

    private sealed class NoopCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings) => Task.FromResult(0);
    }

    private sealed class ConflictingCommand : TigerCliAsyncCommandHandler<ConflictingSettings>
    {
        public override Task<int> ExecuteAsync(ConflictingSettings settings) => Task.FromResult(0);
    }

    private sealed class PositionalCommand : TigerCliAsyncCommandHandler<PositionalSettings>
    {
        private readonly Action<PositionalSettings> execute;

        public PositionalCommand(Action<PositionalSettings> execute)
        {
            this.execute = execute;
        }

        public override Task<int> ExecuteAsync(PositionalSettings settings)
        {
            execute(settings);
            return Task.FromResult(0);
        }
    }

    private sealed class ObservingCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        private readonly Action execute;

        public ObservingCommand(Action execute)
        {
            this.execute = execute;
        }

        public override Task<int> ExecuteAsync(EmptySettings settings)
        {
            execute();
            return Task.FromResult(0);
        }
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
    public void HostApp_CanRegisterContribution()
    {
        var configured = false;

        _ = TigerCliApp.CreateBuilder()
            .AddContribution(new Contribution(builder =>
            {
                configured = true;
                AddOption(builder, (_, _) => TigerCliValidationResult.Success());
            }))
            .AddCommand<NoopCommand>("run")
            .Build();

        Assert.True(configured);
    }

    [Fact]
    public async Task AbsentOption_AppliesNullBeforeCommandExecution()
    {
        string? appliedValue = "not-applied";
        string? observedByCommand = "not-executed";
        var app = CreateObservingApp(
            (_, value) =>
            {
                appliedValue = value;
                return TigerCliValidationResult.Success();
            },
            () => observedByCommand = appliedValue);

        var result = await RunCapturedAsync(app, ["run"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Null(appliedValue);
        Assert.Null(observedByCommand);
    }

    [Fact]
    public async Task OptionBeforeCommand_IsRejectedAsUnknownOption()
    {
        var applied = false;
        var app = CreateObservingApp(
            (_, _) =>
            {
                applied = true;
                return TigerCliValidationResult.Success();
            },
            () => Assert.Fail("Command must not execute."));

        var result = await RunCapturedAsync(app, [OptionName, "before.json", "run"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"Unknown option: '{OptionName}'", result.Stderr);
        Assert.False(applied);
    }

    [Fact]
    public async Task OptionAfterCommand_AppliesSuppliedValue()
    {
        string? appliedValue = null;
        var app = CreateObservingApp(
            (_, value) =>
            {
                appliedValue = value;
                return TigerCliValidationResult.Success();
            },
            () => { });

        var result = await RunCapturedAsync(app, ["run", OptionName, "after.json"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("after.json", appliedValue);
    }

    [Fact]
    public async Task EqualsForm_AcceptsAStringValueBeginningWithDash()
    {
        string? appliedValue = null;
        var app = CreateObservingApp(
            (_, value) =>
            {
                appliedValue = value;
                return TigerCliValidationResult.Success();
            },
            () => { });

        var result = await RunCapturedAsync(app, ["run", $"{OptionName}=--special"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("--special", appliedValue);
    }

    [Fact]
    public async Task MissingValue_FailsClearly()
    {
        var app = CreateObservingApp(
            (_, _) => TigerCliValidationResult.Success(),
            () => Assert.Fail("Command must not execute."));

        var result = await RunCapturedAsync(app, ["run", OptionName]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"Option '{OptionName}' requires a value.", result.Stderr);
    }

    [Fact]
    public async Task OptionAfterCommandPositionals_AppliesSuppliedValue()
    {
        string? appliedValue = null;
        string? observedTarget = null;
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test-app")
            .AddContribution(new Contribution(builder => AddOption(
                builder,
                (_, value) =>
                {
                    appliedValue = value;
                    return TigerCliValidationResult.Success();
                })))
            .AddCommand("run", () => new PositionalCommand(settings => observedTarget = settings.Target))
            .Build();

        var result = await RunCapturedAsync(app, ["run", "project", OptionName, "after.json"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("after.json", appliedValue);
        Assert.Equal("project", observedTarget);
    }

    [Fact]
    public async Task OptionBeforeCommandPositionals_IsRejected()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test-app")
            .AddContribution(new Contribution(builder => AddOption(
                builder,
                (_, _) => TigerCliValidationResult.Success())))
            .AddCommand("run", () => new PositionalCommand(_ => Assert.Fail("Command must not execute.")))
            .Build();

        var result = await RunCapturedAsync(app, ["run", OptionName, "before.json", "project"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"Unknown option: '{OptionName}'", result.Stderr);
    }

    [Fact]
    public async Task RepeatedOption_FailsClearly()
    {
        var app = CreateObservingApp(
            (_, _) => TigerCliValidationResult.Success(),
            () => Assert.Fail("Command must not execute."));

        var result = await RunCapturedAsync(
            app,
            ["run", OptionName, "one", OptionName, "two"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"Global option '{OptionName}' may only be specified once.", result.Stderr);
    }

    [Fact]
    public async Task RootHelp_RendersContributedOptionWithoutShortAliasOrApplyingIt()
    {
        var applyCount = 0;
        var app = CreateObservingApp(
            (_, _) =>
            {
                applyCount++;
                return TigerCliValidationResult.Success();
            },
            () => { });

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"{OptionName} <path>", result.Stdout);
        Assert.Contains("Use a reusable library configuration file.", result.Stdout);
        Assert.DoesNotContain("-l,", result.Stdout);
        Assert.Equal(0, applyCount);
    }

    [Fact]
    public async Task CommandHelp_RendersContributedOption()
    {
        var app = CreateObservingApp(
            (_, _) => TigerCliValidationResult.Success(),
            () => { });

        var result = await RunCapturedAsync(app, ["run", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"{OptionName} <path>", result.Stdout);
        Assert.Contains("Use a reusable library configuration file.", result.Stdout);
    }

    [Fact]
    public async Task InteractiveAndNonInteractiveRuns_ApplyTheSameValueWithoutPrompting()
    {
        var applied = new List<(string? Value, TigerCliInteractionMode Mode)>();
        var app = CreateObservingApp(
            (context, value) =>
            {
                applied.Add((value, context.InteractionMode));
                return TigerCliValidationResult.Success();
            },
            () => { });
        var interactiveShell = new TestShell();
        var nonInteractiveShell = new TestShell();

        var interactive = await RunCapturedAsync(
            app,
            ["run", OptionName, "same.json"],
            interactiveShell);
        var nonInteractive = await RunCapturedAsync(
            app,
            ["run", OptionName, "same.json", "--non-interactive"],
            nonInteractiveShell);

        Assert.Equal(0, interactive.ExitCode);
        Assert.Equal(0, nonInteractive.ExitCode);
        Assert.Equal(
            [("same.json", TigerCliInteractionMode.SemiInteractive), ("same.json", TigerCliInteractionMode.NonInteractive)],
            applied);
        Assert.Equal(0, interactiveShell.Terminal.ReadCount);
        Assert.Equal(0, nonInteractiveShell.Terminal.ReadCount);
    }

    [Fact]
    public async Task CommandMenu_PromptsOnlyForCommandSelection_NotForGlobalOption()
    {
        var applied = false;
        var executed = false;
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test-app")
            .AddContribution(new Contribution(builder => AddOption(
                builder,
                (_, value) =>
                {
                    Assert.Null(value);
                    applied = true;
                    return TigerCliValidationResult.Success();
                })))
            .AddCommand("run", () => new ObservingCommand(() => executed = true))
            .UseCommandMenu()
            .Build();
        var shell = new TestShell();

        var runTask = RunCapturedAsync(app, [], shell);
        await shell.Terminal.WaitForRenderCountAsync(
            1,
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.True(applied);
        Assert.True(executed);
        Assert.Equal(1, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ContributionValidation_FailsBeforeCommandBindingAndExecution()
    {
        var executed = false;
        var app = CreateObservingApp(
            (_, _) => TigerCliValidationResult.Error("The library configuration is invalid."),
            () => executed = true);

        var result = await RunCapturedAsync(app, ["run", OptionName, "bad.json"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Validation error: The library configuration is invalid.", result.Stderr);
        Assert.False(executed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("name")]
    [InlineData("-x")]
    [InlineData("--")]
    [InlineData("---bad")]
    [InlineData("--bad=value")]
    [InlineData("--a|--b")]
    [InlineData("--bad name")]
    public void InvalidName_FailsAtAppBuild(string name)
    {
        var builder = TigerCliApp.CreateBuilder()
            .AddContribution(new Contribution(contribution =>
                contribution.GlobalOptions.AddOptionalString(
                    name,
                    "value",
                    "Description.",
                    (_, _) => TigerCliValidationResult.Success())));

        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("contributed global option name", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--help-env")]
    [InlineData("--help-errors")]
    [InlineData("--version")]
    [InlineData("--version-full")]
    [InlineData("--non-interactive")]
    [InlineData("--theme")]
    [InlineData("--culture")]
    [InlineData("--color")]
    [InlineData("--no-color")]
    public void BuiltInNameCollision_FailsAtAppBuild(string name)
    {
        var builder = TigerCliApp.CreateBuilder()
            .AddContribution(new Contribution(contribution =>
                contribution.GlobalOptions.AddOptionalString(
                    name,
                    "value",
                    "Description.",
                    (_, _) => TigerCliValidationResult.Success())));

        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("reserved by TigerCli", error.Message);
    }

    [Fact]
    public void DuplicateContributedName_FailsAtAppBuildWithoutLastOneWins()
    {
        var builder = TigerCliApp.CreateBuilder()
            .AddContribution(new Contribution(contribution => AddOption(
                contribution,
                (_, _) => TigerCliValidationResult.Success())))
            .AddContribution(new Contribution(contribution =>
                contribution.GlobalOptions.AddOptionalString(
                    "--LIBRARY-CONFIG",
                    "other",
                    "Other description.",
                    (_, _) => TigerCliValidationResult.Success())));

        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("already registered", error.Message);
    }

    [Fact]
    public void CommandSpecificOptionCollision_FailsAtAppBuild()
    {
        var builder = TigerCliApp.CreateBuilder()
            .AddContribution(new Contribution(contribution => AddOption(
                contribution,
                (_, _) => TigerCliValidationResult.Success())))
            .AddCommand<ConflictingCommand>("run");

        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("conflicts with a command-specific option", error.Message);
    }

    private static TigerCliApp CreateObservingApp(
        Func<TigerCliGlobalOptionContext, string?, TigerCliValidationResult> apply,
        Action execute)
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("test-app")
            .AddContribution(new Contribution(builder => AddOption(builder, apply)))
            .AddCommand("run", () => new ObservingCommand(execute))
            .Build();
    }

    private static void AddOption(
        TigerCliAppContributionBuilder builder,
        Func<TigerCliGlobalOptionContext, string?, TigerCliValidationResult> apply)
    {
        builder.GlobalOptions.AddOptionalString(
            OptionName,
            "path",
            "Use a reusable library configuration file.",
            apply);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args,
        TestShell? shell = null)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(
                args,
                shell ?? new TestShell(),
                ct: TestContext.Current.CancellationToken);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
