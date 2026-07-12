namespace ItTiger.TigerCli.PngSink;

/// <summary>
/// Describes the font files used by the PNG renderer for terminal text or title text.
/// </summary>
public sealed class PngFontSource
{
    private PngFontSource(
        string displayName,
        PngFontSourceKind kind,
        string regular,
        string? bold,
        string? italic,
        string? boldItalic)
    {
        DisplayName = displayName;
        Kind = kind;
        Regular = regular;
        Bold = bold;
        Italic = italic;
        BoldItalic = boldItalic;
    }

    /// <summary>Bundled Cascadia Mono regular/bold/italic/bold-italic font files.</summary>
    public static PngFontSource BundledCascadiaMono { get; } = new(
        "Cascadia Mono",
        PngFontSourceKind.EmbeddedResource,
        "CascadiaMono-Regular.ttf",
        "CascadiaMono-Bold.ttf",
        "CascadiaMono-Italic.ttf",
        "CascadiaMono-BoldItalic.ttf");

    /// <summary>Bundled Noto Sans regular/bold/italic/bold-italic font files.</summary>
    public static PngFontSource BundledNotoSans { get; } = new(
        "Noto Sans",
        PngFontSourceKind.EmbeddedResource,
        "NotoSans-Regular.ttf",
        "NotoSans-Bold.ttf",
        "NotoSans-Italic.ttf",
        "NotoSans-BoldItalic.ttf");

    internal static PngFontSource BundledNotoSansSymbols2 { get; } = new(
        "Noto Sans Symbols 2",
        PngFontSourceKind.EmbeddedResource,
        "NotoSansSymbols2-Regular.ttf",
        bold: null,
        italic: null,
        boldItalic: null);

    /// <summary>Human-readable font name used in diagnostics.</summary>
    public string DisplayName { get; }

    internal PngFontSourceKind Kind { get; }
    internal string Regular { get; }
    internal string? Bold { get; }
    internal string? Italic { get; }
    internal string? BoldItalic { get; }

    /// <summary>
    /// Creates a font source from caller-provided font files. The regular font path is required; bold,
    /// italic, and bold-italic paths are optional but required at render time if those styles are used.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="regularPath"/> is null, empty, or whitespace.</exception>
    public static PngFontSource FromFile(
        string regularPath,
        string? boldPath = null,
        string? italicPath = null,
        string? boldItalicPath = null,
        string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regularPath);

        return new PngFontSource(
            displayName ?? Path.GetFileNameWithoutExtension(regularPath),
            PngFontSourceKind.File,
            regularPath,
            boldPath,
            italicPath,
            boldItalicPath);
    }
}

internal enum PngFontSourceKind
{
    EmbeddedResource,
    File
}
