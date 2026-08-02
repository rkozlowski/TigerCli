using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Option placement for a default/root command. The command path is empty, so the grammar
/// <c>app &lt;command-path&gt; &lt;positional-arguments&gt; [options]</c> collapses to
/// <c>app &lt;positional-arguments&gt; [options]</c> and the options area may start at the first
/// token when the command declares no positionals.
/// </summary>
public sealed class TigerCliDefaultCommandOptionPlacementTests
{
    private sealed class RootSettings : TigerCliSettings
    {
    }

    private sealed class PositionalRootSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "input")]
        public string Input { get; set; } = string.Empty;
    }

    /// <summary>Reports the run state the framework options are expected to have configured.</summary>
    private sealed class RootCommand : TigerCliAsyncCommandHandler<RootSettings>
    {
        public override Task<int> ExecuteAsync(RootSettings settings)
        {
            TigerConsole.MarkupLine(
                $"ran|{TigerConsole.CurrentTheme.Name}|{TigerConsole.ColorMode}"
                + $"|{settings.Culture.Name}|{settings.InteractionMode}");
            return Task.FromResult(0);
        }
    }

    private sealed class PositionalRootCommand : TigerCliAsyncCommandHandler<PositionalRootSettings>
    {
        public override Task<int> ExecuteAsync(PositionalRootSettings settings)
        {
            TigerConsole.MarkupLine(
                $"ran|{settings.Input}|{TigerConsole.CurrentTheme.Name}|{TigerConsole.ColorMode}"
                + $"|{settings.Culture.Name}|{settings.InteractionMode}");
            return Task.FromResult(0);
        }
    }

    // ── Root command without required positionals ───────────────────

    [Fact]
    public async Task Help_IsAccepted()
    {
        var result = await RunAsync(CreateApp(), "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("root-test", result.StdOut);
        Assert.DoesNotContain("ran|", result.StdOut);
    }

    [Fact]
    public async Task ExitCodeHelp_IsAccepted()
    {
        var result = await RunAsync(CreateApp(), "--help-errors");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("ran|", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task EnvironmentHelp_IsAccepted()
    {
        var result = await RunAsync(CreateApp(), "--help-env");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ROOT_TEST_MODE", result.StdOut);
        Assert.DoesNotContain("ran|", result.StdOut);
    }

    [Fact]
    public async Task Version_IsAccepted()
    {
        var result = await RunAsync(CreateApp(), "--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("root-test version 1.0.0", result.StdOut);
    }

    [Fact]
    public async Task VersionFull_IsAccepted()
    {
        var result = await RunAsync(CreateApp(), "--version-full");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("root-test", result.StdOut);
        Assert.DoesNotContain("ran|", result.StdOut);
    }

    [Fact]
    public async Task Theme_IsAcceptedAndApplied()
    {
        var result = await RunAsync(CreateApp(), "--theme", "light");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran|light|", result.StdOut);
    }

    [Fact]
    public async Task Color_IsAcceptedAndApplied()
    {
        var result = await RunAsync(CreateApp(), "--color", "256");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"|{CliColorMode.Ansi256}|", result.StdOut);
    }

    [Fact]
    public async Task ColorNever_IsAcceptedAndApplied()
    {
        var result = await RunAsync(CreateApp(), "--color", "never");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"|{CliColorMode.Never}|", result.StdOut);
    }

    [Fact]
    public async Task NoColor_IsAcceptedAndApplied()
    {
        var result = await RunAsync(CreateApp(), "--no-color");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"|{CliColorMode.Never}|", result.StdOut);
    }

    [Fact]
    public async Task Culture_IsAcceptedAndApplied()
    {
        var result = await RunAsync(CreateApp(), "--culture", "pl-PL");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("|pl-PL|", result.StdOut);
    }

    [Fact]
    public async Task NonInteractive_IsAcceptedAndApplied()
    {
        var result = await RunAsync(CreateApp(), "--non-interactive");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"|{TigerCliInteractionMode.NonInteractive}", result.StdOut);
    }

    [Fact]
    public async Task Help_WithExecutionOption_IsAccepted()
    {
        var result = await RunAsync(CreateApp(), "--help", "--theme", "light");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("root-test", result.StdOut);
        Assert.DoesNotContain("ran|", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task ComposedHelp_WithExecutionOption_IsAccepted()
    {
        var result = await RunAsync(CreateApp(), "--help", "--help-env", "--theme", "light");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("root-test", result.StdOut);
        Assert.Contains("ROOT_TEST_MODE", result.StdOut);
        Assert.DoesNotContain("ran|", result.StdOut);
    }

    [Fact]
    public async Task FullyComposedHelp_WithExecutionOption_IsAccepted()
    {
        var result = await RunAsync(
            CreateApp(), "--help", "--help-errors", "--help-env", "--theme", "light");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("root-test", result.StdOut);
        Assert.Contains("ROOT_TEST_MODE", result.StdOut);
        Assert.DoesNotContain("ran|", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Version_WithExecutionOption_IsAccepted()
    {
        var result = await RunAsync(CreateApp(), "--version", "--theme", "light");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("root-test version 1.0.0", result.StdOut);
        Assert.DoesNotContain("ran|", result.StdOut);
    }

    // ── Root command with a required positional ─────────────────────

    [Theory]
    [InlineData("--theme", "light")]
    [InlineData("--color", "never")]
    [InlineData("--culture", "pl-PL")]
    public async Task OptionAfterRequiredPositional_IsAccepted(string option, string value)
    {
        var result = await RunAsync(CreatePositionalApp(), "input.txt", option, value);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran|input.txt|", result.StdOut);
    }

    [Fact]
    public async Task NonInteractiveAfterRequiredPositional_IsAccepted()
    {
        var result = await RunAsync(CreatePositionalApp(), "input.txt", "--non-interactive");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran|input.txt|", result.StdOut);
        Assert.Contains($"|{TigerCliInteractionMode.NonInteractive}", result.StdOut);
    }

    [Theory]
    [InlineData("--theme", "light")]
    [InlineData("--color", "never")]
    public async Task OptionBeforeRequiredPositional_IsRejected(string option, string value)
    {
        var result = await RunAsync(CreatePositionalApp(), option, value, "input.txt");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unexpected positional argument after options: input.txt", result.StdErr);
        Assert.DoesNotContain("ran|", result.StdOut);
    }

    /// <summary>
    /// The placement is still rejected, and the rejection is reported in the requested culture:
    /// the misplaced <c>--culture</c> option is claimed by the framework, only the positional
    /// following it is the grammar error.
    /// </summary>
    [Fact]
    public async Task CultureBeforeRequiredPositional_IsRejectedInRequestedCulture()
    {
        var result = await RunAsync(CreatePositionalApp(), "--culture", "pl-PL", "input.txt");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Nieoczekiwany argument pozycyjny po opcjach: input.txt", result.StdErr);
        Assert.DoesNotContain("ran|", result.StdOut);
    }

    [Fact]
    public async Task NonInteractiveBeforeRequiredPositional_IsRejected()
    {
        var result = await RunAsync(CreatePositionalApp(), "--non-interactive", "input.txt");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unexpected positional argument after options: input.txt", result.StdErr);
        Assert.DoesNotContain("ran|", result.StdOut);
    }

    /// <summary>
    /// The regression this suite guards: a default command that declares a positional must still
    /// recognize framework options, both on their own and combined with an informational option.
    /// </summary>
    [Fact]
    public async Task ExecutionOption_OnPositionalRootCommand_IsRecognized()
    {
        // Non-interactive so the missing positional fails instead of prompting for it: the point of
        // the assertion is that the framework option is claimed rather than rejected as unknown.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("positional-root-test")
            .SetInteractionMode(TigerCliInteractionMode.NonInteractive)
            .SetDefaultCommand<PositionalRootCommand>()
            .Build();

        var themeOnly = await RunAsync(app, "--theme", "light");
        var helpWithTheme = await RunAsync(app, "--help", "--theme", "light");

        Assert.DoesNotContain("Unknown option: '--theme'", themeOnly.StdErr);
        Assert.Contains("Missing required argument: <input>", themeOnly.StdErr);
        Assert.Equal(0, helpWithTheme.ExitCode);
        Assert.DoesNotContain("Unknown option: '--help'", helpWithTheme.StdErr);
        Assert.Contains("positional-root-test", helpWithTheme.StdOut);
    }

    private static TigerCliApp CreateApp() => TigerCliApp.CreateBuilder()
        .SetApplicationName("root-test")
        .SetVersion("1.0.0")
        .SetSupportedCultures("en-US", "pl-PL")
        .AddEnvironmentVariable("ROOT_TEST_MODE", "Selects the root test mode.")
        .SetDefaultCommand<RootCommand>()
        .Build();

    private static TigerCliApp CreatePositionalApp() => TigerCliApp.CreateBuilder()
        .SetApplicationName("positional-root-test")
        .SetVersion("1.0.0")
        .SetSupportedCultures("en-US", "pl-PL")
        .SetDefaultCommand<PositionalRootCommand>()
        .Build();

    private static Task<TigerCliAppRunResult> RunAsync(TigerCliApp app, params string[] args) =>
        TigerCliAppTestHost
            .For(app)
            .WithArgs(args)
            .RunAsync(TestContext.Current.CancellationToken);
}
