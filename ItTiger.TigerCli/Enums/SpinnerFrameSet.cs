namespace ItTiger.TigerCli.Enums;

/// <summary>
/// A predefined spinner frame sequence owned by <c>SpinnerTicker</c>. Selects one of a small curated set
/// of cyclic indicators; the ticker resolves the enum to its raw frame strings. Frames are raw content —
/// any presentation (brackets, styling, title prefixing) is decided by the overlay or title that renders
/// them, never by the frame set itself.
/// </summary>
public enum SpinnerFrameSet
{
    /// <summary>The four-step braille spinner used by default: ⠖ ⠲ ⠴ ⠦.</summary>
    Default,

    /// <summary>A six-step braille rotation: ⠇ ⠋ ⠙ ⠸ ⠴ ⠦.</summary>
    Dots6,

    /// <summary>An eight-step heavier braille rotation: ⡇ ⠏ ⠛ ⠹ ⢸ ⣰ ⣤ ⣆.</summary>
    Dots8,

    /// <summary>A four-step settling slide: ⠉ ⠒ ⠤ ⣀.</summary>
    Slide,

    /// <summary>A six-step slide that bounces back: ⠉ ⠒ ⠤ ⣀ ⠤ ⠒.</summary>
    SlideBounce,

    /// <summary>An eight-step two-column travelling braille snake.</summary>
    Snake,

    /// <summary>A classic four-step ASCII line spinner: | / — \.</summary>
    Line,
}
