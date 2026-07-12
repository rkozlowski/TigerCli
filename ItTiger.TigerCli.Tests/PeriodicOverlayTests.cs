using System.Reflection;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Slice 1: the generic periodic-overlay mechanism. A control declares an <see cref="InlineActivityOverlay"/>
/// backed by a <see cref="TuiTicker"/>; the dialog adds it once through the normal overlay system, and the
/// semi-interactive modal loop advances it on the (manual) clock, re-rendering only when a frame changes.
/// Nothing here is folder-picker specific.
/// </summary>
public sealed class PeriodicOverlayTests : TestBase
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    // Bracketed braille frames so the rendered top border visibly carries the spinner.
    private static readonly string[] Frames = ["[\u2816]", "[\u2832]", "[\u2834]", "[\u2826]"];

    // A minimal control that exposes one spinner overlay on the top frame (or none, to prove idle dialogs
    // do not re-render). The spinner sits just inside the top-left corner: ╔[⠖]══╗.
    private sealed class SpinnerProbeControl : InlineControlBase
    {
        private readonly IReadOnlyList<InlineActivityOverlay> _overlays;

        public SpinnerProbeControl(ICliAppShell shell, SpinnerTicker? ticker = null, bool withOverlay = true, int maxLength = 3)
            : base(shell)
        {
            Ticker = ticker ?? new SpinnerTicker(Interval, Frames);
            _overlays = withOverlay
                ? new[]
                {
                    new InlineActivityOverlay
                    {
                        Area = InlineDialogArea.TopFrame,
                        ColumnOffset = 1,
                        MaxLength = maxLength,
                        Ticker = Ticker,
                    }
                }
                : Array.Empty<InlineActivityOverlay>();
        }

        public SpinnerTicker Ticker { get; }

        public override object? Payload => null;

        // Leave keys unhandled so the dialog's Enter/Escape fallback drives the result.
        public override InlineKeyResult HandleKey(KeyEvent key) => InlineKeyResult.NotHandled;

        public override IReadOnlyList<InlineActivityOverlay> GetActivityOverlays() => _overlays;

        public override CliGrid ToGrid()
        {
            var g = ToGrid(1, 1);
            g.Set(0, 0, "content");
            return g;
        }
    }

    private static IReadOnlyList<CliOverlay> Overlays(CliGrid grid)
    {
        var field = typeof(CliGrid).GetField("_overlays", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CliGrid._overlays not found.");
        return ((IEnumerable<CliOverlay>)field.GetValue(grid)!).ToList();
    }

    // ------------------------------------------------------------------
    // 1. A periodic overlay is added structurally exactly once.
    // ------------------------------------------------------------------

    [Fact]
    public void PeriodicOverlay_IsAddedStructurallyOnce()
    {
        var shell = new TestShell();
        var control = new SpinnerProbeControl(shell);
        var dialog = new InlineDialog(shell, title: null, control);

        var first = dialog.ToGrid();
        var overlay = Assert.Single(Overlays(first));
        Assert.Equal(CliOrientation.Horizontal, overlay.Orientation);
        Assert.Equal(new CliPoint(1, 0), overlay.Start); // TopFrame column 0 + offset 1, top row
        // The strip covers up to MaxLength grid cells, clamped to the TopFrame area end
        // (min(MaxLength 3, span 5 − offset 1) = 3). MaxLength caps the content.
        Assert.Equal(3, overlay.LogicalLength);

        // Re-querying the grid reuses the cache and never re-adds the overlay.
        var second = dialog.ToGrid();
        Assert.Same(first, second);
        Assert.Single(Overlays(second));
    }

    // ------------------------------------------------------------------
    // 8. The cached dialog grid is reused; animation does not rebuild it.
    // ------------------------------------------------------------------

    [Fact]
    public void Animation_ReusesCachedGrid_WithoutStructuralRebuild()
    {
        var shell = new TestShell();
        var control = new SpinnerProbeControl(shell);
        var dialog = new InlineDialog(shell, title: null, control);

        var grid1 = dialog.ToGrid();
        Assert.Contains(Frames[0], string.Join("\n", TigerConsole.RenderGridToLines(grid1)));

        var t0 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(dialog.AdvanceAnimations(t0));            // first call only captures the baseline
        Assert.True(dialog.AdvanceAnimations(t0 + Interval));  // one interval → frame advanced

        var grid2 = dialog.ToGrid();
        Assert.Same(grid1, grid2);                             // same cached instance: no rebuild
        Assert.Single(Overlays(grid2));
        // Re-rendering the SAME grid now shows the next frame, because the renderer reads the live ticker.
        Assert.Contains(Frames[1], string.Join("\n", TigerConsole.RenderGridToLines(grid2)));
    }

    // ------------------------------------------------------------------
    // MaxLength is a content contract: shorter content renders in full, wider content fails loudly.
    // ------------------------------------------------------------------

    [Fact]
    public void ContentWithinMaxLength_RendersInFull_EvenWhenWiderThanOneCell()
    {
        var shell = new TestShell();
        // A Snake-style two-column frame, bracketed to 4 cells — well within a 10-cell contract.
        var ticker = new SpinnerTicker(Interval, ["[⣎ ]"]);
        var control = new SpinnerProbeControl(shell, ticker, maxLength: 10);
        var dialog = new InlineDialog(shell, title: null, control);

        var rendered = string.Join("\n", TigerConsole.RenderGridToLines(dialog.ToGrid()));
        Assert.Contains("[⣎ ]", rendered); // full frame, including the closing bracket
    }

    [Fact]
    public void ContentWiderThanMaxLength_ThrowsInsteadOfRenderingOrHiding()
    {
        var shell = new TestShell();
        var ticker = new SpinnerTicker(Interval, ["WIDE!"]); // 5 cells against a 3-cell contract
        var control = new SpinnerProbeControl(shell, ticker, maxLength: 3);
        var dialog = new InlineDialog(shell, title: null, control);

        var ex = Assert.Throws<TigerCliException>(() => TigerConsole.RenderGridToLines(dialog.ToGrid()));
        Assert.Contains("MaxLength", ex.Message);
    }

    [Fact]
    public void MaxLengthBelowOne_IsRejectedAtToGrid()
    {
        var shell = new TestShell();
        var control = new SpinnerProbeControl(shell, maxLength: 0);
        var dialog = new InlineDialog(shell, title: null, control);

        var ex = Assert.Throws<TigerCliException>(() => dialog.ToGrid());
        Assert.Contains("MaxLength", ex.Message);
    }

    // ------------------------------------------------------------------
    // 6. A dialog with no periodic overlays does not re-render as time advances.
    // ------------------------------------------------------------------

    [Fact]
    public async Task DialogWithoutPeriodicOverlay_DoesNotReRenderOnTimeAdvance()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(useManualClock: true);
        var control = new SpinnerProbeControl(shell, withOverlay: false);
        var dialog = new InlineDialog(shell, title: null, control);

        var modal = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(5), ct);

        // Advancing virtual time must not provoke a render when nothing is animated.
        shell.AdvanceTime(TimeSpan.FromSeconds(5));

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var result = await modal.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
        Assert.Equal(1, shell.Terminal.RenderCount); // only the initial render
    }

    // ------------------------------------------------------------------
    // 2 + 3 + 4. Advancing by the interval re-renders; frames advance in order and wrap.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AdvancingTime_ReRendersAndCyclesFramesInOrder_AndWraps()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(useManualClock: true);
        var control = new SpinnerProbeControl(shell);
        var dialog = new InlineDialog(shell, title: null, control);

        var modal = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(5), ct);
        Assert.Contains(Frames[0], shell.Terminal.LastRenderedText); // initial frame

        // One full cycle plus a wrap back to frame 0: each interval advance yields exactly one render.
        var expected = new[] { Frames[1], Frames[2], Frames[3], Frames[0] };
        for (int i = 0; i < expected.Length; i++)
        {
            shell.AdvanceTime(Interval);
            await shell.Terminal.WaitForRenderCountAsync(i + 2, TimeSpan.FromSeconds(5), ct);
            Assert.Contains(expected[i], shell.Terminal.LastRenderedText);
        }

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await modal.WaitAsync(TimeSpan.FromSeconds(5), ct);

        // Five renders total: the initial one plus four interval advances. No spurious renders.
        Assert.Equal(5, shell.Terminal.RenderCount);
    }

    // ------------------------------------------------------------------
    // 5. No re-render happens before the interval elapses.
    // ------------------------------------------------------------------

    [Fact]
    public async Task NoReRender_BeforeIntervalElapses()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(useManualClock: true);
        var control = new SpinnerProbeControl(shell);
        var dialog = new InlineDialog(shell, title: null, control);

        var modal = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(5), ct);

        // A sub-interval advance must not flip a frame or render…
        shell.AdvanceTime(TimeSpan.FromMilliseconds(200));
        // …and completing the interval produces exactly one render (the 200ms step did not add its own).
        shell.AdvanceTime(TimeSpan.FromMilliseconds(300));
        await shell.Terminal.WaitForRenderCountAsync(2, TimeSpan.FromSeconds(5), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await modal.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Equal(2, shell.Terminal.RenderCount); // initial + a single interval flip, nothing in between
        Assert.Contains(Frames[1], shell.Terminal.LastRenderedText);
    }

    // ------------------------------------------------------------------
    // 7. Keyboard input still works while a periodic overlay is present.
    // ------------------------------------------------------------------

    [Fact]
    public async Task KeyboardInput_StillExits_WhilePeriodicOverlayActive()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(useManualClock: true);
        var control = new SpinnerProbeControl(shell);
        var dialog = new InlineDialog(shell, title: null, control);

        var modal = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(5), ct);

        // Animate a couple of frames, then confirm a key still drives the dialog to completion.
        shell.AdvanceTime(Interval);
        await shell.Terminal.WaitForRenderCountAsync(2, TimeSpan.FromSeconds(5), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var result = await modal.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }
}
