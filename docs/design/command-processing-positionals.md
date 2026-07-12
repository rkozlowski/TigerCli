# Command Processing Positionals Design

## Purpose

This page explains why TigerCli uses a strict command-line shape for command paths, positional arguments, and options. For usage details, see [arguments and options](../guides/arguments-and-options.md).

## Core Idea

TigerCli command lines follow one predictable shape:

```text
app <command-path> <positional-arguments> [options]
```

Command paths select behavior. Positional arguments provide required command context. Options modify the selected command.

## Why This Exists

TigerCli is script-safe first. A strict shape keeps command parsing predictable for users, tests, generated help, and parser-driven prompts. It also makes dependent prompts possible because required context is resolved before optional modifiers.

## Design Decisions

- Command paths are token-based and may contain multiple tokens.
- Each registered command name is a single token; multi-token paths are represented by explicit command groups (`AddCommandGroup(...)`) and their child commands, not flattened multi-token `AddCommand(...)` names.
- The longest matching command path wins.
- Positional arguments come after the command path.
- Positional arguments are always required.
- Options come after positionals and are unordered relative to other options.
- Once option parsing starts, later positional values are rejected.

## Prompting Implications

Prompt order follows command meaning, not raw option order:

1. Missing positional arguments, by index.
2. Missing required options.
3. Optional promptable options.

This supports flows where later prompts depend on earlier context, such as choosing a project after choosing a connection.

## Boundaries

TigerCli intentionally does not support freely interleaving positionals and options. That flexibility would make generated help, prompt ordering, and app-level tests less predictable.

## Related Docs

- [Arguments and options](../guides/arguments-and-options.md)
- [Prompting and providers](../guides/prompting-and-providers.md)
- [Command processing prompting](command-processing-prompting.md)
