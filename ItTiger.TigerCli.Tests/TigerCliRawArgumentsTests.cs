using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Command-declared raw trailing arguments: the first <c>--</c> splits the command line, TigerCli
/// parses only the head, and the tail binds verbatim to a command that opted in with
/// <c>[TigerCliRawArguments]</c>.
/// </summary>
public sealed class TigerCliRawArgumentsTests
{
    private const string Separator = "--";

    // ── Settings and commands ───────────────────────────────────────

    private sealed class ExecSettings : TigerCliSettings
    {
        [TigerCliOption("-c|--connection", Description = "Connection name.")]
        public string Connection { get; set; } = "default";

        [TigerCliRawArguments(Name = "tool-args", Description = "Child tool and its arguments.")]
        public IReadOnlyList<string> Arguments { get; init; } = [];
    }

    private sealed class RequiredTailSettings : TigerCliSettings
    {
        [TigerCliRawArguments(Name = "tool-args", Required = true)]
        public IReadOnlyList<string> Arguments { get; init; } = [];
    }

    private sealed class SelectorTailSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "target", Description = "Target name.")]
        public string Target { get; set; } = string.Empty;

        [TigerCliOption("--schema")]
        public string Schema { get; set; } = "dbo";

        [TigerCliRawArguments(Name = "tool-args")]
        public string[] Arguments { get; init; } = [];
    }

    private sealed class ListTailSettings : TigerCliSettings
    {
        [TigerCliRawArguments]
        public List<string> ToolArguments { get; init; } = [];
    }

    private sealed class NoTailSettings : TigerCliSettings
    {
        [TigerCliOption("--message")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class PromptedTailSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Promptable = TigerCliPromptable.Normal)]
        public string Name { get; set; } = string.Empty;

        [TigerCliRawArguments(Name = "tool-args")]
        public IReadOnlyList<string> Arguments { get; init; } = [];
    }

    private sealed class ExecCommand : TigerCliAsyncCommandHandler<ExecSettings>
    {
        public override Task<int> ExecuteAsync(ExecSettings settings)
        {
            WriteTail(settings.Connection, settings.Arguments);
            WriteMode(settings);
            return Task.FromResult(0);
        }
    }

    private sealed class RequiredTailCommand : TigerCliAsyncCommandHandler<RequiredTailSettings>
    {
        public override Task<int> ExecuteAsync(RequiredTailSettings settings)
        {
            WriteTail("required", settings.Arguments);
            return Task.FromResult(0);
        }
    }

    private sealed class SelectorTailCommand : TigerCliAsyncCommandHandler<SelectorTailSettings>
    {
        public override Task<int> ExecuteAsync(SelectorTailSettings settings)
        {
            WriteTail($"{settings.Target}/{settings.Schema}", settings.Arguments);
            return Task.FromResult(0);
        }
    }

    private sealed class ListTailCommand : TigerCliAsyncCommandHandler<ListTailSettings>
    {
        public override Task<int> ExecuteAsync(ListTailSettings settings)
        {
            WriteTail("list", settings.ToolArguments);
            return Task.FromResult(0);
        }
    }

    private sealed class NoTailCommand : TigerCliAsyncCommandHandler<NoTailSettings>
    {
        public override Task<int> ExecuteAsync(NoTailSettings settings)
        {
            TigerConsole.MarkupLine($"ran:{CliMarkupParser.Escape(settings.Message)}");
            return Task.FromResult(0);
        }
    }

    private sealed class PromptedTailCommand : TigerCliAsyncCommandHandler<PromptedTailSettings>
    {
        public override Task<int> ExecuteAsync(PromptedTailSettings settings)
        {
            WriteTail(settings.Name, settings.Arguments);
            return Task.FromResult(0);
        }
    }

    // Tail tokens are printed inside pipes so an empty tail and a tail holding an empty string are
    // distinguishable, and so markup-significant tokens stay literal in the captured output.
    private static void WriteTail(string prefix, IReadOnlyList<string> tail)
    {
        // The whole line is escaped once, so the delimiting brackets and any markup-significant
        // token survive as literal text in the captured output.
        var rendered = string.Join("|", tail);
        TigerConsole.MarkupLine(
            CliMarkupParser.Escape($"{prefix} count={tail.Count} [{rendered}]"));
    }

    private static void WriteMode(TigerCliSettings settings) =>
        TigerConsole.MarkupLine($"mode={settings.InteractionMode}");

    // ── Binding fidelity ────────────────────────────────────────────

    [Fact]
    public async Task Tail_BindsTokensAfterSeparator()
    {
        var result = await RunAsync(CreateApp(), "exec", Separator, "my-tool", "--report");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("default count=2 [my-tool|--report]", result.StdOut);
    }

    [Fact]
    public async Task Tail_PreservesOptionLikeTokensOrderAndDuplicates()
    {
        var result = await RunAsync(
            CreateApp(), "exec", Separator, "-x", "--flag", "-x", "--target={connection-string}");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("count=4 [-x|--flag|-x|--target={connection-string}]", result.StdOut);
    }

    [Fact]
    public async Task Tail_PreservesLaterSeparatorsAsData()
    {
        var result = await RunAsync(CreateApp(), "exec", Separator, "my-tool", Separator, "inner");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("count=3 [my-tool|--|inner]", result.StdOut);
    }

    [Fact]
    public async Task Tail_PreservesEmptyStringTokens()
    {
        var result = await RunAsync(CreateApp(), "exec", Separator, "my-tool", string.Empty, "tail");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("count=3 [my-tool||tail]", result.StdOut);
    }

    [Fact]
    public async Task Tail_BindsToListAndArrayProperties()
    {
        var listResult = await RunAsync(CreateApp(), "list-tail", Separator, "a", "b");
        var arrayResult = await RunAsync(CreateApp(), "process", "target", Separator, "a", "b");

        Assert.Equal(0, listResult.ExitCode);
        Assert.Contains("list count=2 [a|b]", listResult.StdOut);
        Assert.Equal(0, arrayResult.ExitCode);
        Assert.Contains("target/dbo count=2 [a|b]", arrayResult.StdOut);
    }

    // ── The head still parses normally ──────────────────────────────

    [Fact]
    public async Task OptionsBeforeSeparator_StillBind()
    {
        var result = await RunAsync(CreateApp(), "exec", "--connection", "prod", Separator, "my-tool");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("prod count=1 [my-tool]", result.StdOut);
    }

    [Fact]
    public async Task PositionalsAndOptionsBeforeSeparator_StillBind()
    {
        var result = await RunAsync(
            CreateApp(), "process", "target", "--schema", "sales", Separator, "child", "--arg");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("target/sales count=2 [child|--arg]", result.StdOut);
    }

    [Fact]
    public async Task FrameworkOptionsBeforeSeparator_StillApply()
    {
        var result = await RunAsync(
            CreateApp(), "exec", "--non-interactive", Separator, "my-tool");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("mode=NonInteractive", result.StdOut);
        Assert.Contains("count=1 [my-tool]", result.StdOut);
    }

    [Fact]
    public async Task FrameworkOptionsAfterSeparator_StayInTail()
    {
        var result = await RunAsync(
            CreateApp(), "exec", Separator, "my-tool", "--non-interactive", "--theme", "dark", "--no-color");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("count=5 [my-tool|--non-interactive|--theme|dark|--no-color]", result.StdOut);
        // The framework must not have claimed any of those tokens for itself.
        Assert.Contains("mode=SemiInteractive", result.StdOut);
    }

    [Fact]
    public async Task ContributedGlobalOptionAfterSeparator_StaysInTail()
    {
        string? applied = null;
        var app = CreateApp(builder => builder.AddContribution(
            new GlobalOptionContribution(value =>
            {
                applied = value;
                return TigerCliValidationResult.Success();
            })));

        var result = await RunAsync(app, "exec", Separator, "my-tool", "--acme-config", "settings.json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("count=3 [my-tool|--acme-config|settings.json]", result.StdOut);
        Assert.Null(applied);
    }

    [Fact]
    public async Task ContributedGlobalOptionBeforeSeparator_StillBinds()
    {
        string? applied = null;
        var app = CreateApp(builder => builder.AddContribution(
            new GlobalOptionContribution(value =>
            {
                applied = value;
                return TigerCliValidationResult.Success();
            })));

        var result = await RunAsync(app, "exec", "--acme-config", "settings.json", Separator, "my-tool");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("count=1 [my-tool]", result.StdOut);
        Assert.Equal("settings.json", applied);
    }

    [Fact]
    public async Task PositionalAfterSeparator_IsTailNotPositional()
    {
        // `target` is consumed by the tail, so the command's required positional is still missing.
        var result = await RunAsync(
            CreateNonInteractiveApp(), "process", Separator, "target", "extra");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Missing required argument: <target>", result.StdErr);
    }

    [Fact]
    public async Task StrictOptionPlacementBeforeSeparator_IsUnchanged()
    {
        var result = await RunAsync(
            CreateApp(), "process", "--schema", "sales", "target", Separator, "child");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unexpected positional argument after options: target", result.StdErr);
    }

    [Fact]
    public async Task SeparatorAsOptionValue_ReportsMissingOptionValue()
    {
        // The separator is consumed before option parsing, so it can never be an option's value.
        var result = await RunAsync(CreateApp(), "exec", "--connection", Separator);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Option '--connection' requires a value.", result.StdErr);
    }

    [Fact]
    public async Task InlineOptionValue_CanBeTheDoubleDashToken()
    {
        // `--connection=--` is a single token, not the separator, so the documented escape works.
        var result = await RunAsync(CreateApp(), "exec", "--connection=--", Separator, "child");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("-- count=1 [child]", result.StdOut);
    }

    // ── Opt-in enforcement ──────────────────────────────────────────

    [Fact]
    public async Task CommandWithoutDeclaration_RejectsTail()
    {
        var result = await RunAsync(CreateApp(), "plain", "--message", "hi", Separator, "extra");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Arguments after '--' are not supported here.", result.StdErr);
        Assert.Contains("raw-test plain --help", result.StdErr);
        Assert.DoesNotContain("ran:", result.StdOut);
    }

    [Fact]
    public async Task CommandWithoutDeclaration_RejectsEmptyTail()
    {
        var result = await RunAsync(CreateApp(), "plain", Separator);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Arguments after '--' are not supported here.", result.StdErr);
    }

    [Fact]
    public async Task GroupPrefixWithTail_IsUsageError()
    {
        var result = await RunAsync(CreateApp(), "tools", Separator, "child");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Arguments after '--' are not supported here.", result.StdErr);
        Assert.Contains("raw-test tools --help", result.StdErr);
    }

    [Fact]
    public async Task GroupChildCommand_AcceptsTail()
    {
        var result = await RunAsync(CreateApp(), "tools", "run", Separator, "child", "--arg");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("list count=2 [child|--arg]", result.StdOut);
    }

    [Fact]
    public async Task NoCommandResolvedWithTail_IsUsageError()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("raw-test")
            .AddCommand<NoTailCommand>("plain")
            .Build();

        var result = await RunAsync(app, Separator, "child");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Arguments after '--' are not supported here.", result.StdErr);
    }

    [Fact]
    public async Task CommandMenuWithTail_IsUsageError()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("raw-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<ExecCommand>("exec")
            .Build();

        var result = await RunAsync(app, Separator, "child");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Arguments after '--' are not supported here.", result.StdErr);
    }

    // ── Default / root command ──────────────────────────────────────

    [Fact]
    public async Task DefaultCommand_AcceptsTail()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("raw-test")
            .SetDefaultCommand<ExecCommand>()
            .Build();

        var result = await RunAsync(app, "--connection", "prod", Separator, "child", "--x");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("prod count=2 [child|--x]", result.StdOut);
    }

    [Fact]
    public async Task DefaultCommandWithPositional_BindsPositionalAndTail()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("raw-test")
            .SetDefaultCommand<SelectorTailCommand>()
            .Build();

        var result = await RunAsync(app, "target", Separator, "child");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("target/dbo count=1 [child]", result.StdOut);
    }

    // ── Empty and required tails ────────────────────────────────────

    [Fact]
    public async Task OptionalTail_EmptyAfterSeparator_BindsEmpty()
    {
        var result = await RunAsync(CreateApp(), "exec", Separator);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("default count=0 []", result.StdOut);
    }

    [Fact]
    public async Task OptionalTail_NoSeparator_BindsEmpty()
    {
        var result = await RunAsync(CreateApp(), "exec");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("default count=0 []", result.StdOut);
    }

    [Fact]
    public async Task RequiredTail_EmptyAfterSeparator_IsUsageError()
    {
        var result = await RunAsync(CreateApp(), "run-required", Separator);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Missing required arguments after '--': <tool-args>", result.StdErr);
        Assert.Contains("raw-test run-required --help", result.StdErr);
    }

    [Fact]
    public async Task RequiredTail_NoSeparator_IsUsageError()
    {
        var result = await RunAsync(CreateApp(), "run-required");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Missing required arguments after '--': <tool-args>", result.StdErr);
    }

    [Fact]
    public async Task RequiredTail_NonInteractive_FailsWithoutPrompting()
    {
        var result = await RunAsync(CreateApp(), "run-required", "--non-interactive");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Missing required arguments after '--': <tool-args>", result.StdErr);
    }

    [Fact]
    public async Task RequiredTail_WithTokens_Runs()
    {
        var result = await RunAsync(CreateApp(), "run-required", Separator, "child");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("required count=1 [child]", result.StdOut);
    }

    [Fact]
    public async Task RequiredTail_MissingUsesMissingRequiredArgumentPolicy()
    {
        var app = CreateApp(builder => builder
            .UseExitCodes(0, -9)
            .ExitKind(TigerCliExitKind.MissingRequiredArgument, 77));

        var result = await RunAsync(app, "run-required");

        Assert.Equal(77, result.ExitCode);
    }

    [Fact]
    public async Task UnsupportedTailUsesInvalidArgumentsPolicy()
    {
        var app = CreateApp(builder => builder
            .UseExitCodes(0, -9)
            .ExitKind(TigerCliExitKind.InvalidArguments, 64));

        var result = await RunAsync(app, "plain", Separator, "x");

        Assert.Equal(64, result.ExitCode);
    }

    // ── Informational options ───────────────────────────────────────

    [Fact]
    public async Task HelpBeforeSeparator_PrintsHelpAndDoesNotRun()
    {
        var result = await RunAsync(CreateApp(), "exec", "--help", Separator, "child");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("raw-test exec", result.StdOut);
        Assert.DoesNotContain("count=", result.StdOut);
    }

    [Fact]
    public async Task RootHelpBeforeSeparator_PrintsRootHelp()
    {
        var result = await RunAsync(CreateApp(), "--help", Separator, "child");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Commands:", result.StdOut);
        Assert.DoesNotContain("count=", result.StdOut);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("--help-env")]
    [InlineData("--help-errors")]
    [InlineData("--version")]
    public async Task InformationalOptionsAfterSeparator_AreTailData(string token)
    {
        var result = await RunAsync(CreateApp(), "exec", Separator, "my-tool", token);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"count=2 [my-tool|{token}]", result.StdOut);
    }

    // ── Prompting and providers ─────────────────────────────────────

    [Fact]
    public async Task Tail_IsNotPromptedAndDoesNotDisturbOptionPrompting()
    {
        var app = CreateApp();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("prompted", Separator, "child", "--arg")
            .WithTextInput("riley")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("riley count=2 [child|--arg]", result.StdOut);
    }

    [Fact]
    public async Task Tail_IsNotValidatedAgainstProviders()
    {
        var providerCalls = 0;
        var app = CreateApp(builder => builder.ConfigureProviders(providers =>
            providers.Add("name", _ =>
            {
                providerCalls++;
                return Task.FromResult<IReadOnlyList<string>>(["riley"]);
            })));

        var result = await RunAsync(
            app, "prompted", "--name", "riley", "--non-interactive", Separator, "not-a-choice", "--x");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("riley count=2 [not-a-choice|--x]", result.StdOut);
        // The provider may be consulted for --name, but never for a tail token.
        Assert.True(providerCalls <= 1, $"Provider was called {providerCalls} times.");
    }

    // ── Generated help ──────────────────────────────────────────────

    [Fact]
    public async Task Help_DocumentsOptionalTailInUsageAndArguments()
    {
        var result = await RunAsync(CreateApp(), "exec", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("raw-test exec [options] [-- <tool-args>...]", result.StdOut);
        Assert.Contains("-- <tool-args>...", result.StdOut);
        Assert.Contains("Child tool and its arguments.", result.StdOut);
        Assert.Contains("Everything after '--' is passed through unchanged.", result.StdOut);
    }

    [Fact]
    public async Task Help_DocumentsRequiredTailWithoutBrackets()
    {
        var result = await RunAsync(CreateApp(), "run-required", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("raw-test run-required [options] -- <tool-args>...", result.StdOut);
        Assert.Contains("Required.", result.StdOut);
    }

    [Fact]
    public async Task Help_ListsTailAfterPositionalArguments()
    {
        var result = await RunAsync(CreateApp(), "process", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("raw-test process <target> [options] [-- <tool-args>...]", result.StdOut);
        Assert.True(
            result.StdOut.IndexOf("<target>", StringComparison.Ordinal)
            < result.StdOut.LastIndexOf("-- <tool-args>...", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Help_ForCommandWithoutTail_IsUnchanged()
    {
        var result = await RunAsync(CreateApp(), "plain", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("raw-test plain [options]", result.StdOut);
        Assert.DoesNotContain("--]", result.StdOut);
        Assert.DoesNotContain("passed through unchanged", result.StdOut);
    }

    [Fact]
    public async Task Help_DerivesTailDisplayNameFromPropertyName()
    {
        var result = await RunAsync(CreateApp(), "list-tail", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[-- <tool-arguments>...]", result.StdOut);
    }

    // ── Localization ────────────────────────────────────────────────

    [Fact]
    public async Task UnsupportedTail_IsLocalized()
    {
        var app = CreateApp(builder => builder.SetSupportedCultures("en-US", "pl-PL"));

        var result = await RunAsync(app, "plain", "--culture", "pl-PL", Separator, "x");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Argumenty po '--' nie są tutaj obsługiwane.", result.StdErr);
    }

    [Fact]
    public async Task MissingRequiredTail_IsLocalized()
    {
        var app = CreateApp(builder => builder.SetSupportedCultures("en-US", "pl-PL"));

        var result = await RunAsync(app, "run-required", "--culture", "pl-PL");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Brak wymaganych argumentów po '--': <tool-args>", result.StdErr);
    }

    [Fact]
    public async Task TailHelpHint_IsLocalized()
    {
        var app = CreateApp(builder => builder.SetSupportedCultures("en-US", "pl-PL"));

        var result = await RunAsync(app, "exec", "--culture", "pl-PL", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Wszystko po '--' jest przekazywane bez zmian.", result.StdOut);
    }

    // ── Build-time validation ───────────────────────────────────────

    private sealed class DuplicateTailSettings : TigerCliSettings
    {
        [TigerCliRawArguments]
        public IReadOnlyList<string> First { get; init; } = [];

        [TigerCliRawArguments]
        public IReadOnlyList<string> Second { get; init; } = [];
    }

    private sealed class BadTypeTailSettings : TigerCliSettings
    {
        [TigerCliRawArguments]
        public List<int> Arguments { get; init; } = [];
    }

    private sealed class ScalarTailSettings : TigerCliSettings
    {
        [TigerCliRawArguments]
        public string Arguments { get; init; } = string.Empty;
    }

    private sealed class ReadOnlyTailSettings : TigerCliSettings
    {
        [TigerCliRawArguments]
        public IReadOnlyList<string> Arguments => [];
    }

    private sealed class CombinedTailSettings : TigerCliSettings
    {
        [TigerCliOption("--args")]
        [TigerCliRawArguments]
        public List<string> Arguments { get; init; } = [];
    }

    private sealed class DuplicateTailCommand : TigerCliAsyncCommandHandler<DuplicateTailSettings>
    {
        public override Task<int> ExecuteAsync(DuplicateTailSettings settings) => Task.FromResult(0);
    }

    private sealed class BadTypeTailCommand : TigerCliAsyncCommandHandler<BadTypeTailSettings>
    {
        public override Task<int> ExecuteAsync(BadTypeTailSettings settings) => Task.FromResult(0);
    }

    private sealed class ScalarTailCommand : TigerCliAsyncCommandHandler<ScalarTailSettings>
    {
        public override Task<int> ExecuteAsync(ScalarTailSettings settings) => Task.FromResult(0);
    }

    private sealed class ReadOnlyTailCommand : TigerCliAsyncCommandHandler<ReadOnlyTailSettings>
    {
        public override Task<int> ExecuteAsync(ReadOnlyTailSettings settings) => Task.FromResult(0);
    }

    private sealed class CombinedTailCommand : TigerCliAsyncCommandHandler<CombinedTailSettings>
    {
        public override Task<int> ExecuteAsync(CombinedTailSettings settings) => Task.FromResult(0);
    }

    [Fact]
    public void MultipleRawArgumentProperties_FailAtBuild()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => TigerCliApp.CreateBuilder()
            .SetApplicationName("raw-test")
            .AddCommand<DuplicateTailCommand>("bad")
            .Build());

        Assert.Contains("Multiple [TigerCliRawArguments] properties", exception.Message);
    }

    [Fact]
    public void UnsupportedCollectionElementType_FailsAtBuild()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => TigerCliApp.CreateBuilder()
            .SetApplicationName("raw-test")
            .AddCommand<BadTypeTailCommand>("bad")
            .Build());

        Assert.Contains("requires a string[], List<string>, or IReadOnlyList<string> property", exception.Message);
    }

    [Fact]
    public void ScalarPropertyType_FailsAtBuild()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => TigerCliApp.CreateBuilder()
            .SetApplicationName("raw-test")
            .SetDefaultCommand<ScalarTailCommand>()
            .Build());

        Assert.Contains("requires a string[], List<string>, or IReadOnlyList<string> property", exception.Message);
    }

    [Fact]
    public void ReadOnlyProperty_FailsAtBuild()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => TigerCliApp.CreateBuilder()
            .SetApplicationName("raw-test")
            .AddCommand<ReadOnlyTailCommand>("bad")
            .Build());

        Assert.Contains("requires a writable property", exception.Message);
    }

    [Fact]
    public void CombinedWithOptionAttribute_FailsAtBuild()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => TigerCliApp.CreateBuilder()
            .SetApplicationName("raw-test")
            .AddCommand<CombinedTailCommand>("bad")
            .Build());

        Assert.Contains("cannot be combined with [TigerCliOption] or [TigerCliArgument]", exception.Message);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private sealed class GlobalOptionContribution : ITigerCliAppContribution
    {
        private readonly Func<string?, TigerCliValidationResult> _apply;

        public GlobalOptionContribution(Func<string?, TigerCliValidationResult> apply)
        {
            _apply = apply;
        }

        public void Configure(TigerCliAppContributionBuilder builder) =>
            builder.GlobalOptions.AddOptionalString(
                "--acme-config",
                "path",
                "Reusable library configuration file.",
                (_, value) => _apply(value));
    }

    private static TigerCliApp CreateApp(Action<TigerCliAppBuilder>? configure = null)
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("raw-test")
            .SetVersion("1.0.0")
            .AddCommand<ExecCommand>("exec")
            .AddCommand<RequiredTailCommand>("run-required")
            .AddCommand<SelectorTailCommand>("process")
            .AddCommand<ListTailCommand>("list-tail")
            .AddCommand<NoTailCommand>("plain")
            .AddCommand<PromptedTailCommand>("prompted")
            .AddCommandGroup("tools", group => group.AddCommand<ListTailCommand>("run"));

        configure?.Invoke(builder);
        return builder.Build();
    }

    private static TigerCliApp CreateNonInteractiveApp() =>
        CreateApp(builder => builder.SetInteractionMode(TigerCliInteractionMode.NonInteractive));

    private static Task<TigerCliAppRunResult> RunAsync(TigerCliApp app, params string[] args) =>
        TigerCliAppTestHost
            .For(app)
            .WithArgs(args)
            .RunAsync(TestContext.Current.CancellationToken);
}
