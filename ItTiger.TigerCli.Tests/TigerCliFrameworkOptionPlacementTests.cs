using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliFrameworkOptionPlacementTests
{
    private sealed class EmptySettings : TigerCliSettings
    {
    }

    private sealed class PositionalSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "target")]
        public string Target { get; set; } = string.Empty;
    }

    private sealed class ModeCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings)
        {
            TigerConsole.MarkupLine(settings.InteractionMode.ToString());
            return Task.FromResult(0);
        }
    }

    private sealed class PositionalModeCommand : TigerCliAsyncCommandHandler<PositionalSettings>
    {
        public override Task<int> ExecuteAsync(PositionalSettings settings)
        {
            TigerConsole.MarkupLine($"{settings.Target}:{settings.InteractionMode}");
            return Task.FromResult(0);
        }
    }

    private sealed class ColorCommand : TigerCliAsyncCommandHandler<PositionalSettings>
    {
        public override Task<int> ExecuteAsync(PositionalSettings settings)
        {
            TigerConsole.MarkupLine($"{settings.Target}:{TigerConsole.ColorMode}");
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task NonInteractive_AfterCommand_IsAccepted()
    {
        var result = await RunAsync(CreateApp(), "run", "--non-interactive");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NonInteractive", result.StdOut);
    }

    [Fact]
    public async Task NonInteractive_AfterCommandPositionals_IsAccepted()
    {
        var result = await RunAsync(CreateApp(), "process", "project", "--non-interactive");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("project:NonInteractive", result.StdOut);
    }

    [Fact]
    public async Task NonInteractive_BeforeCommand_IsRejected()
    {
        var result = await RunAsync(CreateApp(), "--non-interactive", "run");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--non-interactive'", result.StdErr);
    }

    [Fact]
    public async Task NonInteractive_BeforeRequiredPositional_IsRejected()
    {
        var result = await RunAsync(CreateApp(), "process", "--non-interactive", "project");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--non-interactive'", result.StdErr);
    }

    [Fact]
    public async Task Color_AfterCommandPositionals_IsAccepted()
    {
        var result = await RunAsync(CreateApp(), "color", "project", "--color", "never");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("project:Never", result.StdOut);
    }

    [Theory]
    [InlineData("--color", "never", "color", "project")]
    [InlineData("color", "--color", "never", "project")]
    public async Task Color_BeforeCommandOrRequiredPositional_IsRejected(
        string first,
        string second,
        string third,
        string fourth)
    {
        var result = await RunAsync(CreateApp(), first, second, third, fourth);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--color'", result.StdErr);
    }

    [Theory]
    [InlineData("--no-color", null)]
    [InlineData("--theme", "dark")]
    [InlineData("--culture", "en-US")]
    public async Task OtherExecutionOptions_AfterCommand_AreAccepted(string option, string? value)
    {
        var args = value == null
            ? new[] { "run", option }
            : new[] { "run", option, value };

        var result = await RunAsync(CreateApp(), args);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SemiInteractive", result.StdOut);
    }

    [Theory]
    [InlineData("--no-color", null)]
    [InlineData("--theme", "dark")]
    [InlineData("--culture", "en-US")]
    public async Task OtherExecutionOptions_BeforeCommand_AreRejected(string option, string? value)
    {
        var args = value == null
            ? new[] { option, "run" }
            : new[] { option, value, "run" };

        var result = await RunAsync(CreateApp(), args);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"Unknown option: '{option}'", result.StdErr);
    }

    [Fact]
    public async Task RootAndCommandInformationalForms_RemainValid()
    {
        var app = CreateApp();

        var rootHelp = await RunAsync(app, "--help");
        var rootVersion = await RunAsync(app, "--version");
        var commandHelp = await RunAsync(app, "run", "--help");
        var commandExitCodes = await RunAsync(app, "run", "--help-errors");

        Assert.Equal(0, rootHelp.ExitCode);
        Assert.Contains("placement-test <command> [options]", rootHelp.StdOut);
        Assert.DoesNotContain("placement-test [options] <command>", rootHelp.StdOut);
        Assert.Equal(0, rootVersion.ExitCode);
        Assert.Contains("placement-test version 1.0.0", rootVersion.StdOut);
        Assert.Equal(0, commandHelp.ExitCode);
        Assert.Contains("placement-test run", commandHelp.StdOut);
        Assert.Equal(0, commandExitCodes.ExitCode);
        Assert.Contains("No documented exit-code enum is configured.", commandExitCodes.StdOut);
    }

    [Fact]
    public async Task RootHelp_BeforeCommand_IsRejectedAsCommandHelp()
    {
        var result = await RunAsync(CreateApp(), "--help", "run");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--help'", result.StdErr);
        Assert.DoesNotContain("placement-test run", result.StdOut);
    }

    private static TigerCliApp CreateApp() => TigerCliApp.CreateBuilder()
        .SetApplicationName("placement-test")
        .SetVersion("1.0.0")
        .AddCommand<ModeCommand>("run")
        .AddCommand<PositionalModeCommand>("process")
        .AddCommand<ColorCommand>("color")
        .Build();

    private static Task<TigerCliAppRunResult> RunAsync(TigerCliApp app, params string[] args) =>
        TigerCliAppTestHost
            .For(app)
            .WithArgs(args)
            .RunAsync(TestContext.Current.CancellationToken);
}
