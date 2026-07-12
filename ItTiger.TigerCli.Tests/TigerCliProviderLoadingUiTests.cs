using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

// Stage 2 of the slow-provider plan: a generic loading UI (spinner + "Loading options…") appears while a
// slow interactive provider resolves its choices, then the normal select prompt follows. These tests
// drive the real modal loop through app.RunAsync with gated providers, so loading is deterministic
// without real sleeps beyond the (small) display threshold. "╔[" marks the spinner sitting on the loading
// dialog's top frame next to the top-left corner.
public sealed class TigerCliProviderLoadingUiTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

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

    // ── Fast provider: no loading UI ─────────────────────────────────────────

    [Fact]
    public async Task FastAsyncProvider_DoesNotShowLoadingUi_GoesStraightToSelect()
    {
        // A provider that returns an already-completed task never crosses the display threshold, so no
        // loading modal is opened — the select renders directly.
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>("connection", _ =>
                    Task.FromResult<IReadOnlyList<OptionItem<string>>>(
                        [new OptionItem<string>("fast", "Fast choice")])))
            .Build();

        var result = await RunCapturedAsync(app, ["run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("fast", result.Stdout);
        Assert.Contains("Fast choice", shell.Terminal.LastRenderedText);   // reached the select
        Assert.DoesNotContain("Loading options", shell.Terminal.LastRenderedText);
    }

    // ── Slow provider: loading UI then select ────────────────────────────────

    [Fact]
    public async Task SlowAsyncProvider_ShowsLoadingUi_ThenSelect()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shell = new TestShell(useManualClock: true);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>("connection", async _ =>
                {
                    await gate.Task;
                    return [new OptionItem<string>("picked", "Picked choice")];
                }))
            .Build();

        var (run, restore) = StartRun(app, ["run"], shell, ct);
        try
        {
            // The provider is still running after the threshold → generic loading UI appears.
            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
            Assert.Contains("Loading options", shell.Terminal.LastRenderedText);
            Assert.Contains("╔[", shell.Terminal.LastRenderedText);   // spinner on the top frame

            // Releasing the provider closes the loading UI and shows the real select with the choices.
            gate.TrySetResult();
            await WaitForTextAsync(shell, "Picked choice", Timeout, ct);
            Assert.DoesNotContain("Loading options", shell.Terminal.LastRenderedText);

            shell.Terminal.EnqueueKey(ConsoleKey.Enter);
            var exit = await run.WaitAsync(Timeout, ct);
            Assert.Equal(0, exit);
        }
        finally
        {
            gate.TrySetResult();
            restore();
        }
    }

    [Fact]
    public async Task SlowSyncProvider_IsOffloaded_SoLoadingUiStaysResponsive()
    {
        // A sync-origin provider would block the calling thread inside GetChoicesAsync. The interactive
        // loading path offloads it (narrowly) so the spinner can render while it runs. If it were not
        // offloaded, the modal loop could never render and this assert would hang/fail.
        var ct = TestContext.Current.CancellationToken;
        using var gate = new ManualResetEventSlim(false);
        var shell = new TestShell(useManualClock: true);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddProvider<string>("connection", _ =>
                {
                    gate.Wait(Timeout);
                    return [new OptionItem<string>("synced", "Synced choice")];
                }))
            .Build();

        var (run, restore) = StartRun(app, ["run"], shell, ct);
        try
        {
            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
            Assert.Contains("Loading options", shell.Terminal.LastRenderedText);

            gate.Set();
            await WaitForTextAsync(shell, "Synced choice", Timeout, ct);

            shell.Terminal.EnqueueKey(ConsoleKey.Enter);
            var exit = await run.WaitAsync(Timeout, ct);
            Assert.Equal(0, exit);
        }
        finally
        {
            gate.Set();
            restore();
        }
    }

    // ── Cancellation while loading ───────────────────────────────────────────

    [Fact]
    public async Task EscapeWhileLoading_MapsToCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shell = new TestShell(useManualClock: true);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 77).ExitKind(TigerCliExitKind.UnhandledException, 88).ExitKind(TigerCliExitKind.ValidationError, 99)
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>("connection", async _ =>
                {
                    await gate.Task;
                    return [new OptionItem<string>("never", "Never")];
                }))
            .Build();

        var (run, restore) = StartRun(app, ["run"], shell, ct);
        try
        {
            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
            Assert.Contains("Loading options", shell.Terminal.LastRenderedText);

            shell.Terminal.EnqueueKey(ConsoleKey.Escape);
            var exit = await run.WaitAsync(Timeout, ct);
            Assert.Equal(77, exit);   // cancellation maps to TigerCliExitKind.Cancelled
        }
        finally
        {
            gate.TrySetResult();   // release the abandoned load
            restore();
        }
    }

    [Fact]
    public async Task CallerTokenCancelWhileLoading_MapsToCancelled()
    {
        using var cts = new CancellationTokenSource();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shell = new TestShell(useManualClock: true);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 77).ExitKind(TigerCliExitKind.UnhandledException, 88).ExitKind(TigerCliExitKind.ValidationError, 99)
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>("connection", async _ =>
                {
                    await gate.Task;
                    return [new OptionItem<string>("never", "Never")];
                }))
            .Build();

        var (run, restore) = StartRun(app, ["run"], shell, cts.Token);
        try
        {
            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, cts.Token);
            Assert.Contains("Loading options", shell.Terminal.LastRenderedText);

            await cts.CancelAsync();
            var exit = await run.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal(77, exit);
        }
        finally
        {
            gate.TrySetResult();
            restore();
        }
    }

    [Fact]
    public async Task SystemCancelWhileLoading_MapsToCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shell = new TestShell(useManualClock: true);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 77).ExitKind(TigerCliExitKind.UnhandledException, 88).ExitKind(TigerCliExitKind.ValidationError, 99)
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>("connection", async _ =>
                {
                    await gate.Task;
                    return [new OptionItem<string>("never", "Never")];
                }))
            .Build();

        var (run, restore) = StartRun(app, ["run"], shell, ct);
        try
        {
            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
            Assert.Contains("Loading options", shell.Terminal.LastRenderedText);

            shell.RaiseSystemCancellation();
            var exit = await run.WaitAsync(Timeout, ct);
            Assert.Equal(77, exit);
        }
        finally
        {
            gate.TrySetResult();
            restore();
        }
    }

    // ── Failure while loading ────────────────────────────────────────────────

    [Fact]
    public async Task ProviderFailureWhileLoading_SurfacesAsProviderFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shell = new TestShell(useManualClock: true);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 77).ExitKind(TigerCliExitKind.UnhandledException, 88).ExitKind(TigerCliExitKind.ValidationError, 99)
            .AddCommand<ConnectionCommand>("run", command =>
                command.AddAsyncProvider<string>("connection", async _ =>
                {
                    await gate.Task;
                    throw new InvalidOperationException("boom");
                }))
            .Build();

        var (run, restore) = StartRun(app, ["run"], shell, ct);
        try
        {
            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
            Assert.Contains("Loading options", shell.Terminal.LastRenderedText);

            gate.TrySetResult();   // provider faults after the gate
            var exit = await run.WaitAsync(Timeout, ct);
            Assert.Equal(88, exit);   // Error_PromptProviderFailed → UnhandledException, not a loading error
        }
        finally
        {
            restore();
        }

        Assert.Contains("provider failed", StderrCapture);
    }

    private string StderrCapture = string.Empty;

    // ── Non-interactive: no loading UI ───────────────────────────────────────

    [Fact]
    public async Task NonInteractive_SlowProvider_ShowsNoLoadingUi()
    {
        // Even a provider slower than the display threshold renders no loading UI in non-interactive mode:
        // the validation path resolves it directly with the effective token and no modal.
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-test")
            .SetDefaultCommand<ProviderValidatedCommand>()
            .ConfigureProviders(providers =>
                providers.AddAsync<string>("configured-connection", async _ =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
                    return [new OptionItem<string>("known", "Known")];
                }))
            .Build();

        var result = await RunCapturedAsync(app, ["--connection", "known", "--non-interactive"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("Loading options", shell.Terminal.LastRenderedText);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
            StderrCapture = stderr.ToString();
        }

        return (run, Restore);
    }

    private static async Task WaitForTextAsync(TestShell shell, string text, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (shell.Terminal.LastRenderedText.Contains(text, StringComparison.Ordinal))
                return;
            await Task.Delay(10, ct);
        }

        throw new TimeoutException($"Text '{text}' did not appear within {timeout}.");
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
