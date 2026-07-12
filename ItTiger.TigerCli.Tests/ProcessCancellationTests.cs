using ItTiger.TigerCli.Commands;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// S1b: TigerCli-owned process/system cancellation handling — lifetime/token ownership, signal
/// registration, and the builder opt-out. Uses the internal lifecycle types (visible to the test
/// assembly via <c>InternalsVisibleTo</c>) so the product handling is exercised, not a fake seam.
/// </summary>
public sealed class ProcessCancellationTests
{
    private sealed class EmptySettings : TigerCliSettings
    {
    }

    private sealed class EmptyCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings) => Task.FromResult(0);
    }

    [Fact]
    public void Lifetime_TokenIsNotCancelled_Initially()
    {
        using var lifetime = new TigerCliLifetime();

        Assert.False(lifetime.IsSystemCancellationRequested);
        Assert.False(lifetime.SystemCancellation.IsCancellationRequested);
    }

    [Fact]
    public void RequestCancellation_CancelsLifetimeToken()
    {
        using var lifetime = new TigerCliLifetime();
        using var registration = TigerCliProcessCancellation.Register(lifetime);

        // The product seam invoked by a real signal handler (minus the OS-supplied context).
        registration.RequestCancellation();

        Assert.True(lifetime.IsSystemCancellationRequested);
        Assert.True(lifetime.SystemCancellation.IsCancellationRequested);
        Assert.Equal(1, registration.SignalsHandled);
    }

    [Fact]
    public void Register_RegistersAtLeastOneSignalHandler()
    {
        using var lifetime = new TigerCliLifetime();
        using var registration = TigerCliProcessCancellation.Register(lifetime);

        // SIGINT (Ctrl-C) is supported on every target platform, including Windows, so registration
        // should succeed for at least one signal. This also proves handlers are actually installed,
        // which is what lets the real handler set context.Cancel = true to prevent abrupt termination.
        Assert.True(registration.RegistrationCount > 0);
    }

    [Fact]
    public void Lifetime_RequestSystemCancellation_IsIdempotentAndSafeAfterDispose()
    {
        var lifetime = new TigerCliLifetime();
        lifetime.RequestSystemCancellation();
        lifetime.RequestSystemCancellation(); // idempotent
        Assert.True(lifetime.IsSystemCancellationRequested);

        lifetime.Dispose();
        lifetime.RequestSystemCancellation(); // must not throw after dispose
    }

    [Fact]
    public void ProcessCancellation_IsEnabledByDefault()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.True(app.ProcessCancellationEnabled);
    }

    [Fact]
    public void DisableProcessCancellation_DisablesHandlerRegistration()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EmptyCommand>()
            .DisableProcessCancellation()
            .Build();

        Assert.False(app.ProcessCancellationEnabled);
    }

    /*
    // Real-signal coverage is intentionally not run in CI: raising a genuine SIGINT/SIGTERM in-process
    // would cancel the test host, and driving it via a spawned child process is flaky across platforms
    // (notably Windows Ctrl-C requires console-group control events). The product handler is implemented
    // with PosixSignalRegistration (see TigerCliProcessCancellation); the registration is verified above
    // and the handler→token wiring is verified via RequestCancellation. End-to-end signal behavior is
    // validated by manual/smoke testing. This placeholder documents the procedure.
    [Fact(Skip = "Real OS-signal integration test; run manually — see comment for rationale.")]
    public void RealSignal_CtrlC_TripsSystemCancellation_Manual()
    {
        // Manual procedure:
        //   1. Run a TigerCli app that enters an interactive prompt.
        //   2. Press Ctrl-C (or send SIGINT/SIGTERM on POSIX).
        //   3. Observe: the prompt completes via SystemCancel, the terminal state is restored
        //      (cursor visible, no stray rendering), and the process exits cleanly rather than aborting.
    }
    */
}
