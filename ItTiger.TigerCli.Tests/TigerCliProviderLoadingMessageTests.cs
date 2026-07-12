using System.Globalization;
using System.Resources;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

// Stage 3 of the slow-provider plan: a provider registration can optionally specify a custom loading
// message (literal or app resource key) for the Stage 2 loading UI. The generic localized
// "Loading options…" remains the default. These tests drive the real modal loop through app.RunAsync with
// gated providers.
public sealed class TigerCliProviderLoadingMessageTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // Culture-agnostic in-memory ResourceManager: a configured key resolves to its value regardless of the
    // active culture, so the test does not depend on the app's resolved run-culture name.
    private sealed class FakeResources : ResourceManager
    {
        private readonly Dictionary<string, string> _values;

        public FakeResources(Dictionary<string, string> values)
            : base("FakeResources", typeof(FakeResources).Assembly)
            => _values = values;

        public override string? GetString(string name, CultureInfo? culture)
            => _values.TryGetValue(name, out var value) ? value : null;

        public override string? GetString(string name) => GetString(name, CultureInfo.CurrentUICulture);
    }

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
    public async Task NoCustomMessage_ShowsGenericLoadingMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shell = new TestShell(useManualClock: true);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>("connection", GatedProvider(gate)))
            .Build();

        var (run, restore) = StartRun(app, ["run"], shell, ct);
        try
        {
            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
            Assert.Contains("Loading options", shell.Terminal.LastRenderedText);
        }
        finally
        {
            gate.TrySetResult();
            restore();
        }
    }

    [Fact]
    public async Task CustomLiteralMessage_AsyncProvider_IsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shell = new TestShell(useManualClock: true);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>(
                    "connection",
                    GatedProvider(gate),
                    configure: options => options.LoadingMessage("Scanning media roots")))
            .Build();

        var (run, restore) = StartRun(app, ["run"], shell, ct);
        try
        {
            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
            Assert.Contains("Scanning media roots", shell.Terminal.LastRenderedText);
            Assert.DoesNotContain("Loading options", shell.Terminal.LastRenderedText);
        }
        finally
        {
            gate.TrySetResult();
            restore();
        }
    }

    [Fact]
    public async Task CustomLiteralMessage_SyncOriginProvider_IsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        using var gate = new ManualResetEventSlim(false);
        var shell = new TestShell(useManualClock: true);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddProvider<string>(
                    "connection",
                    _ =>
                    {
                        gate.Wait(Timeout);
                        return [new OptionItem<string>("synced", "Synced choice")];
                    },
                    configure: options => options.LoadingMessage("Loading destination groups")))
            .Build();

        var (run, restore) = StartRun(app, ["run"], shell, ct);
        try
        {
            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
            Assert.Contains("Loading destination groups", shell.Terminal.LastRenderedText);
        }
        finally
        {
            gate.Set();
            restore();
        }
    }

    [Fact]
    public async Task CustomResourceKeyMessage_ResolvesLocalizedText()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rm = new FakeResources(new() { ["Provider_Loading_Scan"] = "Scanning destination groups" });
        var shell = new TestShell(useManualClock: true);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .UseAppResources(rm)
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>(
                    "connection",
                    GatedProvider(gate),
                    configure: options => options.LoadingMessageResource("Provider_Loading_Scan")))
            .Build();

        var (run, restore) = StartRun(app, ["run"], shell, ct);
        try
        {
            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
            Assert.Contains("Scanning destination groups", shell.Terminal.LastRenderedText);
            Assert.DoesNotContain("Provider_Loading_Scan", shell.Terminal.LastRenderedText); // key never surfaces
        }
        finally
        {
            gate.TrySetResult();
            restore();
        }
    }

    [Fact]
    public async Task CustomResourceKey_Missing_FallsBackToLiteral()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shell = new TestShell(useManualClock: true);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>(
                    "connection",
                    GatedProvider(gate),
                    // No app resources registered → the key can't resolve → the literal fallback shows.
                    configure: options => options.LoadingMessageResource("Missing_Key", fallback: "Scanning roots")))
            .Build();

        var (run, restore) = StartRun(app, ["run"], shell, ct);
        try
        {
            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
            Assert.Contains("Scanning roots", shell.Terminal.LastRenderedText);
            Assert.DoesNotContain("Missing_Key", shell.Terminal.LastRenderedText);
        }
        finally
        {
            gate.TrySetResult();
            restore();
        }
    }

    [Fact]
    public async Task FastProvider_WithCustomMessage_DoesNotShowLoadingUi()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>(
                    "connection",
                    _ => Task.FromResult<IReadOnlyList<OptionItem<string>>>(
                        [new OptionItem<string>("fast", "Fast choice")]),
                    configure: options => options.LoadingMessage("Scanning media roots")))
            .Build();

        var result = await RunCapturedAsync(app, ["run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("fast", result.Stdout);
        Assert.Contains("Fast choice", shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("Scanning media roots", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task NonInteractive_WithCustomMessage_ShowsNoLoadingUi()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<ProviderValidatedCommand>()
            .ConfigureProviders(providers =>
                providers.AddAsync<string>(
                    "configured-connection",
                    async _ =>
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
                        return [new OptionItem<string>("known", "Known")];
                    },
                    configure: options => options.LoadingMessage("Scanning media roots")))
            .Build();

        var result = await RunCapturedAsync(app, ["--connection", "known", "--non-interactive"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("Scanning media roots", shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("Loading options", shell.Terminal.LastRenderedText);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Func<TigerCliProviderContext, Task<IReadOnlyList<OptionItem<string>>>> GatedProvider(
        TaskCompletionSource gate) =>
        async _ =>
        {
            await gate.Task;
            return [new OptionItem<string>("picked", "Picked choice")];
        };

    private (Task<int> Run, Action Restore) StartRun(
        TigerCliApp app, string[] args, TestShell shell, CancellationToken ct)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);

        var run = app.RunAsync(args, shell, ct: ct);

        void Restore()
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        return (run, Restore);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app, string[] args, TestShell shell)
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
