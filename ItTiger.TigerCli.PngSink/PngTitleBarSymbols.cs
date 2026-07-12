namespace ItTiger.TigerCli.PngSink;

/// <summary>
/// Describes the window-control symbol strip rendered in the PNG title bar when window chrome is enabled.
/// </summary>
public sealed class PngTitleBarSymbols
{
    private PngTitleBarSymbols(
        string displayName,
        PngTitleBarAssetKind kind,
        string? path,
        byte[]? bytes)
    {
        DisplayName = displayName;
        Kind = kind;
        Path = path;
        Bytes = bytes;
    }

    /// <summary>The bundled TigerCli window-control symbol strip.</summary>
    public static PngTitleBarSymbols Default { get; } = new(
        "TigerCli window symbols",
        PngTitleBarAssetKind.EmbeddedResource,
        "Assets/tc_window_symbols.png",
        bytes: null);

    /// <summary>No title-bar window-control symbols.</summary>
    public static PngTitleBarSymbols None { get; } = new(
        "None",
        PngTitleBarAssetKind.None,
        path: null,
        bytes: null);

    /// <summary>Human-readable asset name used in diagnostics.</summary>
    public string DisplayName { get; }

    internal PngTitleBarAssetKind Kind { get; }
    internal string? Path { get; }
    internal byte[]? Bytes { get; }

    /// <summary>Loads a title-bar symbol strip from a PNG file path when the image is rendered.</summary>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty, or whitespace.</exception>
    public static PngTitleBarSymbols FromFile(string path, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new PngTitleBarSymbols(
            displayName ?? System.IO.Path.GetFileName(path),
            PngTitleBarAssetKind.File,
            path,
            bytes: null);
    }

    /// <summary>
    /// Creates a title-bar symbol strip from PNG bytes. The byte array is cloned so later caller
    /// mutations do not affect rendering.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="bytes"/> is empty.</exception>
    public static PngTitleBarSymbols FromBytes(byte[] bytes, string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
            throw new ArgumentException("Symbol bytes must not be empty.", nameof(bytes));

        return new PngTitleBarSymbols(
            displayName ?? "Custom symbols",
            PngTitleBarAssetKind.Bytes,
            path: null,
            bytes: (byte[])bytes.Clone());
    }
}
