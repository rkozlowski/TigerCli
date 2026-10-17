# Shell Completion

TigerCli apps can offer tab completion in Bash, Zsh, Fish, and PowerShell. Completion is opt-in,
static, and metadata-backed: it completes what the app's registration and settings attributes
already describe, using the same grammar the parser enforces.

```text
app <command-path> <positional-arguments> [options] [-- <raw-arguments>]
```

## Enable It

```csharp
return TigerCliApp.CreateBuilder()
    .UseAssemblyMetadata(typeof(MyApp).Assembly)
    .UseShellCompletion()
    .AddCommand<DeployCommand>("deploy", "Deploys a target.")
    .Build();
```

`UseShellCompletion()` adds one framework-owned root option, `--completion <shell>`, which prints
an activation script. The script registers completion for the app's **application name** (normally
`<AssemblyName>` through `UseAssemblyMetadata(...)`), so that name must be the command users type.

Supported shells: `bash`, `zsh`, `fish`, and `pwsh` (`powershell` is accepted as a synonym).

## Activate It In A Shell

Activation is a user or installer action: the app never edits shell profiles, writes completion
files, or changes the shell environment itself.

| Shell | Current session | Persistent |
|---|---|---|
| Bash 4+ | `source <(my-tool --completion bash)` | Add that line to `~/.bashrc`, or save the output as `~/.local/share/bash-completion/completions/my-tool`. |
| Zsh | `source <(my-tool --completion zsh)` (after `compinit`) | Add that line to `~/.zshrc` after `compinit`, or save the output as `_my-tool` in a directory on `$fpath`. |
| Fish | `my-tool --completion fish \| source` | Save the output as `~/.config/fish/completions/my-tool.fish`. |
| PowerShell 7+ / Windows PowerShell 5.1 | `my-tool --completion pwsh \| Out-String \| Invoke-Expression` | Add the guarded line below to `$PROFILE.CurrentUserAllHosts`, once in each edition you use. |

```powershell
if (Get-Command my-tool -ErrorAction Ignore) { my-tool --completion pwsh | Out-String | Invoke-Expression }
```

The guard keeps the profile quiet if `my-tool` is later uninstalled. Windows PowerShell 5.1 and
PowerShell 7 read different profile files, and Windows PowerShell's default `Restricted` execution
policy on Windows client editions runs no profile at all.

The script is a thin adapter that asks the app for candidates on every Tab press, so it keeps
working when the app's commands change. A line that generates it at shell start is always current.
A saved script must be regenerated when the app is upgraded.

Installers and anyone who needs per-machine activation, a saved script, or a clean uninstall
should follow [shell completion installation](../reference/shell-completion-installation.md). It
defines the generation contract and the verified Windows PowerShell lifecycle for install, upgrade,
repair, and uninstall.

## What Is Completed

| Position | Candidates |
|---|---|
| Command path | Top-level commands, groups, and aliases at the root; a group's child commands and subgroups after its path. |
| Positional argument | Enum member names and `true`/`false` for `bool?`. Other `string` positionals fall back to the shell's file completion. |
| Option name (word starts with `-`) | The selected command's options, contributed global options, execution options (`--non-interactive`, `--theme`, `--color`, `--no-color`, `--culture`), and `-h`/`--help`/`--help-errors`/`--help-env`. A command that declares a raw tail also offers `--`. |
| Option value | Enum member names (minus `ForbiddenValues`), `true`/`false` for `bool?`, the app's selectable themes, `auto`/`never`/`16`/`256` for `--color`, and the supported cultures for `--culture`. Both `--mode Fast` and `--mode=Fast` forms complete. |
| Root option | `--help`, `--help-errors`, `--help-env`, `--version`/`--version-full` when version output is enabled, and `--completion` with its shell names. |

Descriptions come from the same metadata as generated help, with markup removed. Bash cannot
display descriptions; Zsh, Fish, and PowerShell show them.

File and directory paths are never listed by TigerCli. When no candidates apply, the app tells the
shell which native fallback to use:

- `[TigerCliFileOpen]` / `[TigerCliFileSave]` options, `string`/`string[]` options, `string`
  positionals, and contributed global options fall back to **file** completion;
- `[TigerCliFolderSelect]` options fall back to **directory** completion;
- everything else (integers, provider-backed values, key-value options) gets **no** fallback.

## Grammar Rules Completion Respects

Completion offers only what the parser would accept at that position:

- Positionals come before options. Once an option appears, only options and option values are
  offered; a positional typed after an option makes the line invalid and nothing further is offered.
- Options are also valid before every positional is supplied (missing positionals are prompted or
  rejected at run time), so a word starting with `-` always offers the command's options.
- Unknown options make the line invalid. Already-used options are not offered again unless they are
  repeatable.
- A group prefix selects no command, so after `my-tool widgets` only child commands and the
  informational options are offered.
- Execution and contributed global options appear in a command's options area, never before the
  command path. `--version` and `--completion` are root-only.
- Prompt-only options (`AllowCommandLineValue = false`) are not offered.
- **Raw tail boundary:** everything after the first `--` belongs to the command's raw tail and is
  never completed as TigerCli input — not even option-like words. A command that declares
  `[TigerCliRawArguments]` gets the shell's file completion there; any other command gets nothing.

## What Completion Never Does

A completion request reads metadata only. It does not:

- construct command handlers or call command factories;
- apply contributed global options (their `apply` callbacks never run);
- prompt, render UI, set the terminal title, or register cancellation handlers;
- call value providers, so provider-backed arguments and options get no candidates.

This keeps Tab presses side-effect free and fast, and it is why completion is "static": candidates
that need app data (connections, cities, database names) are not offered yet. Prompting and
providers remain TigerCli's primary guided-input model; see
[prompting and providers](prompting-and-providers.md).

## How It Works

TigerCli uses a runtime metadata query rather than a script that embeds the command tree:

```text
Tab -> shell script -> my-tool --tigercli-complete <shell> _word1 ... _wordN -> candidates
```

- The script passes every word after the program name up to and including the word under the
  cursor. Each word is prefixed with `_`, so an empty current word, a literal `--`, and option-like
  words survive every shell's argument passing.
- The app answers with one candidate per line in that shell's format and a final `:none`,
  `:files`, or `:directories` fallback line. Bash, Zsh, and Fish answers are UTF-8, TigerCli's
  normal [redirected-output encoding](../reference/console-output-encoding.md). A PowerShell answer
  is written in the console's code page, because PowerShell decodes a program's output with that
  code page; a request never changes it.
- The PowerShell script selects UTF-8 for each request, so descriptions survive any console code
  page. Under Constrained Language Mode, which forbids that switch, answers still decode for every
  character the code page has. The script restores `$LASTEXITCODE` and the console encoding after
  each Tab press and records nothing in `$Error`.
- A request sets up no console state: no colour mode, theme registration, terminal title, or
  cancellation handler. It only reads metadata and writes the answer in its protocol's encoding.
- `--tigercli-complete` is a hidden, framework-owned entry point. It is not listed in help and
  exists only while completion is enabled.

A single C# implementation of the grammar serves all four shells and stays in sync with the binary.
Each Tab press costs one app start-up; that is the trade-off for always-current candidates and for a
model that can later add dynamic, provider-backed values without changing installed scripts.

## Reserved Names

With completion enabled, `--completion` and `--tigercli-complete` are framework-owned. `Build()`
throws when a command option or a contributed global option uses either name. Apps that do not call
`UseShellCompletion()` are unaffected.

## Limitations

- No dynamic values: provider-backed arguments and options, multi-select options, and edit providers
  get no candidates.
- Bash needs version 4 or later (`mapfile`, `compopt`); macOS's bundled Bash 3.2 is not supported.
- Path fallback for the inline `--option=<path>` form works in Bash only; in Zsh, Fish, and
  PowerShell use the separate form `--option <path>`.
- In PowerShell, a position with no candidates falls back to PowerShell's own path completion even
  when TigerCli requested no fallback, and a directory fallback offers files too.
- Windows PowerShell 5.1 does not call native completers for a word that is only `-` or `--`; type
  one more character (`--h<Tab>`). PowerShell 7 completes both.
- Under PowerShell's Constrained Language Mode (AppLocker or WDAC policy), candidates have no
  descriptions.
- Candidates are not quoted for the shell. Command names, option names, enum members, theme names,
  and culture names contain no spaces, so this matters only for app-defined enum or theme names
  with unusual characters.
- Completion uses the application name, not the path the binary was started from. Running through
  `dotnet run` or an unregistered alias gets no completion. The executable must be on `PATH` under
  that name; a Windows `App Paths` registration is not enough for PowerShell.

## Testing

Completion requests run through the normal app entry point, so `TigerCliAppTestHost` can assert the
protocol response directly:

```csharp
var result = await TigerCliAppTestHost
    .For(MyApp.Create())
    .WithArgs("--tigercli-complete", "bash", "_dep")
    .RunAsync();

Assert.Equal("deploy\n:none\n", result.StdOut);
```

The wire format is an internal contract between TigerCli and its own generated scripts. Prefer
asserting on a few representative candidates rather than on whole responses.
