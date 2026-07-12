namespace ItTiger.TigerCli.Tui.Abstractions;

/// <summary>
/// A single selectable folder row in <see cref="IFolderBrowser"/>: either a child
/// directory or, at the Windows top level, a drive root.
/// </summary>
/// <param name="Label">Display text for the row (the directory name, or a drive name such as <c>C:\</c>).</param>
/// <param name="Path">The full path the row represents and that selection returns.</param>
/// <param name="HasChildren">True when the folder contains at least one subfolder, so it can be opened.</param>
public readonly record struct FolderEntry(string Label, string Path, bool HasChildren);

/// <summary>
/// Filesystem navigation policy used by <c>InlineFolderSelect</c>. The control is agnostic to
/// the underlying filesystem and platform; all platform rules (drive lists, root behavior,
/// parent resolution) and all error handling live behind this abstraction.
/// </summary>
/// <remarks>
/// Implementations must be exception-safe: inaccessible, missing, or transiently failing
/// locations must surface as an empty entry list (or <see cref="FolderEntry.HasChildren"/> =
/// <c>false</c>), never as a thrown exception.
/// </remarks>
public interface IFolderBrowser
{
    /// <summary>
    /// The conceptual top location. On Windows this is <c>null</c> (a synthetic drive list);
    /// on Unix this is <c>"/"</c>. Used as the fallback target for unresolvable initial paths.
    /// </summary>
    string? RootLocation { get; }

    /// <summary>
    /// Resolves a caller-supplied initial path into a starting location and the path that should
    /// be highlighted within that location. When the path cannot be found or read, falls back to
    /// the closest readable ancestor, and ultimately to <see cref="RootLocation"/> with no
    /// specific highlight.
    /// </summary>
    (string? location, string? highlightPath) ResolveInitial(string? initialPath);

    /// <summary>
    /// Lists the selectable folder rows at <paramref name="location"/>. A <c>null</c> location
    /// means the Windows drive list. Returns an empty list when the location is unreadable.
    /// </summary>
    IReadOnlyList<FolderEntry> GetEntries(string? location);

    /// <summary>
    /// Computes the parent location to navigate to from <paramref name="location"/>.
    /// Returns <c>false</c> when already at the top (the Windows drive list, or Unix <c>"/"</c>),
    /// in which case no upward navigation occurs. When it returns <c>true</c>, <paramref name="parent"/>
    /// is the parent location (which may be <c>null</c> to return to the Windows drive list).
    /// </summary>
    bool TryGetParent(string? location, out string? parent);
}
