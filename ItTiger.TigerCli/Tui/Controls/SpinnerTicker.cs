using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// A <see cref="TuiTicker"/> that cycles through a fixed sequence of string frames, wrapping back to the
/// first frame after the last. Suitable for a loading spinner or any small cyclic indicator. Frames come
/// either from a predefined <see cref="SpinnerFrameSet"/> (the default being a four-step braille spinner)
/// or from a caller-supplied custom sequence. Generic by design — it owns frames and timing only and
/// carries no knowledge of any particular control. The frame strings are raw content: any presentation
/// (brackets, styling, title prefixing) is decided by the overlay or title that renders them.
/// </summary>
public sealed class SpinnerTicker : TuiTicker
{
    // Four-step braille spinner: dots-235, dots-256, dots-356, dots-236. This is SpinnerFrameSet.Default
    // and the canonical default frame set; controls must not re-declare it.
    private static readonly string[] DefaultFrames = ["⠖", "⠲", "⠴", "⠦"];

    private readonly string[] _frames;
    private int _index;
    private bool _active;

    /// <summary>The default frame period (500&#160;ms) used when no interval is supplied.</summary>
    public static TimeSpan DefaultInterval { get; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Creates a spinner from a predefined <paramref name="frameSet"/>.
    /// </summary>
    /// <param name="frameSet">Which predefined frame sequence to cycle; defaults to <see cref="SpinnerFrameSet.Default"/>.</param>
    /// <param name="interval">Frame period; defaults to <see cref="DefaultInterval"/> (500&#160;ms).</param>
    /// <param name="active">Whether the spinner starts active (indefinite) or idle (scoped).</param>
    public SpinnerTicker(
        SpinnerFrameSet frameSet = SpinnerFrameSet.Default,
        TimeSpan? interval = null,
        bool active = true)
        : base(interval ?? DefaultInterval)
    {
        _frames = FramesFor(frameSet);
        _active = active;
    }

    /// <summary>
    /// Creates a spinner from a caller-supplied custom <paramref name="frames"/> sequence.
    /// </summary>
    /// <param name="interval">Frame period; must be positive.</param>
    /// <param name="frames">Custom frame sequence; must be non-empty and contain no null/empty frame.</param>
    /// <param name="active">Whether the spinner starts active (indefinite) or idle (scoped).</param>
    public SpinnerTicker(TimeSpan interval, IReadOnlyList<string> frames, bool active = true)
        : base(interval)
    {
        _frames = ValidateCustomFrames(frames);
        _active = active;
    }

    /// <summary>The raw frame strings backing <paramref name="frameSet"/>, in cycle order.</summary>
    public static IReadOnlyList<string> Frames(SpinnerFrameSet frameSet) => FramesFor(frameSet);

    /// <inheritdoc/>
    public override bool IsActive => _active;

    /// <inheritdoc/>
    public override string CurrentContent => _frames[_index];

    /// <summary>Begins animating (scoped use). Indefinite spinners are already active.</summary>
    public void Start() => _active = true;

    /// <summary>Stops animating; the overlay renders nothing until <see cref="Start"/> is called again.</summary>
    public void Stop() => _active = false;

    /// <inheritdoc/>
    protected override bool AdvanceFrame()
    {
        _index = (_index + 1) % _frames.Length;
        return true;
    }

    private static string[] FramesFor(SpinnerFrameSet frameSet) => frameSet switch
    {
        SpinnerFrameSet.Default => DefaultFrames,
        SpinnerFrameSet.Dots6 => ["⠇", "⠋", "⠙", "⠸", "⠴", "⠦"],
        SpinnerFrameSet.Dots8 => ["⡇", "⠏", "⠛", "⠹", "⢸", "⣰", "⣤", "⣆"],
        SpinnerFrameSet.Slide => ["⠉", "⠒", "⠤", "⣀"],
        SpinnerFrameSet.SlideBounce => ["⠉", "⠒", "⠤", "⣀", "⠤", "⠒"],
        SpinnerFrameSet.Snake =>
        [
            "⢎ ", "⠎⠁", "⠊⠑", "⠈⠱",
            " ⡱", "⢀⡰", "⢄⡠", "⢆⡀",
        ],
        SpinnerFrameSet.Line => ["|", "/", "—", "\\"],
        _ => throw new ArgumentOutOfRangeException(nameof(frameSet), frameSet, "Unknown spinner frame set."),
    };

    private static string[] ValidateCustomFrames(IReadOnlyList<string> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
            throw new ArgumentException("Frame sequence must be non-empty.", nameof(frames));

        var copy = new string[frames.Count];
        for (int i = 0; i < frames.Count; i++)
        {
            string frame = frames[i];
            if (frame is null)
                throw new ArgumentException($"Frame {i} must not be null.", nameof(frames));
            if (frame.Length == 0)
                throw new ArgumentException($"Frame {i} must not be empty.", nameof(frames));
            copy[i] = frame;
        }

        return copy;
    }
}
