using System.Globalization;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Activity;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Windowing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Non-interactive behaviour for the rich activity dialog: a <c>--non-interactive</c> run disables
/// interaction, not execution, so <see cref="TigerTui.RunActivityAsync{T}(Tui.Abstractions.ICliAppShell, string?, ActivityDialogSpec, System.Func{ActivityContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}, ActivityStopMode, ActivitySpinnerSpec?, System.TimeSpan?, System.Threading.CancellationToken)"/>
/// runs the operation body headlessly (no dialog, spinner, stop button, or keyboard) and returns the
/// result normally rather than reporting <see cref="ActivityOutcome.Cancelled"/> or failing.
/// </summary>
public sealed class InlineActivityNonInteractiveTests
{
    private static TestShell InteractiveShell() =>
        new(interactionMode: TigerCliInteractionMode.SemiInteractive, culture: CultureInfo.GetCultureInfo("en-US"));

    private static TestShell NonInteractiveShell() =>
        new(interactionMode: TigerCliInteractionMode.NonInteractive, culture: CultureInfo.GetCultureInfo("en-US"));

    private static ActivityDialogSpec MessageSpec() =>
        ActivityDialogSpec.Create()
            .AddColumn(align: CliTextAlignment.Left)
            .AddRow("r", r => r.Cell(0).Text("{0}").Values("start"))
            .Build();

    [Fact]
    public async Task Interactive_StillRendersActivityDialog()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = InteractiveShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var (result, stdout) = await CaptureStdoutAsync(async () =>
        {
            var run = TigerTui.RunActivityAsync(shell, "Working", "Please wait",
                async (_, _) => { await gate.Task.ConfigureAwait(false); return 1; }, ct: ct);

            // The modal loop renders the dialog before any keypress.
            await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
            Assert.Contains("Please wait", shell.Terminal.LastRenderedText);

            gate.SetResult();
            return await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        });

        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(string.Empty, stdout);
    }

    [Fact]
    public async Task NonInteractive_ExecutesOperationBody_ReturnsResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();
        var ran = false;

        var result = await TigerTui.RunActivityAsync(shell, "Working", MessageSpec(),
            (context, _) =>
            {
                ran = true;
                context.SetValue("r", 0, "progress"); // progress reporting still works (goes nowhere)
                return Task.FromResult(42);
            }, ct: ct);

        Assert.True(ran);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(DialogResultKind.Ok, result.DialogResultKind);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task NonInteractive_RendersNoDialogOrControls()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();

        var result = await TigerTui.RunActivityAsync(shell, "Working", MessageSpec(),
            (_, _) => Task.FromResult(0), ct: ct);

        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        // No dialog, spinner, accept/cancel button, or keyboard wait: nothing is rendered or read.
        Assert.Equal(0, shell.Terminal.RenderCount);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task NonInteractive_ValueLessOverload_CompletesWithTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();
        var ran = false;

        var result = await TigerTui.RunActivityAsync(shell, "Saving",
            (_, _) => { ran = true; return Task.CompletedTask; },
            timeout: null, ct: ct);

        Assert.True(ran);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.True(result.Value);
        Assert.Equal(0, shell.Terminal.RenderCount);
    }

    [Fact]
    public async Task NonInteractive_OperationThrows_MapsToFailed_SameAsInteractive()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();

        var result = await TigerTui.RunActivityAsync<int>(shell, "Working", MessageSpec(),
            (_, _) => throw new InvalidOperationException("boom"), ct: ct);

        Assert.Equal(ActivityOutcome.Failed, result.Outcome);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Equal("boom", result.Exception!.Message);
        Assert.Equal(0, shell.Terminal.RenderCount);
    }

    [Fact]
    public async Task NonInteractive_WrongValueCount_FaultsOperation_SameAsInteractive()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();

        var spec = ActivityDialogSpec.Create()
            .AddColumn(align: CliTextAlignment.Left)
            .AddRow("files", r => r.Cell(0).Text("{0}").Values(0)) // row declares 1 value
            .Build();

        var result = await TigerTui.RunActivityAsync<int>(shell, "Working", spec,
            (context, _) =>
            {
                context.SetValues("files", 1, 2); // wrong count -> throws on the caller thread
                return Task.FromResult(0);
            }, ct: ct);

        Assert.Equal(ActivityOutcome.Failed, result.Outcome);
        Assert.IsType<ArgumentException>(result.Exception);
    }

    [Fact]
    public async Task NonInteractive_CallerTokenCancelled_MapsToCancelled()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var shell = NonInteractiveShell();

        var result = await TigerTui.RunActivityAsync(shell, "Working", MessageSpec(),
            async (_, token) =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                return 0;
            }, ct: cts.Token);

        Assert.Equal(ActivityOutcome.Cancelled, result.Outcome);
        Assert.Equal(DialogResultKind.TokenCancel, result.DialogResultKind);
    }

    [Fact]
    public void DefaultShell_DefersInteractionMode_ToAmbientScope()
    {
        // The no-shell RunActivityAsync overloads run on the InlineShell singleton (DefaultShell). It must
        // report the run's ambient interaction mode (published by TigerCliApp) so a command handler's
        // no-shell activity call observes --non-interactive without threading a shell through. This is the
        // mechanism behind the observed bug fix; explicit shells (TestShell) keep their own mode.
        var previous = InteractionModeScope.Current;
        try
        {
            InteractionModeScope.Current = TigerCliInteractionMode.NonInteractive;
            Assert.Equal(TigerCliInteractionMode.NonInteractive, TigerTui.DefaultShell.InteractionMode);

            InteractionModeScope.Current = TigerCliInteractionMode.SemiInteractive;
            Assert.Equal(TigerCliInteractionMode.SemiInteractive, TigerTui.DefaultShell.InteractionMode);

            // Unset ambient falls back to the singleton's default (semi-interactive).
            InteractionModeScope.Current = null;
            Assert.Equal(TigerCliInteractionMode.SemiInteractive, TigerTui.DefaultShell.InteractionMode);
        }
        finally
        {
            InteractionModeScope.Current = previous;
        }
    }

    [Fact]
    public async Task NonInteractive_Timeout_MapsToTimedOut()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();

        var result = await TigerTui.RunActivityAsync(shell, "Working", MessageSpec(),
            async (_, token) => { await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false); return 0; },
            timeout: TimeSpan.FromMilliseconds(20), ct: ct);

        Assert.Equal(ActivityOutcome.TimedOut, result.Outcome);
        Assert.Equal(DialogResultKind.Timeout, result.DialogResultKind);
    }

    // ── Goal 1: repeated progress updates are safe headless ───────────────────────

    [Fact]
    public async Task NonInteractive_RepeatedProgressUpdates_DoNotHangOrFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();

        var spec = ActivityDialogSpec.Create()
            .AddColumn(align: CliTextAlignment.Left)
            .AddRow("status", r => r.Cell(0).Text("{0}").Values("start"))
            .AddRow("counts", r => r.Cell(0).Text("{0}/{1}").Values(0, 0))
            .Build();

        var result = await TigerTui.RunActivityAsync(shell, "Working", spec,
            (context, _) =>
            {
                // A tight loop of every ActivityContext update surface. With no modal loop draining the
                // state there is nothing to render or wait on, so this must neither hang nor throw.
                for (var i = 0; i < 500; i++)
                {
                    context.SetMessage("status", $"step {i}");
                    context.SetValue("counts", 0, i);
                    context.SetValues("counts", i, i * 2);
                }
                return Task.FromResult(500);
            }, ct: ct);

        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(500, result.Value);
        Assert.Equal(0, shell.Terminal.RenderCount); // never rendered
        Assert.Equal(0, shell.Terminal.ReadCount);   // never read a key
    }

    [Fact]
    public void HeadlessProgressUpdates_AreRecordedInState_WithoutADrainLoop()
    {
        // Documents the chosen behavior: headless progress updates are RECORDED in ActivityState (and
        // validated on the caller thread), not silently dropped. Nothing renders them, but the latest
        // value per slot is retained — exactly what a drain loop would have observed had a UI existed.
        var spec = ActivityDialogSpec.Create()
            .AddColumn(align: CliTextAlignment.Left)
            .AddRow("status", r => r.Cell(0).Text("{0}").Values("start"))
            .Build();
        var state = new ActivityState(spec);
        var context = new ActivityContext(state);

        context.SetMessage("status", "done");

        Assert.True(state.TryDrainSnapshot(out var snapshot));
        Assert.Equal("done", snapshot["status"][0]);
    }

    // ── Goal 2: optional non-interactive static message ───────────────────────────

    private static ActivityDialogSpec MessageSpecWithNonInteractive(string? nonInteractiveMessage) =>
        ActivityDialogSpec.Create()
            .SetNonInteractiveMessage(nonInteractiveMessage)
            .AddColumn(align: CliTextAlignment.Left)
            .AddRow("r", r => r.Cell(0).Text("{0}").Values("start"))
            .Build();

    [Fact]
    public async Task NonInteractive_WithNonInteractiveMessage_PrintsExactlyOneStaticLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();

        var (result, stdout) = await CaptureStdoutAsync(() =>
            TigerTui.RunActivityAsync(shell, "Working",
                MessageSpecWithNonInteractive("Importing card..."),
                (_, _) => Task.FromResult(0), ct: ct));

        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        // Exactly one static line, printed before the (silent) body. No dialog, no keyboard.
        var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Importing card..."], lines);
        Assert.Equal(0, shell.Terminal.RenderCount);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task NonInteractive_SimpleMessage_PrintsMessageOnce_AndRunsBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();
        var ran = false;

        var (result, stdout) = await CaptureStdoutAsync(() =>
            TigerTui.RunActivityAsync(shell, "Importing card...",
                (_, _) => { ran = true; return Task.FromResult(12); }, ct: ct));

        Assert.True(ran);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(12, result.Value);
        var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Importing card..."], lines);
        Assert.Equal(0, shell.Terminal.RenderCount);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task NonInteractive_SimpleTitleAndMessage_PrintsMessageOnce_AndRunsBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();
        var ran = false;

        var (result, stdout) = await CaptureStdoutAsync(() =>
            TigerTui.RunActivityAsync(shell, "Import", "Importing card...",
                (_, _) => { ran = true; return Task.FromResult(12); }, ct: ct));

        Assert.True(ran);
        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(12, result.Value);
        var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Importing card..."], lines);
        Assert.Equal(0, shell.Terminal.RenderCount);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task NonInteractive_WithoutNonInteractiveMessage_PrintsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();

        var (result, stdout) = await CaptureStdoutAsync(() =>
            TigerTui.RunActivityAsync(shell, "Working", MessageSpec(),
                (context, _) => { context.SetValue("r", 0, "progress"); return Task.FromResult(0); }, ct: ct));

        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(string.Empty, stdout);          // no dialog, no progress, no message
        Assert.Equal(0, shell.Terminal.RenderCount);
    }

    [Fact]
    public async Task NonInteractive_EmptyNonInteractiveMessage_PrintsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NonInteractiveShell();

        var (result, stdout) = await CaptureStdoutAsync(() =>
            TigerTui.RunActivityAsync(shell, "Working",
                MessageSpecWithNonInteractive(""),
                (_, _) => Task.FromResult(0), ct: ct));

        Assert.Equal(ActivityOutcome.Completed, result.Outcome);
        Assert.Equal(string.Empty, stdout);
    }

    [Fact]
    public async Task Interactive_WithNonInteractiveMessage_DoesNotPrintIt_AndStillRendersDialog()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = InteractiveShell();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var spec = ActivityDialogSpec.Create()
            .SetNonInteractiveMessage("Importing card...")
            .AddColumn(align: CliTextAlignment.Left)
            .AddRow(null, r => r.Cell(0).Text("Please wait"))
            .Build();

        Task<ActivityResult<int>>? run = null;
        var (_, stdout) = await CaptureStdoutAsync(async () =>
        {
            run = TigerTui.RunActivityAsync(shell, "Working", spec,
                async (_, _) => { await gate.Task.ConfigureAwait(false); return 7; }, ct: ct);

            // The dialog renders before any keypress; the non-interactive message is never printed.
            await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
            gate.SetResult();
            return await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        });

        Assert.Contains("Please wait", shell.Terminal.LastRenderedText); // dialog UI rendered
        Assert.DoesNotContain("Importing card", stdout);                 // static message suppressed
    }

    // Redirects Console.Out for the duration of the run so the headless static message (written through
    // TigerConsole.MarkupLine to stdout) can be asserted. Mirrors the capture pattern used by the
    // provider-value tests.
    private static async Task<(TResult Result, string Stdout)> CaptureStdoutAsync<TResult>(
        Func<Task<TResult>> run)
    {
        var originalOut = Console.Out;
        using var stdout = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            var result = await run().ConfigureAwait(false);
            return (result, stdout.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
