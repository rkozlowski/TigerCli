namespace ItTiger.TigerCli.Enums;

/// <summary>
/// The single stop action a rich activity dialog exposes. An activity dialog offers exactly one stop
/// action — never Cancel and Abort together — so the button, the confirmation prompt, and the
/// in-progress state all describe the same intent.
/// </summary>
public enum ActivityStopMode
{
    /// <summary>
    /// Cooperative cancellation. The dialog shows a <c>Cancel</c> button, confirms with
    /// "Cancel this operation?", and switches to a "Cancelling…" state while it waits for the operation
    /// to observe its <see cref="System.Threading.CancellationToken"/> and stop.
    /// </summary>
    Cancel,

    /// <summary>
    /// Abort. The dialog shows an <c>Abort</c> button, confirms with "Abort this operation?", and
    /// switches to an "Aborting…" state while it waits for the operation to stop. Mechanically identical
    /// to <see cref="Cancel"/> (the same token is cancelled); the distinct wording and
    /// <see cref="ActivityOutcome.Aborted"/> outcome let callers treat a forceful stop differently.
    /// </summary>
    Abort,
}
