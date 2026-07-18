using FolderCopy;
using ItTiger.TigerCli.Testing;

namespace FolderCopy.Tests;

/// <summary>
/// App-boundary tests for the Folder Copy app: the same <see cref="FolderCopyApp.Create"/> factory
/// that Program.cs runs, driven through <see cref="TigerCliAppTestHost"/>.
/// </summary>
/// <remarks>
/// The copy runs through a single <c>RunActivityAsync</c> path for both interaction modes. These
/// host-driven tests use <c>--non-interactive</c>, where that path executes headlessly (no dialog, no
/// keyboard) while still performing the copy — so the whole app boundary is exercised deterministically.
/// Semi-interactive folder-picker prompting is covered separately in
/// <see cref="FolderCopyFolderPickerTests"/>.
/// </remarks>
public sealed class FolderCopyAppTests
{
    [Fact]
    public async Task Copy_CopiesNestedFilesPreservingRelativePaths()
    {
        using var temp = new TempDir();
        var source = temp.CreateDir("source");
        var destination = temp.CreateDir("destination");
        temp.WriteFile("source/top.txt", "top");
        temp.WriteFile("source/nested/child.txt", "child");

        var result = await TigerCliAppTestHost
            .For(FolderCopyApp.Create())
            .WithArgs("--source", source, "--destination", destination, "--non-interactive")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FolderCopyExitCode.Ok, result.ExitCode);
        Assert.Equal("top", File.ReadAllText(Path.Combine(destination, "top.txt")));
        Assert.Equal("child", File.ReadAllText(Path.Combine(destination, "nested", "child.txt")));
        Assert.Contains("Copied", result.StdOut);
    }

    [Fact]
    public async Task Copy_BracketedDestinationPath_IsEscapedOnceInOutput()
    {
        using var temp = new TempDir();
        var source = temp.CreateDir("source");
        var destination = temp.CreateDir("destination[brackets]");
        temp.WriteFile("source/top.txt", "top");

        var result = await TigerCliAppTestHost
            .For(FolderCopyApp.Create())
            .WithArgs("--source", source, "--destination", destination, "--non-interactive")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FolderCopyExitCode.Ok, result.ExitCode);
        Assert.Contains("destination[brackets]", result.StdOut);
        Assert.DoesNotContain("destination[[brackets]]", result.StdOut);
    }

    [Fact]
    public async Task Copy_EmptySource_ReportsNothingToCopy()
    {
        using var temp = new TempDir();
        var source = temp.CreateDir("source");
        var destination = temp.CreateDir("destination");

        var result = await TigerCliAppTestHost
            .For(FolderCopyApp.Create())
            .WithArgs("-s", source, "-d", destination, "--non-interactive")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FolderCopyExitCode.Ok, result.ExitCode);
        Assert.Contains("No files to copy", result.StdOut);
    }

    [Fact]
    public async Task Copy_MissingSource_NonInteractive_FailsCleanly()
    {
        using var temp = new TempDir();
        var destination = temp.CreateDir("destination");

        var result = await TigerCliAppTestHost
            .For(FolderCopyApp.Create())
            .WithArgs("--destination", destination, "--non-interactive")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FolderCopyExitCode.ValidationError, result.ExitCode);
        Assert.Contains("--source", result.StdErr);
    }

    [Fact]
    public async Task Copy_MissingDestination_NonInteractive_FailsCleanly()
    {
        using var temp = new TempDir();
        var source = temp.CreateDir("source");
        temp.WriteFile("source/top.txt", "top");

        var result = await TigerCliAppTestHost
            .For(FolderCopyApp.Create())
            .WithArgs("--source", source, "--non-interactive")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FolderCopyExitCode.ValidationError, result.ExitCode);
        Assert.Contains("--destination", result.StdErr);
    }

    [Fact]
    public async Task Copy_NonExistentSource_FailsWithAppError()
    {
        using var temp = new TempDir();
        var missing = Path.Combine(temp.Path, "does-not-exist");
        var destination = temp.CreateDir("destination");

        var result = await TigerCliAppTestHost
            .For(FolderCopyApp.Create())
            .WithArgs("--source", missing, "--destination", destination, "--non-interactive")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FolderCopyExitCode.CopyFailed, result.ExitCode);
        Assert.Contains("does not exist", result.StdErr);
    }

    [Fact]
    public async Task Help_ShowsSourceAndDestinationOptions()
    {
        var result = await TigerCliAppTestHost
            .For(FolderCopyApp.Create())
            .WithArgs("--help")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FolderCopyExitCode.Ok, result.ExitCode);
        Assert.Contains("folder-copy", result.StdOut);
        Assert.Contains("--source", result.StdOut);
        Assert.Contains("--destination", result.StdOut);
    }

    [Fact]
    public async Task Version_WritesDisplayNameAndSharedVersion()
    {
        var result = await TigerCliAppTestHost
            .For(FolderCopyApp.Create())
            .WithArgs("--version")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FolderCopyExitCode.Ok, result.ExitCode);
        Assert.Contains("Folder Copy version 0.8.1", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task VersionFull_WritesInformationalVersion()
    {
        var result = await TigerCliAppTestHost
            .For(FolderCopyApp.Create())
            .WithArgs("--version-full")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FolderCopyExitCode.Ok, result.ExitCode);
        Assert.Contains("Folder Copy product version 0.8.1+", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task HelpErrors_DocumentsTypedExitCodes()
    {
        var result = await TigerCliAppTestHost
            .For(FolderCopyApp.Create())
            .WithArgs("--help-errors")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FolderCopyExitCode.Ok, result.ExitCode);
        Assert.Contains("Copy failed", result.StdOut);
        Assert.Contains("Validation error", result.StdOut);
        Assert.Empty(result.StdErr);
    }
}
