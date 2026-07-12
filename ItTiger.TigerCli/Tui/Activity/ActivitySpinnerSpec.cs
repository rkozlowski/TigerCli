using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;

namespace ItTiger.TigerCli.Tui.Activity;

/// <summary>
/// Configures the spinner an activity dialog shows on its top frame. Pick one of three modes:
/// <see cref="Default"/> (the framework's default <see cref="SpinnerFrameSet.Default"/> and interval),
/// <see cref="FromFrameSet"/> (a predefined frame set with an optional interval), or
/// <see cref="FromTicker"/> (a caller-created <see cref="TuiTicker"/>). The spec owns only the choice of
/// ticker; it knows nothing about overlays, titles, or markup — frames stay raw and the dialog's overlay
/// decides presentation.
/// </summary>
public sealed class ActivitySpinnerSpec
{
    private readonly SpinnerFrameSet _frameSet;
    private readonly TimeSpan? _interval;
    private readonly TuiTicker? _ticker;

    private ActivitySpinnerSpec(SpinnerFrameSet frameSet, TimeSpan? interval, TuiTicker? ticker)
    {
        _frameSet = frameSet;
        _interval = interval;
        _ticker = ticker;
    }

    /// <summary>The framework default: <see cref="SpinnerFrameSet.Default"/> at the default interval.</summary>
    public static ActivitySpinnerSpec Default { get; } = new(SpinnerFrameSet.Default, null, null);

    /// <summary>A predefined <paramref name="frameSet"/> with an optional <paramref name="interval"/>.</summary>
    public static ActivitySpinnerSpec FromFrameSet(SpinnerFrameSet frameSet, TimeSpan? interval = null) =>
        new(frameSet, interval, null);

    /// <summary>A caller-created <paramref name="ticker"/>; the caller owns its frames and timing.</summary>
    public static ActivitySpinnerSpec FromTicker(TuiTicker ticker) =>
        new(SpinnerFrameSet.Default, null, ticker ?? throw new ArgumentNullException(nameof(ticker)));

    /// <summary>
    /// Builds the overlay ticker and, when it is a <see cref="SpinnerTicker"/> the dialog should drive,
    /// the handle used to start/stop it across the operation's lifetime. For a frame-set spec the ticker
    /// is created idle (the control starts it on modal open); for an external ticker the caller-owned
    /// instance is returned as-is and is started/stopped only when it is itself a <see cref="SpinnerTicker"/>.
    /// </summary>
    internal (TuiTicker Ticker, SpinnerTicker? Spinner) Build()
    {
        if (_ticker is not null)
            return (_ticker, _ticker as SpinnerTicker);

        var spinner = new SpinnerTicker(_frameSet, _interval, active: false);
        return (spinner, spinner);
    }
}
