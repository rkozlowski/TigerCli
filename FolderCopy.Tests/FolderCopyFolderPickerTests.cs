using FolderCopy;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace FolderCopy.Tests;

/// <summary>
/// Semi-interactive tests proving that a missing source/destination is resolved through the inline
/// folder picker (not a text prompt), driven by a <see cref="TestShell"/> and a filesystem browser
/// rooted at a temporary directory.
/// </summary>
/// <remarks>
/// Both folders are resolved through the picker, then the command is steered into a guard that runs
/// <b>before</b> any activity (the source picker selects the same folder for both, tripping the
/// "same folder" guard). This exercises the full prompting path deterministically without entering the
/// scanning/copy activity dialogs — those run on the console singleton, which a test shell cannot
/// intercept (their live UI is validated by manual/smoke testing and the framework's own activity
/// tests; the copy work is covered headlessly in <see cref="FolderCopyAppTests"/>).
/// </remarks>
public sealed class FolderCopyFolderPickerTests
{
    [Fact]
    public async Task MissingFolders_ResolvedThroughFolderPicker()
    {
        using var temp = new TempDir();
        temp.CreateDir("shared");

        var app = FolderCopyApp.Create(new TempFolderBrowser(temp.Path));
        var shell = new TestShell();

        // Source prompt: confirm the highlighted first entry (shared).
        shell.Terminal.EnqueueKey(ConsoleKey.Tab);        // list -> buttons
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);      // OK
        // Destination prompt: confirm the same first entry (shared).
        shell.Terminal.EnqueueKey(ConsoleKey.Tab);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunCapturedAsync(app, [], shell);

        // Reaching the "same folder" guard proves both required folders were supplied by the picker —
        // an unresolved required option would have failed validation before the command body ran. The
        // guard runs before the scanning activity, so the run stays deterministic under a test shell.
        Assert.Equal((int)FolderCopyExitCode.CopyFailed, result.ExitCode);
        Assert.Contains("same folder", result.Stderr);
        Assert.True(shell.Terminal.ReadCount > 0);
    }

    [Fact]
    public async Task MissingSource_Cancelled_FailsCleanly()
    {
        using var temp = new TempDir();
        temp.CreateDir("a_source");

        var app = FolderCopyApp.Create(new TempFolderBrowser(temp.Path));
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape); // cancel the source picker

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal((int)FolderCopyExitCode.Cancelled, result.ExitCode);
        Assert.Contains("Cancelled", result.Stderr);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        ItTiger.TigerCli.Commands.TigerCliApp app, string[] args, TestShell shell)
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

/// <summary>
/// A real-filesystem folder browser rooted at a fixed directory, listing subfolders in a stable
/// alphabetical order so picker navigation is deterministic in tests.
/// </summary>
internal sealed class TempFolderBrowser(string root) : IFolderBrowser
{
    private readonly string _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    public string? RootLocation => _root;

    public IReadOnlyList<FolderEntry> GetEntries(string? location)
    {
        var key = location ?? _root;
        if (!Directory.Exists(key))
            return Array.Empty<FolderEntry>();

        return Directory.GetDirectories(key)
            .OrderBy(d => Path.GetFileName(d), StringComparer.Ordinal)
            .Select(d => new FolderEntry(Path.GetFileName(d), d, Directory.GetDirectories(d).Length > 0))
            .ToList();
    }

    public bool TryGetParent(string? location, out string? parent)
    {
        parent = null;
        if (location is null)
            return false;

        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(location));
        if (string.Equals(current, _root, StringComparison.OrdinalIgnoreCase))
            return false;

        parent = Path.GetDirectoryName(current);
        return parent is not null;
    }

    public (string? location, string? highlightPath) ResolveInitial(string? initialPath)
    {
        if (string.IsNullOrWhiteSpace(initialPath))
            return (_root, null);

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(initialPath));
        if (Directory.Exists(full) && TryGetParent(full, out var parent))
            return (parent, full);

        return (_root, null);
    }
}
