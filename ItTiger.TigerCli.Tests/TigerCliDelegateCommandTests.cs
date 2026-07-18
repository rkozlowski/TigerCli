using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Delegate commands are thin registrations over the normal settings/handler pipeline. These tests
/// cover their exit semantics, dispatch/help behavior, settings instance, and existing failure and
/// command-name rules without introducing typed lambda-parameter binding.
/// </summary>
public sealed class TigerCliDelegateCommandTests
{
    [Fact]
    public async Task DefaultAction_ResolvesSuccessThroughExitPolicy()
    {
        var invoked = false;
        var app = Builder()
            .AddDefaultCommand(() => invoked = true)
            .UseExitCodes(17, 91)
            .Build();

        var result = await RunAsync(app);

        Assert.True(invoked);
        Assert.Equal(17, result.ExitCode);
    }

    [Fact]
    public async Task DefaultExitKind_ResolvesThroughExitPolicy()
    {
        var app = Builder()
            .AddDefaultCommand(() => TigerCliExitKind.ValidationError)
            .UseExitCodes(0, 1)
            .ExitKind(TigerCliExitKind.ValidationError, 42)
            .Build();

        var result = await RunAsync(app);

        Assert.Equal(42, result.ExitCode);
    }

    [Fact]
    public async Task DefaultInt_ReturnsRawIntegerExit()
    {
        var app = Builder()
            .AddDefaultCommand(() => 37)
            .UseExitCodes(0, 99)
            .Build();

        var result = await RunAsync(app);

        Assert.Equal(37, result.ExitCode);
    }

    [Fact]
    public async Task DefaultAsyncDelegate_IsAwaited()
    {
        var completed = false;
        var app = Builder()
            .AddDefaultCommand(async () =>
            {
                await Task.Yield();
                completed = true;
                return 23;
            })
            .Build();

        var result = await RunAsync(app);

        Assert.True(completed);
        Assert.Equal(23, result.ExitCode);
    }

    [Fact]
    public async Task NamedDelegate_IsDiscoverableAndExecutable()
    {
        var invoked = false;
        var app = Builder()
            .AddCommand("ping", () =>
            {
                invoked = true;
                return 12;
            })
            .Build();

        var result = await RunAsync(app, "ping");

        Assert.True(invoked);
        Assert.Equal(12, result.ExitCode);
    }

    [Fact]
    public async Task NamedDelegate_AppearsInFlatCommandHelp()
    {
        var app = Builder()
            .AddCommand("ping", () => { }, "Checks connectivity.")
            .Build();

        var result = await RunAsync(app, "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ping", result.StdOut);
        Assert.Contains("Checks connectivity.", result.StdOut);
    }

    [Fact]
    public async Task DefaultDelegateDescription_AppearsInRootHelp()
    {
        var app = Builder()
            .AddDefaultCommand(() => { }, "Runs the tiny tool.")
            .Build();

        var result = await RunAsync(app, "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Runs the tiny tool.", result.StdOut);
    }

    [Fact]
    public async Task SettingsAwareDelegate_ReceivesFrameworkSettings()
    {
        TigerCliSettings? received = null;
        var app = Builder()
            .AddDefaultCommand(settings =>
            {
                received = settings;
                return TigerCliExitKind.Success;
            })
            .Build();

        var result = await RunAsync(app);

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(received);
        Assert.IsAssignableFrom<TigerCliSettings>(received);
    }

    [Fact]
    public async Task DelegateException_UsesNormalUnhandledExceptionBehavior()
    {
        var app = Builder()
            .AddDefaultCommand((Action)(() => throw new InvalidOperationException("delegate failed")))
            .UseExitCodes(0, 1)
            .ExitKind(TigerCliExitKind.UnhandledException, 73)
            .Build();

        var result = await RunAsync(app);

        Assert.Equal(73, result.ExitCode);
        Assert.Contains("delegate failed", result.StdErr);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-bad")]
    [InlineData("two tokens")]
    public void NamedDelegate_RejectsNamesLikeExistingNamedCommands(string name)
    {
        var builder = Builder();

        Assert.Throws<ArgumentException>(() => builder.AddCommand(name, () => { }));
    }

    [Fact]
    public async Task DuplicateNamedDelegate_PreservesExistingFirstRegistrationBehavior()
    {
        var firstInvoked = false;
        var secondInvoked = false;
        var app = Builder()
            .AddCommand("run", () => firstInvoked = true)
            .AddCommand("RUN", () => secondInvoked = true)
            .Build();

        var result = await RunAsync(app, "run");

        Assert.Equal(0, result.ExitCode);
        Assert.True(firstInvoked);
        Assert.False(secondInvoked);
    }

    private static TigerCliAppBuilder Builder() =>
        TigerCliApp.CreateBuilder().SetApplicationName("delegate-test");

    private static Task<TigerCliAppRunResult> RunAsync(TigerCliApp app, params string[] args) =>
        TigerCliAppTestHost
            .For(app)
            .WithArgs(args)
            .RunAsync(TestContext.Current.CancellationToken);
}
