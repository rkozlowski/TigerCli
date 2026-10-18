# Command Processing Prompting Design

## Purpose

This page explains why TigerCli owns prompting policy during command processing. For app-author usage, see [prompting and providers](../guides/prompting-and-providers.md).

## Core Idea

Prompting is framework-owned. Command handlers should receive fully bound and validated settings, not decide at execution time whether missing command input should block, prompt, or fail.

## Why This Exists

TigerCli commands must be safe in scripts while still helping humans in a terminal. Centralizing prompting keeps `--non-interactive`, provider calls, prompt ordering, validation, exit-code policy, and tests on one path.

## Human-Help Rationale

Prompting helps humans run a command without stopping to inspect `--help` every time a value is missing. It does not replace help; generated help remains the authoritative description of command shape, arguments, options, and errors.

The goal is to reduce interruption when command intent is already clear but required values are missing. Parser-driven prompts and providers make human interaction easy while preserving script-safe execution.

TigerCli intentionally favors parser-driven prompts and provider-backed choices over command-line completion as its primary guided-interaction model. Completion helps type tokens, but prompts can use command metadata, current settings, provider data, culture, and interaction mode to guide actual decisions.

TigerCli's opt-in [shell completion](../guides/shell-completion.md) is deliberately static: it
completes command paths, option names, and metadata-backed values, but it never prompts and never
calls providers. It helps type tokens; prompts remain the model for decisions that need app data.

## Policy Inputs

Prompting depends on four separate concerns:

- Requiredness: whether a value must exist before execution.
- Promptability: whether a missing value may be requested interactively.
- Prompt mode: the app or command default for missing values.
- Interaction mode: whether the current run allows prompting at all.

Keeping these separate prevents optional values from becoming required just because they can be prompted.

## Design Decisions

- Positional arguments are required.
- Options are optional unless marked `Required = true`.
- `Promptable = [TigerCliPromptable](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Commands.TigerCliPromptable.html).No` always prevents prompting.
- `Promptable = TigerCliPromptable.First`, `Normal`, or `Last` allows prompting only when interaction mode allows it.
- Omitted `Promptable` uses the effective prompt mode.
- The framework default prompt mode is `RequiredOnly`.
- Prompt mode inherits from app default to command group to command; more specific configuration wins.
- `--non-interactive` always wins and prevents prompts.
- Providers are not called in non-interactive mode.

## Processing Order

Prompting happens after parsing and binding provided values, but before validation and handler execution:

```text
resolve command -> bind provided values -> prompt missing values when allowed -> validate -> execute
```

Prompting may fill values. It does not replace framework validation or app validation.

## Provider Boundary

Provider-backed prompts let apps offer choices without moving prompt logic into handlers. Named providers receive `TigerCliProviderContext` and, for typed overloads, partially bound settings, so dependent choices can use earlier command context.

Provider keys are stable values. Labels are display text.

## Cancellation Boundary

Prompting is one consumer of run cancellation, not its owner. A run has two cancellation sources, and they are combined once:

```text
RunAsync(ct) ─────────────┐
                          ├─► linked run token ─► TigerCliSettings.CancellationToken ─► handler
process/system signal ────┘                                     (Ctrl-C / SIGINT / SIGTERM / SIGQUIT)
     │
     └─► SystemCancellationScope (ambient) ─► modal loop  ─► DialogResultKind.SystemCancel
                                           └─► headless activity ─► ActivityOutcome.SystemCancelled
```

Design decisions:

- **Signal handlers are registered in both interaction modes.** A non-interactive run never prompts, but it still executes handler work. Registration is gated only on TigerCli owning signal handling — that is, no injected shell and no `DisableProcessCancellation()`. It uses `PosixSignalRegistration` alone, so it touches no console handles and is safe under redirected stdin/stdout/stderr.
- **The system token is ambient; the caller token is not.** The system token is published through `SystemCancellationScope` because prompts and activities must observe it without every call site threading it. The `RunAsync` token stays explicit: it reaches prompts and providers as a parameter, and reaches handler work through `TigerCliSettings.CancellationToken`. There is deliberately no ambient run-token scope — it would add a fourth precedence rung to the modal and headless paths and hide the cancellation source from the call site.
- **Precedence is fixed and identical in both modes:** system > caller token > timeout. Because the handler-visible token is *linked* to the system token, both trip together on a signal; both paths check the system token first, so a signal is always attributed as system cancellation.
- **The first signal is cooperative; the second escalates.** The first suppresses default termination so the run can restore terminal state and return an exit code. The second leaves default termination in place so a non-cooperative process stays killable.
- **Cancellation is a normal flow, not a failure.** Prompt cancellation and cancellation escaping a handler both resolve through `TigerCliExitKind.Cancelled` with a muted notice. An `OperationCanceledException` raised while the run token was never cancelled is unrelated and still maps through `UnhandledException`.

## Related Docs

- [Prompting and providers](../guides/prompting-and-providers.md)
- [Interaction modes](../guides/interaction-modes.md)
- [Command processing positionals](command-processing-positionals.md)
- [Exit codes and cancellation](../guides/exit-codes.md#cancellation)
- [Activity and progress design](activity-progress.md)
