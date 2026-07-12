# Pull Request Requirements

## Purpose

This checklist defines the expected shape of TigerCli pull requests from contributors and AI agents. Keep changes focused, testable, and aligned with TigerCli's opinionated model.

For issue reports and feature requests, see [creating issues](creating-issues.md).

## Before Opening A PR

- Keep the diff small and centered on one behavior, doc area, or API change.
- Run `dotnet build TigerCli.sln`.
- Run `dotnet test` unless the change is clearly documentation-only.
- Check generated output or screenshots when changing visible console rendering.
- Review the diff for unrelated formatting, generated files, or accidental churn.

## Code Requirements

- Follow existing C# style, nullable reference usage, and four-space indentation.
- Keep grid coordinates in `(column, row)` order.
- Fix rendering behavior at the source: measurement, style cascade, layout, or render buffering.
- Keep TigerCli opinionated. Do not add generic flexibility without a clear TigerCli use case.
- Prefer existing framework patterns over new parallel abstractions.

## Tests

- New command-processing behavior should include tests.
- App-level behavior should prefer `TigerCliAppTestHost` where appropriate.
- Pure rendering behavior should use deterministic rendering tests and snapshots.
- Semi-interactive behavior should use `TestShell` / `TestTerminal`.
- Do not use real console input or output in automated app tests.

## Documentation

- User-facing behavior changes should update the relevant guide docs.
- Design changes should update the relevant design doc when the rationale matters.
- Keep public docs concise and written in en-US English.
- Do not link public docs to internal-only notes.

## Public API Changes

- Public API changes must update XML documentation comments and, when the public API shape or docs change, the generated API map workflow.
- Add or update examples when a new public API changes the intended usage pattern.
- Avoid public APIs that expose implementation details without a clear app-author need.

## Localization/Resource Changes

- Update localization resources and tests when framework-owned text changes.
- Use en-US English in public code, docs, fallback text, and resources.
- Do not modify `pl-PL` resources unless intentionally changing localized text.
- Keep localized labels display-only; do not make them command-line tokens.

## Console/Output Rules

- Use `TigerConsole.MarkupLine(...)` and `TigerConsole.MarkupErrorLine(...)` for app and framework output paths.
- Do not use raw `Console.WriteLine(...)` for normal app/framework output.
- Escape dynamic values inserted into markup.
- Prefer `settings.E(...)` for localized markup command output with formatted dynamic values; use `CliMarkupParser.Escape(...)` for raw value escaping.

## AI-Generated Changes

- Read the local docs and surrounding code before editing.
- Do not invent new TigerCli patterns when an existing one fits.
- Do not expand scope into unrelated refactors.
- Do not commit, push, tag, or create branches unless explicitly instructed.
- Summarize changed files, why they changed, and verification results.

## Final Checklist

- `dotnet build TigerCli.sln` passes.
- `dotnet test` passes, or the PR explains why it was not needed.
- Public API changes update XML documentation comments and run the DocFX/API-map check when relevant.
- Relevant guides/design docs are updated.
- Localization resources/tests are updated when framework-owned text changes.
- Dynamic markup values are escaped.
- The diff is focused and free of unrelated churn.
