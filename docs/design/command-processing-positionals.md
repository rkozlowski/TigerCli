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
- Framework-owned execution and presentation options, such as `--non-interactive`, `--culture`,
  `--theme`, and `--color`, follow the same placement rule. Their meaning is app-wide; their syntax
  is still that of an option.
- App-contributed global options follow the same rule and do not participate in command selection.

For example:

```text
file-copy copy-to-file source.txt target.txt --non-interactive
my-app run positional --acme-config settings.json
```

The following forms are invalid because an execution option appears before the command path or
before required positional context:

```text
file-copy --non-interactive copy-to-file source.txt target.txt
my-app run --acme-config settings.json positional
```

Root informational forms such as `app --help`, `app --version`, `app --help-errors`, and
`app --help-env` are handled
only as root requests when no command is being executed. They do not create an exception for
command execution: command help sections are written as `app command positional --help`,
`app command positional --help-errors`, or `app command positional --help-env`, and
`app --help-env command` is invalid.

## Prompting Implications

Prompt order follows command meaning, not raw option order:

1. Missing positional arguments, by index.
2. Missing required options.
3. Optional promptable options.

This supports flows where later prompts depend on earlier context, such as choosing a project after choosing a connection.

## Boundaries

TigerCli intentionally does not support freely interleaving positionals and options or extracting
app-wide execution options before command selection. That flexibility would make generated help,
prompt ordering, and app-level tests less predictable.

## Related Docs

- [Arguments and options](../guides/arguments-and-options.md)
- [Prompting and providers](../guides/prompting-and-providers.md)
- [Command processing prompting](command-processing-prompting.md)
