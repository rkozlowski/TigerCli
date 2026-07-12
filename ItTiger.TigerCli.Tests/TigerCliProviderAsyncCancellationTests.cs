using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

// Stage 1 of the slow-provider plan: providers have a clear async/cancellable invocation path while
// existing sync registrations stay source-compatible. These tests cover the new async registration
// surface (AddAsyncProvider / AddAsync), that the provider context carries the effective run token,
// and that cooperative cancellation is preserved rather than masked as a provider/validation error.
public sealed class TigerCliProviderAsyncCancellationTests
{
    private sealed class ConnectionOptionSettings : TigerCliSettings
    {
        [TigerCliOption("--connection", Required = true, Description = "Connection")]
        public string Connection { get; set; } = string.Empty;
    }

    private sealed class ProviderValidatedSettings : TigerCliSettings
    {
        [TigerCliOption("--connection", Required = true, Provider = "configured-connection", Description = "Connection")]
        public string Connection { get; set; } = string.Empty;
    }

    private sealed class ConnectionCommand : TigerCliAsyncCommandHandler<ConnectionOptionSettings>
    {
        public override Task<int> ExecuteAsync(ConnectionOptionSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Connection));
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderValidatedCommand : TigerCliAsyncCommandHandler<ProviderValidatedSettings>
    {
        public override Task<int> ExecuteAsync(ProviderValidatedSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Connection));
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task AddAsyncProvider_CommandLevel_ProvidesPromptChoices()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>("connection", async _ =>
                {
                    await Task.Yield();
                    return [new OptionItem<string>("async-pick", "Async choice")];
                }))
            .Build();

        var result = await RunCapturedAsync(app, ["run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("async-pick", result.Stdout);
        Assert.Contains("Async choice", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task AddAsyncProvider_GroupLevel_ProvidesPromptChoices()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommandGroup("connections", group =>
            {
                group.AddAsyncProvider<string>("connection", async _ =>
                {
                    await Task.Yield();
                    return [new OptionItem<string>("group-async", "Group async")];
                });
                group.AddCommand<ConnectionCommand>("test");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["connections", "test"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("group-async", result.Stdout);
    }

    [Fact]
    public async Task AddAsync_AppLevel_ProvidesPromptChoices()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<ConnectionCommand>()
            .ConfigureProviders(providers =>
                providers.AddAsync<string>("connection", async _ =>
                {
                    await Task.Yield();
                    return [new OptionItem<string>("app-async", "App async")];
                }))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("app-async", result.Stdout);
    }

    [Fact]
    public async Task SyncProvider_RemainsSourceCompatible()
    {
        // The existing sync registration shape must keep compiling and behaving unchanged.
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddProvider<string>("connection", _ =>
                    [new OptionItem<string>("sync-pick", "Sync choice")]))
            .Build();

        var result = await RunCapturedAsync(app, ["run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("sync-pick", result.Stdout);
    }

    [Fact]
    public async Task InteractiveProvider_ReceivesCancellableEffectiveToken()
    {
        // On the interactive path the provider receives a token linked to the caller's token (so the
        // loading UI can cancel the provider independently). It is therefore cancellable and reflects the
        // caller's cancellation, but is not reference-identical to the caller token.
        using var cts = new CancellationTokenSource();
        var canBeCanceled = false;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>("connection", async context =>
                {
                    canBeCanceled = context.CancellationToken.CanBeCanceled;
                    await Task.Yield();
                    return [new OptionItem<string>("ok", "Ok")];
                }))
            .Build();

        var result = await RunCapturedWithTokenAsync(app, ["run"], shell, cts.Token);

        Assert.Equal(0, result.ExitCode);
        Assert.True(canBeCanceled);
    }

    [Fact]
    public async Task NonInteractiveProvider_ReceivesCallerTokenDirectly()
    {
        // The non-interactive (validation) path resolves the provider directly with the caller's token —
        // no loading UI, no linked token — so the provider sees the effective run token itself.
        using var cts = new CancellationTokenSource();
        CancellationToken observed = default;
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<ProviderValidatedCommand>()
            .ConfigureProviders(providers =>
                providers.AddAsync<string>("configured-connection", async context =>
                {
                    observed = context.CancellationToken;
                    await Task.Yield();
                    return [new OptionItem<string>("known", "Known")];
                }))
            .Build();

        var result = await RunCapturedWithTokenAsync(app, ["--connection", "known", "--non-interactive"], shell, cts.Token);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task AsyncProvider_CooperativeCancellation_SurfacesAsPromptCanceled_NotProviderError()
    {
        using var cts = new CancellationTokenSource();
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .UseExitCodes(0, -1)
                .ExitKind(TigerCliExitKind.Cancelled, 77)
                .ExitKind(TigerCliExitKind.UnhandledException, 88)
                .ExitKind(TigerCliExitKind.ValidationError, 99)
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>("connection", async context =>
                {
                    // Slow provider observes the effective token and cancels cooperatively.
                    await cts.CancelAsync();
                    context.CancellationToken.ThrowIfCancellationRequested();
                    return [new OptionItem<string>("never", "Never")];
                }))
            .Build();

        var result = await RunCapturedWithTokenAsync(app, ["run"], shell, cts.Token);

        // Cooperative cancellation maps onto the prompt-cancellation model (gentle "Cancelled." →
        // TigerCliExitKind.Cancelled), never the provider-failure or validation path.
        Assert.Equal(77, result.ExitCode);
        Assert.Contains("Cancelled.", result.Stderr);
        Assert.DoesNotContain("provider failed", result.Stderr);
    }

    [Fact]
    public async Task AsyncProvider_UnrelatedOperationCanceled_IsStillReportedAsProviderError()
    {
        // An OperationCanceledException that is NOT caused by the effective token is a genuine provider
        // fault and must keep flowing through the provider-failure path — the cancellation handling is
        // precise, not a blanket swallow of every OperationCanceledException.
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .UseExitCodes(0, -1)
                .ExitKind(TigerCliExitKind.InteractiveNotAllowed, 77)
                .ExitKind(TigerCliExitKind.UnhandledException, 88)
                .ExitKind(TigerCliExitKind.ValidationError, 99)
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>("connection", _ =>
                    throw new OperationCanceledException("unrelated")))
            .Build();

        var result = await RunCapturedAsync(app, ["run"], shell);

        Assert.Equal(88, result.ExitCode);
        Assert.Contains("provider failed", result.Stderr);
    }

    [Fact]
    public async Task NonInteractive_AsyncProvider_ValidatesSuppliedValue()
    {
        var invoked = false;
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<ProviderValidatedCommand>()
            .ConfigureProviders(providers =>
                providers.AddAsync<string>("configured-connection", async _ =>
                {
                    invoked = true;
                    await Task.Yield();
                    return [new OptionItem<string>("known", "Known")];
                }))
            .Build();

        var result = await RunCapturedAsync(app, ["--connection", "known", "--non-interactive"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.True(invoked);                 // async provider runs in the non-interactive validation path
        Assert.Contains("known", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task NonInteractive_AsyncProvider_RejectsUnknownSuppliedValue()
    {
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<ProviderValidatedCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers =>
                providers.AddAsync<string>("configured-connection", async _ =>
                {
                    await Task.Yield();
                    return [new OptionItem<string>("known", "Known")];
                }))
            .Build();

        var result = await RunCapturedAsync(app, ["--connection", "unknown", "--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
    }

    private static Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args,
        ICliAppShell shell)
        => RunCapturedWithTokenAsync(app, args, shell, TestContext.Current.CancellationToken);

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedWithTokenAsync(
        TigerCliApp app,
        string[] args,
        ICliAppShell shell,
        CancellationToken ct)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args, shell, ct: ct);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
