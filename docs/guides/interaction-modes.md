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
unexpectedly pause for input.

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
- Cancelling a prompt after command selection maps to `TigerCliExitKind.Cancelled`.
- Activity stop actions preserve `Cancelled`, `Aborted`, `TimedOut`, and `SystemCancelled` outcomes.

## Application Configuration

`TigerCliAppBuilder.SetInteractionMode(...)` sets the application policy, and
`SetCommandInteractionMode<THandler>(...)` can override it for one command.
`TigerCliSettings.InteractionMode` exposes the resolved mode to handlers. Applications normally do
not need to branch on it because prompt and activity APIs already apply the policy.

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
