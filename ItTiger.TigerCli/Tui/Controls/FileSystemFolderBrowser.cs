using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Default <see cref="IFolderBrowser"/> backed by <see cref="System.IO"/>. Exception-safe:
/// unauthorized, missing, or transiently failing locations surface as empty listings rather
/// than thrown exceptions. Platform behavior follows the host OS by default and can be
/// overridden for testing.
/// </summary>
public sealed class FileSystemFolderBrowser : IFolderBrowser
{
    private readonly bool _windowsStyle;

    /// <param name="windowsStyle">
    /// When <c>true</c>, the top level is a drive list and drive roots have no parent above the
    /// list. When <c>false</c>, the top level is <c>"/"</c>. Defaults to the host OS.
    /// </param>
    public FileSystemFolderBrowser(bool? windowsStyle = null)
    {
        _windowsStyle = windowsStyle ?? OperatingSystem.IsWindows();
    }

    /// <inheritdoc/>
    public string? RootLocation => _windowsStyle ? null : "/";

    /// <inheritdoc/>
    public (string? location, string? highlightPath) ResolveInitial(string? initialPath)
    {
        if (string.IsNullOrWhiteSpace(initialPath))
            return (RootLocation, null);

        // Walk up from the requested path to the deepest ancestor that actually exists and reads.
        string? target = null;
        var probe = TryNormalize(initialPath);
        while (probe is not null)
        {
            if (DirectoryExists(probe))
            {
                target = probe;
                break;
            }

            probe = ParentPath(probe);
        }

        if (target is null)
            return (RootLocation, null);

        if (TryGetParent(target, out var parent))
            return (parent, target);

        // target is the Unix root "/" (a Windows drive root resolves through TryGetParent above).
        return (target, null);
    }

    /// <inheritdoc/>
    public IReadOnlyList<FolderEntry> GetEntries(string? location)
    {
        if (location is null)
            return GetDrives();

        try
        {
            var entries = new List<FolderEntry>();
            foreach (var dir in Directory.EnumerateDirectories(location))
            {
                var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(name))
                    name = dir;

                entries.Add(new FolderEntry(name, dir, HasChildren(dir)));
            }

            entries.Sort(static (a, b) => string.Compare(a.Label, b.Label, StringComparison.CurrentCultureIgnoreCase));
            return entries;
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return Array.Empty<FolderEntry>();
        }
    }

    /// <inheritdoc/>
    public bool TryGetParent(string? location, out string? parent)
    {
        parent = null;

        if (location is null)
            return false; // already at the Windows drive list

        try
        {
            var info = new DirectoryInfo(location);
            var dirParent = info.Parent;

            if (dirParent is null)
            {
                // A drive root (Windows) → return to the drive list. The Unix root "/" stays put.
                if (_windowsStyle)
                {
                    parent = null;
                    return true;
                }

                return false;
            }

            parent = dirParent.FullName;
            return true;
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return false;
        }
    }

    private IReadOnlyList<FolderEntry> GetDrives()
    {
        try
        {
            var entries = new List<FolderEntry>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!SafeIsReady(drive))
                    continue;

                var name = drive.Name;
                entries.Add(new FolderEntry(name, name, HasChildren(name)));
            }

            entries.Sort(static (a, b) => string.Compare(a.Label, b.Label, StringComparison.CurrentCultureIgnoreCase));
            return entries;
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return Array.Empty<FolderEntry>();
        }
    }

    private static bool HasChildren(string path)
    {
        try
        {
            using var e = Directory.EnumerateDirectories(path).GetEnumerator();
            return e.MoveNext();
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return false;
        }
    }

    private static bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return false;
        }
    }

    private static bool SafeIsReady(DriveInfo drive)
    {
        try
        {
            return drive.IsReady;
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return false;
        }
    }

    private static string? TryNormalize(string path)
    {
        try
        {
            return new DirectoryInfo(path).FullName;
        }
        catch (Exception ex) when (IsFilesystemException(ex) || ex is ArgumentException)
        {
            return null;
        }
    }

    private static string? ParentPath(string path)
    {
        try
        {
            return Directory.GetParent(path)?.FullName;
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return null;
        }
    }

    private static bool IsFilesystemException(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or NotSupportedException;
}
