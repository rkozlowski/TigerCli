# Creating Issues

## Purpose

Issues should help improve TigerCli without fighting documented design decisions. TigerCli is opinionated; not every different preference is a bug.

Use issues to report concrete problems, propose scoped improvements, or discuss design changes that fit TigerCli's model.

## Before Opening An Issue

- Search existing issues if applicable.
- Check the relevant docs, guides, and design notes.
- Verify whether the behavior is documented as intentional.
- Try to reproduce on the latest code.
- If possible, run:

```bash
dotnet build TigerCli.sln
dotnet test
```

## Do Not Open Issues For Documented Design Decisions

If behavior is clearly documented as a design decision, do not open a bug saying it should work differently.

Examples of intentional TigerCli decisions:

- TigerCli uses async command handlers.
- `--non-interactive` is framework-owned.
- Non-interactive mode must not prompt.
- Command shape is `app <command-path> <positionals> [options]`.
- Localized enum labels are display-only and are not command-line values.
- `DescriptionAttribute` is literal and not localization magic.
- `CliGrid` is low-level infrastructure, not the default app-facing output API.
- TigerCli is not a full-screen TUI framework.

A design discussion may still be useful when it explains a real TigerCli use case and proposes a change consistent with the Tiger way.

## Good Issue Types

- Bug report
- Documentation issue
- Missing test or regression
- API polish before hardening
- Feature proposal aligned with TigerCli's scope
- Design discussion

## Bug Report Checklist

Include:

- expected behavior
- actual behavior
- minimal reproduction
- command line used
- interaction mode
- culture used, if relevant
- terminal and OS, if relevant
- stdout/stderr snippets
- exit code
- whether it reproduces in non-interactive mode
- relevant test case, if possible

## Feature Proposal Checklist

Include:

- problem being solved
- why the existing TigerCli model does not cover it
- why it belongs in TigerCli rather than app code
- how it affects script safety
- how it affects semi-interactive behavior
- how it affects testing
- how it affects localization, help, or exit codes, if relevant
- whether it conflicts with documented design decisions

## Documentation Issue Checklist

Include:

- page/path
- unclear or wrong section
- expected clarification
- whether code behavior or docs are wrong

## AI-Agent-Created Issues

- AI-generated issues must be reviewed by a human before submission.
- Do not file speculative issues from AI guesses.
- Include file paths and exact behavior.
- Prefer a failing test or minimal reproduction over broad claims.

## Issue Quality Rules

- Use one issue per topic.
- Keep the title specific.
- Do not mix unrelated bugs and feature requests.
- Do not use issues as a scratchpad for vague ideas.
- Do not request generic flexibility without a concrete TigerCli use case.
- Use en-US English in public issue text where practical.

## Related Docs

- [Documentation index](../README.md)
- [Pull request requirements](pull-request-requirements.md)
- [Command apps](../guides/command-apps.md)
- [Interaction modes](../guides/interaction-modes.md)
- [Prompting and providers](../guides/prompting-and-providers.md)
- [Localization](../guides/localization.md)
- [Context and architecture](../design/context.md)
