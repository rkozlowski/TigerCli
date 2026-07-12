# TigerCli Documentation

TigerCli documentation is organized by how close each page is to day-to-day app development.

## Recommended Reading Path

1. [Getting started](getting-started.md)
2. [Folder Copy sample](examples/folder-copy.md)
3. [Command apps](guides/command-apps.md)
4. [Arguments and options](guides/arguments-and-options.md)
5. [Interaction modes](guides/interaction-modes.md)
6. [Prompting and providers](guides/prompting-and-providers.md)
7. [Exit codes](guides/exit-codes.md)
8. [Localization](guides/localization.md)
9. [App testing](guides/app-testing.md)

## Examples

- [Getting started / ROI Cities](getting-started.md) - the primary command-app example: app factory, `list`/`show`, selectors, providers, command menu, structured output, exit codes, and app-boundary tests
- [Folder Copy](examples/folder-copy.md) - real-operation sample: default command, folder-select options, folder picker prompts, `RunActivityAsync`, progress bars, cancellation, non-interactive execution, and TigerCli-free planner tests
- [Rendered examples](examples/README.md) - generated HTML/PNG artifacts of real TigerCli rendering

## Guides

Guides are the practical docs for building applications with TigerCli.

- [Using TigerCli with AI coding agents](ai-usage.md) - compact implementation rules for AI-generated TigerCli apps
- [Command apps](guides/command-apps.md) - build TigerCli command apps with settings, command paths, options, help, exit codes, localization, and tests
- [Arguments and options](guides/arguments-and-options.md) - bind positional arguments, options, repeated values, key-value options, prompts, and validation
- [Interaction modes](guides/interaction-modes.md)
- [Prompting and providers](guides/prompting-and-providers.md) - collect missing values safely with parser-driven prompts and provider-backed choices
- [Semi-interactive prompts](guides/semi-interactive-prompts.md) - call direct TigerTui select, input, confirm, and multi-select prompts safely
- [Exit codes](guides/exit-codes.md) - use typed enum exit codes, framework policy mapping, and generated --help-errors
- [Localization](guides/localization.md) - configure cultures, localize command output with T/F/E, and use TigerText for app metadata
- [App testing](guides/app-testing.md) - test real TigerCli apps without the real console
- [CRUD command apps](guides/crud-commands.md) - map list/show/add/edit/delete onto CliList, CliDetails, prompts, and `.AsEdit()`
- [Structured output](guides/structured-output.md) - render markup, grids, tables, lists, details, and testable command output
- [Themes, styles & colors](guides/themes-and-styles.md) - raw colors vs semantic styles, custom styles, color aliases, theme families, disabling themes, and opt-in style packages
- [CliTable](guides/cli-table.md) - render table-shaped command output with columns, records, frames, and testable lines

## Reference

Reference docs describe public contracts, framework rules, and stable behavior that app authors and maintainers can rely on.

- [API map](reference/api-map.md) - compact public type map generated from DocFX metadata
- [Generated API reference (DocFX)](api-docfx/README.md) - generate a local, browsable API reference from source and XML doc comments
- [HtmlSink](reference/html-sink.md) - render TigerCli output to deterministic HTML for snapshot tests and documentation examples
- [PngSink](reference/png-sink.md) - optional SkiaSharp-based PNG rendering package for visual documentation artifacts
- [Help rendering trust model](reference/help-rendering-trust-model.md)
- [CliColor reference (ANSI 0–255 swatches)](reference/cli-color.html) - generated visual reference for the full palette
- [Rendered examples](examples/README.md) - generated HTML artifacts of real TigerCli rendering (markup, table presets, CliList, CliDetails)

## Design

Design docs explain why TigerCli works the way it does. They summarize rationale, tradeoffs, and architectural boundaries without duplicating the guides.

- [Command processing positionals](design/command-processing-positionals.md)
- [Command processing prompting](design/command-processing-prompting.md)
- [Interaction modes](design/interaction-modes.md)
- [Localization](design/localization.md)
- [Semi-interactive TUI](design/semi-interactive-tui.md)
- [CliGrid measurement ownership](design/cli-grid-measurement-ownership.md)
- [Activity and progress](design/activity-progress.md)
- [Inline widget composition](design/inline-widget-composition.md)
- [Themes and styles](design/themes-and-styles.md)
- [Overlays](design/overlays.md)
- [Documentation artifacts](design/doc-artifacts.md)
- [Context and architecture](design/context.md)

## Contributing

Contributing docs describe repository workflow, testing strategy, and expectations for humans and agents working on TigerCli itself.

- [Unit testing](contributing/unit-testing.md)
- [AI agents](contributing/ai-agents.md)
- [Creating issues](contributing/creating-issues.md)
- [Pull request requirements](contributing/pull-request-requirements.md)
