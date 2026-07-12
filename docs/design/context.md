# Architecture Context

## Purpose

This page summarizes the architectural boundaries behind TigerCli. It is a map of why the pieces exist, not a project history.

## Product Shape

TigerCli is an opinionated framework for building script-safe, command-driven CLI/TUI applications
that share one model across automation, guided human interaction, and AI-assisted development.

Its core model combines:

- metadata-driven commands, help, and validation
- framework-owned interaction policy
- parser-driven and provider-backed prompts
- typed exit-code policy
- locale-aware CLI text
- structured output through TigerConsole and renderable components
- app-level testability

## Three Readers: DX, UX, And AX

TigerCli optimizes command applications for three readers: the developer writing the app, the human
using it, and the AI agent reading, modifying, or testing it. AX is not a separate feature; it is the
agent-facing result of the same explicit, testable design that supports developer and user experience.

- **DX:** app authors declare commands, selectors, options, providers, prompts, structured output,
  exit policy, and app-boundary tests instead of distributing those concerns across console code.
- **UX:** users get readable command shapes, generated help, validation, guided prompts, command
  menus, activity UI, structured output, and safe non-interactive execution.
- **AX:** AI agents get predictable command shapes, explicit concepts, reusable app factories,
  generated documentation artifacts, focused examples, and application-boundary tests they can
  inspect and verify.

This is why TigerCli favors explicit command shapes, selectors as object keys, providers,
`UseAssemblyMetadata(...)`, `CliList` and `CliDetails`, `RunActivityAsync`, `TigerCliAppTestHost`,
generated documentation artifacts, and the agent-oriented guidance in
[`docs/ai-usage.md`](../ai-usage.md).

## Command Boundary

Commands are async handlers over settings classes. Settings metadata describes command input, generated help, validation, prompting, localization, and tests.

This keeps the command contract in one place instead of spreading behavior across manual parsing, console output, and handler-specific prompt code.

## Interaction Boundary

Interaction modes are safety boundaries:

- `NonInteractive` never prompts.
- `SemiInteractive` may use inline prompts.
- `FullInteractive` is reserved. TigerCli does not provide a persistent session shell, and
  applications must not rely on it as an implemented mode.

The framework decides whether prompting is allowed before handlers run.

## Rendering Boundary

`CliGrid` is low-level rendering infrastructure. App code should generally use:

- `TigerConsole.MarkupLine(...)` and `TigerConsole.MarkupErrorLine(...)` for simple output
- `CliList<T>` for list command output
- `CliDetails` for single-record details output
- `CliTable` for lower-level record-shaped output
- custom `CliRenderableComponent` types for reusable structured output

Direct `CliGrid` use belongs mostly in rendering infrastructure, focused tests, and advanced component implementations.

## Measurement Boundary

TigerCli resolves layout through a measure-then-render pipeline. Measurement owns wrapping, sizing, alignment, spans, scrollable cells, and active-point mapping. Rendering should project already-measured content rather than re-decide layout.

This keeps output deterministic and easier to test.

## Localization Boundary

TigerCli is locale-aware, not a localization framework. It uses app-supported cultures, a resolved run culture, source-text helpers, and resource lookup where useful. Localized labels are display-only and do not change command-line tokens.

## Design Bias

TigerCli prefers predictable behavior over generic flexibility. New features should strengthen the command model, interaction safety, structured output, localization, or app-level testability.

## Related Docs

- [Command apps](../guides/command-apps.md)
- [Interaction modes design](interaction-modes.md)
- [Localization design](localization.md)
- [Structured output](../guides/structured-output.md)
