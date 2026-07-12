using System.Runtime.InteropServices;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Registers TigerCli's process/system cancellation handlers (Ctrl-C / SIGINT, SIGTERM, and SIGQUIT
/// where supported) and routes them to a <see cref="TigerCliLifetime"/>. Handlers are cooperative: they
/// cancel the runtime's default abrupt termination so the modal loop's <c>finally</c> can restore
/// terminal state, then trip the lifetime's system-cancellation token so the active prompt completes
/// with <see cref="Enums.DialogResultKind.SystemCancel"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="PosixSignalRegistration"/>, which is cross-platform: on Windows SIGINT maps to
/// Ctrl-C and SIGQUIT to Ctrl-Break; on POSIX all three are native signals. Signals the current
/// platform/runtime does not support are skipped, so registration never throws.
/// </remarks>
internal sealed class TigerCliProcessCancellation : IDisposable
{
    private static readonly PosixSignal[] HandledSignals =
    [
        PosixSignal.SIGINT,   // Ctrl-C (Windows + POSIX)
        PosixSignal.SIGTERM,  // termination request
        PosixSignal.SIGQUIT,  // Ctrl-Break on Windows / quit on POSIX
    ];

    private readonly TigerCliLifetime _lifetime;
    private readonly List<PosixSignalRegistration> _registrations = new();
    private int _signalsHandled;

    private TigerCliProcessCancellation(TigerCliLifetime lifetime) => _lifetime = lifetime;

    /// <summary>Number of OS signals that were successfully registered on this platform.</summary>
    public int RegistrationCount => _registrations.Count;

    /// <summary>Number of times a handler (or the test seam) has requested cancellation.</summary>
    public int SignalsHandled => Volatile.Read(ref _signalsHandled);

    public static TigerCliProcessCancellation Register(TigerCliLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);

        var handler = new TigerCliProcessCancellation(lifetime);
        foreach (var signal in HandledSignals)
            handler.TryRegister(signal);
        return handler;
    }

    private void TryRegister(PosixSignal signal)
    {
        try
        {
            _registrations.Add(PosixSignalRegistration.Create(signal, HandleSignal));
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or ArgumentException or IOException)
        {
            // This signal is not supported on the current platform/runtime; skip it. The remaining
            // signals still register, so registration as a whole never fails.
        }
    }

    // Product signal handler. Cooperative: cancel the default abrupt termination so modal/finally
    // blocks can restore terminal state, then trip the lifetime's system-cancellation token.
    internal void HandleSignal(PosixSignalContext context)
    {
        context.Cancel = true;
        RequestCancellation();
    }

    // Testable product seam: the action a real signal handler performs, minus the OS-supplied context.
    // Invoked by HandleSignal and callable directly by tests that cannot raise a real OS signal.
    internal void RequestCancellation()
    {
        Interlocked.Increment(ref _signalsHandled);
        _lifetime.RequestSystemCancellation();
    }

    public void Dispose()
    {
        foreach (var registration in _registrations)
            registration.Dispose();
        _registrations.Clear();
    }
}
