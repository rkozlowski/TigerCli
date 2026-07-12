using System.Globalization;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Generic optional cancel/abort confirmation for <see cref="InlineDialog"/>: opt-in for Cancel, Abort,
/// or both; Yes preserves the originally requested kind; No/Esc resumes the original dialog; and the
/// loop-produced kinds (TokenCancel / Timeout / SystemCancel) bypass confirmation.
/// </summary>
public sealed class InlineDialogConfirmationTests
{
    // Minimal hosted control: 'C' requests Cancel, 'A' requests Abort, everything else is unhandled so
    // the dialog's Esc->Cancel fallback applies. Exposes a mutable marker so tests can prove the
    // original control survives a confirm/dismiss round trip.
    private sealed class ProbeControl(ICliAppShell shell) : InlineControlBase(shell)
    {
        public string State { get; set; } = "original";

        public override string? Hint => "probe-hint";
        public override object? Payload => "probe-payload";
        public override bool CanConfirm => false; // Enter never completes; tests drive explicit results.

        public override InlineKeyResult HandleKey(KeyEvent key) => key.Key switch
        {
            ConsoleKey.C => InlineKeyResult.WithResult(DialogResultKind.Cancel),
            ConsoleKey.A => InlineKeyResult.WithResult(DialogResultKind.Abort),
            _ => InlineKeyResult.NotHandled,
        };

        public override CliGrid ToGrid()
        {
            var g = ToGrid(1, 1);
            g.Set(0, 0, State);
            return g;
        }
    }

    private static TestShell NewShell() => new(culture: CultureInfo.GetCultureInfo("en-US"));

    private static InlineDialog Dialog(TestShell shell, ProbeControl control, InlineDialogConfirmationPolicy? policy)
        => new(shell, title: "Probe", control, confirmation: policy);

    private static string ConfirmCancelText(TestShell shell) =>
        TigerCliResources.Get("Tui_Confirm_Cancel_Message", shell.Culture);

    private static string ConfirmAbortText(TestShell shell) =>
        TigerCliResources.Get("Tui_Confirm_Abort_Message", shell.Culture);

    // ── No policy: unchanged behavior ────────────────────────────────────────

    [Fact]
    public async Task NoPolicy_Cancel_CompletesImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), policy: null);
        shell.Terminal.EnqueueKey(ConsoleKey.C);

        var result = await shell.RunModalAsync(dialog, ct).WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }

    [Fact]
    public async Task NoPolicy_Abort_CompletesImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), policy: null);
        shell.Terminal.EnqueueKey(ConsoleKey.A);

        var result = await shell.RunModalAsync(dialog, ct).WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Abort, result.Kind);
    }

    [Fact]
    public async Task NoPolicy_EscapeFallback_CompletesAsCancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), policy: null);
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await shell.RunModalAsync(dialog, ct).WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }

    // ── Confirm Cancel only ──────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmCancel_Cancel_ShowsConfirmation_Yes_CompletesCancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmCancel);
        var runTask = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.C);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.Contains(ConfirmCancelText(shell), shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow); // No -> Yes
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);     // confirm
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }

    [Fact]
    public async Task ConfirmCancel_No_ReturnsToOriginalDialog()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmCancel);
        var runTask = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.C);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.Contains(ConfirmCancelText(shell), shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // No is default-focused -> dismiss
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.Contains("original", shell.Terminal.LastRenderedText);
        Assert.DoesNotContain(ConfirmCancelText(shell), shell.Terminal.LastRenderedText);
        Assert.Equal(DialogResultKind.NoResult, dialog.Result);

        shell.Terminal.EnqueueKey(ConsoleKey.A); // Abort is not confirmed under ConfirmCancel
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Abort, result.Kind);
    }

    [Fact]
    public async Task ConfirmCancel_EscapeInsideConfirmation_ReturnsToOriginalDialog()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmCancel);
        var runTask = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.C);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.Contains(ConfirmCancelText(shell), shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape); // Esc in confirmation == No
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.Contains("original", shell.Terminal.LastRenderedText);
        Assert.Equal(DialogResultKind.NoResult, dialog.Result);

        shell.Terminal.EnqueueKey(ConsoleKey.A);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Abort, result.Kind);
    }

    [Fact]
    public async Task ConfirmCancel_Abort_CompletesImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmCancel);
        shell.Terminal.EnqueueKey(ConsoleKey.A);

        var result = await shell.RunModalAsync(dialog, ct).WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Abort, result.Kind);
    }

    // ── Confirm Abort only ───────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmAbort_Abort_Yes_CompletesAbort()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmAbort);
        var runTask = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.A);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.Contains(ConfirmAbortText(shell), shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow); // Yes
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Abort, result.Kind);
    }

    [Fact]
    public async Task ConfirmAbort_No_ReturnsToOriginalDialog()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmAbort);
        var runTask = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.A);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.Contains(ConfirmAbortText(shell), shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // No -> dismiss
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.Contains("original", shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.C); // Cancel is not confirmed under ConfirmAbort
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }

    [Fact]
    public async Task ConfirmAbort_Cancel_CompletesImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmAbort);
        shell.Terminal.EnqueueKey(ConsoleKey.C);

        var result = await shell.RunModalAsync(dialog, ct).WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }

    // ── Confirm both ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmBoth_Cancel_Yes_PreservesCancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmCancelAndAbort);
        var runTask = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.C);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.Contains(ConfirmCancelText(shell), shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }

    [Fact]
    public async Task ConfirmBoth_Abort_Yes_PreservesAbort()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmCancelAndAbort);
        var runTask = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.A);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.Contains(ConfirmAbortText(shell), shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Abort, result.Kind);
    }

    // ── Non-confirmable kinds bypass confirmation ────────────────────────────

    [Fact]
    public async Task TokenCancellation_BypassesConfirmation()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.Cancel();
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmCancelAndAbort);

        var result = await shell.RunModalAsync(dialog, timeout: TimeSpan.FromSeconds(10), ct: cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(DialogResultKind.TokenCancel, result.Kind);
    }

    [Fact]
    public async Task Timeout_BypassesConfirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmCancelAndAbort);

        var result = await shell.RunModalAsync(dialog, timeout: TimeSpan.FromMilliseconds(20), ct: ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Timeout, result.Kind);
    }

    [Fact]
    public async Task SystemCancellation_BypassesConfirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var dialog = Dialog(shell, new ProbeControl(shell), InlineDialogConfirmationPolicy.ConfirmCancelAndAbort);
        shell.RaiseSystemCancellation();

        var result = await shell.RunModalAsync(dialog, ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.SystemCancel, result.Kind);
    }

    // ── State preservation + cached-grid signature ───────────────────────────

    [Fact]
    public async Task OriginalControlState_PreservedAcrossConfirmAndDismiss()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var control = new ProbeControl(shell) { State = "kept" };
        var dialog = Dialog(shell, control, InlineDialogConfirmationPolicy.ConfirmCancel);
        var runTask = shell.RunModalAsync(dialog, ct);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.C);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.DoesNotContain("kept", shell.Terminal.LastRenderedText); // original content hidden while confirming

        shell.Terminal.EnqueueKey(ConsoleKey.Escape); // dismiss
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);
        Assert.Contains("kept", shell.Terminal.LastRenderedText); // same instance + state restored

        shell.Terminal.EnqueueKey(ConsoleKey.A);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Abort, result.Kind);
        Assert.Same(control, dialog.Control);
    }

    [Fact]
    public void CachedGrid_RebuildsBetweenNormalAndConfirmStates()
    {
        var shell = NewShell();
        var control = new ProbeControl(shell) { State = "body" };
        var dialog = Dialog(shell, control, InlineDialogConfirmationPolicy.ConfirmCancel);

        var normalGrid = dialog.ToGrid();
        var normalGridAgain = dialog.ToGrid();
        Assert.Same(normalGrid, normalGridAgain); // unchanged structure reuses the cached grid

        dialog.HandleKey(new KeyEvent(ConsoleKey.C, ConsoleModifiers.None, '\0')); // enter confirmation
        var confirmGrid = dialog.ToGrid();
        Assert.NotSame(normalGrid, confirmGrid);

        dialog.HandleKey(new KeyEvent(ConsoleKey.Escape, ConsoleModifiers.None, '\0')); // leave confirmation
        var backToNormalGrid = dialog.ToGrid();
        Assert.NotSame(confirmGrid, backToNormalGrid);
    }

    [Fact]
    public void NoConfirmationByDefault_DialogConstructsWithoutPolicy()
    {
        // Source-compatibility guard: the existing constructor shape still works (no policy argument).
        var shell = NewShell();
        var dialog = new InlineDialog(shell, "t", new ProbeControl(shell));
        Assert.NotNull(dialog);
    }

    [Fact]
    public void DefaultConfirmationMessages_AreLocalized()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal("Cancel this operation?", TigerCliResources.Get("Tui_Confirm_Cancel_Message", en));
        Assert.Equal("Abort this operation?", TigerCliResources.Get("Tui_Confirm_Abort_Message", en));

        var pl = CultureInfo.GetCultureInfo("pl-PL");
        Assert.NotEqual("Cancel this operation?", TigerCliResources.Get("Tui_Confirm_Cancel_Message", pl));
        Assert.NotEqual("Abort this operation?", TigerCliResources.Get("Tui_Confirm_Abort_Message", pl));
    }
}
