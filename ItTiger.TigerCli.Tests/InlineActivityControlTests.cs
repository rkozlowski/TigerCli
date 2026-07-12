using System.Globalization;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Activity;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Operation lifecycle, rendering, and cancellation behaviour for the rich activity dialog, driven
/// through the real semi-interactive modal loop (<see cref="TestShell"/> + <see cref="TestTerminal"/>).
/// A caller-controlled gate stands in for the background work so completion/cancellation are
/// deterministic.
/// </summary>
public sealed class InlineActivityControlTests
{
    // The expected default frames are owned by SpinnerTicker; the test reads them rather than copying.
    private static readonly IReadOnlyList<string> DefaultFrames = SpinnerTicker.Frames(SpinnerFrameSet.Default);

    private static TestShell NewShell() => new(culture: CultureInfo.GetCultureInfo("en-US"));

    private static ActivityDialogSpec ProgressSpec() =>
        ActivityDialogSpec.Create()
            .AddColumn(width: 8, align: CliTextAlignment.Right)
            .AddColumn(sizing: CliColumnSizing.Star)
            .AddColumn(width: 24, align: CliTextAlignment.Left)
            .AddRow(null, r => r.Cell(0, span: 3).Text("Working..."))
            .AddRow("files", r => r
                .Cell(0).Text("Files:")
                .Cell(1).ProgressBar(valueIndex: 0, maxValueIndex: 1)
                .Cell(2).Text("[Blue]{2,5:F1}%[/] [Green]{0}/{1}[/]")
                .Values(0, 0, 0.0))
            .Build();

    private static async Task WaitForTextAsync(TestShell shell, string needle, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (shell.Terminal.LastRenderedText.Contains(needle, StringComparison.Ordinal))
                return;
            await Task.Delay(10, ct).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for '{needle}'. Last render:\n{shell.Terminal.LastRenderedText}");
    }

    // ── Operation completion ─────────────────────────────────────────────────

    [Fact]
    public async Task Operation_Completes_WithoutKeypress_ReturnsValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 99; }, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        gate.SetResult(); // no key is ever enqueued

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);

        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(99, result.Value);
    }

    [Fact]
    public async Task Operation_Throws_MapsToFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();

        var run = TigerTui.RunActivityAsync<int>(shell, "Activity", ProgressSpec(),
            (_, _) => throw new InvalidOperationException("boom"), ct: ct);

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);

        Assert.Equal(ActivityOutcome.Failed, result.Outcome);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Equal("boom", result.Exception!.Message);
    }

    [Fact]
    public async Task SetValues_WrongCount_FaultsOperation()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();

        var run = TigerTui.RunActivityAsync<int>(shell, "Activity", ProgressSpec(),
            (context, _) =>
            {
                context.SetValues("files", 1, 2); // row declares 3 values
                return Task.FromResult(0);
            }, ct: ct);

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);

        Assert.Equal(ActivityOutcome.Failed, result.Outcome);
        Assert.IsType<ArgumentException>(result.Exception);
    }

    // ── Value updates / rendering ────────────────────────────────────────────

    [Fact]
    public async Task Operation_UpdatesValues_AppliedOnLoopThread_AndRendered()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (context, _) =>
            {
                context.SetValues("files", 36, 73, 49.3);
                await gate.Task.ConfigureAwait(false);
                return 0;
            }, ct: ct);

        await WaitForTextAsync(shell, "36/73", ct);
        var text = shell.Terminal.LastRenderedText;

        // Progress bar (overlay over the star column) fills the resolved width: filled + track glyphs.
        Assert.Contains(ConsoleSymbol.FullBlock, text);
        Assert.Contains(ConsoleSymbol.ShadeLight, text);
        Assert.Contains("49.3", text); // numeric format {2,5:F1}

        gate.SetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
    }

    [Fact]
    public async Task ProgressBar_PredefinedStyle_RendersSelectedGlyphs()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // A Square-style bar at 50%: the resolved star width fills with ■ (filled) and □ (track) instead of
        // the default █/░ glyphs. The default track glyph (░) is used by nothing else, so its absence proves
        // the chosen style is applied end-to-end.
        var spec = ActivityDialogSpec.Create()
            .AddColumn(sizing: CliColumnSizing.Star)
            .AddRow("p", r => r.Cell(0).ProgressBar(valueIndex: 0, maxValue: 100, style: ProgressBarStyle.Square).Values(50.0))
            .Build();

        var run = TigerTui.RunActivityAsync(shell, "Activity", spec,
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; }, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        var text = shell.Terminal.LastRenderedText;
        Assert.Contains(ConsoleSymbol.Square, text);          // ■ filled
        Assert.Contains(ConsoleSymbol.WhiteSquare, text);     // □ track
        Assert.DoesNotContain(ConsoleSymbol.ShadeLight, text); // default track glyph not used

        gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    [Fact]
    public async Task ProgressBar_Brackets_ComposeWithAnyStyle()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Brackets are a separate decoration that composes with a non-default glyph style: a Square bar with
        // Brackets caps must render both the [ ] end caps and the ■/□ glyphs of the chosen style.
        var spec = ActivityDialogSpec.Create()
            .AddColumn(sizing: CliColumnSizing.Star)
            .AddRow("p", r => r.Cell(0)
                .ProgressBar(valueIndex: 0, maxValue: 100, style: ProgressBarStyle.Square, caps: ProgressBarCaps.Brackets)
                .Values(50.0))
            .Build();

        var run = TigerTui.RunActivityAsync(shell, "Activity", spec,
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; }, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        var text = shell.Terminal.LastRenderedText;
        Assert.Contains('[', text);                       // left cap
        Assert.Contains(']', text);                       // right cap
        Assert.Contains(ConsoleSymbol.Square, text);      // ■ filled (style still applied)
        Assert.Contains(ConsoleSymbol.WhiteSquare, text); // □ track

        gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    [Fact]
    public async Task Text_LongValue_TruncatesThroughCliGrid()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 10)
            .AddRow("r", r => r.Cell(0).Text("{0}").Values(string.Empty))
            .Build();

        var run = TigerTui.RunActivityAsync(shell, "Activity", spec,
            async (context, _) =>
            {
                context.SetValue("r", 0, "ABCDEFGHIJKLMNOPQRSTUVWXYZ");
                await gate.Task.ConfigureAwait(false);
                return 0;
            }, ct: ct);

        await WaitForTextAsync(shell, "…", ct);
        Assert.DoesNotContain("ABCDEFGHIJKLMNOPQRSTUVWXYZ", shell.Terminal.LastRenderedText);

        gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    [Fact]
    public async Task Text_PlaceholderValue_CannotInjectMarkup()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var spec = ActivityDialogSpec.Create()
            .AddColumn(width: 40)
            .AddRow("r", r => r.Cell(0).Text("File: [Accent]{0}[/]").Values(string.Empty))
            .Build();

        var run = TigerTui.RunActivityAsync(shell, "Activity", spec,
            async (context, _) =>
            {
                context.SetValue("r", 0, "[Red]evil[/]");
                await gate.Task.ConfigureAwait(false);
                return 0;
            }, ct: ct);

        await WaitForTextAsync(shell, "evil", ct);
        // The value's brackets render literally (escaped); they are not interpreted as markup.
        Assert.Contains("[Red]evil[/]", shell.Terminal.LastRenderedText);

        gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    [Fact]
    public async Task Spinner_Animates_WhileRunning_UsesDefaultFrameSet()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // No spinner spec supplied -> the dialog uses SpinnerTicker's default frame set.
        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; }, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        Assert.True(DefaultFrames.Any(f => shell.Terminal.LastRenderedText.Contains(f, StringComparison.Ordinal)),
            $"No default spinner frame in render:\n{shell.Terminal.LastRenderedText}");

        gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    [Fact]
    public async Task Spinner_PredefinedFrameSet_RendersThatSet()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Dots8 shares no frame with the default set, so its appearance proves the chosen set is used.
        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; },
            spinner: ActivitySpinnerSpec.FromFrameSet(SpinnerFrameSet.Dots8), ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        var dots8 = SpinnerTicker.Frames(SpinnerFrameSet.Dots8);
        Assert.True(dots8.Any(f => shell.Terminal.LastRenderedText.Contains(f, StringComparison.Ordinal)),
            $"No Dots8 spinner frame in render:\n{shell.Terminal.LastRenderedText}");
        Assert.False(DefaultFrames.Any(f => shell.Terminal.LastRenderedText.Contains(f, StringComparison.Ordinal)),
            "Default frames must not appear when a different frame set is selected.");

        gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    [Fact]
    public async Task Spinner_ExternalTicker_RendersItsContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // A caller-created, non-SpinnerTicker ticker: the dialog renders its raw content ("[X]" is well
        // within the overlay's MaxLength contract) without owning its frames or lifecycle.
        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; },
            spinner: ActivitySpinnerSpec.FromTicker(new FixedTicker("X")), ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        Assert.Contains("[X]", shell.Terminal.LastRenderedText);

        gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    [Fact]
    public async Task Spinner_Snake_TwoColumnFrames_RenderFullyBracketed_EveryFrame()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(culture: CultureInfo.GetCultureInfo("en-US"), useManualClock: true);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = SpinnerTicker.Frames(SpinnerFrameSet.Snake);

        // Snake frames are two columns wide, so "[frame]" occupies 4 cells — the overlay's MaxLength
        // contract must render them whole (closing bracket included), never clipped or hidden. The
        // manual clock steps the modal loop's animation pass one frame per interval, so render count
        // maps 1:1 to spinner frames.
        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; },
            spinner: ActivitySpinnerSpec.FromFrameSet(SpinnerFrameSet.Snake), ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(2), ct);
        Assert.Contains($"[{frames[0]}]", shell.Terminal.LastRenderedText);

        for (int i = 1; i < frames.Count; i++)
        {
            shell.AdvanceTime(SpinnerTicker.DefaultInterval);
            await shell.Terminal.WaitForRenderCountAsync(i + 1, TimeSpan.FromSeconds(2), ct);
            Assert.Contains($"[{frames[i]}]", shell.Terminal.LastRenderedText);
        }

        gate.SetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
    }

    [Fact]
    public async Task Spinner_FrameWiderThanMaxLength_FailsLoudly()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // A 9-cell custom frame formats to "[xxxxxxxxx]" = 11 cells, exceeding the activity overlay's
        // 10-cell MaxLength contract. The dialog must throw, not truncate or silently hide the spinner.
        var oversized = new SpinnerTicker(TimeSpan.FromMilliseconds(500), [new string('x', 9)]);
        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, token) => { await gate.Task.WaitAsync(token).ConfigureAwait(false); return 0; },
            spinner: ActivitySpinnerSpec.FromTicker(oversized), ct: ct);

        var ex = await Assert.ThrowsAsync<TigerCliException>(() => run.WaitAsync(TimeSpan.FromSeconds(2), ct));
        Assert.Contains("MaxLength", ex.Message);
    }

    // A minimal external ticker (not a SpinnerTicker) that always shows one fixed frame.
    private sealed class FixedTicker : TuiTicker
    {
        private readonly string _content;
        public FixedTicker(string content) : base(TimeSpan.FromMilliseconds(500)) => _content = content;
        public override string CurrentContent => _content;
        protected override bool AdvanceFrame() => false;
    }

    // ── Cancellation / confirmation ──────────────────────────────────────────

    [Fact]
    public async Task Cancel_RaisesConfirmation_DoesNotCancelOperationYet()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken opToken = default;

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, token) => { opToken = token; await gate.Task.ConfigureAwait(false); return 0; }, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.Escape); // Esc behaves like Cancel -> confirmation
        await WaitForTextAsync(shell, ConfirmText(shell), ct);

        Assert.False(opToken.IsCancellationRequested); // not cancelled while merely confirming
        Assert.False(run.IsCompleted);

        gate.SetResult(); // let the op finish so the modal can close
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    [Fact]
    public async Task Cancel_ConfirmNo_ResumesAndOperationContinues()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 7; }, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await WaitForTextAsync(shell, ConfirmText(shell), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // No is default-focused -> dismiss
        await WaitForTextAsync(shell, "Working...", ct); // original activity view restored
        Assert.False(run.IsCompleted);

        gate.SetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task Cancel_ConfirmYes_SwitchesToCancelling_WaitsForOperation_ThenCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        // The op intentionally ignores its token (awaits a plain gate) so we can observe the
        // "Cancelling…" view and prove the dialog does not report Cancelled until the op stops.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; }, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await WaitForTextAsync(shell, ConfirmText(shell), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow); // No -> Yes
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);     // confirm cancel
        await WaitForTextAsync(shell, CancellingText(shell), ct);
        Assert.False(run.IsCompleted); // still waiting on the operation

        gate.SetResult(); // operation finally stops
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(ActivityOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public async Task Operation_Completes_WhileConfirmationOpen_CompletionWins()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 5; }, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await WaitForTextAsync(shell, ConfirmText(shell), ct);

        gate.SetResult(); // op finishes while the confirmation is still shown
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);

        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(5, result.Value);
    }

    // ── Lifecycle kinds bypass confirmation ──────────────────────────────────

    [Fact]
    public async Task TokenCancel_BypassesConfirmation_MapsToCancelled()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.Cancel();
        var shell = NewShell();

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, token) => { await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false); return 0; },
            timeout: TimeSpan.FromSeconds(10), ct: cts.Token);

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(ActivityOutcome.Cancelled, result.Outcome);
        Assert.Equal(DialogResultKind.TokenCancel, result.DialogResultKind);
    }

    [Fact]
    public async Task Timeout_BypassesConfirmation_MapsToTimedOut()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, token) => { await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false); return 0; },
            timeout: TimeSpan.FromMilliseconds(20), ct: ct);

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);

        Assert.Equal(ActivityOutcome.TimedOut, result.Outcome);
    }

    [Fact]
    public async Task SystemCancel_BypassesConfirmation_MapsToSystemCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        shell.RaiseSystemCancellation();

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, token) => { await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false); return 0; }, ct: ct);

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);

        Assert.Equal(ActivityOutcome.SystemCancelled, result.Outcome);
    }

    // ── Single stop action (Cancel XOR Abort) ────────────────────────────────

    [Fact]
    public async Task CancelMode_ShowsOnlyCancelButton()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Default stop mode is Cancel.
        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; }, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        var text = shell.Terminal.LastRenderedText;

        // A focused stop button (markers) labelled Cancel — and no Abort peer button.
        Assert.Contains(ButtonLabel(shell, abort: false), text);
        Assert.Contains(ConsoleSymbol.MarkerRight.ToString(), text);
        Assert.DoesNotContain(ButtonLabel(shell, abort: true), text);

        gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    [Fact]
    public async Task AbortMode_ShowsOnlyAbortButton()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; },
            stopMode: ActivityStopMode.Abort, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        var text = shell.Terminal.LastRenderedText;

        // A focused stop button (markers) labelled Abort — and "Cancel" appears nowhere (button or hint).
        Assert.Contains(ButtonLabel(shell, abort: true), text);
        Assert.Contains(ConsoleSymbol.MarkerRight.ToString(), text);
        Assert.DoesNotContain(ButtonLabel(shell, abort: false), text);

        gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    [Fact]
    public async Task CancelMode_CancellingState_ShowsNoActionButton()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; }, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        // The running view shows a focused button (markers present).
        Assert.Contains(ConsoleSymbol.MarkerRight.ToString(), shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await WaitForTextAsync(shell, ConfirmText(shell), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow); // No -> Yes
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);     // confirm cancel
        await WaitForTextAsync(shell, CancellingText(shell), ct);

        // Once accepted, the dialog only waits — no action button (no selection markers).
        var text = shell.Terminal.LastRenderedText;
        Assert.DoesNotContain(ConsoleSymbol.MarkerRight.ToString(), text);
        Assert.DoesNotContain(ConsoleSymbol.MarkerLeft.ToString(), text);

        gate.SetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(ActivityOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public async Task AbortMode_AbortingState_ShowsNoActionButton_AndMapsToAborted()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = TigerTui.RunActivityAsync(shell, "Activity", ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; },
            stopMode: ActivityStopMode.Abort, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        Assert.Contains(ConsoleSymbol.MarkerRight.ToString(), shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape); // Esc requests the Abort stop action
        await WaitForTextAsync(shell, AbortConfirmText(shell), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow); // No -> Yes
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);     // confirm abort
        await WaitForTextAsync(shell, AbortingText(shell), ct);

        var text = shell.Terminal.LastRenderedText;
        Assert.DoesNotContain(ConsoleSymbol.MarkerRight.ToString(), text);
        Assert.DoesNotContain(ConsoleSymbol.MarkerLeft.ToString(), text);

        gate.SetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(ActivityOutcome.Aborted, result.Outcome);
        Assert.Equal(DialogResultKind.Abort, result.DialogResultKind);
    }

    // ── Convenience overloads ────────────────────────────────────────────────

    [Fact]
    public async Task SpecWithoutTitle_RunsSpec_Completes()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // No-title overload: delegates to the canonical (title: null) spec overload.
        var run = TigerTui.RunActivityAsync(shell, ProgressSpec(),
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 11; }, ct: ct);

        await WaitForTextAsync(shell, "Working...", ct); // the spec's static row still renders

        gate.SetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(11, result.Value);
    }

    [Fact]
    public async Task SimpleMessage_WithTitle_RendersTitleAndMessage_Completes()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Static-message overload builds a one-row, one-column, left-aligned spec internally.
        var run = TigerTui.RunActivityAsync(shell, "Importing", "Crunching numbers",
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 7; }, ct: ct);

        await WaitForTextAsync(shell, "Crunching numbers", ct);
        var text = shell.Terminal.LastRenderedText;
        Assert.Contains("Importing", text);          // title row
        Assert.Contains("Crunching numbers", text);  // message cell

        gate.SetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task SimpleMessage_WithoutTitle_RendersMessage_Completes()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = TigerTui.RunActivityAsync(shell, "Please wait",
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 3; }, ct: ct);

        await WaitForTextAsync(shell, "Please wait", ct);

        gate.SetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(3, result.Value);
    }

    [Fact]
    public async Task SimpleMessage_ValueLess_WithTitle_Completes()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Value-less message overload: Value is true on completion.
        var run = TigerTui.RunActivityAsync(shell, "Saving", "Writing files",
            async (_, _) => { await gate.Task.ConfigureAwait(false); }, ct: ct);

        await WaitForTextAsync(shell, "Writing files", ct);
        Assert.Contains("Saving", shell.Terminal.LastRenderedText);

        gate.SetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task SimpleMessage_ValueLess_WithoutTitle_Completes()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = TigerTui.RunActivityAsync(shell, "Cleaning up",
            async (_, _) => { await gate.Task.ConfigureAwait(false); }, ct: ct);

        await WaitForTextAsync(shell, "Cleaning up", ct);

        gate.SetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task SimpleMessage_StopMode_ShowsSelectedStopButton()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The convenience overloads keep the existing parameters: stopMode flows through unchanged.
        var run = TigerTui.RunActivityAsync(shell, "Working away",
            async (_, _) => { await gate.Task.ConfigureAwait(false); return 0; },
            stopMode: ActivityStopMode.Abort, ct: ct);

        await WaitForTextAsync(shell, "Working away", ct);
        var text = shell.Terminal.LastRenderedText;
        Assert.Contains(ButtonLabel(shell, abort: true), text);
        Assert.DoesNotContain(ButtonLabel(shell, abort: false), text);

        gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    private static string ConfirmText(TestShell shell) =>
        TigerCliResources.Get("Tui_Confirm_Cancel_Message", shell.Culture);

    private static string AbortConfirmText(TestShell shell) =>
        TigerCliResources.Get("Tui_Confirm_Abort_Message", shell.Culture);

    private static string CancellingText(TestShell shell) =>
        TigerCliResources.Get("Tui_Activity_Cancelling", shell.Culture);

    private static string AbortingText(TestShell shell) =>
        TigerCliResources.Get("Tui_Activity_Aborting", shell.Culture);

    private static string ButtonLabel(TestShell shell, bool abort) =>
        TigerCliResources.Get(abort ? "Tui_Button_Abort" : "Tui_Button_Cancel", shell.Culture);
}
