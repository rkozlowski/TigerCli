using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// App-level (default and top-level named) command factory overloads. These mirror the command-group
/// factory registration: a factory lets a default/top-level command receive constructor dependencies
/// without a DI container, while the parameterless generic registrations keep the new-constructor model.
/// </summary>
public sealed class TigerCliAppCommandFactoryTests
{
    private interface IFakeStore
    {
        string Describe();
    }

    private sealed class FakeStore : IFakeStore
    {
        private readonly string _name;
        public FakeStore(string name) => _name = name;
        public string Describe() => _name;
    }

    private sealed class QuerySettings : TigerCliSettings
    {
    }

    // Handler with NO parameterless constructor: it can only be created through a factory.
    private sealed class QueryCommand : TigerCliAsyncCommandHandler<QuerySettings>
    {
        private readonly IFakeStore _store;
        public QueryCommand(IFakeStore store) => _store = store;

        public override Task<int> ExecuteAsync(QuerySettings settings)
        {
            TigerConsole.MarkupLine($"query:{CliMarkupParser.Escape(_store.Describe())}");
            return Task.FromResult(0);
        }
    }

    private sealed class RunSettings : TigerCliSettings
    {
    }

    private sealed class RunCommand : TigerCliAsyncCommandHandler<RunSettings>
    {
        private readonly IFakeStore _store;
        public RunCommand(IFakeStore store) => _store = store;

        public override Task<int> ExecuteAsync(RunSettings settings)
        {
            TigerConsole.MarkupLine($"run:{CliMarkupParser.Escape(_store.Describe())}");
            return Task.FromResult(0);
        }
    }

    // Parameterless handler used to prove the generic (non-factory) registrations still work.
    private sealed class PlainSettings : TigerCliSettings
    {
    }

    private sealed class PlainCommand : TigerCliAsyncCommandHandler<PlainSettings>
    {
        public override Task<int> ExecuteAsync(PlainSettings settings)
        {
            TigerConsole.MarkupLine("plain");
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task DefaultCommandFactory_CreatesHandlerWithInjectedDependency()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("factory-test")
            .SetDefaultCommand(() => new QueryCommand(new FakeStore("store-A")))
            .Build();

        var result = await RunCapturedAsync(app, []);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("query:store-A", result.Stdout);
    }

    [Fact]
    public async Task TopLevelNamedCommandFactory_CreatesHandlerWithInjectedDependency()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("factory-test")
            .AddCommand("run", () => new RunCommand(new FakeStore("store-B")))
            .Build();

        var result = await RunCapturedAsync(app, ["run"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("run:store-B", result.Stdout);
    }

    [Fact]
    public async Task Factory_IsInvokedEachTimeCommandRuns()
    {
        var invocations = 0;
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("factory-test")
            .AddCommand("run", () =>
            {
                invocations++;
                return new RunCommand(new FakeStore("store-C"));
            })
            .Build();

        await RunCapturedAsync(app, ["run"]);
        await RunCapturedAsync(app, ["run"]);

        // Factory is invoked only when the command actually executes, once per run.
        Assert.Equal(2, invocations);
    }

    [Fact]
    public async Task Factory_IsNotInvokedWhenCommandDoesNotRun()
    {
        var invoked = false;
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("factory-test")
            .AddCommand("run", () =>
            {
                invoked = true;
                return new RunCommand(new FakeStore("store-D"));
            })
            .Build();

        // Help is requested instead of running the command; the factory must not be called.
        var result = await RunCapturedAsync(app, ["run", "--help"]);

        Assert.False(invoked);
    }

    [Fact]
    public async Task GenericDefaultRegistration_StillUsesParameterlessConstructor()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("factory-test")
            .SetDefaultCommand<PlainCommand>()
            .Build();

        var result = await RunCapturedAsync(app, []);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("plain", result.Stdout);
    }

    [Fact]
    public async Task GenericNamedRegistration_StillUsesParameterlessConstructor()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("factory-test")
            .AddCommand<PlainCommand>("plain")
            .Build();

        var result = await RunCapturedAsync(app, ["plain"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("plain", result.Stdout);
    }

    [Fact]
    public async Task FactoryDefaultAndFactoryNamedCommand_Coexist_AndDispatchIndependently()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("factory-test")
            .SetDefaultCommand(() => new QueryCommand(new FakeStore("default-store")))
            .AddCommand("run", () => new RunCommand(new FakeStore("run-store")))
            .Build();

        var defaultResult = await RunCapturedAsync(app, []);
        var runResult = await RunCapturedAsync(app, ["run"]);

        Assert.Equal(0, defaultResult.ExitCode);
        Assert.Contains("query:default-store", defaultResult.Stdout);
        Assert.Equal(0, runResult.ExitCode);
        Assert.Contains("run:run-store", runResult.Stdout);
    }

    [Fact]
    public async Task NamedCommandFactory_ListedInHelp_LikeGenericRegistration()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("factory-test")
            .AddCommand("run", () => new RunCommand(new FakeStore("store")), "Runs the thing.")
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("run", result.Stdout);
        Assert.Contains("Runs the thing.", result.Stdout);
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
            var exitCode = await app.RunAsync(args, promptShell: null, ct: TestContext.Current.CancellationToken);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
