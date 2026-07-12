# TigerCli Help Rendering — Trust Model & Markup Safety

## Overview

All help output in TigerCli is rendered through `TigerConsole.MarkupLine`.
All framework error output is rendered through `TigerConsole.MarkupErrorLine`.
No `Console.WriteLine` or `Console.Error.WriteLine` is used in help or framework
error paths.

---

## Trusted Markup Sources

The following are allowed to contain TigerCli markup and are **not** escaped before
rendering. Framework-owned wrappers use **semantic theme tokens** (a curated subset
of theme styles — see [structured output](../guides/structured-output.md#semantic-theme-markup)),
which are resolved through the active theme (`TigerConsole.CurrentTheme`) rather than
fixed raw colours. Only this curated subset is exposed; table-only and other internal
theme styles are never reachable from markup.

| Source | Location |
|---|---|
| Framework wrappers around localized labels | Section headings wrap a resource-driven label in `[Accent]…[/]`, e.g. `[Accent]Usage:[/]`, `[Accent]Commands:[/]`, `[Accent]Arguments:[/]`, `[Accent]Options:[/]`, `[Accent]Prompted values:[/]`, `[Accent]Notes:[/]`, `[Accent]Exit codes:[/]`. Framework errors wrap the localized `Error_Prefix` in `[Error]…[/]`. The wrapping is trusted; the localized label inside is escaped. |
| Application description | `Description` passed to `AddDescription(...)` on `TigerCliAppBuilder` |
| Attribute descriptions | `TigerCliOptionAttribute.Description`, `TigerCliArgumentAttribute.Description` |
| Command descriptions | `Description` passed to `AddCommand(...)` via `TigerCliCommandRegistration` |
| Framework markers | `[Muted](default)[/]` for default command indicator (the literal `(default)` text is the resource value and is escaped) |
| Application links | Help footer URLs from `AddLink(...)`, `AddWebsite(...)`, `AddRepository(...)`, and `AddDocumentation(...)` are wrapped in TigerCli `[Link]…[/]` markup; visible URL text is escaped before insertion. |

The trusted surface is unchanged by the migration to semantic tokens: the same
framework wrappers are trusted, only the token names changed (`[cyan]`→`[Accent]`,
`[red]`→`[Error]`, `[DarkGray]`→`[Muted]`). Raw colour markup (e.g. `[yellow]…[/]`)
remains valid and is still resolved as before.

---

## Escaped Sources

All dynamically derived values are escaped via `CliMarkupParser.Escape(...)` before insertion into markup strings.

| Value | Example |
|---|---|
| Application name | `tiger-sqlcmd` |
| Application display name | `TigerWrap` |
| Application version | `1.2.3` |
| Application copyright | `Copyright (c) IT Tiger` |
| Application link labels and URLs | `Documentation`, `https://example.com/docs` |
| Command names | `run`, `migrate` |
| Positional argument display names | `connection`, `project-name` |
| Option aliases | `--connection`, `-c` |
| Prompted value display names | `password`, `connection-secret` |
| Value placeholders | `Silent\|Quiet\|Normal`, `connection-string` |
| Default values | `Normal`, `true` |
| ExactlyOneOf notes | `Exactly one of --file or --query must be specified.` |
| Exit-code enum type/member names | `ToolExitCode`, `InvalidArguments` |
| Exit-code enum descriptions | `DescriptionAttribute` values on enum types and members |

The `Esc(...)` private helper in `TigerCliApp` is a shorthand for `CliMarkupParser.Escape`.

---

## Display Tokens

Tokens like `[options]` and `<command>` in usage lines are display conventions, not TigerCli markup:

- `[options]` → rendered as `[[options]]` (escaped brackets) so it displays as literal `[options]`
- `<command>` → rendered as-is since `<` and `>` are not markup characters

---

## Localized Strings

Framework-owned text comes from `Resources/TigerCliStrings.resx` (en-US neutral)
and `Resources/TigerCliStrings.pl-PL.resx` (Polish satellite). The accessor
`TigerCliResources.Get(key, culture)` / `Format(key, culture, args)` requires an
explicit `CultureInfo`; the framework never reads `CultureInfo.CurrentUICulture`.

The resolved culture for a run is computed before any help or error is rendered:
`--culture` (if supported), else the app default. Help, parser errors, framework
validation errors, prompt-failure messages, the `--help-errors` framework
heading, built-in `--version` / `--version-full` lines, standard application link labels, and TUI built-in labels (`Yes`, `No`, MultiSelect hint, empty state) all
read from the resolved culture.

Resource values are treated as **plain text** unless explicitly wrapped in
markup at the call site (section headings wrap the localized label in
`[Accent]…[/]`; framework errors wrap the localized `Error_Prefix` in
`[Error]…[/]`; the default-command marker wraps the localized text in
`[Muted]…[/]`). These wrappers use semantic theme tokens resolved through the
active theme. The `Esc(...)` helper is applied to resource values that are
inserted into any markup-bearing string, so a hypothetical resource containing
`[` would not break parsing.

## Error Output

Framework parse, validation, prompt-failure, missing-argument, missing-option, and
unhandled-exception messages are rendered through `TigerConsole.MarkupErrorLine`
with a localized prefix (`Error:` / `Błąd:`) styled by the semantic `[Error]` token
(resolved through the active theme), so they share the markup parser with help output
but write to `Console.Error`.

The framework does not embed intentional markup in error messages; the rendered
text matches the existing user-visible strings (now sourced from resources). To
keep that contract under the markup parser, all dynamically derived values
inserted into error messages are escaped through `CliMarkupParser.Escape(...)`
(the `Esc(...)` helper in `TigerCliApp`). Escaping applies to:

| Source | Origin |
|---|---|
| `parseResult.Error` | Token, option-name, and key-value text built by the argument parser |
| `promptResult.Error` | Display name, alias, or provider message embedded in prompt-failure strings |
| `missingArgument.DisplayName` | Argument display names from `[TigerCliArgument(Name = "…")]` |
| `frameworkValidation.ErrorMessage` | Option aliases and forbidden-value text from framework validation |
| `validation.ErrorMessage` | User-supplied messages returned by `TigerCliSettings.Validate()` |
| `ex.Message` | Exception messages from command handlers |

Static framework message text such as `"Validation error: "` and
`"Missing required argument: "` is localized resource text and does not contain
intentional markup. The outer error prefix is localized separately and styled
by TigerCli.

---

## Rendering Methods

| Method | Role |
|---|---|
| `PrintHelp` | Root and command-specific help entry point |
| `PrintApplicationMetadataFooter` | Renders configured copyright and application links |
| `PrintVersion` | Renders opt-in `--version` and `--version-full` output |
| `PrintOptions` | Renders argv-capable options with aliases, descriptions, defaults, and prompt-only options under `Prompted values:` without option tokens |
| `PrintArguments` | Renders the Arguments section with positional argument names and descriptions |
| `PrintExactlyOneOfNotes` | Renders the Notes section for ExactlyOneOf constraints |
| `PrintHelpOnlyOption` | Renders minimal help when no commands/options exist |
| `PrintExitCodeHelp` | Renders `--help-errors` output from enum metadata |
