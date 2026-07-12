using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliCommandGroupTests
{
    // ── Reusable-library style commands ─────────────────────────────

    private interface IFakeConnectionService
    {
        string Describe();
    }

    private sealed class FakeConnectionService : IFakeConnectionService
    {
        private readonly string _name;
        public FakeConnectionService(string name) => _name = name;
        public string Describe() => _name;
    }

    private sealed class ListConnectionsSettings : TigerCliSettings
    {
    }

    private sealed class ListConnectionsCommand : TigerCliAsyncCommandHandler<ListConnectionsSettings>
    {
        private readonly IFakeConnectionService _service;
        public ListConnectionsCommand(IFakeConnectionService service) => _service = service;

        public override Task<int> ExecuteAsync(ListConnectionsSettings settings)
        {
            TigerConsole.MarkupLine($"list:{CliMarkupParser.Escape(_service.Describe())}");
            return Task.FromResult(0);
        }
    }

    private sealed class AddConnectionSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "name", Description = "Connection name.")]
        public string Name { get; set; } = string.Empty;

        [TigerCliOption("--database", Description = "Database name.")]
        public string Database { get; set; } = string.Empty;
    }

    private sealed class AddConnectionCommand : TigerCliAsyncCommandHandler<AddConnectionSettings>
    {
        private readonly IFakeConnectionService _service;
        private readonly bool _databaseRequired;

        public AddConnectionCommand(IFakeConnectionService service, bool databaseRequired)
        {
            _service = service;
            _databaseRequired = databaseRequired;
        }

        public override Task<int> ExecuteAsync(AddConnectionSettings settings)
        {
            TigerConsole.MarkupLine(
                $"add:{CliMarkupParser.Escape(settings.Name)}:{CliMarkupParser.Escape(settings.Database)}:required={_databaseRequired}:{CliMarkupParser.Escape(_service.Describe())}");
            return Task.FromResult(0);
        }
    }

    /// <summary>
    /// Mirrors the reusable command-library pattern: a static helper populates a group
    /// the consuming app supplies, passing services and options through command factories.
    /// </summary>
    private sealed class ConnectionCommandOptions
    {
        public IFakeConnectionService ConnectionService { get; set; } = new FakeConnectionService("default");
        public bool DatabaseRequired { get; set; }
    }

    private static class ConnectionCommands
    {
        public static void Configure(
            TigerCliCommandGroupBuilder group,
            Action<ConnectionCommandOptions>? configure = null)
        {
            var options = new ConnectionCommandOptions();
            configure?.Invoke(options);

            group.AddCommand("list", () => new ListConnectionsCommand(options.ConnectionService),
                "List saved connections.");
            group.AddCommand("add", () => new AddConnectionCommand(options.ConnectionService, options.DatabaseRequired),
                "Add a saved connection.");
        }
    }

    // ── Parameterless group command (no factory) ────────────────────

    private sealed class PingSettings : TigerCliSettings
    {
    }

    private sealed class PingCommand : TigerCliAsyncCommandHandler<PingSettings>
    {
        public override Task<int> ExecuteAsync(PingSettings settings)
        {
            TigerConsole.MarkupLine("pong");
            return Task.FromResult(0);
        }
    }

    // ── Tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task GroupCommand_RegistersAndExecutesNestedPath()
    {
        var app = CreateConnectionsApp(new FakeConnectionService("svc-A"));

        var result = await RunCapturedAsync(app, ["connections", "list"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("list:svc-A", result.Stdout);
    }

    [Fact]
    public async Task NestedCommandPath_BindsArgumentsAndOptions()
    {
        var app = CreateConnectionsApp(new FakeConnectionService("svc-A"));

        var result = await RunCapturedAsync(app, ["connections", "add", "MyConn", "--database", "SalesDb"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("add:MyConn:SalesDb:required=False:svc-A", result.Stdout);
    }

    [Fact]
    public async Task ParameterlessGroupCommand_UsesDefaultConstructor()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tiger-sqlcmd")
            .AddCommandGroup("diagnostics", group =>
            {
                group.SetDescription("Diagnostics tools.");
                group.AddCommand<PingCommand>("ping", "Pings the server.");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["diagnostics", "ping"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("pong", result.Stdout);
    }

    [Fact]
    public async Task CommandFactory_IsUsedToCreateHandler()
    {
        // A distinct service value can only appear in output if the factory ran.
        var app = CreateConnectionsApp(new FakeConnectionService("factory-built"));

        var result = await RunCapturedAsync(app, ["connections", "list"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("list:factory-built", result.Stdout);
    }

    [Fact]
    public async Task FactoryOptions_FlowIntoGroupCommands()
    {
        var app = CreateConnectionsApp(new FakeConnectionService("svc-A"), databaseRequired: true);

        var result = await RunCapturedAsync(app, ["connections", "add", "MyConn"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("required=True", result.Stdout);
    }

    [Fact]
    public async Task TopLevelHelp_ShowsGroupDescription()
    {
        var app = CreateConnectionsApp(new FakeConnectionService("svc-A"));

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("connections", result.Stdout);
        Assert.Contains("Manage saved connections", result.Stdout);
    }

    [Fact]
    public async Task TopLevelHelp_ListsGroupOnly_NotNestedCommands()
    {
        var app = CreateConnectionsApp(new FakeConnectionService("svc-A"));

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("connections list", result.Stdout);
        Assert.DoesNotContain("connections add", result.Stdout);
    }

    [Fact]
    public async Task GroupHelp_ListsImmediateChildCommands()
    {
        var app = CreateConnectionsApp(new FakeConnectionService("svc-A"));

        var result = await RunCapturedAsync(app, ["connections", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tiger-sqlcmd connections", result.Stdout);
        Assert.Contains("Manage saved connections", result.Stdout);
        Assert.Contains("list", result.Stdout);
        Assert.Contains("List saved connections.", result.Stdout);
        Assert.Contains("add", result.Stdout);
        Assert.Contains("Add a saved connection.", result.Stdout);
    }

    [Fact]
    public async Task BareGroupInvocation_ShowsGroupHelp()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tiger-sqlcmd")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.NoCommand, 3)
            .AddCommandGroup("connections", group =>
            {
                group.SetDescription("Manage saved connections");
                ConnectionCommands.Configure(group, options =>
                    options.ConnectionService = new FakeConnectionService("svc-A"));
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections"]);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("tiger-sqlcmd connections", result.Stdout);
        Assert.Contains("List saved connections.", result.Stdout);
    }

    [Fact]
    public async Task LeafCommandHelp_WorksNormally()
    {
        var app = CreateConnectionsApp(new FakeConnectionService("svc-A"));

        var result = await RunCapturedAsync(app, ["connections", "add", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tiger-sqlcmd connections add <name> [options]", result.Stdout);
        Assert.Contains("<name>", result.Stdout);
        Assert.Contains("--database", result.Stdout);
    }

    [Fact]
    public void RootMultiTokenCommandName_IsRejected()
    {
        // Flattened groups are removed: a multi-token path must be owned by a command group.
        var builder = TigerCliApp.CreateBuilder().SetApplicationName("tiger-sqlcmd");

        Assert.Throws<ArgumentException>(() => builder.AddCommand<PingCommand>("server ping"));
    }

    [Fact]
    public void GroupChildMultiTokenName_IsRejected()
    {
        var builder = TigerCliApp.CreateBuilder().SetApplicationName("tiger-sqlcmd");

        Assert.Throws<ArgumentException>(() => builder.AddCommandGroup(
            "server", group => group.AddCommand<PingCommand>("ping now")));
    }

    [Fact]
    public async Task ExistingDefaultCommand_RemainsUnchangedAlongsideGroups()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tiger-sqlcmd")
            .SetDefaultCommand<PingCommand>()
            .AddCommandGroup("connections", group =>
            {
                group.SetDescription("Manage saved connections");
                ConnectionCommands.Configure(group, options =>
                    options.ConnectionService = new FakeConnectionService("svc-A"));
            })
            .Build();

        var defaultRun = await RunCapturedAsync(app, []);
        Assert.Equal(0, defaultRun.ExitCode);
        Assert.Contains("pong", defaultRun.Stdout);

        var groupRun = await RunCapturedAsync(
            CreateConnectionsApp(new FakeConnectionService("svc-A")),
            ["connections", "list"]);
        Assert.Equal(0, groupRun.ExitCode);
        Assert.Contains("list:svc-A", groupRun.Stdout);
    }

    [Fact]
    public async Task TestHost_RunsGroupedCommandThroughAppPipeline()
    {
        var app = CreateConnectionsApp(new FakeConnectionService("svc-host"));

        var result = await TigerCliAppTestHost
            .For(app)
            .WithArgs("connections", "list")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("list:svc-host", result.StdOut);
    }

    // ── Nested subgroups ─────────────────────────────────────────────

    private static TigerCliApp CreateProjectsApp()
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("tiger-sqlcmd")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.NoCommand, 3)
            .AddCommandGroup("projects", projects =>
            {
                projects.SetDescription("Manage projects");
                projects.AddCommand<PingCommand>("list", "List projects.");
                projects.AddCommandGroup("sp", sp =>
                {
                    sp.SetDescription("Stored-procedure tools");
                    sp.AddCommand<PingCommand>("add", "Add a stored procedure.");
                });
            })
            .Build();
    }

    [Fact]
    public async Task NestedSubgroup_RegistersAndExecutesDeepPath()
    {
        var app = CreateProjectsApp();

        var result = await RunCapturedAsync(app, ["projects", "sp", "add"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("pong", result.Stdout);
    }

    [Fact]
    public async Task TopLevelHelp_ListsParentGroupOnly_NotSubgroupsOrDeepCommands()
    {
        var app = CreateProjectsApp();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("projects", result.Stdout);
        Assert.Contains("Manage projects", result.Stdout);
        // The subgroup and deep command are represented by the parent group entry, not flattened.
        Assert.DoesNotContain("Stored-procedure tools", result.Stdout);
        Assert.DoesNotContain("projects sp", result.Stdout);
    }

    [Fact]
    public async Task GroupHelp_ListsImmediateChildCommandsAndSubgroups()
    {
        var app = CreateProjectsApp();

        var result = await RunCapturedAsync(app, ["projects", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tiger-sqlcmd projects", result.Stdout);
        Assert.Contains("list", result.Stdout);
        Assert.Contains("List projects.", result.Stdout);
        // The subgroup is listed by its relative name and its own description...
        Assert.Contains("sp", result.Stdout);
        Assert.Contains("Stored-procedure tools", result.Stdout);
        // ...but its child command is not flattened into the parent's help.
        Assert.DoesNotContain("Add a stored procedure.", result.Stdout);
    }

    [Fact]
    public async Task SubgroupHelp_ListsItsOwnChildCommands()
    {
        var app = CreateProjectsApp();

        var result = await RunCapturedAsync(app, ["projects", "sp", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tiger-sqlcmd projects sp", result.Stdout);
        Assert.Contains("Stored-procedure tools", result.Stdout);
        Assert.Contains("add", result.Stdout);
        Assert.Contains("Add a stored procedure.", result.Stdout);
    }

    [Fact]
    public async Task BareSubgroupInvocation_ShowsSubgroupHelp()
    {
        var app = CreateProjectsApp();

        var result = await RunCapturedAsync(app, ["projects", "sp"]);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("tiger-sqlcmd projects sp", result.Stdout);
        Assert.Contains("Add a stored procedure.", result.Stdout);
    }

    [Fact]
    public void NestedSubgroupChild_MultiTokenName_IsRejected()
    {
        var builder = TigerCliApp.CreateBuilder().SetApplicationName("tiger-sqlcmd");

        Assert.Throws<ArgumentException>(() => builder.AddCommandGroup(
            "projects", projects => projects.AddCommandGroup(
                "sp", sp => sp.AddCommand<PingCommand>("add now"))));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static TigerCliApp CreateConnectionsApp(
        IFakeConnectionService service,
        bool databaseRequired = false)
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("tiger-sqlcmd")
            .AddCommandGroup("connections", group =>
            {
                group.SetDescription("Manage saved connections");
                ConnectionCommands.Configure(group, options =>
                {
                    options.ConnectionService = service;
                    options.DatabaseRequired = databaseRequired;
                });
            })
            .Build();
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
