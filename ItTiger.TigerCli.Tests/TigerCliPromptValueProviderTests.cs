using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliPromptValueProviderTests
{
    private sealed class ConnectionSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "connection", Description = "Connection")]
        public string ConnectionName { get; set; } = string.Empty;
    }

    private sealed class OptionSettings : TigerCliSettings
    {
        [TigerCliOption("--connection", Required = true, Description = "Connection")]
        public string ConnectionName { get; set; } = string.Empty;
    }

    private sealed class ProjectSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "connection", Description = "Connection")]
        public string ConnectionName { get; set; } = string.Empty;

        [TigerCliArgument(1, Name = "project", Description = "Project")]
        public string ProjectName { get; set; } = string.Empty;
    }

    private sealed class OptionalProviderSettings : TigerCliSettings
    {
        [TigerCliOption("--schema", Promptable = TigerCliPromptable.Normal, Description = "Schema")]
        public string Schema { get; set; } = "dbo";
    }

    private sealed class NotPromptableProviderSettings : TigerCliSettings
    {
        [TigerCliOption("--connection", Required = true, Promptable = TigerCliPromptable.No, Description = "Connection")]
        public string ConnectionName { get; set; } = string.Empty;
    }

    private sealed class OptionProviderMetadataSettings : TigerCliSettings
    {
        [TigerCliOption(
            "--connection",
            Required = true,
            Provider = "configured-connection",
            Description = "Connection")]
        public string ConnectionName { get; set; } = string.Empty;
    }

    private sealed class ArgumentProviderMetadataSettings : TigerCliSettings
    {
        [TigerCliArgument(
            0,
            Name = "database",
            Provider = "configured-database",
            Description = "Database")]
        public string DatabaseName { get; set; } = string.Empty;
    }

    private sealed class NotPromptableProviderMetadataSettings : TigerCliSettings
    {
        [TigerCliOption(
            "--connection",
            Required = true,
            Promptable = TigerCliPromptable.No,
            Provider = "configured-connection",
            Description = "Connection")]
        public string ConnectionName { get; set; } = string.Empty;
    }

    // Dogfood pattern: required + promptable + provider-backed. Prompts when missing in
    // semi-interactive mode; fails as a missing required option in non-interactive mode.
    private sealed class RequiredPromptableProviderSettings : TigerCliSettings
    {
        [TigerCliOption(
            "-c|--connection",
            Required = true,
            Promptable = TigerCliPromptable.Normal,
            Provider = "connections",
            Description = "Connection")]
        public string Connection { get; set; } = string.Empty;
    }

    private sealed class AutoSelectRequiredPromptableProviderSettings : TigerCliSettings
    {
        [TigerCliOption(
            "-c|--connection",
            Required = true,
            Promptable = TigerCliPromptable.Normal,
            Provider = "connections",
            AutoSelectSingleChoice = true,
            Description = "Connection")]
        public string Connection { get; set; } = string.Empty;
    }

    private sealed class AutoSelectArgumentProviderSettings : TigerCliSettings
    {
        [TigerCliArgument(
            0,
            Name = "connection",
            Provider = "connections",
            AutoSelectSingleChoice = true,
            Description = "Connection")]
        public string Connection { get; set; } = string.Empty;
    }

    private sealed class ConnectionCommand : TigerCliAsyncCommandHandler<ConnectionSettings>
    {
        public override Task<int> ExecuteAsync(ConnectionSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.ConnectionName));
            return Task.FromResult(0);
        }
    }

    private sealed class RequiredPromptableProviderCommand :
        TigerCliAsyncCommandHandler<RequiredPromptableProviderSettings>
    {
        public override Task<int> ExecuteAsync(RequiredPromptableProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Connection));
            return Task.FromResult(0);
        }
    }

    private sealed class AutoSelectRequiredPromptableProviderCommand :
        TigerCliAsyncCommandHandler<AutoSelectRequiredPromptableProviderSettings>
    {
        public override Task<int> ExecuteAsync(AutoSelectRequiredPromptableProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Connection));
            return Task.FromResult(0);
        }
    }

    private sealed class AutoSelectArgumentProviderCommand :
        TigerCliAsyncCommandHandler<AutoSelectArgumentProviderSettings>
    {
        public override Task<int> ExecuteAsync(AutoSelectArgumentProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Connection));
            return Task.FromResult(0);
        }
    }

    private sealed class OptionCommand : TigerCliAsyncCommandHandler<OptionSettings>
    {
        public override Task<int> ExecuteAsync(OptionSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.ConnectionName));
            return Task.FromResult(0);
        }
    }

    private sealed class ProjectCommand : TigerCliAsyncCommandHandler<ProjectSettings>
    {
        public override Task<int> ExecuteAsync(ProjectSettings settings)
        {
            TigerConsole.MarkupLine(
                $"{CliMarkupParser.Escape(settings.ConnectionName)}:{CliMarkupParser.Escape(settings.ProjectName)}");
            return Task.FromResult(0);
        }
    }

    private sealed class OptionalProviderCommand : TigerCliAsyncCommandHandler<OptionalProviderSettings>
    {
        public override Task<int> ExecuteAsync(OptionalProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Schema));
            return Task.FromResult(0);
        }
    }

    private sealed class NotPromptableProviderCommand : TigerCliAsyncCommandHandler<NotPromptableProviderSettings>
    {
        public override Task<int> ExecuteAsync(NotPromptableProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.ConnectionName));
            return Task.FromResult(0);
        }
    }

    private sealed class OptionProviderMetadataCommand :
        TigerCliAsyncCommandHandler<OptionProviderMetadataSettings>
    {
        public override Task<int> ExecuteAsync(OptionProviderMetadataSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.ConnectionName));
            return Task.FromResult(0);
        }
    }

    private sealed class ArgumentProviderMetadataCommand :
        TigerCliAsyncCommandHandler<ArgumentProviderMetadataSettings>
    {
        public override Task<int> ExecuteAsync(ArgumentProviderMetadataSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.DatabaseName));
            return Task.FromResult(0);
        }
    }

    private sealed class NotPromptableProviderMetadataCommand :
        TigerCliAsyncCommandHandler<NotPromptableProviderMetadataSettings>
    {
        public override Task<int> ExecuteAsync(NotPromptableProviderMetadataSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.ConnectionName));
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task StringPositionalArgument_UsesProviderSelectInsteadOfTextInput()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<ConnectionCommand, ConnectionSettings>(prompts =>
            prompts.For(s => s.ConnectionName).SelectFrom((_, _) =>
                [new OptionItem<string>("local", "Local connection")]));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("local", result.Stdout);
        Assert.Contains("Local connection", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task StringOption_UsesProviderSelectInsteadOfTextInput()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<OptionCommand, OptionSettings>(prompts =>
            prompts.For(s => s.ConnectionName).SelectFrom((_, _) =>
            [
                new OptionItem<string>("local", "Local connection"),
                new OptionItem<string>("remote", "Remote connection")
            ]));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("remote", result.Stdout);
        Assert.Contains("Remote connection", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task ConfigureProviders_AppLevelProvider_IsUsedForPrompting()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionCommand>()
            .ConfigureProviders(providers =>
                providers.Add<string>("connection", _ =>
                    [new OptionItem<string>("app", "App connection")]))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("app", result.Stdout);
        Assert.Contains("App connection", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task AppLevelSimpleStringProvider_WorksForPromptChoices()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionCommand>()
            .ConfigureProviders(providers =>
                providers.Add("connection", _ => Task.FromResult(Strings("app-simple"))))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("app-simple", result.Stdout);
        Assert.Contains("app-simple", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task GroupLevelProvider_IsUsedForCommandsInsideGroup()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider<string>("connection", _ =>
                    [new OptionItem<string>("group", "Group connection")]);
                group.AddCommand<OptionCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("group", result.Stdout);
        Assert.Contains("Group connection", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task GroupLevelSimpleStringProvider_WorksForChildCommands()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider("connection", _ => Strings("group-simple"));
                group.AddCommand<OptionCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("group-simple", result.Stdout);
    }

    [Fact]
    public async Task ParentGroupProvider_IsInheritedByCommandsInNestedSubgroup()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommandGroup("projects", projects =>
            {
                projects.AddProvider("connection", _ => Strings("parent-provider"));
                projects.AddCommandGroup("sp", sp => sp.AddCommand<OptionCommand>("add"));
            })
            .Build();

        var result = await RunCapturedAsync(app, ["projects", "sp", "add"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("parent-provider", result.Stdout);
    }

    [Fact]
    public async Task SubgroupProvider_OverridesParentGroupProviderForSameKey()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommandGroup("projects", projects =>
            {
                projects.AddProvider("connection", _ => Strings("parent-provider"));
                projects.AddCommandGroup("sp", sp =>
                {
                    sp.AddProvider("connection", _ => Strings("subgroup-provider"));
                    sp.AddCommand<OptionCommand>("add");
                });
            })
            .Build();

        var result = await RunCapturedAsync(app, ["projects", "sp", "add"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("subgroup-provider", result.Stdout);
        Assert.DoesNotContain("parent-provider", result.Stdout);
    }

    [Fact]
    public async Task CommandLevelProvider_IsUsedOnlyForThatCommand()
    {
        var commandShell = new TestShell();
        commandShell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var otherShell = ShellWithText("typed");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<OptionCommand>(
                "run",
                command => command.AddProvider<string>("connection", _ =>
                    [new OptionItem<string>("command", "Command connection")]))
            .AddCommand<OptionCommand>("other")
            .Build();

        var commandResult = await RunCapturedAsync(app, ["run"], commandShell);
        var otherResult = await RunCapturedAsync(app, ["other"], otherShell);

        Assert.Equal(0, commandResult.ExitCode);
        Assert.Contains("command", commandResult.Stdout);
        Assert.Contains("Command connection", commandShell.Terminal.LastRenderedText);
        Assert.Equal(0, otherResult.ExitCode);
        Assert.Contains("typed", otherResult.Stdout);
    }

    [Fact]
    public async Task AppLevelProvider_IsVisibleToTopLevelNamedCommand()
    {
        // The TigerQuery lesson: a provider shared by the default command and a top-level named
        // command (e.g. `run`) belongs at app scope, where both commands can see it.
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<OptionCommand>("run")
            .ConfigureProviders(providers =>
                providers.Add("connection", _ => Strings("app-shared")))
            .Build();

        var result = await RunCapturedAsync(app, ["run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("app-shared", result.Stdout);
    }

    [Fact]
    public async Task GroupLevelProvider_IsNotVisibleToTopLevelCommandOutsideGroup()
    {
        // A provider registered inside a command group does NOT leak to top-level commands.
        // The top-level command falls back to automatic text input; the group provider is never
        // invoked for it. This is the dogfooding pitfall: a `connections` provider registered in
        // the connections group will not serve a top-level `-c|--connection` option.
        var groupProviderInvoked = false;
        var shell = ShellWithText("typed-fallback");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<OptionCommand>("run")
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider("connection", _ =>
                {
                    groupProviderInvoked = true;
                    return Strings("group-only");
                });
                group.AddCommand<OptionCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.False(groupProviderInvoked);          // group provider did not leak out of the group
        Assert.Contains("typed-fallback", result.Stdout); // top-level command used text input
        Assert.DoesNotContain("group-only", result.Stdout);
    }

    [Fact]
    public async Task RequiredPromptableProvider_Missing_SemiInteractive_PromptsAndUsesProviderChoice()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow); // move to second choice
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<RequiredPromptableProviderCommand>()
            .ConfigureProviders(providers =>
                providers.Add("connections", _ => Strings("local", "demo")))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("demo", result.Stdout); // the provider choice was used
    }

    [Fact]
    public async Task RequiredPromptableProvider_Missing_NonInteractive_FailsWithoutInvokingProvider()
    {
        var providerInvoked = false;
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<RequiredPromptableProviderCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers =>
                providers.Add("connections", _ =>
                {
                    providerInvoked = true;
                    return Strings("local", "demo");
                }))
            .Build();

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --connection", result.Stderr);
        Assert.False(providerInvoked);          // provider not consulted on the non-interactive failure
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task CommandLevelSimpleStringProvider_WorksForThatCommand()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<OptionCommand>(
                "run",
                command => command.AddProvider("connection", _ => Strings("command-simple")))
            .Build();

        var result = await RunCapturedAsync(app, ["run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("command-simple", result.Stdout);
    }

    [Fact]
    public async Task CommandLevelProvider_OverridesGroupAndAppProviderWithSameKey()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .ConfigureProviders(providers =>
                providers.Add<string>("connection", _ =>
                    [new OptionItem<string>("app", "App connection")]))
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider<string>("connection", _ =>
                    [new OptionItem<string>("group", "Group connection")]);
                group.AddCommand<OptionCommand>(
                    "test",
                    command => command.AddProvider<string>("connection", _ =>
                        [new OptionItem<string>("command", "Command connection")]));
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("command", result.Stdout);
        Assert.Contains("Command connection", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task CommandLevelSimpleStringProvider_OverridesGroupAndAppProvider()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .ConfigureProviders(providers =>
                providers.Add("connection", _ => Strings("app-simple")))
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider("connection", _ => Strings("group-simple"));
                group.AddCommand<OptionCommand>(
                    "test",
                    command => command.AddProvider("connection", _ => Strings("command-simple")));
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("command-simple", result.Stdout);
        Assert.DoesNotContain("group-simple", result.Stdout);
        Assert.DoesNotContain("app-simple", result.Stdout);
    }

    [Fact]
    public async Task GroupLevelProvider_OverridesAppLevelProviderWithSameKey()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .ConfigureProviders(providers =>
                providers.Add<string>("connection", _ =>
                    [new OptionItem<string>("app", "App connection")]))
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider<string>("connection", _ =>
                    [new OptionItem<string>("group", "Group connection")]);
                group.AddCommand<OptionCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("group", result.Stdout);
        Assert.Contains("Group connection", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task GroupLevelSimpleStringProvider_OverridesAppProvider()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .ConfigureProviders(providers =>
                providers.Add("connection", _ => Strings("app-simple")))
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider("connection", _ => Strings("group-simple"));
                group.AddCommand<OptionCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("group-simple", result.Stdout);
        Assert.DoesNotContain("app-simple", result.Stdout);
    }

    [Fact]
    public async Task GroupProvider_WorksWithCommandFactoryRegistration()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider<string>("connection", _ =>
                    [new OptionItem<string>("group-factory", "Group factory connection")]);
                group.AddCommand("test", () => new OptionCommand());
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("group-factory", result.Stdout);
        Assert.Contains("Group factory connection", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task SimpleStringProvider_WorksWithCommandFactoryRegistration()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider("connection", _ => Strings("factory-simple"));
                group.AddCommand("test", () => new OptionCommand());
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("factory-simple", result.Stdout);
    }

    [Fact]
    public async Task CommandFactoryProvider_OverridesGroupProvider()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider<string>("connection", _ =>
                    [new OptionItem<string>("group", "Group connection")]);
                group.AddCommand(
                    "test",
                    () => new OptionCommand(),
                    command => command.AddProvider<string>("connection", _ =>
                        [new OptionItem<string>("factory-command", "Factory command connection")]));
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("factory-command", result.Stdout);
        Assert.Contains("Factory command connection", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task SimpleStringProvider_MapsValueAndLabelToSameString()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionCommand>()
            .ConfigureProviders(providers =>
                providers.Add("connection", _ => Strings("same-value-and-label")))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("same-value-and-label", result.Stdout);
        Assert.Contains("same-value-and-label", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task ProviderBackedPrompt_RespectsEffectivePromptModeInheritance()
    {
        var called = false;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .AddCommandGroup("connections", group =>
            {
                group.SetPromptMode(TigerCliPromptMode.RequiredOnly);
                group.AddProvider<string>("connection", _ =>
                {
                    called = true;
                    return [new OptionItem<string>("inherited", "Inherited connection")];
                });
                group.AddCommand<OptionCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.True(called);
        Assert.Contains("inherited", result.Stdout);
    }

    [Fact]
    public async Task SimpleStringProvider_RespectsEffectivePromptModeInheritance()
    {
        var called = false;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .AddCommandGroup("connections", group =>
            {
                group.SetPromptMode(TigerCliPromptMode.RequiredOnly);
                group.AddProvider("connection", _ =>
                {
                    called = true;
                    return Strings("simple-inherited");
                });
                group.AddCommand<OptionCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.True(called);
        Assert.Contains("simple-inherited", result.Stdout);
    }

    [Fact]
    public async Task OptionProviderMetadata_SelectsAppLevelProvider()
    {
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionProviderMetadataCommand>()
            .ConfigureProviders(providers =>
                providers.Add("configured-connection", _ => Strings("app-option")))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("app-option", result.Stdout);
    }

    [Fact]
    public async Task ArgumentProviderMetadata_SelectsAppLevelProvider()
    {
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<ArgumentProviderMetadataCommand>()
            .ConfigureProviders(providers =>
                providers.Add("configured-database", _ => Strings("app-argument")))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("app-argument", result.Stdout);
    }

    [Fact]
    public async Task OptionProviderMetadata_SelectsGroupLevelProvider()
    {
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider("configured-connection", _ => Strings("group-option"));
                group.AddCommand<OptionProviderMetadataCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("group-option", result.Stdout);
    }

    [Fact]
    public async Task ArgumentProviderMetadata_SelectsGroupLevelProvider()
    {
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider("configured-database", _ => Strings("group-argument"));
                group.AddCommand<ArgumentProviderMetadataCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("group-argument", result.Stdout);
    }

    [Fact]
    public async Task OptionProviderMetadata_SelectsCommandLevelProvider()
    {
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<OptionProviderMetadataCommand>(
                "test",
                command => command.AddProvider("configured-connection", _ => Strings("command-option")))
            .Build();

        var result = await RunCapturedAsync(app, ["test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("command-option", result.Stdout);
    }

    [Fact]
    public async Task ArgumentProviderMetadata_SelectsCommandLevelProvider()
    {
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ArgumentProviderMetadataCommand>(
                "test",
                command => command.AddProvider("configured-database", _ => Strings("command-argument")))
            .Build();

        var result = await RunCapturedAsync(app, ["test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("command-argument", result.Stdout);
    }

    [Fact]
    public async Task ProviderMetadata_CommandProviderOverridesGroupAndAppProvider()
    {
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .ConfigureProviders(providers =>
                providers.Add("configured-connection", _ => Strings("app-option")))
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider("configured-connection", _ => Strings("group-option"));
                group.AddCommand<OptionProviderMetadataCommand>(
                    "test",
                    command => command.AddProvider("configured-connection", _ => Strings("command-option")));
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("command-option", result.Stdout);
    }

    [Fact]
    public async Task ProviderMetadata_GroupProviderOverridesAppProvider()
    {
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .ConfigureProviders(providers =>
                providers.Add("configured-connection", _ => Strings("app-option")))
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider("configured-connection", _ => Strings("group-option"));
                group.AddCommand<OptionProviderMetadataCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("group-option", result.Stdout);
    }

    [Fact]
    public async Task ProviderMetadata_UsesRichOptionItemProvider()
    {
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionProviderMetadataCommand>()
            .ConfigureProviders(providers =>
                providers.Add<string>("configured-connection", _ =>
                    [new OptionItem<string>("rich-key", "Rich label")]))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("rich-key", result.Stdout);
        Assert.Contains("Rich label", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task ProviderMetadata_NamedProviderOverridesConfigurePromptsAtSameScope()
    {
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionProviderMetadataCommand>()
            .ConfigurePrompts<OptionProviderMetadataSettings>(prompts =>
                prompts.For(s => s.ConnectionName).SelectFrom((_, _) =>
                    [new OptionItem<string>("legacy-property", "Legacy property")]))
            .ConfigureProviders(providers =>
                providers.Add("configured-connection", _ => Strings("explicit-provider")))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("explicit-provider", result.Stdout);
        Assert.DoesNotContain("legacy-property", result.Stdout);
    }

    [Fact]
    public async Task ProviderMetadata_MissingProviderFallsBackToAutomaticPrompt()
    {
        var shell = ShellWithText("typed-provider-missing");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionProviderMetadataCommand>()
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("typed-provider-missing", result.Stdout);
    }

    [Fact]
    public async Task ProviderMetadata_ProviderExceptionFailsThroughUnhandledExceptionPolicy()
    {
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionProviderMetadataCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.UnhandledException, 46)
            .ConfigureProviders(providers =>
                providers.Add(
                    "configured-connection",
                    (Func<TigerCliProviderContext, IReadOnlyList<string>>)
                        (_ => throw new InvalidOperationException("source unavailable"))))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("Prompt value provider failed for --connection: source unavailable", result.Stderr);
    }

    [Fact]
    public async Task ProviderMetadata_PromptableFalsePreventsProviderCall()
    {
        var called = false;
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<NotPromptableProviderMetadataCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers =>
                providers.Add("configured-connection", _ =>
                {
                    called = true;
                    return Strings("blocked");
                }))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.False(called);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ProviderMetadata_NonInteractivePreventsProviderCall()
    {
        var called = false;
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionProviderMetadataCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers =>
                providers.Add("configured-connection", _ =>
                {
                    called = true;
                    return Strings("blocked");
                }))
            .Build();

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.False(called);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ProviderMetadata_EffectivePromptModeControlsProviderPrompting()
    {
        var called = false;
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .AddCommandGroup("connections", group =>
            {
                group.SetPromptMode(TigerCliPromptMode.RequiredOnly);
                group.AddProvider("configured-connection", _ =>
                {
                    called = true;
                    return Strings("prompt-mode");
                });
                group.AddCommand<OptionProviderMetadataCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.True(called);
        Assert.Contains("prompt-mode", result.Stdout);
    }

    [Fact]
    public async Task ProviderMetadata_WorksWithCommandFactoryRegistration()
    {
        var shell = SelectFirstShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider("configured-connection", _ => Strings("factory-metadata"));
                group.AddCommand("test", () => new OptionProviderMetadataCommand());
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("factory-metadata", result.Stdout);
    }

    [Fact]
    public async Task Provider_ReceivesPartiallyBoundSettings()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        string? observedConnection = null;
        var app = App<ProjectCommand, ProjectSettings>(prompts =>
            prompts.For(s => s.ProjectName).SelectFrom((settings, _) =>
            {
                observedConnection = settings.ConnectionName;
                return [new OptionItem<string>("alpha", "Alpha")];
            }));

        var result = await RunCapturedAsync(app, ["local"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("local", observedConnection);
        Assert.Contains("local:alpha", result.Stdout);
    }

    [Fact]
    public async Task LaterProvider_CanDependOnPromptedEarlierPositionalValue()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<ProjectCommand, ProjectSettings>(prompts =>
        {
            prompts.For(s => s.ConnectionName).SelectFrom((_, _) =>
            [
                new OptionItem<string>("local", "Local connection"),
                new OptionItem<string>("remote", "Remote connection")
            ]);
            prompts.For(s => s.ProjectName).SelectFrom((settings, _) =>
                settings.ConnectionName == "local"
                    ?
                    [
                        new OptionItem<string>("alpha", "Alpha"),
                        new OptionItem<string>("beta", "Beta")
                    ]
                    :
                    [
                        new OptionItem<string>("gamma", "Gamma")
                    ]);
        });

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("local:beta", result.Stdout);
    }

    [Fact]
    public async Task PositionalPromptOrder_UsesArgumentIndexEvenWhenProvidersRegisteredOutOfOrder()
    {
        var calls = new List<string>();
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<ProjectCommand, ProjectSettings>(prompts =>
        {
            prompts.For(s => s.ProjectName).SelectFrom((settings, _) =>
            {
                calls.Add($"project:{settings.ConnectionName}");
                return [new OptionItem<string>("alpha", "Alpha")];
            });
            prompts.For(s => s.ConnectionName).SelectFrom((_, _) =>
            {
                calls.Add("connection");
                return [new OptionItem<string>("local", "Local connection")];
            });
        });

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["connection", "project:local"], calls);
        Assert.Contains("local:alpha", result.Stdout);
    }

    [Fact]
    public async Task NonInteractiveMode_DoesNotCallProvider()
    {
        var called = false;
        var shell = new TestShell();
        var app = App<OptionCommand, OptionSettings>(
            prompts => prompts.For(s => s.ConnectionName).SelectFrom((_, _) =>
            {
                called = true;
                return [new OptionItem<string>("local", "Local connection")];
            }),
            builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.False(called);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task NonInteractiveMode_DoesNotCallSimpleStringProvider()
    {
        var called = false;
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers =>
                providers.Add("connection", _ =>
                {
                    called = true;
                    return Strings("blocked");
                }))
            .Build();

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.False(called);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task PromptableFalse_PreventsProviderCall()
    {
        var called = false;
        var shell = new TestShell();
        var app = App<NotPromptableProviderCommand, NotPromptableProviderSettings>(
            prompts => prompts.For(s => s.ConnectionName).SelectFrom((_, _) =>
            {
                called = true;
                return [new OptionItem<string>("local", "Local connection")];
            }),
            builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.False(called);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task PromptableFalse_PreventsSimpleStringProviderCall()
    {
        var called = false;
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<NotPromptableProviderCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers =>
                providers.Add("connection", _ =>
                {
                    called = true;
                    return Strings("blocked");
                }))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.False(called);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ProviderNoChoices_ForRequiredValue_FailsValidationPolicy()
    {
        var shell = new TestShell();
        var app = App<OptionCommand, OptionSettings>(
            prompts => prompts.For(s => s.ConnectionName).SelectFrom((_, _) => []),
            builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("No prompt choices available for --connection.", result.Stderr);
    }

    [Fact]
    public async Task ProviderSingleChoice_ForRequiredValue_ShowsPromptByDefault()
    {
        var shell = SelectFirstShell();
        var app = App<OptionCommand, OptionSettings>(
            prompts => prompts.For(s => s.ConnectionName).SelectFrom((_, _) =>
                [new OptionItem<string>("only", "Only connection")]));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("only", result.Stdout);
        Assert.Contains("Only connection", shell.Terminal.LastRenderedText);
        Assert.True(shell.Terminal.ReadCount > 0);
    }

    [Fact]
    public async Task ProviderSingleChoice_ForRequiredValue_AutoSelectsWhenEnabled()
    {
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<AutoSelectRequiredPromptableProviderCommand>()
            .ConfigureProviders(providers => providers.Add("connections", _ => Strings("only")))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("only", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
        Assert.DoesNotContain("only", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task ProviderMultipleChoices_ForRequiredValue_ShowsPromptEvenWhenAutoSelectEnabled()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<AutoSelectRequiredPromptableProviderCommand>()
            .ConfigureProviders(providers => providers.Add("connections", _ => Strings("first", "second")))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("second", result.Stdout);
        Assert.Contains("second", shell.Terminal.LastRenderedText);
        Assert.True(shell.Terminal.ReadCount > 0);
    }

    [Fact]
    public async Task ProviderSingleChoice_ForArgument_AutoSelectsWhenEnabled()
    {
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<AutoSelectArgumentProviderCommand>()
            .ConfigureProviders(providers => providers.Add("connections", _ => Strings("argument-only")))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("argument-only", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ProviderSingleChoice_ForRequiredValue_NonInteractiveStillFailsWithoutInvokingProvider()
    {
        var called = false;
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<AutoSelectRequiredPromptableProviderCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers => providers.Add("connections", _ =>
            {
                called = true;
                return Strings("only");
            }))
            .Build();

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --connection", result.Stderr);
        Assert.False(called);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ProviderNoChoices_ForOptionalValue_SkipsPrompt()
    {
        var shell = new TestShell();
        var app = App<OptionalProviderCommand, OptionalProviderSettings>(
            prompts => prompts.For(s => s.Schema).SelectFrom((_, _) => []));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("dbo", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ProviderException_FailsThroughUnhandledExceptionPolicy()
    {
        var shell = new TestShell();
        var app = App<OptionCommand, OptionSettings>(
            prompts => prompts.For(s => s.ConnectionName).SelectFrom(ThrowingProvider),
            builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.UnhandledException, 46));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("Prompt value provider failed for --connection: source unavailable", result.Stderr);
    }

    [Fact]
    public async Task SimpleStringProviderException_FailsThroughUnhandledExceptionPolicy()
    {
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.UnhandledException, 46)
            .ConfigureProviders(providers =>
                providers.Add(
                    "connection",
                    (Func<TigerCliProviderContext, IReadOnlyList<string>>)
                        (_ => throw new InvalidOperationException("source unavailable"))))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("Prompt value provider failed for --connection: source unavailable", result.Stderr);
    }

    [Fact]
    public async Task ProviderDuplicateKeys_FailValidationPolicy()
    {
        var shell = new TestShell();
        var app = App<OptionCommand, OptionSettings>(
            prompts => prompts.For(s => s.ConnectionName).SelectFrom((_, _) =>
            [
                new OptionItem<string>("local", "Local A"),
                new OptionItem<string>("local", "Local B")
            ]),
            builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("duplicate key 'local'", result.Stderr);
    }

    [Fact]
    public async Task SelectedProviderKey_IsBoundInsteadOfLabel()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<OptionCommand, OptionSettings>(
            prompts => prompts.For(s => s.ConnectionName).SelectFrom((_, _) =>
                [new OptionItem<string>("local-key", "Human label")]));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("local-key", result.Stdout);
        Assert.DoesNotContain("Human label", result.Stdout);
        Assert.Contains("Human label", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task AutomaticStringPrompt_StillWorksWithoutProvider()
    {
        var shell = ShellWithText("typed");
        var app = App<OptionCommand, OptionSettings>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("typed", result.Stdout);
    }

    [Fact]
    public async Task MissingProviderBehavior_RemainsAutomaticStringPrompt()
    {
        var shell = ShellWithText("typed-without-provider");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<OptionCommand>()
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("typed-without-provider", result.Stdout);
    }

    private static TigerCliApp App<TCommand, TSettings>(
        Action<TigerCliPromptConfiguration<TSettings>>? configurePrompts = null,
        Action<TigerCliAppBuilder>? configureBuilder = null)
        where TCommand : class, new()
        where TSettings : TigerCliSettings
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<TCommand>();

        if (configurePrompts != null)
            builder.ConfigurePrompts(configurePrompts);

        configureBuilder?.Invoke(builder);
        return builder.Build();
    }

    private static TestShell ShellWithText(string value)
    {
        var shell = new TestShell();
        foreach (var ch in value)
        {
            var key = char.IsLetter(ch)
                ? Enum.Parse<ConsoleKey>(char.ToUpperInvariant(ch).ToString())
                : ConsoleKey.Spacebar;
            shell.Terminal.EnqueueKey(key, keyChar: ch);
        }
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        return shell;
    }

    private static TestShell SelectFirstShell()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        return shell;
    }

    private static IReadOnlyList<string> Strings(params string[] values) => values;

    private static IReadOnlyList<OptionItem<string>> ThrowingProvider(
        OptionSettings settings,
        TigerCliPromptContext context)
    {
        throw new InvalidOperationException("source unavailable");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args,
        ICliAppShell shell)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args, shell, ct: TestContext.Current.CancellationToken);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
