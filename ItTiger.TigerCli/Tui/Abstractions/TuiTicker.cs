namespace ItTiger.TigerCli.Tui.Abstractions;

/// <summary>
/// A time-driven source of overlay content for semi-interactive dialogs. A ticker owns a frame/content
/// that changes at a fixed <see cref="Interval"/> while it is <see cref="IsActive"/>. The hosting modal
/// loop advances every active ticker once per iteration via <see cref="Advance"/>; when a ticker reports
/// its visible output changed, the loop re-renders. Tickers carry no rendering or layout logic — they
/// only expose <see cref="CurrentContent"/>, which the dialog's overlay renderer reads.
/// </summary>
/// <remarks>
/// The baseline for the interval is captured on the first <see cref="Advance"/> call, so a ticker never
/// "jumps" a frame at modal entry. Time is supplied by the caller (the shell's <c>ITuiClock</c>), which
/// keeps animation deterministic under a manual test clock. An indefinite ticker keeps
/// <see cref="IsActive"/> <c>true</c> for the modal's life; a scoped ticker toggles it to animate only
/// while an operation runs.
/// </remarks>
public abstract class TuiTicker
{
    private readonly TimeSpan _interval;
    private DateTime _lastAdvanceUtc;
    private bool _baselineCaptured;

    /// <summary>Creates a ticker with the specified frame interval.</summary>
    /// <param name="interval">The positive period between frame advances.</param>
    protected TuiTicker(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");

        _interval = interval;
    }

    /// <summary>The period between frame advances.</summary>
    public TimeSpan Interval => _interval;

    /// <summary>
    /// Whether the ticker currently animates. Indefinite tickers stay active for the modal's life;
    /// scoped tickers return <c>false</c> while idle so their overlay renders nothing.
    /// </summary>
    public virtual bool IsActive => true;

    /// <summary>The current visible content (e.g. a spinner frame or a formatted clock), read by the overlay renderer.</summary>
    public abstract string CurrentContent { get; }

    /// <summary>
    /// Advances the ticker to <paramref name="nowUtc"/>. Returns <c>true</c> when the visible output
    /// changed and the modal should re-render. The first call only captures the interval baseline (no
    /// change); an inactive ticker never advances and resets its baseline so a later restart begins a
    /// fresh interval rather than flipping immediately.
    /// </summary>
    public bool Advance(DateTime nowUtc)
    {
        if (!IsActive)
        {
            _baselineCaptured = false;
            return false;
        }

        if (!_baselineCaptured)
        {
            _baselineCaptured = true;
            _lastAdvanceUtc = nowUtc;
            return false;
        }

        bool changed = false;
        // Catch up across any number of elapsed intervals (e.g. a large clock jump) without busy work.
        while (nowUtc - _lastAdvanceUtc >= _interval)
        {
            _lastAdvanceUtc += _interval;
            changed |= AdvanceFrame();
        }

        return changed;
    }

    /// <summary>
    /// Advances exactly one frame. Returns <c>true</c> when the visible output changed (so even a
    /// content-only ticker such as a clock can report it needs a redraw each interval).
    /// </summary>
    protected abstract bool AdvanceFrame();
}
