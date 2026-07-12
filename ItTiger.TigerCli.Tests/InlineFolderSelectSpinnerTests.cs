using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Slice 2: <see cref="InlineFolderSelect"/> wires the generic periodic-overlay mechanism to a loading
/// spinner and loads folders without blocking the modal loop. These tests drive the real modal loop
/// against a browser whose listing can be held open, so loading state is deterministic without real
/// sleeps. The braille frame asserts use <c>\u</c> escapes; <c>"╔["</c> marks the spinner sitting on the
/// top frame next to the top-left corner.
/// </summary>
public sealed class InlineFolderSelectSpinnerTests : TestBase
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);
    private const string Frame2 = "[\u2832]"; // second spinner frame
    private const string Frame3 = "[\u2834]"; // third spinner frame
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // A small in-memory tree whose GetEntries can be held open ("gated") to simulate a slow load. The
    // gate is one-shot and applies only to the next GetEntries call, so construction's initial (ungated)
    // load returns immediately.
    private sealed class GatedTreeBrowser : IFolderBrowser
    {
        private readonly Dictionary<string, string[]> _tree = new(StringComparer.OrdinalIgnoreCase)
        {
            [@"D:\"] = [@"D:\Media"],
            [@"D:\Media"] = [@"D:\Media\Movies", @"D:\Media\Music"],
            [@"D:\Media\Movies"] = [],
            [@"D:\Media\Music"] = [@"D:\Media\Music\Jazz"],
            [@"D:\Media\Music\Jazz"] = [],
        };

        private readonly bool _throwOnGatedLoad;
        private volatile TaskCompletionSource? _gate;

        public GatedTreeBrowser(bool throwOnGatedLoad = false) => _throwOnGatedLoad = throwOnGatedLoad;

        /// <summary>Holds the next <see cref="GetEntries"/> call open until the returned source is completed.</summary>
        public TaskCompletionSource ArmNextLoad()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _gate = gate;
            return gate;
        }

        public string? RootLocation => null;

        public IReadOnlyList<FolderEntry> GetEntries(string? location)
        {
            var gate = _gate;
            if (gate is not null)
            {
                _gate = null;                          // one-shot
                gate.Task.GetAwaiter().GetResult();    // block this background load until the test releases it
                if (_throwOnGatedLoad)
                    throw new IOException("simulated load failure");
            }

            var loc = location ?? @"D:\";
            if (!_tree.TryGetValue(loc, out var children))
                return Array.Empty<FolderEntry>();

            return children
                .Select(p => new FolderEntry(Leaf(p), p, _tree.TryGetValue(p, out var c) && c.Length > 0))
                .ToList();
        }

        public bool TryGetParent(string? location, out string? parent)
        {
            parent = null;
            if (location is null)
                return false;
            if (string.Equals(location, @"D:\", StringComparison.OrdinalIgnoreCase))
            {
                parent = null; // drive root → drive list
                return true;
            }

            int idx = location.TrimEnd('\\').LastIndexOf('\\');
            parent = idx <= 1 ? @"D:\" : location[..idx];
            return true;
        }

        public (string? location, string? highlightPath) ResolveInitial(string? initialPath)
        {
            if (string.IsNullOrWhiteSpace(initialPath) || !_tree.ContainsKey(initialPath))
                return (null, null);

            return TryGetParent(initialPath, out var parent) ? (parent, initialPath) : (initialPath, null);
        }

        private static string Leaf(string p)
        {
            var t = p.TrimEnd('\\');
            int i = t.LastIndexOf('\\');
            return i < 0 ? t : t[(i + 1)..];
        }
    }

    private static (TestShell shell, InlineFolderSelect control, Task<DialogResult> modal) StartPicker(
        GatedTreeBrowser browser, CancellationToken ct)
    {
        var shell = new TestShell(useManualClock: true);
        var control = new InlineFolderSelect(shell, browser, @"D:\Media\Music"); // highlights Music (openable)
        var dialog = new InlineDialog(shell, "Pick folder", control);
        var modal = shell.RunModalAsync(dialog, ct);
        return (shell, control, modal);
    }

    // ------------------------------------------------------------------
    // 1 + 2 + 3 + 4 + 5 + 8: open a slow folder → spinner appears on the top frame, frames advance while
    // pending, completion applies entries in a single loop render, and the spinner disappears.
    // ------------------------------------------------------------------

    [Fact]
    public async Task OpeningSlowFolder_ShowsSpinner_ThenAppliesEntries_AndHidesSpinner()
    {
        var ct = TestContext.Current.CancellationToken;
        var browser = new GatedTreeBrowser();
        var (shell, _, modal) = StartPicker(browser, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);
        Assert.DoesNotContain("╔[", shell.Terminal.LastRenderedText); // no spinner before a load

        // Open the highlighted folder; its listing blocks in the background.
        var gate = browser.ArmNextLoad();
        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow);
        await shell.Terminal.WaitForRenderCountAsync(2, Timeout, ct); // post-key render

        // (1,2) Loading started; the spinner overlay sits on the top frame next to the corner.
        Assert.Contains("╔[", shell.Terminal.LastRenderedText);

        // (3) Frames advance while the load is still pending.
        shell.AdvanceTime(Interval);
        await shell.Terminal.WaitForRenderCountAsync(3, Timeout, ct);
        Assert.Contains(Frame2, shell.Terminal.LastRenderedText);

        shell.AdvanceTime(Interval);
        await shell.Terminal.WaitForRenderCountAsync(4, Timeout, ct);
        Assert.Contains(Frame3, shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("Jazz", shell.Terminal.LastRenderedText); // not applied yet

        // (4,5,8) Releasing the load applies entries in exactly one loop render; the spinner disappears.
        int before = shell.Terminal.RenderCount;
        gate.TrySetResult();
        await shell.Terminal.WaitForRenderCountAsync(before + 1, Timeout, ct);

        Assert.Contains("Jazz", shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("╔[", shell.Terminal.LastRenderedText);
        Assert.Equal(before + 1, shell.Terminal.RenderCount); // single completion render — no background renders

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await modal.WaitAsync(Timeout, ct);
    }

    // ------------------------------------------------------------------
    // 6: a failing load hides the spinner and falls back to the existing empty/error state.
    // ------------------------------------------------------------------

    [Fact]
    public async Task FolderLoadFailure_HidesSpinner_AndShowsEmptyState()
    {
        var ct = TestContext.Current.CancellationToken;
        var browser = new GatedTreeBrowser(throwOnGatedLoad: true);
        var (shell, _, modal) = StartPicker(browser, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);

        var gate = browser.ArmNextLoad();
        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow);
        await shell.Terminal.WaitForRenderCountAsync(2, Timeout, ct);
        Assert.Contains("╔[", shell.Terminal.LastRenderedText); // spinner while pending

        int before = shell.Terminal.RenderCount;
        gate.TrySetResult(); // GetEntries throws → treated as an empty listing
        await shell.Terminal.WaitForRenderCountAsync(before + 1, Timeout, ct);

        Assert.DoesNotContain("╔[", shell.Terminal.LastRenderedText);            // spinner gone
        Assert.Contains("No folders available", shell.Terminal.LastRenderedText); // existing empty/error state

        // Dialog stays usable: Escape still cancels.
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var result = await modal.WaitAsync(Timeout, ct);
        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }

    // ------------------------------------------------------------------
    // 7: closing the modal while a load is pending is safe — no apply to a closed control, no throw.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ClosingModal_WhileLoadPending_IsSafe()
    {
        var ct = TestContext.Current.CancellationToken;
        var browser = new GatedTreeBrowser();
        var (shell, _, modal) = StartPicker(browser, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, Timeout, ct);

        var gate = browser.ArmNextLoad();
        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow);
        await shell.Terminal.WaitForRenderCountAsync(2, Timeout, ct);
        Assert.Contains("╔[", shell.Terminal.LastRenderedText);

        // Close while the listing is still blocked.
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var result = await modal.WaitAsync(Timeout, ct);
        Assert.Equal(DialogResultKind.Cancel, result.Kind);

        // Releasing the now-abandoned load runs the background task to completion; the closed-control
        // guard discards the result rather than mutating the control or throwing.
        gate.TrySetResult();
    }

    // ------------------------------------------------------------------
    // 9 (companion): outside a modal session, navigation stays synchronous (no spinner, no loop needed).
    // ------------------------------------------------------------------

    [Fact]
    public void Navigation_OutsideModalSession_RemainsSynchronous()
    {
        var shell = new TestShell();
        var browser = new GatedTreeBrowser();
        var control = new InlineFolderSelect(shell, browser, @"D:\Media\Music");

        // No modal loop / OnModalOpened: opening applies immediately on the calling thread.
        Assert.True(control.HandleKey(new KeyEvent(ConsoleKey.RightArrow, ConsoleModifiers.None)).IsHandled);
        Assert.Equal(@"D:\Media\Music\Jazz", control.Payload);
    }
}
