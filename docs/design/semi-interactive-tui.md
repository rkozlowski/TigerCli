# Semi-Interactive TUI Design

## Purpose

This page explains why TigerCli has inline semi-interactive controls and where they fit. For app-author usage, see [semi-interactive prompts](../guides/semi-interactive-prompts.md).

## Core Idea

Semi-interactive controls exist to help a human complete a command without turning the command into a full-screen application.

They are inline, modal, and short-lived. They support command execution; they are not the default application surface.

## Why This Exists

TigerCli is script-safe first. A command should run from provided arguments and options in automation. When a human omits input, the same command can ask for missing values if interaction policy allows it.

This keeps one command model for scripts, help, prompting, validation, exit codes, localization, and tests.

## Interaction Boundary

- `NonInteractive` must not render controls or read keys.
- `SemiInteractive` may render inline prompts.
- `FullInteractive` is reserved and is not implemented by the inline prompt shell. TigerCli's
  command flow does not require it.

Direct prompt helpers and parser-driven prompts both need to respect the active interaction mode.

## Rendering Boundary

Inline controls render through the same structured output infrastructure as other TigerCli components:

- controls return `CliGrid` through `CliRenderableComponent`
- dialogs host controls as subgrids
- measurement resolves wrapping, clipping, active points, and scrollable cells
- overlays provide visual adornments such as scrollbars and horizontal indicators

There is no separate prompt rendering path.

## Control Model

The current semi-interactive controls are intentionally small:

- select
- multi-select
- folder select
- confirm
- text input
- secret text input
- activity/progress dialog

They use keyboard input, confirmation/cancel results, optional timeout/cancellation, and app-testable shell abstractions.

## Design Rules

- Do not prompt in command handlers for values parser-driven prompting can collect.
- Do not bypass `--non-interactive`.
- Keep prompt labels display-only.
- Bind stable values, not localized labels.
- Keep inline controls modal and short-lived.
- Keep full-screen concerns outside the semi-interactive shell.

## Related Docs

- [Semi-interactive prompts](../guides/semi-interactive-prompts.md)
- [Prompting and providers](../guides/prompting-and-providers.md)
- [Interaction modes](interaction-modes.md)
- [Overlay design](overlays.md)
