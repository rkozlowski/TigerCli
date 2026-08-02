using System.Reflection;
using FileCopy;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Testing;
using ItTiger.TigerCli.Tui.Testing;

namespace FileCopy.Tests;

public sealed class FileCopyAppTests
{
    [Fact]
    public async Task CopyToFolder_CopiesUnderOriginalFileName()
    {
        using var temp = new TempDir();
        var source = temp.WriteFile("source/report.txt", "report");
        var destination = temp.CreateDir("destination");

        var result = await RunAsync("copy-to-folder", "--source", source, "--destination", destination, "--non-interactive");

        Assert.Equal((int)FileCopyExitCode.Ok, result.ExitCode);
        Assert.Equal("report", File.ReadAllText(Path.Combine(destination, "report.txt")));
        Assert.Contains("Copied", result.StdOut);
    }

    [Fact]
    public async Task CopyToFolder_ExistingTarget_FailsWithoutSilentOverwrite()
    {
        using var temp = new TempDir();
        var source = temp.WriteFile("source/report.txt", "new");
        var destination = temp.CreateDir("destination");
        temp.WriteFile("destination/report.txt", "existing");

        var result = await RunAsync("copy-to-folder", "--source", source, "--destination", destination, "--non-interactive");

        Assert.Equal((int)FileCopyExitCode.CopyFailed, result.ExitCode);
        Assert.Equal("existing", File.ReadAllText(Path.Combine(destination, "report.txt")));
        Assert.Contains("already exists", result.StdErr);
    }

    [Fact]
    public async Task CopyToFile_NewDestination_Succeeds()
    {
        using var temp = new TempDir();
        var source = temp.WriteFile("source/input.json", "json");
        var destination = Path.Combine(temp.CreateDir("output"), "copy.json");

        var result = await RunAsync("copy-to-file", "--source", source, "--destination", destination, "--non-interactive");

        Assert.Equal((int)FileCopyExitCode.Ok, result.ExitCode);
        Assert.Equal("json", File.ReadAllText(destination));
    }

    [Fact]
    public async Task CopyToFile_ExistingDestinationWithoutForce_FailsNonInteractive()
    {
        using var temp = new TempDir();
        var source = temp.WriteFile("source/input.txt", "new");
        var destination = temp.WriteFile("output/copy.txt", "existing");

        var result = await RunAsync("copy-to-file", "--source", source, "--destination", destination, "--non-interactive");

        Assert.Equal((int)FileCopyExitCode.ValidationError, result.ExitCode);
        Assert.Equal("existing", File.ReadAllText(destination));
        Assert.Contains("confirmation", result.StdErr);
    }

    [Fact]
    public async Task CopyToFile_ExistingDestinationWithForce_OverwritesQuietly()
    {
        using var temp = new TempDir();
        var source = temp.WriteFile("source/input.txt", "new");
        var destination = temp.WriteFile("output/copy.txt", "existing-content");

        var result = await RunAsync(
            "copy-to-file", "--source", source, "--destination", destination, "--force", "--non-interactive");

        Assert.Equal((int)FileCopyExitCode.Ok, result.ExitCode);
        Assert.Equal("new", File.ReadAllText(destination));
    }

    [Fact]
    public async Task CopyToFile_MissingSource_NonInteractiveFailsValidation()
    {
        using var temp = new TempDir();
        var destination = Path.Combine(temp.Path, "copy.txt");

        var result = await RunAsync("copy-to-file", "--destination", destination, "--non-interactive");

        Assert.Equal((int)FileCopyExitCode.ValidationError, result.ExitCode);
        Assert.Contains("--source", result.StdErr);
    }

    [Fact]
    public async Task Help_ShowsBothCommands()
    {
        var result = await TigerCliAppTestHost
            .For(FileCopyApp.Create())
            .WithArgs("--help")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FileCopyExitCode.Ok, result.ExitCode);
        Assert.Contains("copy-to-folder", result.StdOut);
        Assert.Contains("copy-to-file", result.StdOut);
    }

    [Fact]
    public async Task Version_UsesSharedSampleVersion()
    {
        var result = await TigerCliAppTestHost
            .For(FileCopyApp.Create())
            .WithArgs("--version")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)FileCopyExitCode.Ok, result.ExitCode);
        Assert.Contains("File Copy version 0.9.1", result.StdOut);
    }

    [Fact]
    public async Task CommandMenu_SelectThenCancelFilePrompt_MapsToCancelled()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);  // choose copy-to-folder
        shell.Terminal.EnqueueKey(ConsoleKey.Escape); // cancel source-file prompt

        var result = await RunCapturedAsync(FileCopyApp.Create(), [], shell);

        Assert.Equal((int)FileCopyExitCode.Cancelled, result.ExitCode);
        Assert.Contains("Cancelled", result.Stderr);
        Assert.True(shell.Terminal.ReadCount >= 2);
    }

    [Fact]
    public void Settings_UseExpectedFileAndFolderPromptAttributes()
    {
        var toFolderSource = typeof(CopyToFolderSettings).GetProperty(nameof(CopyToFolderSettings.Source))!;
        var folderDestination = typeof(CopyToFolderSettings).GetProperty(nameof(CopyToFolderSettings.DestinationFolder))!;
        var toFileSource = typeof(CopyToFileSettings).GetProperty(nameof(CopyToFileSettings.Source))!;
        var fileDestination = typeof(CopyToFileSettings).GetProperty(nameof(CopyToFileSettings.DestinationFile))!;

        Assert.Equal("*.*", toFolderSource.GetCustomAttribute<TigerCliFileOpenAttribute>()?.Filter);
        Assert.NotNull(folderDestination.GetCustomAttribute<TigerCliFolderSelectAttribute>());
        Assert.Equal("*.*", toFileSource.GetCustomAttribute<TigerCliFileOpenAttribute>()?.Filter);
        var save = fileDestination.GetCustomAttribute<TigerCliFileSaveAttribute>();
        Assert.Equal("*.*", save?.Filter);
        Assert.Equal(TigerCliFileOverwrite.Prompt, save?.Overwrite);
        Assert.Equal(nameof(CopyToFileSettings.Force), save?.OverwriteWhenOption);
    }

    private static Task<TigerCliAppRunResult> RunAsync(params string[] args) =>
        TigerCliAppTestHost
            .For(FileCopyApp.Create())
            .WithArgs(args)
            .RunAsync(TestContext.Current.CancellationToken);

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args,
        TestShell shell)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args, shell, ct: TestContext.Current.CancellationToken);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
