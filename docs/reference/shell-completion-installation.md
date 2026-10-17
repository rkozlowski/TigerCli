# Shell Completion Installation

This page is for installer authors (TigerSetup, MSI or Inno Setup authoring, package-manager scripts)
and for anyone who wants to make an app's shell completion permanent. It defines what a TigerCli app
guarantees when asked for a completion script. It also documents the verified lifecycle on Windows
for Windows PowerShell 5.1 and PowerShell 7+: install, upgrade, repair, and uninstall, per user and
per machine.

App authors enable completion with `UseShellCompletion()`; see [shell completion](../guides/shell-completion.md)
for what is completed and how to try it in a shell.

## Who Owns What

| Party | Owns |
|---|---|
| The app (TigerCli) | Generating the activation script on request (`app --completion <shell>`) and answering the Tab requests that script sends. |
| The installer, or the user | Deciding whether, for whom, and where completion is activated, and every change to the shell environment that activation needs, including undoing it. |

A TigerCli app never edits shell profiles, writes completion files, changes the registry, environment
variables, or execution policy, or registers itself anywhere. Nothing in this contract asks it to.

## The Generation Contract

`app --completion <shell>`, with `<shell>` one of `bash`, `zsh`, `fish`, or `pwsh` (`powershell` is a
synonym for `pwsh`), is the only interface an installer uses.

- **Output.** The activation script goes to stdout and nothing else does. The app exits with its
  success code (normally `0`). A missing or unknown shell name is a usage error: the error goes to
  stderr and the app exits with its invalid-arguments code.
- **Exclusive form.** `--completion <shell>` must be the only thing on the command line. With any
  other argument, `--completion` is an unknown option.
- **Deterministic content.** The script depends only on the TigerCli version the app was built
  with and on the app's application name. It does not depend on the working directory, the user,
  the culture, the terminal, or the environment, so it can be generated once per build and shipped,
  or generated on the target machine by the installed executable.
- **Encoding.** The script is ASCII except for the application name, with LF line endings.
- **No side effects.** Generation reads registration metadata only. It never runs command handlers,
  command factories, contribution callbacks, prompts, or providers. It does run the app's own code
  before `RunAsync` (typically `Main` building the app), which is the app's responsibility.
- **Command name.** The script registers completion for the application name (normally the
  assembly name through `UseAssemblyMetadata(...)`) and, for PowerShell, also `<name>.exe`. The
  installed executable must be callable by that name. See [the PATH requirement](#the-path-requirement).
- **Re-evaluation is safe.** Evaluating the script again replaces the earlier registration.
- **Versioning.** The script is an adapter for the protocol of the TigerCli version that generated
  it. Regenerate it whenever the installed executable changes: on install, upgrade, and repair.
  TigerCli does not promise that a script from one TigerCli version works with an app built with
  another.
- **Internal protocol.** The script calls the app with a hidden `--tigercli-complete` request.
  That request and its output are a private contract between TigerCli and its own scripts. Never
  call it, persist its output, or depend on its format.

What each script adds to the shell:

| Shell | Adds |
|---|---|
| PowerShell | One `Register-ArgumentCompleter -Native` registration for `<name>` and `<name>.exe`. No functions, variables, or aliases. |
| Bash | The function `_tigercli_complete_<ident>` and a `complete -F` registration for `<name>`. |
| Zsh | The function `_<name>` and a `compdef` registration (or, loaded from `$fpath`, the completion function itself). |
| Fish | The function `__tigercli_complete_<ident>` and a `complete -c <name>` registration. |

`<ident>` is the application name with every character other than an ASCII letter or digit
replaced by `_`.

### The PowerShell Completer's Session Contract

At Tab time the PowerShell completer leaves the session as it found it. This is verified in both
editions by `CommandParserTest.Tests/PowerShellCompletionTests.cs`:

- `$LASTEXITCODE` keeps its value, or stays unset.
- The console output code page and `[Console]::OutputEncoding` are restored. The app answers in
  the console's output code page without changing it. The completer selects UTF-8 only for the
  call, so non-ASCII descriptions survive an OEM code page such as 437 or 852.
- Nothing is added to `$Error`. When the command can no longer be found (for example after
  uninstall), the completer returns nothing and PowerShell offers its own path completion.
- Under Constrained Language Mode (AppLocker or WDAC policy) candidates are returned as plain
  strings: completion works, without descriptions, and the console encoding is left alone.

## PowerShell On Windows: Verified Behavior

Observed on Windows 11 (October 2026) with Windows PowerShell 5.1.26100 and PowerShell 7.6.6,
unless a row says otherwise.

| Topic | Behavior |
|---|---|
| Persistence | An argument completer exists only in the session that registered it. Neither edition keeps a list of completers between sessions, so something has to run the registration in every new session. |
| Profile-free activation | None exists. Native completers are not loaded on demand: a module on `$env:PSModulePath` that registers one is not imported by pressing Tab, by `Get-Command <name>`, or by completing one of the module's own command names. |
| `-NativeFallback` | PowerShell 7.6 adds `Register-ArgumentCompleter -NativeFallback`, one session-wide catch-all completer for native commands that have none of their own, meant for modules such as Microsoft.PowerShell.UnixTabCompletion. It is still per session, and there is only one slot. An app must not claim it. |
| Unregistering | 7.6 has no `Unregister-ArgumentCompleter` (proposed in PowerShell PR #26680, not merged). Open sessions keep a completer until they end. After uninstall it stays silent; see the session contract above. |
| Profiles | Windows PowerShell 5.1 and PowerShell 7 read separate profile files; neither reads the other's. See [Profile locations](#profile-locations). |
| Execution policy | Profiles are scripts. Windows PowerShell 5.1 on a Windows client defaults to `Restricted`, which runs no profile, so no persistent activation is possible until the user or an administrator changes the policy. PowerShell 7 ships `RemoteSigned` in `$PSHOME\powershell.config.json`. A file the installer writes locally carries no Mark of the Web, so `RemoteSigned` runs it. `AllSigned` requires every profile and every dot-sourced file to be signed. |
| Shell startup cost | Dot-sourcing a saved script: about 7 ms. `app --completion pwsh \| Out-String \| Invoke-Expression`: about 110 ms, one app start per new session. One Tab press: about 125 ms. (A small framework-dependent app, Debug build.) |
| Command lookup | Completion needs the command to resolve by name. An `App Paths` registration does not make an executable resolvable in either edition; a `PATH` entry does. Path-qualified invocations (`.\my-tool.exe`, a full path, `& 'C:\...\my-tool.exe'`) also complete. |
| Bare `-` or `--` | Windows PowerShell 5.1 never calls a native completer for a word that is only `-` or `--`; type at least one more character. PowerShell 7 calls it. |
| Console encoding | PowerShell decodes a native command's output with its cached `[Console]::OutputEncoding`, which is the OEM code page in a default console. A TigerCli app writes redirected output in the console's output code page and never changes it; see [console output encoding](console-output-encoding.md). |

### Activation Options

| Option | Verdict |
|---|---|
| Saved script plus a guarded dot-source line in a profile | **Recommended for installers.** Fast startup (no app start), no app code at shell startup, and the file can be signed for `AllSigned`. It has to be regenerated on upgrade, which an installer does anyway. |
| A profile line that generates the script at startup: `if (Get-Command my-tool -ErrorAction Ignore) { my-tool --completion pwsh \| Out-String \| Invoke-Expression }` | **Recommended for manual setup.** Always matches the installed app and needs no file. Costs one app start per new session, and runs the app's startup code each time. |
| The all-users profile (`$PSHOME\profile.ps1`) | Administrator opt-in only; see [Per-machine installs](#per-machine-installs). |
| A module on `$env:PSModulePath` | Not an activation mechanism: it still needs an `Import-Module` line in a profile, and gains nothing over dot-sourcing a file. |
| `-NativeFallback` | No. It is a single shared slot for cover-all completers. |
| Windows Terminal profile command line, shortcut arguments (`pwsh -NoExit -Command ...`) | No. They cover only sessions started from that launcher. |
| Changing execution policy, overriding `TabExpansion2`, editing PSReadLine settings | No. Invasive, and outside what an app installer should own. |

## Profile Locations

Activate in the **CurrentUserAllHosts** profile (`profile.ps1`), not the host-specific
`Microsoft.PowerShell_profile.ps1` that `$PROFILE` points to, so editor hosts such as VS Code get
completion too.

| Edition | Current user, all hosts | All users, all hosts |
|---|---|---|
| Windows PowerShell 5.1 | `<Documents>\WindowsPowerShell\profile.ps1` | `%SystemRoot%\System32\WindowsPowerShell\v1.0\profile.ps1`; the 32-bit host reads `%SystemRoot%\SysWOW64\WindowsPowerShell\v1.0\profile.ps1` |
| PowerShell 7 | `<Documents>\PowerShell\profile.ps1` | `$PSHOME\profile.ps1` of each installation (MSI default `%ProgramFiles%\PowerShell\7`) |

- `<Documents>` is the user's Documents known folder. It is often redirected, for example by
  OneDrive Known Folder Move, and its display name may be localized: one test machine's profile is
  `C:\Users\<user>\OneDrive\Dokumenty\PowerShell\profile.ps1`. Resolve it in the target user's
  context (`SHGetKnownFolderPath(FOLDERID_Documents)`,
  `Environment.GetFolderPath(SpecialFolder.MyDocuments)`), never as `%USERPROFILE%\Documents`.
- Neither profile file exists until something creates it.
- A 32-bit installer process that writes to `System32` is redirected to `SysWOW64` (WOW64 file-system
  redirection). Use `Sysnative` or a 64-bit process to reach the 64-bit host's profile.
- MSIX (Microsoft Store) installations of PowerShell 7 keep `$PSHOME` read-only, so their
  all-users profiles cannot be used; only the current-user profiles work there.
- Not verified here: whether a PowerShell 7 MSI upgrade preserves `$PSHOME\profile.ps1`. An
  installer that writes it must check it after a PowerShell upgrade, or avoid it.

## Recommended Integration

### The Script File

- Generate it with the installed executable (`<install dir>\my-tool.exe --completion pwsh`) during
  install, upgrade, and repair, or generate it at build time and ship it. The two produce the same
  bytes. Shipping it lets a publisher Authenticode-sign it for `AllSigned` environments.
- Write it into a directory the installer owns: the app's install directory or a `completion`
  subfolder of it. Write to a temporary file and then replace the target, so an interrupted upgrade
  never leaves a half-written script.
- Save it as UTF-8 **with** a byte-order mark. Windows PowerShell 5.1 reads BOM-less files as ANSI.
  This only matters for a non-ASCII application name.
- **Security:** a file dot-sourced into a profile runs as that profile's user. A file that an
  all-users profile or an administrator's profile loads must be writable only by administrators,
  as anything under `%ProgramFiles%` is. Never point a profile at a file that a less-privileged
  account can modify.

### The Profile Block

Activation is opt-in: an install option the user or administrator chooses. Never edit profiles
silently. The block has fixed markers, so any tool (or a person) can find, replace, or remove it:

```powershell
# >>> TigerCli completion: my-tool >>>
if (Test-Path -LiteralPath 'C:\Program Files\MyTool\completion\my-tool.ps1') { . 'C:\Program Files\MyTool\completion\my-tool.ps1' }
# <<< TigerCli completion: my-tool <<<
```

- The marker name is the application name. A profile holds at most one block per application.
- Quote the path in single quotes and double any single quote inside it.
- The `Test-Path` guard keeps the profile working if the file disappears before the block does.
- Write the block to the CurrentUserAllHosts profile of each edition being activated. Create the
  file, and its directory, when missing.
- Replace an existing block in place; otherwise append it after a blank line.
- Preserve everything outside the markers exactly, including the file's encoding, byte-order mark,
  and line-ending style. Never reformat, reorder, or touch another application's block.
- Edit a user's profile only from a process running as that user. An elevated per-machine install
  must not reach into other users' Documents folders.
- When the profile cannot be written (read-only, locked, or a cloud placeholder that is
  unavailable), skip activation and report it. Do not fail the installation over it.

### The PATH Requirement

Completion is registered by command name, so activation is meaningful only when the app is on
`PATH` for the sessions that load the profile. An installer that makes `PATH` optional should make
completion depend on it, or not offer it when `PATH` is off. New `PATH` entries reach only
processes started after the change.

### Lifecycle

| Step | Script file | Profile block |
|---|---|---|
| Install | Generate and write. | Add, if activation was chosen. Record the choice. |
| Upgrade | Regenerate, always: the executable changed. | Leave it. Rewrite it only if the script path changed. |
| Repair | Regenerate. | Restore the recorded choice: rewrite a stale block, re-add a missing one only if activation was recorded. |
| Uninstall | Delete. | Remove the block, both markers included, and the blank line the installer added before it. Leave the profile file itself, even if it is now empty. |

Changes take effect in new sessions. Open sessions keep their completer until they end; it stays
silent once the executable is gone.

### Per-user Installs

Install for the current user, write the script under the per-user install directory, and activate
the current user's CurrentUserAllHosts profiles for the editions chosen. Everything stays in that
user's profile and folders; no elevation is involved.

### Per-machine Installs

Prefer this split: the machine install puts the executable on the machine `PATH` and the script
under `%ProgramFiles%`. Activation stays per user and opt-in. Either the installing user's own
profile is activated (from an unelevated part of the installer), or users add the block themselves.

All-users activation (`$PSHOME\profile.ps1`) is possible as an explicit administrator option, with
known gaps. It reaches only the PowerShell installations present at install time. It cannot reach
MSIX installations. It needs both the 64-bit and the 32-bit Windows PowerShell paths. Its survival
across PowerShell 7 upgrades is unverified. Its file must be writable by administrators only.
Uninstall must remove the block from every all-users profile it wrote.

A per-user uninstall of a per-machine product cannot reach other users' profiles. The `Test-Path`
guard keeps any blocks it leaves inert.

## Bash, Zsh, And Fish

These shells load completion files from conventional directories, so no profile edit is needed:

| Shell | Per-user | System-wide (packages) |
|---|---|---|
| Bash (bash-completion 2) | `~/.local/share/bash-completion/completions/<name>` | `/usr/share/bash-completion/completions/<name>` |
| Zsh | `_<name>` in a directory on `$fpath` | `_<name>` in the distribution's `site-functions` directory |
| Fish | `~/.config/fish/completions/<name>.fish` | `/usr/share/fish/vendor_completions.d/<name>.fish` |

The lifecycle is the same: write the generated file on install, regenerate it on upgrade and repair,
and delete it on uninstall. These locations are the shells' documented conventions; this repository
does not run automated tests against them.
