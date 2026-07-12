namespace ItTiger.TigerCli.PngSink;

/// <summary>
/// Describes the icon rendered in the PNG title bar when window chrome is enabled.
/// </summary>
public sealed class PngTitleBarIcon
{
    private PngTitleBarIcon(
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

    /// <summary>The bundled TigerCli terminal icon.</summary>
    public static PngTitleBarIcon Default { get; } = new(
        "TigerCli terminal icon",
        PngTitleBarAssetKind.EmbeddedResource,
        "Assets/tc_term_ico.png",
        bytes: null);

    /// <summary>No title-bar icon.</summary>
    public static PngTitleBarIcon None { get; } = new(
        "None",
        PngTitleBarAssetKind.None,
        path: null,
        bytes: null);

    /// <summary>Human-readable asset name used in diagnostics.</summary>
    public string DisplayName { get; }

    internal PngTitleBarAssetKind Kind { get; }
    internal string? Path { get; }
    internal byte[]? Bytes { get; }

    /// <summary>Loads a title-bar icon from a PNG file path when the image is rendered.</summary>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty, or whitespace.</exception>
    public static PngTitleBarIcon FromFile(string path, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new PngTitleBarIcon(
            displayName ?? System.IO.Path.GetFileName(path),
            PngTitleBarAssetKind.File,
            path,
            bytes: null);
    }

    /// <summary>
    /// Creates a title-bar icon from PNG bytes. The byte array is cloned so later caller mutations do
    /// not affect rendering.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="bytes"/> is empty.</exception>
    public static PngTitleBarIcon FromBytes(byte[] bytes, string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
            throw new ArgumentException("Icon bytes must not be empty.", nameof(bytes));

        return new PngTitleBarIcon(
            displayName ?? "Custom icon",
            PngTitleBarAssetKind.Bytes,
            path: null,
            bytes: (byte[])bytes.Clone());
    }
}

internal enum PngTitleBarAssetKind
{
    None,
    EmbeddedResource,
    File,
    Bytes
}
