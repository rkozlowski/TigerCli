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
- `Promptable = TigerCliPromptable.No` always prevents prompting.
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

## Related Docs

- [Prompting and providers](../guides/prompting-and-providers.md)
- [Interaction modes](../guides/interaction-modes.md)
- [Command processing positionals](command-processing-positionals.md)
