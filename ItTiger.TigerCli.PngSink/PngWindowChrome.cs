namespace ItTiger.TigerCli.PngSink;

/// <summary>
/// Controls whether PNG output is just the terminal content area or includes TigerCli's terminal frame
/// and title bar.
/// </summary>
public enum PngWindowChrome
{
    /// <summary>Render only the configured terminal grid.</summary>
    None,

    /// <summary>Render a one-pixel frame and a title bar above the terminal grid.</summary>
    FrameAndTitle
}
