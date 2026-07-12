using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Behavioral tests for the opt-in command menu. The menu only selects a command; the chosen
/// command runs through the normal parse/bind/prompt/execute pipeline. Eligibility is asserted
/// behaviorally (which command a given selection index runs), because the framework internals
/// that compute eligibility are not visible to the test assembly.
/// </summary>
public sealed class TigerCliCommandMenuTests
{
    // ── Test commands ────────────────────────────────────────────────

    private sealed class AlphaSettings : TigerCliSettings { }

    private sealed class AlphaCommand : TigerCliAsyncCommandHandler<AlphaSettings>
    {
        public override Task<int> ExecuteAsync(AlphaSettings settings)
        {
            TigerConsole.MarkupLine("ran=alpha");
            return Task.FromResult(0);
        }
    }

    private sealed class BravoSettings : TigerCliSettings { }

    private sealed class BravoCommand : TigerCliAsyncCommandHandler<BravoSettings>
    {
        public override Task<int> ExecuteAsync(BravoSettings settings)
        {
            TigerConsole.MarkupLine("ran=bravo");
            return Task.FromResult(0);
        }
    }

    private sealed class GreetSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class GreetCommand : TigerCliAsyncCommandHandler<GreetSettings>
    {
        public override Task<int> ExecuteAsync(GreetSettings settings)
        {
            TigerConsole.MarkupLine($"hello={settings.Name}");
            return Task.FromResult(0);
        }
    }

    // ── App-level resolution ─────────────────────────────────────────

    [Fact]
    public async Task AppEnabled_ListsAllInheritCommands()
    {
        // App Enabled + command Inherit => eligible. Both commands listed; index 1 runs bravo.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<AlphaCommand>("alpha")
            .AddCommand<BravoCommand>("bravo")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(1)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=bravo", result.StdOut);
        Assert.DoesNotContain("ran=alpha", result.StdOut);
    }

    [Fact]
    public async Task AppDisabled_DoesNotRegisterMenu()
    {
        // App Disabled + command Enabled => not eligible; the menu is never registered, so with no
        // default command the no-arg run falls back to help instead of opening a picker.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Disabled)
            .AddCommand<AlphaCommand>("alpha", configure: c => c.CommandMenu(CommandMenuMode.Enabled))
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("ran=alpha", result.StdOut);
        Assert.Contains("Usage:", result.StdOut);
    }

    [Fact]
    public async Task AppInherit_OnlyEnabledCommandsAreEligible()
    {
        // App Inherit: Inherit command not eligible, Enabled command eligible. Only bravo is listed,
        // so index 0 runs bravo (alpha would be index 0 if it were listed).
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Inherit)
            .AddCommand<AlphaCommand>("alpha")
            .AddCommand<BravoCommand>("bravo", configure: c => c.CommandMenu(CommandMenuMode.Enabled))
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=bravo", result.StdOut);
        Assert.DoesNotContain("ran=alpha", result.StdOut);
    }

    // ── Command-level override ───────────────────────────────────────

    [Fact]
    public async Task CommandDisabled_OverridesAppEnabled()
    {
        // App Enabled + alpha Disabled => alpha hidden. Only bravo listed; index 0 runs bravo.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<AlphaCommand>("alpha", configure: c => c.CommandMenu(CommandMenuMode.Disabled))
            .AddCommand<BravoCommand>("bravo")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=bravo", result.StdOut);
        Assert.DoesNotContain("ran=alpha", result.StdOut);
    }

    // ── Excludes framework/menu commands ─────────────────────────────

    [Fact]
    public async Task Menu_ExcludesHelpVersionAndMenuItself()
    {
        // Version is enabled and the menu is a named command. Neither help/version (framework
        // options) nor the menu command occupy a menu row, so index 0 runs the only real command.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .SetVersion("1.0.0")
            .UseCommandMenu(CommandMenuMode.Enabled, commandName: "menu")
            .AddCommand<AlphaCommand>("alpha")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("menu")
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=alpha", result.StdOut);
    }

    [Fact]
    public async Task NamedMenuCommand_IsHiddenFromHelp()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("demo-app")
            .UseCommandMenu(CommandMenuMode.Enabled, commandName: "open-picker")
            .AddCommand<AlphaCommand>("alpha")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("--help")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("alpha", result.StdOut);
        Assert.DoesNotContain("open-picker", result.StdOut);
    }

    // ── Registration shapes ──────────────────────────────────────────

    [Fact]
    public async Task DefaultMenuCommand_OpensOnNoArgs()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<AlphaCommand>("alpha")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=alpha", result.StdOut);
    }

    [Fact]
    public async Task NamedMenuCommand_OpensOnName()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled, commandName: "pick")
            .AddCommand<AlphaCommand>("alpha")
            .AddCommand<BravoCommand>("bravo")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("pick")
            .WithSelectIndex(1)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=bravo", result.StdOut);
    }

    [Fact]
    public void DefaultMenu_WithDefaultCommand_Throws()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .SetDefaultCommand<AlphaCommand>()
            .UseCommandMenu(CommandMenuMode.Enabled);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    // ── Pipeline integration ─────────────────────────────────────────

    [Fact]
    public async Task SelectingCommand_RunsNormalPromptPipeline()
    {
        // After the menu selects greet, the normal pipeline prompts for the required --name option.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<GreetCommand>("greet")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(0)
            .WithTextInput("riley")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello=riley", result.StdOut);
    }

    // ── Nested groups ────────────────────────────────────────────────

    [Fact]
    public async Task NestedGroup_IsNavigable()
    {
        // App Enabled, a group with an eligible child. Top menu lists the group; entering it lists
        // the child. Select the group (index 0), then the child (index 0).
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommandGroup("tools", group => group
                .SetDescription("Tooling")
                .AddCommand<AlphaCommand>("alpha"))
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(0)
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=alpha", result.StdOut);
    }

    [Fact]
    public async Task GroupDisabled_HidesGroupFromMenu()
    {
        // The group is Disabled, so its child is not eligible and the group is not shown. Only the
        // ungrouped bravo remains; index 0 runs bravo.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommandGroup("tools", group => group
                .CommandMenu(CommandMenuMode.Disabled)
                .AddCommand<AlphaCommand>("alpha"))
            .AddCommand<BravoCommand>("bravo")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=bravo", result.StdOut);
        Assert.DoesNotContain("ran=alpha", result.StdOut);
    }

    [Fact]
    public async Task NestedSubgroup_IsNavigableThroughMenu()
    {
        // App Enabled; a group with a subgroup holding an eligible command. Top menu lists the
        // parent group; entering it lists the subgroup; entering that lists the command.
        // Select parent (0), subgroup (0), then command (0).
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommandGroup("projects", projects => projects
                .SetDescription("Projects")
                .AddCommandGroup("sp", sp => sp
                    .SetDescription("Stored procedures")
                    .AddCommand<AlphaCommand>("alpha")))
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(0)
            .WithSelectIndex(0)
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=alpha", result.StdOut);
    }

    [Fact]
    public async Task ParentGroupDisabled_HidesEntireSubtreeFromMenu()
    {
        // Disabled on the parent group cascades to the nested subgroup's command, so nothing under
        // projects is eligible and only the ungrouped bravo remains; index 0 runs bravo.
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommandGroup("projects", projects => projects
                .CommandMenu(CommandMenuMode.Disabled)
                .AddCommandGroup("sp", sp => sp.AddCommand<AlphaCommand>("alpha")))
            .AddCommand<BravoCommand>("bravo")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ran=bravo", result.StdOut);
        Assert.DoesNotContain("ran=alpha", result.StdOut);
    }

    // ── Empty + cancel ───────────────────────────────────────────────

    [Fact]
    public async Task EmptyMenu_ReportsNoCommands()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<AlphaCommand>("alpha", configure: c => c.CommandMenu(CommandMenuMode.Disabled))
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("ran=alpha", result.StdOut);
        Assert.Contains("No commands are available.", result.StdOut);
    }

    [Fact]
    public async Task Escape_ExitsWithoutRunningHandler()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<AlphaCommand>("alpha")
            .AddCommand<BravoCommand>("bravo")
            .Build();

        var shell = new TestShell(80, 24);
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var (exitCode, stdout) = await RunWithShellAsync(app, shell);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("ran=alpha", stdout);
        Assert.DoesNotContain("ran=bravo", stdout);
    }

    [Fact]
    public async Task NonInteractive_MenuFailsCleanly()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.InteractiveNotAllowed, 46)
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<AlphaCommand>("alpha")
            .Build();

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("--non-interactive")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(46, result.ExitCode);
        Assert.DoesNotContain("ran=alpha", result.StdOut);
        Assert.Contains("interactive", result.StdErr);
    }

    // ── Layout ───────────────────────────────────────────────────────

    [Fact]
    public async Task Menu_RendersStructuredColumns_WithRightAlignedMarkers()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("menu-test")
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<AlphaCommand>("alpha", "Do the alpha thing.")
            .AddCommandGroup("card", group => group
                .SetDescription("Manage cards.")
                .AddCommand<BravoCommand>("ingest", "Ingest a card."))
            .AddCommandAlias("import", "card ingest", a => a.SetDescription("Import files from a card."))
            .Build();

        var shell = new TestShell(100, 24);
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await RunWithShellAsync(app, shell);

        var lines = shell.Terminal.LastRenderedLines;
        var aliasLine = lines.First(l => l.Contains("import") && l.Contains("Import files from a card."));
        var groupLine = lines.First(l => l.Contains("card") && l.Contains("Manage cards."));
        var commandLine = lines.First(l => l.Contains("alpha") && l.Contains("Do the alpha thing."));

        // Strip the dialog frame border to inspect just the row content.
        static string Inner(string line) => line.Trim().Trim('║');

        // Name is left-aligned; description follows; marker/alias is right-aligned at the row's end.
        Assert.StartsWith("import", Inner(aliasLine).TrimStart());
        Assert.EndsWith("→ card ingest", Inner(aliasLine).TrimEnd());
        Assert.EndsWith("›", Inner(groupLine).TrimEnd());
        // A normal command has no trailing marker: the row ends with its description.
        Assert.EndsWith("Do the alpha thing.", Inner(commandLine).TrimEnd());

        // All three rows render to the same width, so the columns align and markers share a right edge.
        Assert.Equal(aliasLine.Length, groupLine.Length);
        Assert.Equal(aliasLine.Length, commandLine.Length);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static async Task<(int ExitCode, string StdOut)> RunWithShellAsync(
        TigerCliApp app, TestShell shell, params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalColorMode = TigerConsole.ColorMode;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            TigerConsole.ColorMode = CliColorMode.Never;
            Console.SetOut(TextWriter.Synchronized(stdout));
            Console.SetError(TextWriter.Synchronized(stderr));
            var exitCode = await app.RunAsync(args.Append("--no-color").ToArray(), shell);
            return (exitCode, stdout.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            TigerConsole.ColorMode = originalColorMode;
        }
    }
}
