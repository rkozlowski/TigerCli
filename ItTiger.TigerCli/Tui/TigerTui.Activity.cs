using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Activity;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Windowing;

namespace ItTiger.TigerCli.Tui;

public static partial class TigerTui
{
    /// <summary>
    /// Runs a rich activity dialog for a background <paramref name="operation"/> against the default
    /// shell, returning the rich <see cref="ActivityResult{T}"/> (never collapsed). The operation reports
    /// progress through the supplied <see cref="ActivityContext"/>. The dialog exposes a single stop
    /// action selected by <paramref name="stopMode"/> (Cancel or Abort, never both); a confirmed stop
    /// switches the dialog to a "Cancelling…"/"Aborting…" view (with no action button) and waits for the
    /// operation to observe cancellation.
    /// </summary>
    public static Task<ActivityResult<T>> RunActivityAsync<T>(
        string? title,
        ActivityDialogSpec spec,
        Func<ActivityContext, CancellationToken, Task<T>> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync(InlineShell.Instance, title, spec, operation, stopMode, spinner, timeout, ct);

    /// <summary>Runs a rich activity dialog against an explicit <paramref name="shell"/>.</summary>
    public static async Task<ActivityResult<T>> RunActivityAsync<T>(
        ICliAppShell shell,
        string? title,
        ActivityDialogSpec spec,
        Func<ActivityContext, CancellationToken, Task<T>> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(operation);

        // Non-interactive mode disables interaction, not execution: an activity is work-with-UI, not a
        // question to the user. Run the operation body headlessly (no dialog, spinner, stop button, or
        // keyboard wait) and return its result normally — never InteractionNotAllowed, never a spurious
        // Cancelled. The caller token, timeout, and system cancellation still apply.
        if (shell.InteractionMode == TigerCliInteractionMode.NonInteractive)
            return await RunActivityHeadlessAsync(spec, operation, timeout, ct).ConfigureAwait(false);

        var control = new InlineActivityControl<T>(shell, spec, operation, stopMode, spinner);
        var confirmation = stopMode == ActivityStopMode.Abort
            ? InlineDialogConfirmationPolicy.ConfirmAbort
            : InlineDialogConfirmationPolicy.ConfirmCancel;
        var dialog = new InlineDialog(shell, title, control, confirmation: confirmation);

        var dr = await shell.RunModalAsync(dialog, timeout, ct).ConfigureAwait(false);
        return MapResult(dr, control);
    }

    /// <summary>
    /// Runs a rich activity dialog for a value-less operation against the default shell. The result's
    /// <see cref="ActivityResult{T}.Value"/> is <c>true</c> on completion.
    /// </summary>
    public static Task<ActivityResult<bool>> RunActivityAsync(
        string? title,
        ActivityDialogSpec spec,
        Func<ActivityContext, CancellationToken, Task> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync(InlineShell.Instance, title, spec, operation, stopMode, spinner, timeout, ct);

    /// <summary>Runs a value-less rich activity dialog against an explicit <paramref name="shell"/>.</summary>
    public static Task<ActivityResult<bool>> RunActivityAsync(
        ICliAppShell shell,
        string? title,
        ActivityDialogSpec spec,
        Func<ActivityContext, CancellationToken, Task> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return RunActivityAsync<bool>(
            shell, title, spec,
            async (ctx, token) =>
            {
                await operation(ctx, token).ConfigureAwait(false);
                return true;
            },
            stopMode, spinner, timeout, ct);
    }

    // ── Convenience overloads ────────────────────────────────────────────────
    // Thin forwarders over the canonical (shell, title, spec) overloads above: a dropped title forwards
    // as null, and a static message becomes a one-row, one-column, left-aligned activity spec whose
    // non-interactive status line defaults to that same message. Parameters mirror the canonical
    // overloads exactly.

    /// <summary>Runs a rich activity dialog (no title) for a background <paramref name="operation"/>.</summary>
    public static Task<ActivityResult<T>> RunActivityAsync<T>(
        ActivityDialogSpec spec,
        Func<ActivityContext, CancellationToken, Task<T>> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync((string?)null, spec, operation, stopMode, spinner, timeout, ct);

    /// <summary>Runs a rich activity dialog (no title) against an explicit <paramref name="shell"/>.</summary>
    public static Task<ActivityResult<T>> RunActivityAsync<T>(
        ICliAppShell shell,
        ActivityDialogSpec spec,
        Func<ActivityContext, CancellationToken, Task<T>> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync(shell, (string?)null, spec, operation, stopMode, spinner, timeout, ct);

    /// <summary>Runs a rich activity dialog (no title) for a value-less operation.</summary>
    public static Task<ActivityResult<bool>> RunActivityAsync(
        ActivityDialogSpec spec,
        Func<ActivityContext, CancellationToken, Task> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync((string?)null, spec, operation, stopMode, spinner, timeout, ct);

    /// <summary>Runs a value-less rich activity dialog (no title) against an explicit <paramref name="shell"/>.</summary>
    public static Task<ActivityResult<bool>> RunActivityAsync(
        ICliAppShell shell,
        ActivityDialogSpec spec,
        Func<ActivityContext, CancellationToken, Task> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync(shell, (string?)null, spec, operation, stopMode, spinner, timeout, ct);

    /// <summary>
    /// Runs a rich activity dialog that shows a single static <paramref name="message"/> line (one
    /// left-aligned text cell) while <paramref name="operation"/> runs. The message is a trusted activity
    /// text template — it may contain TigerCli markup; write literal braces as <c>{{</c>/<c>}}</c>. In
    /// non-interactive mode the same message is printed once as the default non-interactive status line.
    /// Use an explicit <see cref="ActivityDialogSpec"/> with
    /// <see cref="ActivitySpecBuilder.SetNonInteractiveMessage(string?)"/> when the non-interactive
    /// message should differ from the visible activity message.
    /// </summary>
    public static Task<ActivityResult<T>> RunActivityAsync<T>(
        string? title,
        string message,
        Func<ActivityContext, CancellationToken, Task<T>> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync(title, SimpleMessageSpec(message), operation, stopMode, spinner, timeout, ct);

    /// <summary>
    /// Runs a static-message activity dialog against an explicit <paramref name="shell"/>. In
    /// non-interactive mode the same message is printed once as the default non-interactive status line.
    /// </summary>
    public static Task<ActivityResult<T>> RunActivityAsync<T>(
        ICliAppShell shell,
        string? title,
        string message,
        Func<ActivityContext, CancellationToken, Task<T>> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync(shell, title, SimpleMessageSpec(message), operation, stopMode, spinner, timeout, ct);

    /// <summary>
    /// Runs a static-message activity dialog for a value-less operation. In non-interactive mode the same
    /// message is printed once as the default non-interactive status line.
    /// </summary>
    public static Task<ActivityResult<bool>> RunActivityAsync(
        string? title,
        string message,
        Func<ActivityContext, CancellationToken, Task> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync(title, SimpleMessageSpec(message), operation, stopMode, spinner, timeout, ct);

    /// <summary>
    /// Runs a value-less static-message activity dialog against an explicit <paramref name="shell"/>. In
    /// non-interactive mode the same message is printed once as the default non-interactive status line.
    /// </summary>
    public static Task<ActivityResult<bool>> RunActivityAsync(
        ICliAppShell shell,
        string? title,
        string message,
        Func<ActivityContext, CancellationToken, Task> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync(shell, title, SimpleMessageSpec(message), operation, stopMode, spinner, timeout, ct);

    /// <summary>
    /// Runs a static-message activity dialog (no title) for a background <paramref name="operation"/>. In
    /// non-interactive mode the same message is printed once as the default non-interactive status line.
    /// </summary>
    public static Task<ActivityResult<T>> RunActivityAsync<T>(
        string message,
        Func<ActivityContext, CancellationToken, Task<T>> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync((string?)null, message, operation, stopMode, spinner, timeout, ct);

    /// <summary>
    /// Runs a static-message activity dialog (no title) against an explicit <paramref name="shell"/>. In
    /// non-interactive mode the same message is printed once as the default non-interactive status line.
    /// </summary>
    public static Task<ActivityResult<T>> RunActivityAsync<T>(
        ICliAppShell shell,
        string message,
        Func<ActivityContext, CancellationToken, Task<T>> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync(shell, (string?)null, message, operation, stopMode, spinner, timeout, ct);

    /// <summary>
    /// Runs a static-message activity dialog (no title) for a value-less operation. In non-interactive
    /// mode the same message is printed once as the default non-interactive status line.
    /// </summary>
    public static Task<ActivityResult<bool>> RunActivityAsync(
        string message,
        Func<ActivityContext, CancellationToken, Task> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync((string?)null, message, operation, stopMode, spinner, timeout, ct);

    /// <summary>
    /// Runs a value-less static-message activity dialog (no title) against an explicit
    /// <paramref name="shell"/>. In non-interactive mode the same message is printed once as the default
    /// non-interactive status line.
    /// </summary>
    public static Task<ActivityResult<bool>> RunActivityAsync(
        ICliAppShell shell,
        string message,
        Func<ActivityContext, CancellationToken, Task> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunActivityAsync(shell, (string?)null, message, operation, stopMode, spinner, timeout, ct);

    // Builds a minimal one-row, one-column, left-aligned activity layout for a static message line. The
    // message is a trusted activity text template (same contract as ActivityTextElement.Template), so it
    // may contain TigerCli markup; literal braces must be escaped as {{ }}. Simple message overloads use
    // that same text as their default non-interactive status line.
    private static ActivityDialogSpec SimpleMessageSpec(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return ActivityDialogSpec.Create()
            .SetNonInteractiveMessage(message)
            .AddColumn(align: CliTextAlignment.Left)
            .AddRow(null, r => r.Cell(0).Text(message))
            .Build();
    }

    // Headless activity execution for non-interactive mode: runs the operation with no shell, no
    // rendering, and no keyboard, honoring the same cancellation/timeout precedence the modal path uses
    // (system > caller token > timeout). The operation still reports progress through an ActivityContext
    // — the values are recorded in ActivityState (and validated on the caller thread) but never rendered —
    // so value-validation errors surface identically to the interactive path. If the spec carries a
    // NonInteractiveMessage, it is printed once (stdout, markup-aware) before the body starts, giving
    // scripts a single line of progress context in place of the dialog. Result mapping mirrors MapResult:
    // Completed on success, Failed on a thrown exception, and the matching cancellation flavour otherwise.
    private static async Task<ActivityResult<T>> RunActivityHeadlessAsync<T>(
        ActivityDialogSpec spec,
        Func<ActivityContext, CancellationToken, Task<T>> operation,
        TimeSpan? timeout,
        CancellationToken ct)
    {
        // Optional static progress line: informational output, so it goes to stdout (not stderr). Printed
        // exactly once, before the body, and only when the caller supplied a non-empty message.
        if (!string.IsNullOrEmpty(spec.NonInteractiveMessage))
            TigerConsole.MarkupLine(spec.NonInteractiveMessage);

        // The system token is only ever cancelable during interactive runs (non-interactive runs install
        // no process-cancellation handler), but honoring it here keeps the headless path faithful.
        var systemToken = SystemCancellationScope.Current;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, systemToken);
        if (timeout is { } t)
            linkedCts.CancelAfter(t);

        var state = new ActivityState(spec);
        var context = new ActivityContext(state);
        try
        {
            var value = await operation(context, linkedCts.Token).ConfigureAwait(false);
            return ActivityResult<T>.Completed(value, DialogResultKind.Ok);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            // Cooperative cancellation observed. Attribute it with the modal path's precedence.
            if (systemToken.IsCancellationRequested)
                return new ActivityResult<T>(ActivityOutcome.SystemCancelled, DialogResultKind.SystemCancel);
            if (ct.IsCancellationRequested)
                return new ActivityResult<T>(ActivityOutcome.Cancelled, DialogResultKind.TokenCancel);
            return new ActivityResult<T>(ActivityOutcome.TimedOut, DialogResultKind.Timeout);
        }
        catch (Exception ex)
        {
            return ActivityResult<T>.Failed(ex, DialogResultKind.Ok);
        }
        finally
        {
            state.Close();
        }
    }

    private static ActivityResult<T> MapResult<T>(DialogResult dr, InlineActivityControl<T> control)
    {
        switch (dr.Kind)
        {
            case DialogResultKind.Ok:
                // The operation finished (deferred completion guarantees the op task is done). A captured
                // exception means it faulted; otherwise it completed with a value.
                var ex = control.OperationException;
                return ex is not null
                    ? ActivityResult<T>.Failed(ex, dr.Kind)
                    : ActivityResult<T>.Completed(control.OperationValue, dr.Kind);

            case DialogResultKind.Abort:
                return new ActivityResult<T>(ActivityOutcome.Aborted, dr.Kind);

            case DialogResultKind.Timeout:
                return new ActivityResult<T>(ActivityOutcome.TimedOut, dr.Kind);

            case DialogResultKind.SystemCancel:
                return new ActivityResult<T>(ActivityOutcome.SystemCancelled, dr.Kind);

            // Cancel (user / confirmed) and TokenCancel (caller token) both map to Cancelled.
            case DialogResultKind.Cancel:
            case DialogResultKind.TokenCancel:
            default:
                return new ActivityResult<T>(ActivityOutcome.Cancelled, dr.Kind);
        }
    }
}
