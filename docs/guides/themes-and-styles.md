# Themes, Styles & Colors

TigerCli separates five distinct concepts so terminal styling stays explicit and TigerCli-owned:

| Concept | What it is | Where it is valid | Registered by |
|---|---|---|---|
| **Raw color** | a concrete terminal color ([CliColor](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliColor.html) enum value) | raw color positions: `[Yellow]`, `[on Blue]`, `[Yellow on Blue]` | built-in (the `CliColor` enum) |
| **Raw color alias** | an app name for a raw color | raw color positions, like any color | the app (`RegisterColorAlias`) |
| **Framework semantic style** | a theme role (`Accent`, `Panel`, …) resolved through the active theme | single-token semantic tags: `[Accent]` | built-in (curated subset) |
| **Custom semantic style** | an app-defined theme role | single-token semantic tags: `[ConnectionName]` | the app (`RegisterCustomStyle`) |
| **Theme** | a full palette mapping [ThemeStyle](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.ThemeStyle.html) roles to colors | selected via `--theme` / `TIGERCLI_THEME` / `CurrentTheme` | framework + app (`AddTheme`) |

TigerCli core ships **no** Spectre/CSS-style color aliases and no automatic compatibility mode. Referencing a library never changes your app's styling — every alias, style, and theme is registered explicitly, app-by-app.

## Raw colors vs semantic styles

Raw colors are colors. Semantic styles are theme roles and may stand for a foreground, a background, or both.

```csharp
TigerConsole.MarkupLine("[Yellow]warn[/]");          // raw color
TigerConsole.MarkupLine("[Yellow on Blue]warn[/]");  // raw fg/bg
TigerConsole.MarkupLine("[Accent]Name[/]: value");   // semantic style (resolved by the theme)
```

A semantic style is **always a single token** and is never valid inside an `on` color expression. Compose styles with **nesting**, not by mixing tokens:

```csharp
TigerConsole.MarkupLine("[Panel][Accent]Local[/][/]");   // Accent foreground on the Panel surface
TigerConsole.MarkupLine("[Bold][Accent]Title[/][/]");    // Bold + Accent
```

Invalid forms (throw `FormatException`):

```text
[Accent on Panel]          # semantic names are not colors → [Panel][Accent]…[/][/]
[Bold Accent]              # semantic mixed into a raw expression → [Bold][Accent]…[/][/]
[ConnectionName on Blue]   # a custom style is semantic → [on Blue][ConnectionName]…[/][/]
[Heading Red on Blue]      # a semantic role is standalone-only → [Heading]…[/]
[Key Bold]                 # likewise → [Bold][Key]…[/][/]
```

### Framework semantic tokens

The curated framework semantic tokens resolve through the active theme. The token **name** is framework-known, but its **visual meaning is theme/developer-configurable** — `[Key]abc[/]` means "render `abc` using the active theme's `Key` style", never a hardcoded color. Override any of them on a `ThemeBase` subclass.

| Token | Theme role | Default (overridable) |
|---|---|---|
| `[Accent]` `[Muted]` `[Success]` `[Warning]` `[Error]` | base accents/ink | accent / muted / success / warning / error inks |
| `[Selected]` `[Alert]` | fg + bg inks | selection / attention inks |
| `[Panel]` `[Dialog]` | surface backgrounds | elevated / dialog surfaces |
| `[Heading]` | section/list/details headings | `Accent` + bold |
| `[Key]` | identity/anchor values (IDs, names, codes, slugs) | `Accent` |
| `[Value]` | normal field values | `Text` |
| `[Path]` | filesystem/local paths | `MutedText` |
| `[Link]` | navigable/link values | `Accent` + underline |

```text
[Heading]Devices[/]
[Key]camera-main[/]
[Value]Sony A7IV[/]
[Path]/media/inbox[/]
[Link]https://example.com[/]
```

`[Link]` renders the URL as **visible, copyable text** in every sink (with the theme's Link styling). Clickability is a **progressive enhancement**: where the ANSI sink is active, TigerCli additionally wraps the visible text in an [OSC 8 hyperlink](#clickable-links-osc-8) whose target is the link's own visible text, so supporting terminals make it clickable. The URL always remains visible and copyable — TigerCli never hides the link target behind alternate display text. Nested links (`[Link]…[Link]…[/]…[/]`) are not supported and are rejected, and there is no hidden-target syntax (`[Link=…]label[/]` is not supported yet).

### Clickable links (OSC 8)

A text run with a resolved hyperlink target — from `[Link]…[/]` markup, or from a structured `CliDetails.AddLink` / `CliList.AddLinkColumn` value — can be emitted as a clickable [OSC 8 hyperlink](https://gist.github.com/egmontkob/eb114294efbcd5adb1944c9f3cb5feda) by `AnsiSink`. This is governed by `TigerConsole.HyperlinkMode` ([CliHyperlinkMode](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliHyperlinkMode.html)):

| Mode | Behavior |
|---|---|
| `Auto` *(default)* | Emit OSC 8 only when ANSI support was **capability-detected** for the stream (the same `Auto` path that selects the ANSI sink). When ANSI is force-enabled or support is otherwise uncertain, no hyperlink escape sequences are emitted — only visible text. |
| `Never` | Never emit OSC 8; render link text visibly without escape sequences. |
| `Always` | Emit OSC 8 whenever the ANSI sink is used. |

Regardless of mode, the link **text is always written visibly and copyably** — clickability is the only thing this setting controls. Targets are sanitized (control characters, including `ESC`, are stripped) before they enter the escape sequence, and an open link is always closed at a target change, newline, reset, or flush so it cannot bleed. TigerCli does not invent terminal-specific hyperlink probing beyond its existing ANSI capability model.

```csharp
TigerConsole.HyperlinkMode = CliHyperlinkMode.Always;   // force clickable links on the ANSI sink
TigerConsole.HyperlinkMode = CliHyperlinkMode.Never;     // visible text only, no OSC 8
```

### Decoration shorthands

Full decoration names work standalone and inside composed visual expressions:

```text
[Bold]Bold[/]
[Bold Red on Blue]Error![/]
```

The short aliases `[b]`, `[i]`, `[u]` (case-insensitive — `[B]`, `[I]`, `[U]`) are convenience equivalents of `[Bold]`, `[Italic]`, `[Underline]`. They are **standalone-only**: compose them by nesting, not by mixing tokens.

```text
[b]Bold[/]
[i]Italic[/]
[u]Underline[/]
[B][Red ON Blue]Error![/][/]      # standalone alias wrapping a composed color expression
```

Not supported (throws `FormatException`) — short aliases never participate in composed expressions:

```text
[b red on blue]Error![/]
[i yellow]Warning[/]
[u link]Docs[/]
```

## Configuring an app's appearance

All registration happens through the app builder's `ConfigureThemes` block. The app owns the policy:

```csharp
var app = TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .ConfigureThemes(themes =>
    {
        // Raw color aliases (color category).
        themes.RegisterColorAlias("BrandBlue", CliColor.Blue1);

        // Custom semantic styles (style category).
        themes.RegisterCustomStyle("ConnectionName", ThemeStyle.Accent,
            darkStyle: new CliCellStyle(new CliCharStyle(CliColor.Cyan)),
            lightStyle: new CliCellStyle(new CliCharStyle(CliColor.DarkBlue)));

        themes.RegisterCustomStyleForTheme("ConnectionName", "tiger-blue",
            new CliCellStyle(new CliCharStyle(CliColor.White)));

        // Theme policy.
        themes.AddTheme(new CompanyDarkTheme());
        themes.DisableTheme("tiger-blue");
    })
    .SetDefaultCommand<MyCommand>()
    .Build();
```

`ConfigureThemes` may be called multiple times; each call further configures the same app-scoped registry. The configured aliases, styles, and themes become active for the run — nothing is process-global except while the app is running.

## Raw color aliases

Color aliases are application names for raw `CliColor` values — useful for brand colors, domain-friendly names, or migration layers.

```csharp
themes.RegisterColorAlias("BrandOrange", CliColor.Sand2);
themes.RegisterColorAlias("CompanyBlue", CliColor.Blue1);
```

```text
[BrandOrange]text[/]
[White on CompanyBlue]text[/]
[BrandOrange on CompanyBlue]text[/]
```

- Alias names are **case-insensitive** and must be alphanumeric (no whitespace/punctuation, and not the reserved keywords `on`/`bold`/`italic`/`underline`).
- **Precedence:** a registered alias wins over a `CliColor` enum name. Registering an alias named `Red` is deliberate app policy, so the alias wins; the enum `CliColor.Red` remains the fallback when no alias is registered.
- Aliases are valid **only** in raw color positions, never as semantic tags.

## Custom semantic styles

A custom style is an app-defined theme role. It always has a required framework `ThemeStyle` base fallback, plus optional dark/light [TigerThemeFamily](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.TigerThemeFamily.html) overrides and optional exact theme-name overrides.

```csharp
RegisterCustomStyle(string name, ThemeStyle baseStyle,
    CliCellStyle? darkStyle = null, CliCellStyle? lightStyle = null);

RegisterCustomStyleForTheme(string styleName, string themeName, CliCellStyle style);
```

Used as single-token tags:

```text
[ConnectionName]Local[/]
[EnvironmentProd]PROD[/]
[Panel][ConnectionName]Local[/][/]
```

A custom style may define a foreground, a background, and/or text decorations. With only a base style, it tracks that theme role on every theme:

```csharp
themes.RegisterCustomStyle("ConnectionName", ThemeStyle.Accent);
// → every theme's Accent (Cyan on dark, DarkBlue on light, …)
```

### Resolution order

For a given active theme, a custom style resolves in this order (most specific first):

1. **Exact theme-name override** (`RegisterCustomStyleForTheme`, matched on `ITheme.Name`, case-insensitive).
2. **Dark/light family override** (`darkStyle` / `lightStyle`, chosen by the theme's [family](#theme-families)).
3. The active theme's resolved **base `ThemeStyle`**.

```csharp
themes.RegisterCustomStyle("ConnectionName", ThemeStyle.Accent,
    darkStyle: new CliCellStyle(new CliCharStyle(CliColor.Cyan)),
    lightStyle: new CliCellStyle(new CliCharStyle(CliColor.DarkBlue)));

themes.RegisterCustomStyleForTheme("ConnectionName", "tiger-blue",
    new CliCellStyle(new CliCharStyle(CliColor.White)));
```

| Active theme | Result |
|---|---|
| `tiger-blue` | exact override → White |
| any other dark-family theme | dark override → Cyan |
| any light-family theme | light override → DarkBlue |
| theme with no matching override | falls back to that theme's `Accent` |

Custom styles may carry [CliTextDecoration](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliTextDecoration.html) flags:

```csharp
themes.RegisterCustomStyle("DangerZone", ThemeStyle.Error,
    darkStyle: new CliCellStyle(new CliCharStyle(
        CliColor.Red, decorations: CliTextDecoration.Bold | CliTextDecoration.Underline)));
```

Custom style names are **case-insensitive**, must be alphanumeric, may not be reserved keywords (`on`, `bold`, `italic`, `underline` — and, as standalone tags, the short aliases `b`/`i`/`u`), and **may not collide with a framework semantic token** (`Accent`, `Muted`, `Success`, `Warning`, `Error`, `Selected`, `Alert`, `Panel`, `Dialog`, `Heading`, `Key`, `Value`, `Path`, `Link`).

## Theme families

Every theme declares a contrast **family** — dark or light — so a custom style needs only one dark/light override pair instead of one override per theme. Family is metadata, not inheritance; sealed themes stay sealed.

| Theme | Family |
|---|---|
| `DarkTheme` | Dark |
| `TigerBlueTheme` | Dark (signature theme, still dark-family) |
| `LightTheme` | Light |

A custom theme declares its family by overriding `Family` (defaults to `Dark`):

```csharp
public sealed class CompanyLightTheme : ThemeBase
{
    public override string Name => "company-light";
    public override TigerThemeFamily Family => TigerThemeFamily.Light;
    // … role overrides …
}
```

## Themes: enabled vs disabled

Framework themes (`dark`, `light`, `tiger-blue`) are available by default. An app may disable any theme it does not want:

```csharp
themes.DisableTheme("tiger-blue");
```

A disabled theme behaves exactly like an unknown theme for that app:

```text
--theme tiger-blue            → error (unsupported theme)
TIGERCLI_THEME=tiger-blue     → error (invalid theme)
```

…and it is omitted from `--help`. Disabling is app policy: the theme stays registered globally, but it is unavailable for this app's run.

## Opt-in theme/style packages

Libraries that bundle themes, custom styles, or color aliases ship **extension methods** on `TigerThemeConfiguration` — they never register anything on their own. Referencing the package does nothing; calling the method is the explicit opt-in.

```csharp
// In a library:
public static class CompanyThemePackage
{
    public static TigerThemeConfiguration AddCompanyThemes(this TigerThemeConfiguration themes)
    {
        themes.AddTheme(new CompanyDarkTheme());
        themes.AddTheme(new CompanyLightTheme());
        themes.RegisterCustomStyle("ConnectionName", ThemeStyle.Accent);
        return themes;
    }

    // A migration helper that restores Spectre-style color names, if an app wants them.
    public static TigerThemeConfiguration AddSpectreCompatibilityAliases(this TigerThemeConfiguration themes)
    {
        themes.RegisterColorAlias("aqua", CliColor.Cyan);
        themes.RegisterColorAlias("navy", CliColor.DarkBlue);
        // … etc …
        return themes;
    }
}
```

```csharp
// In the app — explicit opt-in:
builder.ConfigureThemes(themes =>
{
    themes.AddCompanyThemes();
    themes.AddSpectreCompatibilityAliases();
});
```

This keeps ownership with the application: TigerCli ships a clean vocabulary, libraries offer opt-in helpers, and the app decides what themes, aliases, and styles are available.

## API summary

- `TigerCliAppBuilder.ConfigureThemes(Action<TigerThemeConfiguration>)`
- `TigerThemeConfiguration` — `AddTheme`, `DisableTheme`, `RegisterColorAlias`, `RegisterCustomStyle`, `RegisterCustomStyleForTheme`, plus the `ColorAliases` / `CustomStyles` registries.
- `TigerColorAliasRegistry` (`IColorAliasResolver`) — raw color aliases.
- `TigerCustomStyleRegistry` / `CliCustomStyle` — custom semantic styles.
- `TigerThemeFamily`, `ITheme.Family` — theme contrast classification.
- `TigerConsole.ColorAliases` / `TigerConsole.CustomStyles` — the active registries used by markup.

See [structured output](structured-output.md) for the markup grammar and [api-map.md](../reference/api-map.md) plus the generated DocFX API reference for the full member reference.
