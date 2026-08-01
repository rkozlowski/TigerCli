using FileCopy;

namespace FileCopy.Tests;

public sealed class FileCopyPlannerTests
{
    [Fact]
    public async Task CopyAsync_ReportsProgressAndCopiesExactContent()
    {
        using var temp = new TempDir();
        var content = new string('x', 300_000);
        var source = temp.WriteFile("source.bin", content);
        var destination = Path.Combine(temp.Path, "destination.bin");
        var progress = new List<FileCopyProgress>();

        await FileCopyPlanner.CopyAsync(
            source,
            destination,
            overwrite: false,
            progress.Add,
            TestContext.Current.CancellationToken);

        Assert.Equal(content, File.ReadAllText(destination));
        Assert.NotEmpty(progress);
        Assert.Equal(new FileInfo(source).Length, progress[^1].BytesCopied);
        Assert.Equal(progress[^1].TotalBytes, progress[^1].BytesCopied);
        Assert.True(progress.Select(item => item.BytesCopied).SequenceEqual(
            progress.Select(item => item.BytesCopied).OrderBy(value => value)));
    }

    [Fact]
    public async Task CopyAsync_ExistingDestinationWithoutOverwrite_ThrowsAndPreservesFile()
    {
        using var temp = new TempDir();
        var source = temp.WriteFile("source.txt", "new");
        var destination = temp.WriteFile("destination.txt", "existing");

        await Assert.ThrowsAsync<IOException>(() => FileCopyPlanner.CopyAsync(
            source,
            destination,
            overwrite: false,
            ct: TestContext.Current.CancellationToken));

        Assert.Equal("existing", File.ReadAllText(destination));
    }

    [Fact]
    public async Task CopyAsync_SameSourceAndDestination_Throws()
    {
        using var temp = new TempDir();
        var source = temp.WriteFile("source.txt", "content");

        var ex = await Assert.ThrowsAsync<IOException>(() => FileCopyPlanner.CopyAsync(
            source,
            source,
            overwrite: true,
            ct: TestContext.Current.CancellationToken));

        Assert.Contains("same file", ex.Message);
    }

    [Fact]
    public async Task CopyAsync_CancelledToken_DoesNotCreateDestination()
    {
        using var temp = new TempDir();
        var source = temp.WriteFile("source.txt", "content");
        var destination = Path.Combine(temp.Path, "destination.txt");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FileCopyPlanner.CopyAsync(
            source,
            destination,
            overwrite: false,
            ct: cts.Token));

        Assert.False(File.Exists(destination));
    }
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "file-copy-tests",
        Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string CreateDir(string relative)
    {
        var fullPath = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public string WriteFile(string relative, string content)
    {
        var fullPath = System.IO.Path.Combine(
            Path,
            relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        var fullPath = System.IO.Path.GetFullPath(Path);
        var tempRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
        if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
            || !fullPath.Contains("file-copy-tests", StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to remove unexpected test path '{fullPath}'.");

        try
        {
            Directory.Delete(fullPath, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup for transient test-host file locks.
        }
    }
}
