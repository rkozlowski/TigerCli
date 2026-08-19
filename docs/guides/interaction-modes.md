# TigerCli Interaction Modes

TigerCli's current supported app flow is command execution with optional prompting and command-menu
behavior. Interaction policy controls whether TigerCli may ask for missing input or render inline
activity UI; it does not change the command's business operation.

[Folder Copy](../examples/folder-copy.md) demonstrates the pattern: folder prompts are unavailable
under `--non-interactive`, while scanning and copying still run headlessly when all required input is
supplied.

## Current Modes

| Mode | Lifecycle | Interaction | Flow |
|---|---|---|---|
| `SemiInteractive` | One command invocation | Optional command menu, prompts, providers, and activities | Args or menu -> prompt -> validate -> execute -> exit |
| `NonInteractive` | One command invocation | No menus, prompts, or keyboard input | Explicit input -> validate -> execute or fail |

TigerCli defaults to `SemiInteractive`. Passing `--non-interactive` reduces the effective mode to
`NonInteractive`; TigerCli does not provide a user-facing option that increases interaction.

`--non-interactive` is an execution option, so it follows the normal command grammar. Put it after
the command path and required positional arguments, for example
`app command positional --non-interactive`. It is invalid before the command or before a required
positional. A default/root command and a command menu are reached through an empty command path, so
`app --non-interactive` is valid for them; a menu then refuses the run because it needs interaction.
Mode changes presentation and prompting policy; it does not change the business operation selected
by the command path.

## Semi-Interactive Command Execution

Semi-interactive mode guides one command invocation. The command may come directly from command-line
arguments or from the optional command menu. TigerCli can then prompt for missing promptable values,
load provider choices, validate the bound settings, run the command, render output, and exit.

Current prompt controls include text and secret input, select, multi-select, flags selection,
confirmation, folder selection, and activity/progress dialogs. Prompting occurs only when member
metadata and the effective prompt policy allow it.

> A command menu is current semi-interactive behavior. Selecting a command does not start a
> persistent application session.

## Non-Interactive Command Execution

Non-interactive mode is automation-safe. It never opens the command menu, renders a prompt, or waits
for keyboard input. Missing required values fail instead of prompting. Supplied values still go
through parsing, binding, framework and custom validation, and configured provider validation.

This mode is suitable for scripts, CI, redirection, and AI agents because a command cannot
unexpectedly pause for input. It is also safe under redirected stdin/stdout/stderr: headless
execution never reads the cursor position or polls the keyboard, so it cannot fail with
"The handle is invalid." Semi-interactive prompting does need a real console — an interactive prompt
under redirected handles is not supported.

Cancellation still applies. See [Cancellation](#cancellation) for how Ctrl-C and the `RunAsync`
token reach headless work through `TigerCliSettings.CancellationToken`.

## Command Menu

In the current flow the command menu is a one-command selection layer:

```text
app starts
-> command menu opens when enabled and needed
-> user selects one command
-> TigerCli prompts for eligible missing values
-> TigerCli validates and executes the command
-> output is rendered
-> app exits
```

The menu does not replace parsing, binding, validation, execution, structured output, or exit-code
mapping. It is disabled in non-interactive mode.

## Interaction Is Disabled, Not Execution

Prompts and menus need an answer from the user, so non-interactive mode disables them. Activities are
different: `TigerTui.RunActivityAsync` wraps work with presentation. In non-interactive mode the same
operation body runs headlessly, without a dialog, spinner, buttons, progress rendering, or keyboard
input.

Use one activity-based execution path and let TigerCli select its presentation:

```csharp
var result = await TigerTui.RunActivityAsync(
    "Importing data...",
    async (context, ct) =>
    {
        await ImportAsync(context, ct);
    });
```

Do not branch to a separate implementation merely to suppress activity UI. Cancellation, timeout,
failure, and activity result semantics still apply in headless execution. An
`ActivityDialogSpec.NonInteractiveMessage` can emit one static progress line before work begins.

Pass `ct: settings.CancellationToken` on that single call so the headless run is cancellable by
Ctrl-C and by the `RunAsync` token — see [Cancellation](#cancellation).

## Activities, Progress, And Spinners

In semi-interactive mode an activity can render its dialog, status rows, progress bars, spinners,
stop action, and cancellation state. In non-interactive mode it runs the same body without that UI.
Progress updates remain accepted and validated even though they are not rendered.

Simple overloads such as `TigerTui.RunActivityAsync("Importing data...", operation)` use their
message as the non-interactive line. Prefer present-progress wording that does not claim success
before the operation completes.

## Prompting Policy

Prompting depends on four separate facts:

- whether a value is required,
- whether the member is promptable,
- the effective prompt mode,
- the effective interaction mode.

The default prompt mode is `RequiredOnly`. Missing positional arguments and required options may be
prompted in semi-interactive mode unless the member opts out or has no supported prompt mapping.
`--non-interactive` always wins.

Prompting fills missing values; it does not replace validation. Normal framework and custom
validation run after prompting.

## Cancellation

- Escape from the command menu before selecting a command can exit without running one.
- Cancelling a prompt after command selection maps to [TigerCliExitKind](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliExitKind.html).Cancelled.
- Activity stop actions preserve `Cancelled`, `Aborted`, `TimedOut`, and `SystemCancelled` outcomes.

TigerCli registers cooperative process/system cancellation handlers (Ctrl-C / SIGINT, SIGTERM, and
Ctrl-Break / SIGQUIT where supported) in **both** interaction modes. A non-interactive run never
prompts, but it still executes handler work, so the signal must reach that work rather than tearing
the process down — otherwise a script sees an abrupt kill (`STATUS_CONTROL_C_EXIT` on Windows)
instead of the app's cancellation exit code.

`TigerCliSettings.CancellationToken` is the handler-visible run token. It combines the token passed
to `TigerCliApp.RunAsync` with TigerCli's process/system cancellation, so a handler observes both
through one token:

```csharp
public override async Task<int> ExecuteAsync(MySettings settings)
{
    var result = await TigerTui.RunActivityAsync(
        "Importing data...",
        (context, ct) => ImportAsync(context, ct),
        ct: settings.CancellationToken);   // ← what makes the headless activity cancellable

    return result.Outcome == ActivityOutcome.Completed ? 0 : 2;
}
```

Pass it explicitly. Activities do not pick the run token up implicitly, and a handler that leaves
`ct` unset gets an activity that only a signal can stop.

The handler decides what cancellation means. Catch it and return your own exit code, or map
`ActivityOutcome.Cancelled` / `SystemCancelled` yourself. A cancellation that escapes the handler
while the run token is cancelled is reported with the quiet cancellation notice and mapped through
`TigerCliExitKind.Cancelled` — never `UnhandledException`. An `OperationCanceledException` thrown
while the run token was *not* cancelled stays an unhandled exception, so an app's own inner timeout
is not silently reclassified.

A **second** signal escalates: cancellation is requested again, but default termination is no longer
suppressed, so a process whose work ignores cooperative cancellation stays killable from the
terminal. Press Ctrl-C twice to force it down.

`TigerCliAppBuilder.DisableProcessCancellation()` hands signal handling back to the host; the run
token then carries only the `RunAsync` token. Signal registration is also skipped when a host injects
its own shell.

## Application Configuration

`TigerCliAppBuilder.SetInteractionMode(...)` sets the application policy, and
`SetCommandInteractionMode<THandler>(...)` can override it for one command.
`TigerCliSettings.InteractionMode` exposes the resolved mode to handlers, and
`TigerCliSettings.CancellationToken` exposes the run's cancellation token. Applications normally do
not need to branch on the mode because prompt and activity APIs already apply the policy.

## Reserved Full-Interactive Value

The `FullInteractive` enum value is reserved. TigerCli does not implement a persistent session shell
or return-to-command navigation after execution. Do not rely on `FullInteractive` as an implemented
mode.

## References

- [Prompting and providers](prompting-and-providers.md)
- [Semi-interactive prompts](semi-interactive-prompts.md)
- [Activity and progress design](../design/activity-progress.md)
- [AI usage](../ai-usage.md)
- [ROI Cities](../getting-started.md)
- [Folder Copy](../examples/folder-copy.md)
