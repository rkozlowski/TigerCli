namespace ItTiger.TigerCli.Tui.Windowing;

/// <summary>
/// Per-run holder for the process/system cancellation token used by the <see cref="InlineShell"/>
/// singleton path. Kept on a dedicated type (not on <see cref="InlineShell"/>) so publishing the token
/// never touches an <see cref="InlineShell"/> static member, which would eagerly initialize the
/// console-backed singleton. Backed by an <see cref="AsyncLocal{T}"/> so the value flows only into the
/// current run's prompt calls and stays isolated across parallel runs/tests.
/// </summary>
internal static class SystemCancellationScope
{
    private static readonly AsyncLocal<CancellationToken> _current = new();

    /// <summary>The ambient system-cancellation token for the current async flow, or a default (uncancelable) token.</summary>
    internal static CancellationToken Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
