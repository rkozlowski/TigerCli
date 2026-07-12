namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Per-run owner of the process/system cancellation token. Created once at app startup (unless process
/// cancellation handling is disabled) and disposed at the end of the run. The token it exposes is
/// tripped cooperatively by <see cref="TigerCliProcessCancellation"/> when a Ctrl-C / SIGINT / SIGTERM /
/// SIGQUIT signal is observed, and is linked into the modal loop so a prompt completes with
/// <see cref="Enums.DialogResultKind.SystemCancel"/>.
/// </summary>
internal sealed class TigerCliLifetime : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Token cancelled when a process/system cancellation request is observed.</summary>
    public CancellationToken SystemCancellation => _cts.Token;

    /// <summary>Whether a system cancellation has already been requested.</summary>
    public bool IsSystemCancellationRequested => _cts.IsCancellationRequested;

    /// <summary>
    /// Requests cooperative system cancellation. Safe to call more than once and from a signal-handler
    /// thread. Never throws if already cancelled or disposed.
    /// </summary>
    public void RequestSystemCancellation()
    {
        try
        {
            if (!_cts.IsCancellationRequested)
                _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The run has already torn down; nothing left to cancel.
        }
    }

    public void Dispose() => _cts.Dispose();
}
