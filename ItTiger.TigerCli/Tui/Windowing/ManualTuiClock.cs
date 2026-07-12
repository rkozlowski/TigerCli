namespace ItTiger.TigerCli.Tui.Windowing;

/// <summary>
/// Test <see cref="ITuiClock"/> whose time advances only when <see cref="Advance"/> is
/// called. Thread-safe: the modal loop reads <see cref="UtcNow"/> on its own thread
/// while a test advances the clock from another.
/// </summary>
internal sealed class ManualTuiClock : ITuiClock
{
    private readonly object _sync = new();
    private DateTime _utcNow;

    public ManualTuiClock(DateTime? start = null)
    {
        _utcNow = start ?? new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    public DateTime UtcNow
    {
        get
        {
            lock (_sync)
                return _utcNow;
        }
    }

    /// <summary>Moves the clock forward by <paramref name="delta"/>. Time never moves backward.</summary>
    public void Advance(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delta), "Time can only move forward.");

        lock (_sync)
            _utcNow += delta;
    }
}
