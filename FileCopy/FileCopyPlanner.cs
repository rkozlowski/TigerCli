namespace FileCopy;

/// <summary>A progress snapshot for one file copy.</summary>
/// <param name="BytesCopied">Bytes written so far.</param>
/// <param name="TotalBytes">Total source-file length.</param>
public readonly record struct FileCopyProgress(long BytesCopied, long TotalBytes);

/// <summary>
/// TigerCli-free single-file copy logic. It does not create directories, retry, preserve ACLs, or
/// perform multi-file work; the command layer supplies a validated source and destination.
/// </summary>
public static class FileCopyPlanner
{
    private const int BufferSize = 128 * 1024;

    /// <summary>
    /// Copies one file asynchronously and reports byte progress. When <paramref name="overwrite"/>
    /// is false, opening an existing destination fails atomically.
    /// </summary>
    public static async Task CopyAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        Action<FileCopyProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path must be provided.", nameof(destinationPath));

        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        if (!File.Exists(source))
            throw new FileNotFoundException("Source file does not exist.", source);
        var parent = Path.GetDirectoryName(destination);
        if (parent is null || !Directory.Exists(parent))
            throw new DirectoryNotFoundException($"Destination parent folder does not exist: {parent}");
        if (PathsAreSame(source, destination))
            throw new IOException("Source and destination are the same file.");

        ct.ThrowIfCancellationRequested();
        var length = new FileInfo(source).Length;
        long copied = 0;
        onProgress?.Invoke(new FileCopyProgress(0, length));

        var destinationMode = overwrite ? FileMode.Create : FileMode.CreateNew;
        var buffer = new byte[BufferSize];
        await using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        await using var output = new FileStream(
            destination, destinationMode, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

        int read;
        while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            copied += read;
            onProgress?.Invoke(new FileCopyProgress(copied, length));
        }
    }

    private static bool PathsAreSame(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(first, second, comparison);
    }
}
