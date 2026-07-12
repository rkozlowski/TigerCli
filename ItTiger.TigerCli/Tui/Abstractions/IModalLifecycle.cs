namespace ItTiger.TigerCli.Tui.Abstractions;

/// <summary>
/// Implemented by a dialog that needs to know the bounds of its modal session — when the semi-interactive
/// modal loop begins and ends hosting it. The shell calls <see cref="OnModalOpened"/> once before the
/// first render, supplying a token cancelled when the modal closes for any reason, and
/// <see cref="OnModalClosed"/> exactly once on exit (key result, timeout, external cancellation, or
/// exception). Used to give controls a cancellation token for background work and a safe teardown point.
/// </summary>
public interface IModalLifecycle
{
    /// <summary>Called once as the modal loop starts; <paramref name="modalToken"/> is cancelled when the modal closes.</summary>
    void OnModalOpened(CancellationToken modalToken);

    /// <summary>Called exactly once as the modal loop ends, on every exit path.</summary>
    void OnModalClosed();
}
