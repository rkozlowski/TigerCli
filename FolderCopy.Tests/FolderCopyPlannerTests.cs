using System.Text;
using FolderCopy;

namespace FolderCopy.Tests;

/// <summary>
/// Focused tests for the copy planner/executor, exercised directly (no TigerCli UI) against real
/// temporary directories.
/// </summary>
public sealed class FolderCopyPlannerTests
{
    [Fact]
    public void Plan_EnumeratesNestedFilesWithRelativePaths()
    {
        using var temp = new TempDir();
        var source = temp.CreateDir("source");
        temp.WriteFile("source/top.txt", "top");
        temp.WriteFile("source/nested/child.txt", "child");
        temp.WriteFile("source/nested/deep/leaf.bin", "leaf-bytes");

        var plan = FolderCopyPlanner.Plan(source);

        var relatives = plan.Items.Select(i => i.RelativePath.Replace('\\', '/')).OrderBy(p => p).ToArray();
        Assert.Equal(new[] { "nested/child.txt", "nested/deep/leaf.bin", "top.txt" }, relatives);
        Assert.Equal(plan.Items.Sum(i => i.Length), plan.TotalBytes);
    }

    [Fact]
    public async Task PlanAsync_MatchesSyncPlan()
    {
        using var temp = new TempDir();
        var source = temp.CreateDir("source");
        temp.WriteFile("source/top.txt", "top");
        temp.WriteFile("source/nested/child.txt", "child");

        var sync = FolderCopyPlanner.Plan(source);
        var async = await FolderCopyPlanner.PlanAsync(source, TestContext.Current.CancellationToken);

        Assert.Equal(sync.TotalBytes, async.TotalBytes);
        Assert.Equal(
            sync.Items.Select(i => i.RelativePath).OrderBy(p => p, StringComparer.Ordinal),
            async.Items.Select(i => i.RelativePath).OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public async Task PlanAsync_CancelledToken_Throws()
    {
        using var temp = new TempDir();
        var source = temp.CreateDir("source");
        temp.WriteFile("source/top.txt", "top");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => FolderCopyPlanner.PlanAsync(source, cts.Token));
    }

    [Fact]
    public void Plan_MissingSource_Throws()
    {
        using var temp = new TempDir();
        var missing = Path.Combine(temp.Path, "does-not-exist");

        Assert.Throws<DirectoryNotFoundException>(() => FolderCopyPlanner.Plan(missing));
    }

    [Fact]
    public async Task Execute_CopiesNestedFilesPreservingRelativePaths()
    {
        using var temp = new TempDir();
        var source = temp.CreateDir("source");
        var destination = temp.CreateDir("destination");
        temp.WriteFile("source/top.txt", "top");
        temp.WriteFile("source/nested/child.txt", "child");

        var plan = FolderCopyPlanner.Plan(source);
        await FolderCopyPlanner.ExecuteAsync(plan, destination, onProgress: null, TestContext.Current.CancellationToken);

        Assert.Equal("top", File.ReadAllText(Path.Combine(destination, "top.txt")));
        Assert.Equal("child", File.ReadAllText(Path.Combine(destination, "nested", "child.txt")));
    }

    [Fact]
    public async Task Execute_OverwritesExistingDestinationFiles()
    {
        using var temp = new TempDir();
        var source = temp.CreateDir("source");
        var destination = temp.CreateDir("destination");
        temp.WriteFile("source/data.txt", "new-content");
        temp.WriteFile("destination/data.txt", "stale-content-that-is-longer");

        var plan = FolderCopyPlanner.Plan(source);
        await FolderCopyPlanner.ExecuteAsync(plan, destination, onProgress: null, TestContext.Current.CancellationToken);

        Assert.Equal("new-content", File.ReadAllText(Path.Combine(destination, "data.txt")));
    }

    [Fact]
    public async Task Execute_ReportsMonotonicProgressReachingCompletion()
    {
        using var temp = new TempDir();
        var source = temp.CreateDir("source");
        var destination = temp.CreateDir("destination");
        temp.WriteFile("source/a.txt", new string('a', 4096));
        temp.WriteFile("source/b.txt", new string('b', 8192));

        var plan = FolderCopyPlanner.Plan(source);
        long lastBytes = 0;
        CopyProgress final = default;
        await FolderCopyPlanner.ExecuteAsync(plan, destination, p =>
        {
            Assert.True(p.BytesCopied >= lastBytes, "byte progress must be monotonic");
            lastBytes = p.BytesCopied;
            final = p;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(2, final.FilesCompleted);
        Assert.Equal(plan.TotalBytes, final.BytesCopied);
    }

    [Fact]
    public async Task Execute_ObservesCancellationBeforeCopyingAnything()
    {
        using var temp = new TempDir();
        var source = temp.CreateDir("source");
        var destination = temp.CreateDir("destination");
        temp.WriteFile("source/data.txt", "content");

        var plan = FolderCopyPlanner.Plan(source);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => FolderCopyPlanner.ExecuteAsync(plan, destination, onProgress: null, cts.Token));

        Assert.False(File.Exists(Path.Combine(destination, "data.txt")));
    }
}

/// <summary>A unique temporary directory that deletes itself on dispose.</summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; }

    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "folder-copy-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string CreateDir(string relative)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(full);
        return full;
    }

    public void WriteFile(string relative, string content)
    {
        var full = System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content, Encoding.UTF8);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a transient lock must not fail the test.
        }
    }
}
