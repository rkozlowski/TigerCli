using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.PngSink;

/// <summary>
/// Immutable options used by <see cref="PngSink"/> and <see cref="PngRenderer"/> to size and style a
/// rendered PNG image.
/// </summary>
public sealed class PngSinkOptions
{
    /// <summary>The number of terminal text columns to render. Must be positive.</summary>
    public required int Columns { get; init; }

    /// <summary>The number of terminal text rows to render. Must be positive.</summary>
    public required int Rows { get; init; }

    /// <summary>Whether to render only terminal content or include TigerCli window chrome.</summary>
    public PngWindowChrome Chrome { get; init; } = PngWindowChrome.None;

    /// <summary>
    /// Optional title displayed in the title bar when <see cref="Chrome"/> is
    /// <see cref="PngWindowChrome.FrameAndTitle"/>. Control characters are sanitized before rendering.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>The title-bar icon source. Use <see cref="PngTitleBarIcon.None"/> to suppress it.</summary>
    public PngTitleBarIcon TitleBarIcon { get; init; } = PngTitleBarIcon.Default;

    /// <summary>The title-bar window-control symbol source. Use <see cref="PngTitleBarSymbols.None"/> to suppress it.</summary>
    public PngTitleBarSymbols TitleBarSymbols { get; init; } = PngTitleBarSymbols.Default;

    /// <summary>The monospace font used for terminal cells.</summary>
    public PngFontSource TerminalFont { get; init; } = PngFontSource.BundledCascadiaMono;

    /// <summary>The font used for the title bar.</summary>
    public PngFontSource TitleFont { get; init; } = PngFontSource.BundledCascadiaMono;

    /// <summary>The terminal font size in pixels. Must be positive.</summary>
    public float TerminalFontSize { get; init; } = 18;

    /// <summary>The title-bar font size in pixels. Must be positive.</summary>
    public float TitleFontSize { get; init; } = 16;

    /// <summary>Default foreground colour used when a text segment has no explicit foreground.</summary>
    public CliColor DefaultForeground { get; init; } = CliColor.Gray;

    /// <summary>The background colour of the terminal content area.</summary>
    public CliColor TerminalBackground { get; init; } = CliColor.Black;

    /// <summary>The background colour of the title bar.</summary>
    public CliColor TitleBackground { get; init; } = CliColor.Gray15;

    /// <summary>The foreground colour of title-bar text and symbols.</summary>
    public CliColor TitleForeground { get; init; } = CliColor.White;

    /// <summary>The frame colour used when window chrome is enabled.</summary>
    public CliColor FrameColor { get; init; } = CliColor.DarkGray;

    /// <summary>Controls whether writes outside <see cref="Columns"/> by <see cref="Rows"/> throw or clip.</summary>
    public PngOverflowMode OverflowMode { get; init; } = PngOverflowMode.Throw;

    internal void Validate()
    {
        if (Columns <= 0)
            throw new ArgumentOutOfRangeException(nameof(Columns), Columns, "Columns must be positive.");
        if (Rows <= 0)
            throw new ArgumentOutOfRangeException(nameof(Rows), Rows, "Rows must be positive.");
        if (TerminalFontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(TerminalFontSize), TerminalFontSize, "TerminalFontSize must be positive.");
        if (TitleFontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(TitleFontSize), TitleFontSize, "TitleFontSize must be positive.");
        ArgumentNullException.ThrowIfNull(TerminalFont);
        ArgumentNullException.ThrowIfNull(TitleFont);
        ArgumentNullException.ThrowIfNull(TitleBarIcon);
        ArgumentNullException.ThrowIfNull(TitleBarSymbols);
    }
}
