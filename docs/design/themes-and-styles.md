# Themes and Styles Design

TigerCli separates semantic styles from raw colors. Applications own theme, style, and color-alias policy through the app builder.

## Current Model

- Framework semantic roles are represented by `ThemeStyle`.
- Semantic markup tokens such as `[Accent]`, `[Success]`, `[Key]`, `[Path]`, and `[Dialog]` resolve through the active theme.
- Raw color names and registered raw color aliases are used only in raw color positions.
- App-level registration happens through `TigerCliAppBuilder.ConfigureThemes(...)`.
- `TigerThemeConfiguration` owns custom themes, disabled themes, color aliases, and custom semantic styles for the app.

## Semantic Styles vs Raw Colors

Semantic styles are visual roles, not colors. They are standalone markup tokens:

```text
[Accent]value[/]
[Panel][Key]id[/][/]
```

Raw colors and aliases are concrete terminal colors:

```text
[Yellow]warning[/]
[White on BrandBlue]label[/]
```

Semantic styles are intentionally not valid inside raw foreground/background syntax such as `[Accent on Panel]`.

## Registration Ownership

- TigerCli ships framework themes and semantic roles.
- TigerCli core does not ship compatibility color-alias tables.
- Libraries may expose opt-in extension methods for registering aliases, styles, or themes.
- Referencing a library must not mutate a TigerCli app automatically.
- Registries are app-scoped, not process-global policy.

## Custom Styles

Custom semantic styles have a required `ThemeStyle` fallback plus optional dark/light and exact-theme overrides. Resolution order is:

1. exact theme-name override
2. theme-family override
3. active theme's resolved base `ThemeStyle`

This keeps custom app tokens theme-aware without forcing every app to define a full theme.

See also:

- [Themes, styles & colors](../guides/themes-and-styles.md)
- [Structured output](../guides/structured-output.md#semantic-theme-markup)
- [API map](../reference/api-map.md)
