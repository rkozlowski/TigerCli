# Localization Design

## Purpose

This page explains TigerCli's localization model and boundaries. It is design rationale, not the full usage guide.

## Core Idea

TigerCli is not a localization framework. It is a locale-aware CLI framework that makes command output, help, prompts, and enum metadata consistent for CLI apps.

## Why This Exists

.NET resource plumbing is functional, but the developer experience is awkward for command-line apps. TigerCli provides a simpler authoring model for common CLI text while still using standard .NET resource infrastructure.

## Key Design Decisions

- The default culture is `en-US` only.
- Apps opt into supported cultures.
- `--culture` is framework-owned.
- Framework text and app text use the same resolved run culture.
- `settings.T(...)`, `settings.F(...)`, and `settings.E(...)` use source-text localization.
- `TigerTextAttribute` uses source-text localization by default.
- `DescriptionAttribute` remains literal metadata; there is no localization magic.
- `DisplayAttribute` remains a standard .NET fallback.
- Localized labels are display-only and are not command-line tokens.

## Non-Goals

TigerCli does not provide:

- a pluralization engine
- ICU MessageFormat
- translator workflow tooling
- automatic extraction
- arbitrary external localization systems

## Related Docs

- [Localization guide](../guides/localization.md)
- [Help rendering trust model](../reference/help-rendering-trust-model.md)
- [App testing](../guides/app-testing.md)
