# Activity and Progress Design

TigerCli activity/progress UI is built on the semi-interactive dialog stack. It is for bounded long-running operations in command-line workflows, not for full-screen dashboards.

## Current Model

- `TigerTui.RunActivityAsync(...)` is the public entry point for long-running work with activity UI.
- `ActivityDialogSpec` describes the layout: columns, rows, cells, text elements, progress-bar elements, and named dynamic rows.
- `ActivityContext` is the only update surface given to the operation. Operations update values; they do not render.
- `ActivityResult<T>` reports `Completed`, `Cancelled`, `Aborted`, `TimedOut`, `SystemCancelled`, or `Failed` without collapsing outcomes.
- [ActivityStopMode](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.ActivityStopMode.html) chooses the single stop action (`Cancel` or `Abort`).
- `ActivitySpinnerSpec` configures the top-frame spinner.

## Ownership Boundaries

- `CliGrid` owns layout, measurement, clipping, cell padding, alignment, and rendering.
- Activity code maps the spec and current values into a `CliGrid`; it does not implement a parallel layout engine.
- Progress bars render through framework overlay/rendering primitives, not by manually measuring terminal width in operation code.
- Background operations update state through `ActivityContext`; pending updates are applied on the modal loop thread.
- `InlineDialog` owns confirmation behavior for stop actions. Token, timeout, and system cancellation bypass confirmation.

## Non-Interactive Execution

An activity is *work-with-UI*, not a prompt. In non-interactive [TigerCliInteractionMode](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.TigerCliInteractionMode.html), `RunActivityAsync` runs the operation body **headlessly**: no dialog, spinner, stop button, or keyboard wait. It returns the normal `ActivityResult<T>` — `Completed`/`Failed` with [DialogResultKind](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.DialogResultKind.html).Ok — never `InteractionNotAllowed` and never a spurious `Cancelled`. The caller token, `timeout`, and system cancellation are honored with the same precedence as the modal path (system > caller token > timeout), and value updates through `ActivityContext` still validate identically (they are just never rendered).

**Progress updates are safe headless.** With no modal loop draining `ActivityState`, `ActivityContext.SetValue`/`SetValues`/`SetMessage` calls neither hang, block on rendering, nor fail merely because there is no dialog. Each valid update is **recorded** in `ActivityState` (the latest value per slot is retained — exactly what a drain loop would have observed) and is **validated on the caller thread** (unknown row, bad index, or wrong value count throws, faulting the operation), so correctness protection is identical in both modes; the recorded values are simply never rendered. A consumer can therefore write one activity-based execution path and call progress freely regardless of mode.

**Optional static message.** A spec may set `ActivityDialogSpec.NonInteractiveMessage` (via `ActivitySpecBuilder.SetNonInteractiveMessage("Importing card...")`). In non-interactive mode that single line is printed once to **stdout** (via `TigerConsole.MarkupLine`, markup-aware) before the body starts, giving scripts one line of progress context in place of the live dialog. It is only used in non-interactive mode, printed once, skipped when `null`/empty, and never printed in interactive mode (the dialog renders instead). Simple `RunActivityAsync` overloads that take a static message use that same message as their default non-interactive message. For richer dialogs, or when script output should differ from the visible activity message, build an `ActivityDialogSpec` and call `SetNonInteractiveMessage(...)`. Prefer present-progress phrasing ("Importing card…", "Generating code…") that does not imply success before the work completes, and localize it at the call site.

The no-shell overloads run on the `InlineShell` singleton, which reports its interaction mode from the per-run ambient scope published by `TigerCliApp` (`InteractionModeScope`). So a `RunActivityAsync` call inside a command handler observes the run's real `--non-interactive` mode without the handler threading a shell through. This mirrors the existing `SystemCancellationScope` deferral. See [interaction modes](../guides/interaction-modes.md#interaction-is-disabled-not-execution).

## Command Pattern

TigerCli activity APIs are mode-aware. Do not write separate "direct" and "activity" execution paths just to support `--non-interactive`. Use one `RunActivityAsync` path and let TigerCli render it interactively or run it headlessly.

Recommended: its columns use [CliTextAlignment](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliTextAlignment.html) and [CliColumnSizing](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliColumnSizing.html) for layout.

```csharp
var spec = ActivityDialogSpec.Create()
    .SetNonInteractiveMessage("Importing card...")
    .AddColumn(width: 10, align: CliTextAlignment.Right)
    .AddColumn(sizing: CliColumnSizing.Star)
    .AddRow("progress", r => r
        .Cell(0).Text("Import:")
        .Cell(1).ProgressBar(valueIndex: 0, maxValueIndex: 1)
        .Values(0, 100))
    .Build();

var result = await TigerTui.RunActivityAsync(
    "Import",
    spec,
    async (ctx, ct) =>
    {
        // one execution path
        // progress updates are safe in interactive and headless modes
    });
```

Discouraged:

```csharp
if (settings.InteractionMode == TigerCliInteractionMode.NonInteractive)
    await RunDirectAsync(...);
else
    await RunWithActivityAsync(...);
```

That split duplicates the business operation, creates two places for behavior to drift, weakens tests, bypasses activity cancellation/timeout/result semantics, and pushes TigerCli's interaction policy into app code. Branch on interaction mode only when the command has genuinely different semantics, such as refusing to ask a required question or applying a different confirmation policy. Do not branch merely to hide progress UI.

## Providers vs Activity

Use async providers for slow provider-backed choices. TigerCli owns the loading message and spinner for interactive provider prompts.

Use `RunActivityAsync` for real long-running operations after command input has been resolved.

Do not wrap providers in local spinners or background writer loops.

See also:

- [Semi-interactive prompts](../guides/semi-interactive-prompts.md#activity-dialog-overloads)
- [API map](../reference/api-map.md)
