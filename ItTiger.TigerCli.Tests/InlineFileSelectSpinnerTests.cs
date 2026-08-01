using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Verifies that file-open and file-save navigation use the modal loading spinner while the shared
/// folder browser and local file enumeration refresh the entry list.
/// </summary>
public sealed class InlineFileSelectSpinnerTests : TestBase
{
    private static readonly TimeSpan SpinnerInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private const string SecondFrame = "[\u2832]";

    private sealed class GatedFolderBrowser : IFolderBrowser
    {
        private readonly FileSystemFolderBrowser _inner = new();
        private TaskCompletionSource? _nextLoadGate;

        public string? RootLocation => _inner.RootLocation;

        public TaskCompletionSource ArmNextLoad()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _nextLoadGate = gate;
            return gate;
        }

        public IReadOnlyList<FolderEntry> GetEntries(string? location)
        {
            var gate = Interlocked.Exchange(ref _nextLoadGate, null);
            gate?.Task.GetAwaiter().GetResult();
            return _inner.GetEntries(location);
        }

        public bool TryGetParent(string? location, out string? parent) =>
            _inner.TryGetParent(location, out parent);

        public (string? location, string? highlightPath) ResolveInitial(string? initialPath) =>
            _inner.ResolveInitial(initialPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpeningSlowFolder_FileOpenAndSave_ShowSpinnerUntilEntriesAreApplied(bool save)
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("tigercli_file_spinner_");
        TaskCompletionSource? gate = null;
        TestShell? shell = null;
        Task<DialogResult>? modal = null;
        try
        {
            var child = Directory.CreateDirectory(Path.Combine(root.FullName, "child"));
            File.WriteAllText(Path.Combine(child.FullName, "inside.txt"), "content");

            var browser = new GatedFolderBrowser();
            shell = new TestShell(useManualClock: true);
            var control = new InlineFileSelect(
                shell,
                browser,
                save,
                root.FullName,
                defaultFileName: save ? "copy.txt" : null,
                filter: "*");
            modal = shell.RunModalAsync(new InlineDialog(shell, "Pick file", control), ct);

            await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
            Assert.DoesNotContain("╔[", shell.Terminal.LastRenderedText);

            gate = browser.ArmNextLoad();
            shell.Terminal.EnqueueKey(ConsoleKey.RightArrow);
            await shell.Terminal.WaitForRenderCountAsync(2, Timeout, ct);
            Assert.Contains("╔[", shell.Terminal.LastRenderedText);
            Assert.DoesNotContain("inside.txt", shell.Terminal.LastRenderedText);

            shell.AdvanceTime(SpinnerInterval);
            await shell.Terminal.WaitForRenderCountAsync(3, Timeout, ct);
            Assert.Contains(SecondFrame, shell.Terminal.LastRenderedText);

            var renderCount = shell.Terminal.RenderCount;
            gate.TrySetResult();
            await shell.Terminal.WaitForRenderCountAsync(renderCount + 1, Timeout, ct);

            Assert.Contains("inside.txt", shell.Terminal.LastRenderedText);
            Assert.DoesNotContain("╔[", shell.Terminal.LastRenderedText);

            shell.Terminal.EnqueueKey(ConsoleKey.Escape);
            var result = await modal.WaitAsync(Timeout, ct);
            Assert.Equal(DialogResultKind.Cancel, result.Kind);
        }
        finally
        {
            gate?.TrySetResult();
            if (shell is not null && modal is { IsCompleted: false })
            {
                shell.Terminal.EnqueueKey(ConsoleKey.Escape);
                await modal.WaitAsync(Timeout, CancellationToken.None);
            }

            root.Delete(recursive: true);
        }
    }
}
