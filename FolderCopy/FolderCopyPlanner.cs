namespace FolderCopy;

/// <summary>One file to copy: its full source path, its path relative to the source root, and size.</summary>
public readonly record struct CopyItem(string SourcePath, string RelativePath, long Length);

/// <summary>The materialized set of files to copy under a source root, plus their total size.</summary>
public sealed record CopyPlan(IReadOnlyList<CopyItem> Items, long TotalBytes);

/// <summary>
/// A single progress snapshot reported while a <see cref="CopyPlan"/> executes. It is a value type so
/// callers can forward it straight into UI without allocation churn.
/// </summary>
/// <param name="FilesCompleted">Files fully copied so far.</param>
/// <param name="TotalFiles">Total files in the plan.</param>
/// <param name="BytesCopied">Bytes copied so far across all files.</param>
/// <param name="TotalBytes">Total bytes in the plan.</param>
/// <param name="CurrentRelativePath">Relative path of the file currently being copied.</param>
/// <param name="CurrentFileBytes">Bytes copied so far for the current file.</param>
/// <param name="CurrentFileLength">Length of the current file.</param>
public readonly record struct CopyProgress(
    int FilesCompleted,
    int TotalFiles,
    long BytesCopied,
    long TotalBytes,
    string CurrentRelativePath,
    long CurrentFileBytes,
    long CurrentFileLength);

/// <summary>
/// Recursively copies the contents of one folder into another. Deliberately minimal: it preserves
/// relative paths, creates destination directories as needed, and <b>overwrites</b> existing files.
/// There is no retry, ACL preservation, symlink handling, exclude/ignore support, checksum, or mirror
/// (delete) behavior — this is a usage example, not a production sync tool.
/// </summary>
/// <remarks>
/// Planning (<see cref="Plan"/>) is separated from execution (<see cref="ExecuteAsync"/>) so both are
/// independently testable and so the file list is fully materialized before any write happens — that
/// keeps the copy well-defined even when the destination sits under the source (newly written files
/// are never re-enumerated).
/// </remarks>
public static class FolderCopyPlanner
{
    private const int BufferSize = 128 * 1024;

    /// <summary>
    /// Enumerates every file beneath <paramref name="sourceRoot"/> (recursively) into a
    /// <see cref="CopyPlan"/>. The enumeration is fully materialized before returning.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="sourceRoot"/> is null or whitespace.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="sourceRoot"/> does not exist.</exception>
    public static CopyPlan Plan(string sourceRoot) => BuildPlan(sourceRoot, CancellationToken.None);

    /// <summary>
    /// Asynchronous, cancellation-aware form of <see cref="Plan"/>. The filesystem walk runs on a thread
    /// pool thread (so it never blocks the caller's UI thread) and observes <paramref name="ct"/> as it
    /// descends directories and lists files, so scanning a large tree can be cancelled promptly.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="sourceRoot"/> is null or whitespace.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="sourceRoot"/> does not exist.</exception>
    public static Task<CopyPlan> PlanAsync(string sourceRoot, CancellationToken ct = default) =>
        Task.Run(() => BuildPlan(sourceRoot, ct), ct);

    private static CopyPlan BuildPlan(string sourceRoot, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot))
            throw new ArgumentException("Source root must be provided.", nameof(sourceRoot));

        var root = Path.GetFullPath(sourceRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Source folder does not exist: {root}");

        var items = new List<CopyItem>();
        long totalBytes = 0;

        // Manual iterative walk (instead of EnumerateFiles(AllDirectories)) so the cancellation token can
        // be checked as the traversal descends each directory and lists its files.
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
                pending.Push(subdirectory);

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                ct.ThrowIfCancellationRequested();
                var length = new FileInfo(file).Length;
                var relative = Path.GetRelativePath(root, file);
                items.Add(new CopyItem(file, relative, length));
                totalBytes += length;
            }
        }

        return new CopyPlan(items, totalBytes);
    }

    /// <summary>
    /// Copies every file in <paramref name="plan"/> into <paramref name="destinationRoot"/>, preserving
    /// relative paths and creating directories as needed. Existing files are overwritten. Progress is
    /// reported through <paramref name="onProgress"/> (before each file, after each write, and after each
    /// file completes). The <paramref name="ct"/> is observed between files and threaded into every read
    /// and write, so a cancellation request stops the copy promptly.
    /// </summary>
    public static async Task ExecuteAsync(
        CopyPlan plan,
        string destinationRoot,
        Action<CopyProgress>? onProgress,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (string.IsNullOrWhiteSpace(destinationRoot))
            throw new ArgumentException("Destination root must be provided.", nameof(destinationRoot));

        var destRoot = Path.GetFullPath(destinationRoot);
        Directory.CreateDirectory(destRoot);

        var buffer = new byte[BufferSize];
        long bytesCopied = 0;
        var filesCompleted = 0;

        foreach (var item in plan.Items)
        {
            ct.ThrowIfCancellationRequested();

            var destPath = Path.Combine(destRoot, item.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            onProgress?.Invoke(new CopyProgress(
                filesCompleted, plan.Items.Count, bytesCopied, plan.TotalBytes,
                item.RelativePath, 0, item.Length));

            long currentFileBytes = 0;
            await using (var source = new FileStream(
                item.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true))
            await using (var destination = new FileStream(
                destPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    bytesCopied += read;
                    currentFileBytes += read;

                    onProgress?.Invoke(new CopyProgress(
                        filesCompleted, plan.Items.Count, bytesCopied, plan.TotalBytes,
                        item.RelativePath, currentFileBytes, item.Length));
                }
            }

            filesCompleted++;
            onProgress?.Invoke(new CopyProgress(
                filesCompleted, plan.Items.Count, bytesCopied, plan.TotalBytes,
                item.RelativePath, item.Length, item.Length));
        }
    }
}
