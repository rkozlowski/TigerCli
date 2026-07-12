# Interaction Modes Design

## Purpose

TigerCli separates interaction policy from command behavior so the same command remains safe in
automation and helpful to a person at a terminal.

## Current Model

TigerCli's supported application flow is one command invocation:

- `NonInteractive` executes without menus, prompts, or keyboard input.
- `SemiInteractive` may use the command menu and inline prompts, then executes one command and exits.

Both modes use the same parse, bind, validate, execute, output, and exit-code pipeline. Interaction
changes how missing input and activity presentation are handled; it does not create a second
business-operation path.

The command menu is current semi-interactive behavior. It selects one command before the normal
pipeline and does not turn the application into a persistent terminal session.

## Reserved Boundary

`FullInteractive` is reserved and does not provide a persistent session shell, terminal ownership,
or return-to-menu navigation. Applications must not rely on it as an implemented mode.

## Design Rules

- `--non-interactive` is framework-owned and always prevents prompting.
- Parser-driven and direct prompt helpers respect the resolved interaction mode.
- Semi-interactive menus and prompts support one command invocation.
- `TigerTui.RunActivityAsync` runs the same operation body headlessly when interaction is disabled.
- Command handlers do not reimplement interaction policy or duplicate work paths just to suppress UI.

## Related Docs

- [Interaction modes guide](../guides/interaction-modes.md)
- [Prompting and providers](../guides/prompting-and-providers.md)
- [Command processing prompting](command-processing-prompting.md)
