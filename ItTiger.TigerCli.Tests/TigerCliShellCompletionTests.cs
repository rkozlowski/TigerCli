using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Opt-in shell completion: the static, metadata-backed completion engine, the
/// <c>--tigercli-complete</c> request protocol, and the <c>--completion &lt;shell&gt;</c> scripts.
/// </summary>
public sealed class TigerCliShellCompletionTests
{
    private const string RequestToken = "--tigercli-complete";

    // ── Settings and commands ───────────────────────────────────────

    private enum DeployMode
    {
        Unspecified,
        Fast,
        Careful
    }

    private sealed class DeploySettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "target", Description = "Deployment target.")]
        public string Target { get; set; } = string.Empty;

        [TigerCliOption("-m|--mode", ForbiddenValues = new object[] { DeployMode.Unspecified }, Description = "Deployment [Key]mode[/].")]
        public DeployMode Mode { get; set; } = DeployMode.Fast;

        [TigerCliOption("--dry-run", Description = "Preview only.")]
        public bool DryRun { get; set; }

        [TigerCliOption("--verify")]
        public bool? Verify { get; set; }

        [TigerCliOption("--tag")]
        public string[] Tags { get; set; } = [];

        [TigerCliOption("--var")]
        public List<KeyValuePair<string, string>> Variables { get; set; } = [];

        [TigerCliOption("--region", Provider = "regions")]
        public string? Region { get; set; }

        [TigerCliOption("--password", Secret = true, AllowCommandLineValue = false, Promptable = TigerCliPromptable.Normal)]
        public string? Password { get; set; }

        [TigerCliOption("--config")]
        [TigerCliFileOpen]
        public string? Config { get; set; }

        [TigerCliOption("--output-dir")]
        [TigerCliFolderSelect]
        public string? OutputDir { get; set; }

        [TigerCliOption("--retries")]
        public int Retries { get; set; }
    }

    private sealed class ExecSettings : TigerCliSettings
    {
        [TigerCliOption("-c|--connection", Description = "Connection name.")]
        public string Connection { get; set; } = "default";

        [TigerCliRawArguments(Name = "tool-args", Description = "Child tool and its arguments.")]
        public IReadOnlyList<string> Arguments { get; init; } = [];
    }

    private sealed class LevelSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "level")]
        public DeployMode Level { get; set; }

        [TigerCliArgument(1, Name = "note")]
        public string Note { get; set; } = string.Empty;
    }

    private sealed class EmptySettings : TigerCliSettings
    {
    }

    private sealed class ReservedOptionSettings : TigerCliSettings
    {
        [TigerCliOption("--completion")]
        public string? Completion { get; set; }
    }

    private sealed class DeployCommand : TigerCliAsyncCommandHandler<DeploySettings>
    {
        public override Task<int> ExecuteAsync(DeploySettings settings) => Task.FromResult(0);
    }

    private sealed class ExecCommand : TigerCliAsyncCommandHandler<ExecSettings>
    {
        public override Task<int> ExecuteAsync(ExecSettings settings) => Task.FromResult(0);
    }

    private sealed class LevelCommand : TigerCliAsyncCommandHandler<LevelSettings>
    {
        public override Task<int> ExecuteAsync(LevelSettings settings) => Task.FromResult(0);
    }

    private sealed class NoopCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings) => Task.FromResult(0);
    }

    private sealed class ReservedOptionCommand : TigerCliAsyncCommandHandler<ReservedOptionSettings>
    {
        public override Task<int> ExecuteAsync(ReservedOptionSettings settings) => Task.FromResult(0);
    }

    private sealed class Contribution(Action<TigerCliAppContributionBuilder> configure) : ITigerCliAppContribution
    {
        public void Configure(TigerCliAppContributionBuilder builder) => configure(builder);
    }

    /// <summary>Counts every app callback completion must never reach.</summary>
    private sealed class SideEffects
    {
        public int Factories;
        public int Handlers;
        public int Providers;
        public int GlobalOptionApplies;
        public int Total => Factories + Handlers + Providers + GlobalOptionApplies;
    }

    private static TigerCliApp CreateApp(SideEffects? sideEffects = null, Action<TigerCliAppBuilder>? configure = null)
    {
        sideEffects ??= new SideEffects();
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("my-tool")
            .SetVersion("1.0.0")
            .UseShellCompletion()
            .AddContribution(new Contribution(contribution => contribution.GlobalOptions.AddOptionalString(
                "--profile",
                "name",
                "Library [Key]profile[/].",
                (_, _) =>
                {
                    sideEffects.GlobalOptionApplies++;
                    return TigerCliValidationResult.Success();
                })))
            .AddCommand(
                "deploy",
                () =>
                {
                    sideEffects.Factories++;
                    return new DeployCommand();
                },
                "Deploys a [Key]target[/].")
            .AddCommand<ExecCommand>("exec", "Runs a child tool.")
            .AddCommand<LevelCommand>("level", "Sets a level.")
            .AddCommand("ping", () =>
            {
                sideEffects.Handlers++;
                return 0;
            }, "Checks connectivity.")
            .AddCommandGroup("widgets", group => group
                .SetDescription("Manage widgets.")
                .AddCommand<NoopCommand>("list", "List widgets.")
                .AddCommand<NoopCommand>("add", "Add a widget.")
                .AddCommandGroup("parts", parts => parts
                    .SetDescription("Manage widget parts.")
                    .AddCommand<NoopCommand>("remove", "Remove a part.")))
            .AddCommandAlias("ship", "deploy", alias => alias.SetDescription("Ship a target."))
            .AddCommandAlias("stealth-ship", "deploy", alias => alias.HideFromHelp())
            .UseCommandMenu(CommandMenuMode.Enabled, commandName: "menu")
            .ConfigureProviders(providers => providers.Add<string>("regions", _ =>
            {
                sideEffects.Providers++;
                return [new OptionItem<string>("eu", "Europe")];
            }));

        configure?.Invoke(builder);
        return builder.Build();
    }

    // The last word is the word being completed; the others precede it on the command line.
    private static TigerCliCompletionResult Complete(TigerCliApp app, params string[] line) =>
        app.Complete(line[..^1], line[^1]);

    private static string[] Values(TigerCliApp app, params string[] line) =>
        Complete(app, line).Candidates.Select(candidate => candidate.Value).ToArray();

    private static Task<TigerCliAppRunResult> RunAsync(TigerCliApp app, params string[] args) =>
        TigerCliAppTestHost
            .For(app)
            .WithArgs(args)
            .RunAsync(TestContext.Current.CancellationToken);

    // ── Command paths ───────────────────────────────────────────────

    [Fact]
    public void Root_OffersCommandsGroupsAndVisibleAliases()
    {
        var values = Values(CreateApp(), "");

        Assert.Equal(["deploy", "exec", "level", "ping", "widgets", "ship"], values);
    }

    [Fact]
    public void Root_FiltersByPrefixCaseInsensitively()
    {
        Assert.Equal(["deploy"], Values(CreateApp(), "DE"));
    }

    [Fact]
    public void Root_DoesNotOfferHiddenAliasesOrCommandMenu()
    {
        var app = CreateApp();

        Assert.Empty(Values(app, "stealth"));
        Assert.Empty(Values(app, "men"));
    }

    [Fact]
    public void Group_OffersImmediateChildCommandsAndSubgroups()
    {
        var app = CreateApp();

        Assert.Equal(["list", "add", "parts"], Values(app, "widgets", ""));
        Assert.Equal(["remove"], Values(app, "widgets", "parts", ""));
    }

    [Fact]
    public void Candidates_CarryPlainTextDescriptions()
    {
        var candidates = Complete(CreateApp(), "").Candidates;

        Assert.Equal("Deploys a target.", candidates.Single(c => c.Value == "deploy").Description);
        Assert.Equal(TigerCliCompletionKind.Group, candidates.Single(c => c.Value == "widgets").Kind);
        Assert.Equal("Ship a target.", candidates.Single(c => c.Value == "ship").Description);
        Assert.Equal(TigerCliCompletionKind.Alias, candidates.Single(c => c.Value == "ship").Kind);
    }

    [Fact]
    public void Group_AcceptsOnlyInformationalOptions()
    {
        var values = Values(CreateApp(), "widgets", "-");

        Assert.Equal(["-h", "--help", "--help-errors", "--help-env"], values);
    }

    // ── Root informational forms ────────────────────────────────────

    [Fact]
    public void Root_OptionWord_OffersRootInformationalOptions()
    {
        var values = Values(CreateApp(), "--");

        Assert.Contains("--help", values);
        Assert.Contains("--version", values);
        Assert.Contains("--version-full", values);
        Assert.Contains("--completion", values);
        Assert.DoesNotContain("--mode", values);
    }

    [Fact]
    public void Root_VersionOptionsNeedVersionMetadata()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("bare")
            .UseShellCompletion()
            .AddCommand<NoopCommand>("run")
            .Build();

        var values = Values(app, "--");

        Assert.DoesNotContain("--version", values);
        Assert.Contains("--completion", values);
    }

    [Fact]
    public void CompletionOption_OffersShellNamesAndIsExclusive()
    {
        var app = CreateApp();

        Assert.Equal(["bash", "zsh", "fish", "pwsh"], Values(app, "--completion", ""));
        Assert.Equal(["--completion=fish"], Values(app, "--completion=f"));
        Assert.DoesNotContain("--completion", Values(app, "--help", "--"));
    }

    // ── Command options and positionals ─────────────────────────────

    [Fact]
    public void Command_PositionalSlot_DoesNotOfferOptionsForEmptyWord()
    {
        var result = Complete(CreateApp(), "deploy", "");

        Assert.Empty(result.Candidates);
        Assert.Equal(TigerCliCompletionFallback.Files, result.Fallback);
    }

    [Fact]
    public void Command_OptionWord_OffersCommandFrameworkAndGlobalOptions()
    {
        var values = Values(CreateApp(), "deploy", "prod", "-");

        Assert.Contains("-m", values);
        Assert.Contains("--mode", values);
        Assert.Contains("--dry-run", values);
        Assert.Contains("--profile", values);
        Assert.Contains("--non-interactive", values);
        Assert.Contains("--theme", values);
        Assert.Contains("--color", values);
        Assert.Contains("--no-color", values);
        Assert.Contains("--culture", values);
        Assert.Contains("--help", values);
        Assert.DoesNotContain("--version", values);
        Assert.DoesNotContain("--completion", values);
    }

    [Fact]
    public void Command_PromptOnlyOptionsAreNotOffered()
    {
        Assert.DoesNotContain("--password", Values(CreateApp(), "deploy", "prod", "--"));
    }

    [Fact]
    public void Command_OptionsAreValidBeforeMissingPositionals()
    {
        Assert.Contains("--mode", Values(CreateApp(), "deploy", "--"));
    }

    [Fact]
    public void Command_AfterPositionals_EmptyWordOffersOptions()
    {
        var result = Complete(CreateApp(), "deploy", "prod", "");

        Assert.Contains("--mode", result.Candidates.Select(c => c.Value));
        Assert.Equal(TigerCliCompletionFallback.None, result.Fallback);
    }

    [Fact]
    public void Command_UsedScalarOptionsAreNotOfferedAgain_RepeatableOnesAre()
    {
        var values = Values(CreateApp(), "deploy", "prod", "--mode", "Fast", "--tag", "a", "--");

        Assert.DoesNotContain("--mode", values);
        Assert.Contains("--tag", values);
    }

    [Fact]
    public void Command_OptionDescriptionsArePlainText()
    {
        var candidate = Complete(CreateApp(), "deploy", "prod", "--mo").Candidates.Single();

        Assert.Equal("--mode", candidate.Value);
        Assert.Equal("Deployment mode.", candidate.Description);
    }

    [Fact]
    public void Command_PositionalAfterOptions_IsInvalid()
    {
        var result = Complete(CreateApp(), "deploy", "--dry-run", "prod", "--");

        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Command_UnknownOption_IsInvalid()
    {
        Assert.Empty(Values(CreateApp(), "deploy", "prod", "--bogus", "--"));
    }

    [Fact]
    public void Command_NonOptionWordInOptionsArea_OffersNothing()
    {
        Assert.Empty(Values(CreateApp(), "deploy", "prod", "--dry-run", "x"));
    }

    [Fact]
    public void EnumPositional_OffersMemberNames()
    {
        var app = CreateApp();

        Assert.Equal(["Unspecified", "Fast", "Careful"], Values(app, "level", ""));
        Assert.Equal(TigerCliCompletionFallback.Files, Complete(app, "level", "Fast", "").Fallback);
    }

    [Fact]
    public void Alias_CompletesItsTargetCommand()
    {
        Assert.Contains("--mode", Values(CreateApp(), "ship", "prod", "--"));
    }

    [Fact]
    public void CommandMenu_OffersNoNonInteractiveOption()
    {
        var values = Values(CreateApp(), "menu", "-");

        Assert.Contains("--theme", values);
        Assert.Contains("--help", values);
        Assert.DoesNotContain("--non-interactive", values);
    }

    // ── Option values ───────────────────────────────────────────────

    [Fact]
    public void EnumOption_OffersAllowedMemberNames()
    {
        var app = CreateApp();

        Assert.Equal(["Fast", "Careful"], Values(app, "deploy", "prod", "--mode", ""));
        Assert.Equal(["Careful"], Values(app, "deploy", "prod", "-m", "c"));
    }

    [Fact]
    public void InlineOptionValue_IsCompletedAsOneWord()
    {
        Assert.Equal(["--mode=Fast"], Values(CreateApp(), "deploy", "prod", "--mode=f"));
    }

    [Fact]
    public void NullableBoolOption_OffersTrueAndFalse()
    {
        Assert.Equal(["true", "false"], Values(CreateApp(), "deploy", "prod", "--verify", ""));
    }

    [Fact]
    public void FrameworkOptions_OfferThemeColorAndCultureValues()
    {
        var app = CreateApp();

        Assert.Contains("light", Values(app, "deploy", "prod", "--theme", ""));
        Assert.Equal(["auto", "never", "16", "256"], Values(app, "deploy", "prod", "--color", ""));
        Assert.Equal(["en-US"], Values(app, "deploy", "prod", "--culture", ""));
    }

    [Theory]
    [InlineData("--config", "Files")]
    [InlineData("--output-dir", "Directories")]
    [InlineData("--tag", "Files")]
    [InlineData("--profile", "Files")]
    [InlineData("--retries", "None")]
    [InlineData("--region", "None")]
    public void FreeFormOptionValues_UseTheShellFallback(string option, string expected)
    {
        var result = Complete(CreateApp(), "deploy", "prod", option, "");

        Assert.Empty(result.Candidates);
        Assert.Equal(Enum.Parse<TigerCliCompletionFallback>(expected), result.Fallback);
    }

    [Fact]
    public void KeyValueOption_ConsumesKeyAndValueTokens()
    {
        var app = CreateApp();

        Assert.Empty(Values(app, "deploy", "prod", "--var", "env", ""));
        Assert.Contains("--mode", Values(app, "deploy", "prod", "--var", "env", "dev", "--"));
        Assert.Contains("--mode", Values(app, "deploy", "prod", "--var=env=dev", "--"));
    }

    // ── Raw trailing arguments ──────────────────────────────────────

    [Fact]
    public void RawTailCommand_OffersSeparatorAsOption()
    {
        var app = CreateApp();

        Assert.Contains("--", Values(app, "exec", "--"));
        Assert.DoesNotContain("--", Values(app, "deploy", "prod", "--"));
    }

    [Fact]
    public void RawTail_IsNeverCompletedAsTigerCliInput()
    {
        var result = Complete(CreateApp(), "exec", "--", "child", "--c");

        Assert.Empty(result.Candidates);
        Assert.Equal(TigerCliCompletionFallback.Files, result.Fallback);
    }

    [Fact]
    public void SeparatorOnCommandWithoutTail_OffersNothing()
    {
        var result = Complete(CreateApp(), "deploy", "prod", "--", "");

        Assert.Empty(result.Candidates);
        Assert.Equal(TigerCliCompletionFallback.None, result.Fallback);
    }

    // ── Default command ─────────────────────────────────────────────

    [Fact]
    public void DefaultCommandWithoutPositionals_OptionsAreValidAtRoot()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("single")
            .UseShellCompletion()
            .SetDefaultCommand<ExecCommand>()
            .Build();

        var values = Values(app, "-");

        Assert.Contains("--connection", values);
        Assert.Contains("--theme", values);
        Assert.Contains("--help", values);
        Assert.Contains("--completion", values);
        Assert.Contains("--", values);
    }

    [Fact]
    public void DefaultCommandWithPositional_RootOffersCommandsAndPositionalValues()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("leveler")
            .UseShellCompletion()
            .SetDefaultCommand<LevelCommand>()
            .AddCommand<NoopCommand>("status")
            .Build();

        Assert.Equal(["status", "Unspecified", "Fast", "Careful"], Values(app, ""));
        Assert.Equal(["Fast"], Values(app, "f"));
    }

    // ── No side effects ─────────────────────────────────────────────

    [Theory]
    [InlineData("deploy", "prod", "--region", "")]
    [InlineData("deploy", "prod", "--profile", "x", "--")]
    [InlineData("ping", "")]
    [InlineData("")]
    public async Task CompletionRequest_InvokesNoFactoriesHandlersProvidersOrContributions(params string[] line)
    {
        var sideEffects = new SideEffects();
        var app = CreateApp(sideEffects);

        var result = await RunAsync(app, [RequestToken, "bash", .. line.Select(word => "_" + word)]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, sideEffects.Total);
    }

    // ── Protocol ────────────────────────────────────────────────────

    [Fact]
    public async Task CompletionRequest_Bash_WritesValuesAndFallbackLine()
    {
        var result = await RunAsync(CreateApp(), RequestToken, "bash", "_de");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("deploy\n:none\n", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    private sealed class CompletionOnlyTheme : ThemeBase
    {
        public override string Name => "completion-boundary-test-theme";

        protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
        protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.Cyan));
        protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
        protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
    }

    [Fact]
    public async Task CompletionRequest_OffersCustomThemesWithoutRegisteringThem()
    {
        var app = CreateApp(configure: builder => builder.ConfigureThemes(themes => themes
            .AddTheme(new CompletionOnlyTheme())
            .DisableTheme("tiger-blue")));

        var result = await RunAsync(app, RequestToken, "bash", "_deploy", "_prod", "_--theme", "_");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("completion-boundary-test-theme\n", result.StdOut);
        Assert.Contains("light\n", result.StdOut);
        Assert.DoesNotContain("tiger-blue", result.StdOut);
        // A completion request reads the app's theme configuration; it never applies it to the
        // process-wide theme registry.
        Assert.DoesNotContain("completion-boundary-test-theme", TigerConsole.GetThemeNames());
    }

    [Fact]
    public async Task CompletionRequest_FishAndPwsh_WriteTabSeparatedDescriptions()
    {
        var fish = await RunAsync(CreateApp(), RequestToken, "fish", "_de");
        var pwsh = await RunAsync(CreateApp(), RequestToken, "pwsh", "_de");

        Assert.Equal("deploy\tDeploys a target.\n:none\n", fish.StdOut);
        Assert.Equal(fish.StdOut, pwsh.StdOut);
    }

    [Fact]
    public async Task CompletionRequest_Zsh_EscapesColonsInValues()
    {
        var response = TigerCliCompletionProtocol.FormatResponse(
            TigerCliCompletionShell.Zsh,
            new TigerCliCompletionResult(
                [new TigerCliCompletionCandidate("a:b", "Desc", TigerCliCompletionKind.Value)],
                TigerCliCompletionFallback.Files));

        Assert.Equal("a\\:b:Desc\n:files\n", response);

        var result = await RunAsync(CreateApp(), RequestToken, "zsh", "_de");
        Assert.Equal("deploy:Deploys a target.\n:none\n", result.StdOut);
    }

    [Fact]
    public async Task CompletionRequest_EmptyCurrentWordAndSeparatorSurviveTransport()
    {
        var result = await RunAsync(CreateApp(), RequestToken, "bash", "_exec", "_--", "_");

        Assert.Equal(":files\n", result.StdOut);
    }

    [Fact]
    public async Task CompletionRequest_UnknownShell_IsRejectedWithoutOutput()
    {
        var result = await RunAsync(CreateApp(), RequestToken, "tcsh", "_");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Empty(result.StdOut);
    }

    // ── Scripts ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("bash", "complete -F _tigercli_complete_my_tool 'my-tool'")]
    [InlineData("zsh", "compdef _my-tool 'my-tool'")]
    [InlineData("fish", "complete -c 'my-tool' -f -a '(__tigercli_complete_my_tool)'")]
    [InlineData("pwsh", "Register-ArgumentCompleter -Native -CommandName @('my-tool', 'my-tool.exe')")]
    [InlineData("powershell", "Register-ArgumentCompleter -Native -CommandName @('my-tool', 'my-tool.exe')")]
    public async Task CompletionOption_PrintsLfOnlyActivationScript(string shell, string registration)
    {
        var result = await RunAsync(CreateApp(), "--completion", shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(registration, result.StdOut);
        Assert.Contains(RequestToken, result.StdOut);
        Assert.DoesNotContain('\r', result.StdOut);
        Assert.DoesNotContain("{{", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task CompletionOption_AcceptsInlineShell()
    {
        var result = await RunAsync(CreateApp(), "--completion=bash");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("complete -F", result.StdOut);
    }

    [Fact]
    public async Task CompletionOption_UnknownShell_IsUsageError()
    {
        var result = await RunAsync(CreateApp(), "--completion", "tcsh");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Empty(result.StdOut);
        Assert.Contains("Unsupported --completion shell 'tcsh'. Available: bash, zsh, fish, pwsh.", result.StdErr);
    }

    [Fact]
    public async Task CompletionOption_MissingShell_IsUsageError()
    {
        var result = await RunAsync(CreateApp(), "--completion");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Option '--completion' requires a value.", result.StdErr);
    }

    [Fact]
    public async Task CompletionOption_IsRootOnly()
    {
        var result = await RunAsync(CreateApp(), "ping", "--completion", "bash");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--completion'", result.StdErr);
    }

    [Fact]
    public async Task Help_ListsCompletionOptionWhenEnabled()
    {
        var result = await RunAsync(CreateApp(), "--help");

        Assert.Contains("--completion <shell>", result.StdOut);
        Assert.Contains("bash | zsh | fish | pwsh", result.StdOut);
    }

    // ── Opt-in and reserved names ───────────────────────────────────

    [Fact]
    public async Task Disabled_CompletionEntryPointsAreOrdinaryUnknownOptions()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("plain")
            .AddCommand<NoopCommand>("run")
            .Build();

        var request = await RunAsync(app, RequestToken, "bash", "_");
        var script = await RunAsync(app, "--completion", "bash");
        var help = await RunAsync(app, "--help");

        Assert.NotEqual(0, request.ExitCode);
        Assert.DoesNotContain(":none", request.StdOut);
        Assert.NotEqual(0, script.ExitCode);
        Assert.DoesNotContain("complete -F", script.StdOut);
        Assert.DoesNotContain("--completion", help.StdOut);
    }

    [Fact]
    public void Build_RejectsCommandOptionUsingReservedCompletionName()
    {
        var builder = TigerCliApp.CreateBuilder()
            .UseShellCompletion()
            .AddCommand<ReservedOptionCommand>("run");

        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("'--completion'", error.Message);
    }

    [Fact]
    public void Build_AllowsReservedNameWhenCompletionIsDisabled()
    {
        var app = TigerCliApp.CreateBuilder()
            .AddCommand<ReservedOptionCommand>("run")
            .Build();

        Assert.NotNull(app);
    }

    [Fact]
    public void Build_RejectsGlobalOptionUsingReservedCompletionName()
    {
        var builder = TigerCliApp.CreateBuilder()
            .UseShellCompletion()
            .AddContribution(new Contribution(contribution => contribution.GlobalOptions.AddOptionalString(
                "--completion",
                "value",
                "Description.",
                (_, _) => TigerCliValidationResult.Success())))
            .AddCommand<NoopCommand>("run");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void MarkupToPlainText_DropsTagsAndUnescapesBrackets()
    {
        Assert.Equal(
            "Use [brackets] and bold.",
            TigerCliCompletionProtocol.MarkupToPlainText("Use [[brackets]] and [b]bold[/]."));
    }
}
