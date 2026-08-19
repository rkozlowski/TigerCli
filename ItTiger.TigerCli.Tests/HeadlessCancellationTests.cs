using System.Globalization;
using System.Runtime.InteropServices;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Activity;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Windowing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Script-safe cancellation for non-interactive/headless execution: process/system cancellation is
/// registered in both interaction modes, the run's cancellation token reaches the command handler
/// through <see cref="TigerCliSettings.CancellationToken"/>, and an observed cancellation ends the run
/// with the <see cref="TigerCliExitKind.Cancelled"/> mapping (or the handler's own exit code) instead
/// of abrupt process termination or <see cref="TigerCliExitKind.UnhandledException"/>.
/// </summary>
/// <remarks>
/// Real OS-signal delivery is deliberately not exercised here — raising a genuine SIGINT would cancel
/// the test host, and driving it through a child process is flaky across platforms (see the note in
/// <c>ProcessCancellationTests</c>). Signal <em>handling</em> is covered by invoking the product
/// handler with a constructed <see cref="PosixSignalContext"/>; end-to-end signal delivery stays a
/// manual smoke check.
/// </remarks>
public sealed class HeadlessCancellationTests
{
    private const int OkCode = 0;
    private const int ErrorCode = 70;
    private const int CancelledCode = 130;
    private const int UnhandledCode = 99;

    private sealed class ProbeSettings : TigerCliSettings
    {
    }

    // A handler whose body is supplied per test. Registered through the factory overload of
    // SetDefaultCommand so each test owns its own closure instead of sharing static state.
    private sealed class ProbeCommand(Func<ProbeSettings, Task<int>> body)
        : TigerCliAsyncCommandHandler<ProbeSettings>
    {
        public override Task<int> ExecuteAsync(ProbeSettings settings) => body(settings);
    }

    private static TigerCliApp BuildApp(
        Func<ProbeSettings, Task<int>> body,
        bool disableProcessCancellation = false)
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("probe")
            .SetDefaultCommand(() => new ProbeCommand(body))
            .UseExitCodes(OkCode, ErrorCode)
                .ExitKind(TigerCliExitKind.Cancelled, CancelledCode)
                .ExitKind(TigerCliExitKind.UnhandledException, UnhandledCode);

        if (disableProcessCancellation)
            builder = builder.DisableProcessCancellation();

        return builder.Build();
    }

    private static ActivityDialogSpec MessageSpec() =>
        ActivityDialogSpec.Create()
            .AddColumn(align: CliTextAlignment.Left)
            .AddRow("r", r => r.Cell(0).Text("{0}").Values("start"))
            .Build();

    private static TestShell NonInteractiveShell() =>
        new(interactionMode: TigerCliInteractionMode.NonInteractive, culture: CultureInfo.GetCultureInfo("en-US"));

    // ── Headless activities observe the RunAsync token ────────────────────────────

    [Fact]
    public async Task NonInteractive_HeadlessActivity_ObservesRunAsyncCancellationToken()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        ActivityOutcome? observed = null;

        // The no-shell overload runs on the InlineShell singleton, which reports the run's ambient
        // NonInteractive mode — so this is the real script-first shape, not a hand-built shell.
        var app = BuildApp(async settings =>
        {
            var activity = await TigerTui.RunActivityAsync(
                "Working",
                async (_, token) =>
                {
                    cts.Cancel();
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                    return 0;
                },
                ct: settings.CancellationToken).ConfigureAwait(false);

            observed = activity.Outcome;
            return OkCode;
        });

        var result = await TigerCliAppTestHost.For(app)
            .WithArgs("--non-interactive")
            .RunAsync(cts.Token);

        Assert.Equal(ActivityOutcome.Cancelled, observed);
        Assert.Equal(OkCode, result.ExitCode);
    }

    [Fact]
    public async Task NonInteractive_HeadlessActivity_ObservesSystemCancellationToken()
    {
        var previous = SystemCancellationScope.Current;
        using var systemCts = new CancellationTokenSource();
        try
        {
            // Stands in for a delivered Ctrl-C: TigerCliApp publishes exactly this token for the run.
            SystemCancellationScope.Current = systemCts.Token;

            var result = await TigerTui.RunActivityAsync(
                NonInteractiveShell(),
                "Working",
                MessageSpec(),
                async (_, token) =>
                {
                    systemCts.Cancel();
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                    return 0;
                },
                ct: TestContext.Current.CancellationToken);

            Assert.Equal(ActivityOutcome.SystemCancelled, result.Outcome);
            Assert.Equal(DialogResultKind.SystemCancel, result.DialogResultKind);
        }
        finally
        {
            SystemCancellationScope.Current = previous;
        }
    }

    // ── Process cancellation is owned in NonInteractive too ───────────────────────

    [Fact]
    public async Task NonInteractive_ProcessCancellation_PublishesLiveSystemToken()
    {
        CancellationToken observedSystemToken = default;

        var app = BuildApp(_ =>
        {
            observedSystemToken = SystemCancellationScope.Current;
            return Task.FromResult(OkCode);
        });

        var (exitCode, _, stderr) = await RunWithoutShellAsync(
            app, ["--non-interactive"], TestContext.Current.CancellationToken);

        Assert.Equal(OkCode, exitCode);
        // 0.9.3 skipped registration in NonInteractive, so this token was default (never cancelable)
        // and Ctrl-C killed the process instead of reaching handler work.
        Assert.True(observedSystemToken.CanBeCanceled);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public async Task NonInteractive_DisableProcessCancellation_LeavesSystemTokenUncancelable()
    {
        CancellationToken observedSystemToken = default;

        var app = BuildApp(
            _ =>
            {
                observedSystemToken = SystemCancellationScope.Current;
                return Task.FromResult(OkCode);
            },
            disableProcessCancellation: true);

        var (exitCode, _, _) = await RunWithoutShellAsync(
            app, ["--non-interactive"], TestContext.Current.CancellationToken);

        Assert.Equal(OkCode, exitCode);
        Assert.False(observedSystemToken.CanBeCanceled);
    }

    [Fact]
    public async Task SemiInteractive_ProcessCancellation_StillPublishesLiveSystemToken()
    {
        // Regression lock for the interactive path the 0.9.3 wiring already covered.
        CancellationToken observedSystemToken = default;

        var app = BuildApp(_ =>
        {
            observedSystemToken = SystemCancellationScope.Current;
            return Task.FromResult(OkCode);
        });

        var (exitCode, _, _) = await RunWithoutShellAsync(app, [], TestContext.Current.CancellationToken);

        Assert.Equal(OkCode, exitCode);
        Assert.True(observedSystemToken.CanBeCanceled);
    }

    // ── TigerCliSettings.CancellationToken ────────────────────────────────────────

    [Fact]
    public async Task Settings_CancellationToken_IsLinkedToRunAsyncToken()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cancelable = false;
        var trippedByRunToken = false;

        var app = BuildApp(settings =>
        {
            cancelable = settings.CancellationToken.CanBeCanceled;
            cts.Cancel();
            trippedByRunToken = settings.CancellationToken.IsCancellationRequested;
            return Task.FromResult(OkCode);
        });

        var (exitCode, _, _) = await RunWithoutShellAsync(app, ["--non-interactive"], cts.Token);

        Assert.True(cancelable);
        Assert.True(trippedByRunToken);
        // A cancelled run token does not by itself change the outcome: the handler returned normally,
        // so its exit code stands.
        Assert.Equal(OkCode, exitCode);
    }

    [Fact]
    public async Task Settings_CancellationToken_IsPopulated_WhenHostOwnsSignalHandling()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var tripped = false;

        // With an injected shell TigerCli does not register signal handlers, so the token carries the
        // caller's RunAsync token alone — it must still be the handler-visible token.
        var app = BuildApp(settings =>
        {
            cts.Cancel();
            tripped = settings.CancellationToken.IsCancellationRequested;
            return Task.FromResult(OkCode);
        });

        var result = await TigerCliAppTestHost.For(app)
            .WithArgs("--non-interactive")
            .RunAsync(cts.Token);

        Assert.True(tripped);
        Assert.Equal(OkCode, result.ExitCode);
    }

    // ── Exit-code ownership ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handler_ObservesCancellation_ReturnsAppExitCodeVerbatim()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        const int appCancelledCode = 17;

        var app = BuildApp(async settings =>
        {
            try
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, settings.CancellationToken).ConfigureAwait(false);
                return OkCode;
            }
            catch (OperationCanceledException)
            {
                // The app maps cancellation itself; the framework must not override this.
                return appCancelledCode;
            }
        });

        var result = await TigerCliAppTestHost.For(app)
            .WithArgs("--non-interactive")
            .RunAsync(cts.Token);

        Assert.Equal(appCancelledCode, result.ExitCode);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public async Task Handler_EscapedObservedCancellation_MapsToCancelled_NotUnhandledException()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var app = BuildApp(async settings =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, settings.CancellationToken).ConfigureAwait(false);
            return OkCode;
        });

        var result = await TigerCliAppTestHost.For(app)
            .WithArgs("--non-interactive")
            .RunAsync(cts.Token);

        Assert.Equal(CancelledCode, result.ExitCode);
        Assert.NotEqual(UnhandledCode, result.ExitCode);
        // The quiet cancellation notice, not an error line.
        Assert.DoesNotContain("Error", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handler_UnrelatedOperationCanceledException_StillMapsToUnhandledException()
    {
        // The run token was never cancelled, so this cancellation is the app's own business (e.g. an
        // inner timeout) and must not be quietly reclassified as a run cancellation.
        var app = BuildApp(_ => throw new OperationCanceledException("unrelated"));

        var result = await TigerCliAppTestHost.For(app)
            .WithArgs("--non-interactive")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UnhandledCode, result.ExitCode);
        Assert.NotEqual(CancelledCode, result.ExitCode);
        Assert.NotEqual(string.Empty, result.StdErr);
    }

    // ── Redirected handles stay safe ──────────────────────────────────────────────

    [Fact]
    public async Task NonInteractive_RedirectedHandles_HeadlessActivity_DoesNotThrowHandleIsInvalid()
    {
        var app = BuildApp(async settings =>
        {
            var activity = await TigerTui.RunActivityAsync(
                "Importing...",
                (_, _) => Task.FromResult(5),
                ct: settings.CancellationToken).ConfigureAwait(false);

            return activity.Outcome == ActivityOutcome.Completed ? OkCode : ErrorCode;
        });

        // Console.Out/Error are redirected for the duration of the run, and the run installs no shell:
        // the headless path must never read CursorTop/KeyAvailable, which throw under redirection.
        var (exitCode, stdout, stderr) = await RunWithoutShellAsync(
            app, ["--non-interactive"], TestContext.Current.CancellationToken);

        Assert.Equal(OkCode, exitCode);
        Assert.DoesNotContain("handle is invalid", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("Importing...", stdout, StringComparison.Ordinal);
    }

    // ── Second-signal escalation ──────────────────────────────────────────────────

    [Fact]
    public void HandleSignal_FirstSignal_SuppressesTermination_AndRequestsCancellation()
    {
        using var lifetime = new TigerCliLifetime();
        using var registration = TigerCliProcessCancellation.Register(lifetime);

        var context = new PosixSignalContext(PosixSignal.SIGINT);
        registration.HandleSignal(context);

        // Cooperative: the runtime must not tear the process down, so the run can restore terminal
        // state and return an app-defined exit code.
        Assert.True(context.Cancel);
        Assert.True(lifetime.IsSystemCancellationRequested);
        Assert.Equal(1, registration.SignalsHandled);
    }

    [Fact]
    public void HandleSignal_SecondSignal_AllowsDefaultTermination()
    {
        using var lifetime = new TigerCliLifetime();
        using var registration = TigerCliProcessCancellation.Register(lifetime);

        registration.HandleSignal(new PosixSignalContext(PosixSignal.SIGINT));

        var second = new PosixSignalContext(PosixSignal.SIGINT);
        registration.HandleSignal(second);

        // Escalation: a process that ignores cooperative cancellation stays killable from the terminal.
        Assert.False(second.Cancel);
        Assert.True(lifetime.IsSystemCancellationRequested);
        Assert.Equal(2, registration.SignalsHandled);
    }

    [Fact]
    public void HandleSignal_SecondSignal_OfADifferentKind_AlsoEscalates()
    {
        using var lifetime = new TigerCliLifetime();
        using var registration = TigerCliProcessCancellation.Register(lifetime);

        registration.HandleSignal(new PosixSignalContext(PosixSignal.SIGINT));

        // Ctrl-C then Ctrl-Break: the escalation counter spans signal kinds.
        var second = new PosixSignalContext(PosixSignal.SIGQUIT);
        registration.HandleSignal(second);

        Assert.False(second.Cancel);
        Assert.Equal(2, registration.SignalsHandled);
    }

    // Runs an app on the real (no injected shell) path so TigerCli owns process cancellation, with
    // Console.Out/Error and the TigerConsole sinks redirected to strings. Mirrors what
    // TigerCliAppTestHost does, minus the shell injection that suppresses signal registration.
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunWithoutShellAsync(
        TigerCliApp app,
        string[] args,
        CancellationToken cancellationToken)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalColorMode = TigerConsole.ColorMode;
        var originalTheme = TigerConsole.CurrentTheme;

        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        var synchronizedStdout = TextWriter.Synchronized(stdout);
        var synchronizedStderr = TextWriter.Synchronized(stderr);

        var outputScope = TigerConsole.PushOutputSink(new TextWriterSink(synchronizedStdout), plainBaseStyle: true);
        var errorScope = TigerConsole.PushErrorSink(new TextWriterSink(synchronizedStderr), plainBaseStyle: true);
        try
        {
            TigerConsole.ColorMode = CliColorMode.Never;
            Console.SetOut(synchronizedStdout);
            Console.SetError(synchronizedStderr);

            var exitCode = await app
                .RunAsync(args, promptShell: null, promptTimeout: null, cancellationToken)
                .ConfigureAwait(false);

            synchronizedStdout.Flush();
            synchronizedStderr.Flush();
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            errorScope.Dispose();
            outputScope.Dispose();
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            TigerConsole.ColorMode = originalColorMode;
            TigerConsole.CurrentTheme = originalTheme;
        }
    }
}
