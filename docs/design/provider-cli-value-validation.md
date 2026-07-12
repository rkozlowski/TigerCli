# Provider-Backed CLI Value Validation

This page describes the implemented rules for validating supplied command-line values against
provider choices. Prompt behavior is covered in [prompting and providers](../guides/prompting-and-providers.md).

## Validated Members

- A scalar option with a configured `Provider` validates its supplied value when the option is
  editable and `ValidateAgainstProvider` is `true`.
- A positional argument with an explicit `Provider` validates its supplied value in normal command
  execution when `ValidateAgainstProvider` is `true`.
- Implicit or name-matched provider discovery is a prompting convenience. It does not by itself make
  a supplied positional value authoritative or provider-validated.
- Missing and empty values continue through the normal required-value rules. Provider validation
  applies when the member has a value to validate.

`ValidateAgainstProvider = false` makes a configured scalar provider suggestions-only where the
attribute supports that switch. The supplied value then remains available to the handler without
provider membership validation.

## Selectors And Edit Commands

Positional arguments commonly act as selectors: they identify the object or key a command operates
on. In normal commands, an explicitly provider-backed selector follows the argument validation rule
above.

Edit selectors are loader-authoritative. A supplied edit selector is passed to the edit loader,
which decides whether the target exists and returns the edit not-found outcome when it does not.
Provider validation does not replace that lookup.

`EditProvider` applies only in edit mode. When configured, it overrides `Provider` there; outside
edit mode it is ignored and the normal `Provider` applies. A missing promptable edit selector may use
the effective edit provider before the loader runs, but the loader remains authoritative for the
selected or supplied target.

## Matching And Canonical Values

Supplied values are matched by the member's `ValueMatching` preset:

- `Default` matches string keys and labels case-insensitively and keeps type-safe equality for
  non-string keys.
- `Exact` uses case-sensitive string matching.
- `IgnoreCase` explicitly selects case-insensitive string matching.
- `FileSystemPath` uses platform-appropriate path matching.

A successful match binds the provider's canonical key, not the caller's original spelling or the
display label. This keeps handler input stable even when labels are localized or aliases differ in
case.

## Failure Behavior

Validation runs for interactive and non-interactive command execution. In non-interactive mode an
unknown supplied provider value fails before the handler runs with the localized invalid-provider
message and `TigerCliExitKind.ValidationError`; TigerCli does not prompt for a replacement.

Provider configuration failures are validation errors. Other provider failures map through the
framework's provider error handling, and cooperative cancellation propagates normally. A provider
that returns no choices leaves scalar validation with no choices to validate against.

## Multi-Select Values

Provider-backed multi-select options resolve every supplied token through the configured
`ValueMatching` preset. Unknown tokens fail with `ValidationError`, matched tokens bind canonical
provider keys, and duplicate tokens that resolve to the same canonical key collapse to one bound
value.

For string collections, `[TigerCliMultiSelect(AllowCustomValues = true)]` accepts custom tokens
instead of requiring provider membership. Empty-selection behavior continues to follow the
multi-select attribute's configured rules.
