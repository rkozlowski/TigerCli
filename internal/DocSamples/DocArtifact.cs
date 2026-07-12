namespace DocSamples;

/// <summary>How a committed artifact is written and drift-checked by <c>Program.cs</c>.</summary>
public enum DocArtifactKind
{
    /// <summary>Text artifact (HTML/CSS). Compared newline-normalized, byte-exact otherwise.</summary>
    Text,

    /// <summary>PNG sidecar (<c>.png.txt</c>). Compared like <see cref="Text"/>, except the
    /// volatile environment lines (<c>generated-os:</c>, <c>dotnet:</c>) are masked — they record
    /// where the committed PNG was generated, which legitimately differs per machine.</summary>
    PngSidecar,

    /// <summary>PNG image. Existence is checked everywhere; bytes are compared only on the
    /// canonical generation platform (Windows 11), because glyph rasterization is
    /// platform-backed and not byte-identical across operating systems.</summary>
    Png,
}

/// <summary>
/// One generated documentation artifact: a path relative to <c>docs/examples/</c> (always with
/// <c>/</c> separators) and its full, deterministic content. Text artifacts are LF-only strings;
/// PNG artifacts carry raw bytes and are paired with a <see cref="DocArtifactKind.PngSidecar"/>
/// recording generation metadata and the visible text.
/// </summary>
public sealed record DocArtifact
{
    private DocArtifact(string relativePath, DocArtifactKind kind, string? content, byte[]? bytes)
    {
        RelativePath = relativePath;
        Kind = kind;
        Content = content;
        Bytes = bytes;
    }

    public string RelativePath { get; }
    public DocArtifactKind Kind { get; }

    /// <summary>Text content; <c>null</c> for <see cref="DocArtifactKind.Png"/>.</summary>
    public string? Content { get; }

    /// <summary>Binary content; <c>null</c> unless <see cref="DocArtifactKind.Png"/>.</summary>
    public byte[]? Bytes { get; }

    public static DocArtifact Text(string relativePath, string content)
        => new(relativePath, DocArtifactKind.Text, content, null);

    public static DocArtifact PngSidecar(string relativePath, string content)
        => new(relativePath, DocArtifactKind.PngSidecar, content, null);

    public static DocArtifact Png(string relativePath, byte[] bytes)
        => new(relativePath, DocArtifactKind.Png, bytes: bytes, content: null);
}
