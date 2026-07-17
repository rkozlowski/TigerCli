# Colour mode & automatic ANSI selection

TigerCli's default console output paths — `TigerConsole.RenderGrid(grid)` (no-sink overload),
`TigerConsole.Markup` / `MarkupLine`, and `TigerConsole.MarkupError` / `MarkupErrorLine` — choose
their render sink based on a process-global **colour mode** and, in `Auto`, the detected terminal
capability. Throughout this document `ESC` denotes the escape character `0x1B`.

> The inline semi-interactive TUI (cursor-addressed rendering via `ConsoleTerminal` / `InlineShell`)
> **resolves its sink through the same colour-mode policy**, so live dialogs, menus, and status rows
> use the same effective sink (ANSI 256 / 16-colour / plain) as the default output paths. The TUI's
> line clears are colour-mode-aware too: under ANSI they paint the theme background faithfully via
> SGR sequences instead of a `ConsoleColor` approximation.

## [CliColorMode](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliColorMode.html)

`TigerConsole.ColorMode` (`enum CliColorMode`, default `Auto`) selects the policy; [CliAnsiSupport](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.CliAnsiSupport.html) reports the independently resolved terminal capability.

| Mode | Output sink | Behaviour |
|---|---|---|
| `Auto` | `ConsoleSink` or `AnsiSink` | Detect per stream; upgrade to `AnsiSink` (faithful 256-colour) only when ANSI 256 is safely supported. Never emits ANSI to a redirected/captured stream. |
| `Never` | `TextWriterSink` | Plain text only — no escape sequences and no console colour changes. |
| `Standard16` | `ConsoleSink` / `ConsoleErrorSink` | The legacy behaviour: colour via the `Console.*Color` API; ANSI 16–255 degraded to the nearest `ConsoleColor`. |
| `Ansi256` | `AnsiSink` | Force faithful 256-colour ANSI to the stream, **even when redirected**. Explicit opt-in. |

`Never` and `Standard16` are distinct: `Never` produces no colour at all, while `Standard16` keeps
the current 16-colour console behaviour.

`ColorMode` is a process-global setting (like `TigerConsole.CurrentTheme`). Tests that change it
should restore the previous value in a `finally`.

## ConsoleSink vs AnsiSink

- `ConsoleSink` writes through the `Console.*Color` API. On Unix the runtime renders this as 16-colour
  ANSI and suppresses colour when output is redirected; on Windows it uses the Win32 console colour
  API. ANSI 16–255 colours are degraded to the nearest `ConsoleColor`.
- `AnsiSink` writes ANSI SGR escape sequences directly and renders the full 0–255 palette faithfully
  (see [ansi-sink.md](ansi-sink.md)). This is the only thing `Auto` upgrades to.

## Auto detection policy

`TerminalCapabilities` resolves a `CliAnsiSupport` (`None` / `Ansi16` / `Ansi256`) **independently for
stdout and stderr**. Under `Auto`, the factory upgrades to `AnsiSink` only when the result is
`Ansi256`; `Ansi16` and `None` keep `ConsoleSink`. Detection is **conservative about whether ANSI is
allowed at all, but practical once it is**: redirected streams, `NO_COLOR`, `CLICOLOR=0`, and
`TERM=dumb`/empty disable ANSI, but a normal interactive non-Windows terminal resolves to `Ansi256` —
`TERM` does **not** need to advertise `256color`.

Resolution order:

1. **`NO_COLOR`** present (any value) → `None`. Within `Auto` this wins over `FORCE_COLOR`; only an
   explicit `CliColorMode` (e.g. `Ansi256`) can still force ANSI.
2. **`CLICOLOR=0`** (and not forced) → `None`.
3. **`FORCE_COLOR`** or **`CLICOLOR_FORCE`** truthy (non-empty, not `0`) → `Ansi256`. Forcing ignores
   redirection.
4. **Redirected** stream → `None` (Auto never emits ANSI to a redirected/captured stream).
5. **Windows**: `Ansi256` if the VT probe succeeds for that handle, otherwise `None` (legacy console
   keeps 16-colour via the Win32 API).
6. **Non-Windows** interactive terminal: `None` if `TERM` is empty or `dumb`; otherwise `Ansi256`.

This is a pragmatic modern-terminal policy: common `TERM` values such as `xterm`, `screen`, `tmux`,
`linux`, `vt100`, `ansi`, `rxvt`, `alacritty`, `kitty`, and `wezterm` are all treated as 256-colour
capable under `Auto`. `COLORTERM` and a `256color` suffix in `TERM` remain valid positive signals but
are no longer required. Users who want the legacy 16-colour console can force it with `--color 16`, and
can disable colour entirely with `--color never` / `--no-color`.

### CI

CI is not special-cased. CI output is normally redirected/non-TTY, so `Auto` resolves to `None`
(no ANSI), preserving the current behaviour. Set `FORCE_COLOR=1` to opt into colour in CI.

## Windows VT

On Windows, emitting ANSI requires `ENABLE_VIRTUAL_TERMINAL_PROCESSING` on the console handle.
`TerminalCapabilities` runs a defensive, **cached** probe per stdout/stderr handle
(`GetStdHandle` → `GetConsoleMode` → `SetConsoleMode`). Any failure (legacy conhost, restricted
environment, redirected handle) falls back to `ConsoleSink`. The probe runs only for `Auto` (never
for `Never`/`Standard16`) and is a no-op on non-Windows platforms.

## The `--color` / `--no-color` option

Apps built with `TigerCliApp` expose a framework option:

```
--color auto|never|16|256
--no-color            # alias for --color never
```

- The CLI option wins over the app default (`TigerCliAppBuilder.SetColorMode(...)`).
- Environment variables influence `Auto` detection only; they do not override an explicit
  `--color`/`SetColorMode`.
- Recognized framework values and `--no-color` are stripped from the command's arguments.
- **Collision-friendly:** `--color` is only claimed by the framework when its value is a recognized
  mode (`auto`, `never`, `16`, `256`). Any other `--color <value>` is left for the application, so an
  app may keep its own `--color` option. (Consequently the four mode literals are reserved for an app
  that also defines `--color`.)

```csharp
var app = TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .SetColorMode(CliColorMode.Auto)     // app default (optional)
    .SetDefaultCommand<MyCommand>()
    .Build();

// my-tool --color 256   -> faithful 256-colour ANSI
// my-tool --no-color    -> plain text
```

## Theme preference

Colour capability and theme preference are separate decisions. TigerCli does not inspect the
terminal background and does not auto-detect whether the user prefers a light or dark theme.

Apps built with `TigerCliApp` expose `--theme <theme>` for a per-run override. Users can also set
`TIGERCLI_THEME` once to choose the default theme preference for runs that do not pass `--theme`.
The value is resolved through the same theme registry as `--theme`, so framework themes and
registered custom themes are both supported. Empty or whitespace-only `TIGERCLI_THEME` values are
ignored.

Theme precedence:

1. `--theme`
2. `TIGERCLI_THEME`
3. Existing app/default theme (`TigerConsole.CurrentTheme`, initially `dark`)

Examples:

```sh
TIGERCLI_THEME=light
TIGERCLI_THEME=dark
TIGERCLI_THEME=tiger-blue
```

## Examples

```csharp
// Force 256-colour ANSI for this process.
TigerConsole.ColorMode = CliColorMode.Ansi256;
TigerConsole.MarkupLine("[OceanBlue]Hello[/]");   // emits ESC[38;5;24m…

// Disable colour entirely.
TigerConsole.ColorMode = CliColorMode.Never;
TigerConsole.MarkupLine("[OceanBlue]Hello[/]");   // emits "Hello"
```

## See also

- [ansi-sink.md](ansi-sink.md) — the ANSI sink and its SGR mapping rules
- [API map](api-map.md) — compact index for `CliColorMode`, `CliAnsiSupport`, `TigerConsole.ColorMode`, `TerminalCapabilities`, and `TigerCliAppBuilder.SetColorMode`
- [DocFX API generation](../api-docfx/README.md) — local generated API reference workflow
