using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Behavioral tests for root-level command aliases. An alias is an alternate entry point into the
/// existing command tree: commands resolve first, aliases second, and the target command owns
/// parsing, prompting, validation, and execution. The alias may own only its help/menu presentation.
/// </summary>
public sealed class TigerCliCommandAliasTests
{
    // ── Test commands ────────────────────────────────────────────────

    private sealed class IngestSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "card", Description = "Card identifier.")]
        public string Card { get; set; } = string.Empty;

        [TigerCliOption("--source", Required = true, Description = "Source path.")]
        public string Source { get; set; } = string.Empty;
    }

    private sealed class IngestCommand : TigerCliAsyncCommandHandler<IngestSettings>
    {
        public override Task<int> ExecuteAsync(IngestSettings settings)
        {
            TigerConsole.MarkupLine(
                $"ingest:{CliMarkupParser.Escape(settings.Card)}:{CliMarkupParser.Escape(settings.Source)}");
            return Task.FromResult(0);
        }
    }

    private sealed class RegisterSettings : TigerCliSettings { }

    private sealed class RegisterCommand : TigerCliAsyncCommandHandler<RegisterSettings>
    {
        public override Task<int> ExecuteAsync(RegisterSettings settings)
        {
            TigerConsole.MarkupLine("ran=register");
            return Task.FromResult(0);
        }
    }

    private sealed class ReportSettings : TigerCliSettings { }

    private sealed class ReportCommand : TigerCliAsyncCommandHandler<ReportSettings>
    {
        public override Task<int> ExecuteAsync(ReportSettings settings)
        {
            TigerConsole.MarkupLine("ran=report");
            return Task.FromResult(0);
        }
    }

    // ── Resolution & execution ───────────────────────────────────────

    [Fact]
    public async Task Alias_ResolvesAndExecutesTargetHandler()
    {
        var app = CreateCardApp();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("import", "CARD1", "--source", "D:/drop")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ingest:CARD1:D:/drop", result.StdOut);
    }

    [Fact]
    public async Task Alias_BindsRemainingArgsToTargetSettings()
    {
        // The tokens after the alias bind to the target command's argument/option metadata exactly as
        // if the full command path had been typed.
        var directApp = CreateCardApp();
        var direct = await TigerCliAppTestHost
            .For(directApp)
            .WithArgs("card", "ingest", "CARD7", "--source", "X")
            .RunAsync(TestContext.Current.CancellationToken);

        var aliasApp = CreateCardApp();
        var aliased = await TigerCliAppTestHost
            .For(aliasApp)
            .WithArgs("import", "CARD7", "--source", "X")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, direct.ExitCode);
        Assert.Equal(0, aliased.ExitCode);
        // The alias path binds identically to typing the full command path.
        Assert.Equal(direct.StdOut, aliased.StdOut);
        Assert.Contains("ingest:CARD7:X", aliased.StdOut);
    }

    [Fact]
    public async Task Alias_FallsThroughToTargetPrompting()
    {
        var app = CreateCardApp();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("import", "CARD1")
            .WithTextInput("D:/drop")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ingest:CARD1:D:/drop", result.StdOut);
    }

    [Fact]
    public async Task RealCommand_ResolvesBeforeAlias()
    {
        // A command named the same as a target token is unaffected: 'card' still routes to the group.
        var app = CreateCardApp();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("card", "register")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=register", result.StdOut);
    }

    // ── Prompt-mode inheritance ──────────────────────────────────────

    [Fact]
    public async Task Alias_InheritsTargetPromptMode_No_DoesNotPrompt()
    {
        // The target command sets prompt mode No; invoking it through the alias must honor that, so the
        // missing required --source errors instead of prompting.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .AddCommandGroup("card", group => group
                .SetDescription("Manage cards.")
                .AddCommand<IngestCommand>("ingest", configure: c => c.SetPromptMode(TigerCliPromptMode.No)))
            .AddCommandAlias("import", "card ingest")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("import", "CARD1")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("ingest:", result.StdOut);
    }

    [Fact]
    public async Task Alias_InheritsTargetPromptMode_RequiredOnly_Prompts()
    {
        // Same invocation as above but with the default RequiredOnly prompt mode: the alias path
        // prompts for the missing required value, proving prompt behavior comes from the target.
        var app = CreateCardApp();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("import", "CARD1")
            .WithTextInput("D:/drop")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ingest:CARD1:D:/drop", result.StdOut);
    }

    // ── Help ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RootHelp_ListsAliasesSection_WithTargetMarker()
    {
        var app = CreateCardApp();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("--help")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Aliases:", result.StdOut);
        Assert.Contains("import", result.StdOut);
        Assert.Contains("Import files from a registered card or source.", result.StdOut);
        Assert.Contains("→ card ingest", result.StdOut);
    }

    [Fact]
    public async Task RootHelp_AliasDescription_FallsBackToTarget()
    {
        var app = CreateCardApp();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("--help")
            .RunAsync(TestContext.Current.CancellationToken);

        // register-card has no own description; it falls back to the target's.
        Assert.Contains("register-card", result.StdOut);
        Assert.Contains("Register a card.", result.StdOut);
        Assert.Contains("→ card register", result.StdOut);
    }

    [Fact]
    public async Task AliasHelp_ShowsAliasIdentityAndTargetNote()
    {
        var app = CreateCardApp();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("import", "--help")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("card-cli import", result.StdOut);
        Assert.Contains("Import files from a registered card or source.", result.StdOut);
        Assert.Contains("Alias for: card ingest", result.StdOut);
        // The target command's options are shown on the alias's help page.
        Assert.Contains("--source", result.StdOut);
    }

    [Fact]
    public async Task HiddenAlias_OmittedFromHelp_ButStillResolves()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .AddCommandGroup("card", group => group
                .SetDescription("Manage cards.")
                .AddCommand<RegisterCommand>("register", "Register a card."))
            .AddCommandAlias("register-card", "card register", a => a.HideFromHelp())
            .Build();

        var help = await TigerCliAppTestHost
            .For(app)
            .WithArgs("--help")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("register-card", help.StdOut);

        var run = await TigerCliAppTestHost
            .For(CreateRegisterAliasApp(hideFromHelp: true))
            .WithArgs("register-card")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("ran=register", run.StdOut);
    }

    // ── Command menu ─────────────────────────────────────────────────

    [Fact]
    public async Task Menu_ListsAlias_AndSelectingRunsTarget()
    {
        // Top menu = [register, reg]; index 1 is the alias entry, which runs the target.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<RegisterCommand>("register")
            .AddCommandAlias("reg", "register")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(1)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=register", result.StdOut);
    }

    [Fact]
    public async Task Menu_TargetHidden_AliasVisible_RunsTarget()
    {
        // The target command is hidden from the menu, but its alias stays visible and runs it.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<RegisterCommand>("register", configure: c => c.CommandMenu(CommandMenuMode.Disabled))
            .AddCommandAlias("reg", "register")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=register", result.StdOut);
    }

    [Fact]
    public async Task Menu_AliasDisabled_HiddenFromMenu()
    {
        // Both the command and the alias opt out of the menu, so the menu is empty.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<RegisterCommand>("register", configure: c => c.CommandMenu(CommandMenuMode.Disabled))
            .AddCommandAlias("reg", "register", a => a.CommandMenu(CommandMenuMode.Disabled))
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("ran=register", result.StdOut);
        Assert.Contains("No commands are available.", result.StdOut);
    }

    // ── Conflict validation ──────────────────────────────────────────

    [Fact]
    public void Alias_ConflictingWithCommandPath_Throws()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .AddCommand<RegisterCommand>("register")
            .AddCommand<ReportCommand>("report")
            .AddCommandAlias("register", "report");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Alias_ConflictingWithGroupPath_Throws()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .AddCommandGroup("card", group => group.AddCommand<IngestCommand>("ingest"))
            .AddCommandAlias("card", "card ingest");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Alias_DuplicatePath_Throws()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .AddCommandGroup("card", group => group.AddCommand<IngestCommand>("ingest"))
            .AddCommandAlias("import", "card ingest")
            .AddCommandAlias("import", "card ingest");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Alias_ConflictingWithNamedMenuCommand_Throws()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .UseCommandMenu(CommandMenuMode.Enabled, commandName: "menu")
            .AddCommand<RegisterCommand>("register")
            .AddCommandAlias("menu", "register");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Alias_TargetingUnknownCommand_Throws()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .AddCommand<RegisterCommand>("register")
            .AddCommandAlias("import", "card ingest");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Alias_TargetingGroup_Throws()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .AddCommandGroup("card", group => group.AddCommand<IngestCommand>("ingest"))
            .AddCommandAlias("c", "card");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Alias_MultiTokenName_Throws()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .AddCommandGroup("card", group => group.AddCommand<IngestCommand>("ingest"));

        Assert.Throws<ArgumentException>(() => builder.AddCommandAlias("im port", "card ingest"));
    }

    [Fact]
    public void Alias_NameStartingWithDash_Throws()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .AddCommandGroup("card", group => group.AddCommand<IngestCommand>("ingest"));

        Assert.Throws<ArgumentException>(() => builder.AddCommandAlias("-i", "card ingest"));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static TigerCliApp CreateCardApp()
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .AddCommandGroup("card", group => group
                .SetDescription("Manage cards.")
                .AddCommand<IngestCommand>("ingest", "Ingest a card.")
                .AddCommand<RegisterCommand>("register", "Register a card."))
            .AddCommandAlias("import", "card ingest", a =>
                a.SetDescription("Import files from a registered card or source."))
            .AddCommandAlias("register-card", "card register")
            .Build();
    }

    private static TigerCliApp CreateRegisterAliasApp(bool hideFromHelp)
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("card-cli")
            .AddCommandGroup("card", group => group
                .SetDescription("Manage cards.")
                .AddCommand<RegisterCommand>("register", "Register a card."))
            .AddCommandAlias("register-card", "card register",
                a => { if (hideFromHelp) a.HideFromHelp(); })
            .Build();
    }
}
