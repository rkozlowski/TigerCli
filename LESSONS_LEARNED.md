# Lessons Learned

Current, non-obvious, project-specific knowledge that prevents repeating costly mistakes.

## Regenerating doc examples off the canonical platform rewrites every PNG

`dotnet run --project internal/DocSamples` regenerates **all** of `docs/examples`, PNGs included.
PNG bytes are canonical only on Windows 11 (see `docs/design/doc-artifacts.md`); on any other
machine the run rewrites every committed PNG with different bytes even though nothing visual
changed. When a change only affects text artifacts (for example help output captured in
`docs/examples/roi-cities.html`), regenerate, keep the intended text files, `git restore` the
rest of `docs/examples`, then confirm with `dotnet run --project internal/DocSamples -- check`
(it compares text exactly and checks only PNG existence off-platform).

A change to any sample app's help output — such as a new framework option listed in help — drifts
those captured artifacts and fails `DocExamplesDriftTests`, so expect this whenever help changes.

## Non-ASCII console text can look right while being decoded wrong

**Area:** PowerShell completion; any check of text that crosses a native-process boundary on Windows

**Status:** Active

**Symptom:** a first probe of the PowerShell completer under an OEM console code page printed
Polish descriptions correctly, which suggested that decoding worked. A direct check showed that the
completer had mis-decoded them all along.

**Cause:** the app wrote UTF-8. PowerShell decoded it with its cached OEM
`[Console]::OutputEncoding`, then the probe printed the mojibake back out in that same OEM
encoding, which re-created the original UTF-8 bytes, so the outer terminal showed correct text. Two
mismatched decodings cancelled out. Related traps:
- `chcp` reports the console's *input* code page, not its output code page.
- A `cmd` redirection to `CONOUT$` gives the app a write-only console handle, which .NET reports as
  redirected, so the app takes its redirected-output path on what looks like the console.
- On Windows, assigning `Console.OutputEncoding` anything but UTF-16 changes the shared console
  code page; see `docs/reference/console-output-encoding.md`.

**Do not:** judge encoding correctness from text a child process printed, read the console code
page with `chcp`, or put an app "on the console" with a `CONOUT$` redirection.

**Use instead:** compare code points inside the process that decoded the text, or write results to a
file with an explicit encoding. Read the output code page with `GetConsoleOutputCP` and the screen
with `ReadConsoleOutputCharacterW`. Let the app inherit genuine console handles.

**Prevented by:** `CommandParserTest.Tests/WindowsConsoleProbe.ps1` and the tests that start it
(`PowerShellCompletionTests.cs`, `ConsoleOutputEncodingTests.cs`). The probe writes what the host
decoded and what the screen shows to a JSON file, and the tests compare them exactly.

**Generalization candidate:** TigerWinLab (evidence collection from Windows console processes)

## Real-shell harnesses fail in ways that look like product defects

**Area:** `internal/tools/shell-completion/` real-keypress checks (PowerShell, Bash, Zsh, Fish)

**Status:** Active

**Symptom:** each first run of the real-shell checks failed on harness behavior that looked like a
TigerCli defect: shells reporting `parser-test: command not found`, typed `Łódź` arriving as `Lódz`,
completion menus that never appeared, and a console at code page 65001 with no TigerCli code
involved.

**Cause:**
- tmux replaces `PATH` in a new session's environment; other `-e` variables survive.
- A P/Invoke struct without `CharSet = CharSet.Unicode` marshals a `char` field as one ANSI byte,
  so injected key events lose letters outside code page 1252.
- PSReadLine draws its completion menu below the input line while the cursor stays on it; reading
  the screen only up to the cursor row misses the menu.
- PSReadLine sets the console to UTF-8 while it edits a line and restores the previous encoding
  before a command runs, so the code page seen at an idle PowerShell prompt is PSReadLine's. This
  also hides an app that changes the code page, which `cmd.exe` exposes.
- Bash and Zsh spend the first Tab inserting a common prefix; the listing comes on a later press.

**Do not:** read a failing first real-shell run as a product regression before ruling these out, or
assert console code-page preservation at an interactive PSReadLine prompt.

**Use instead:** carry the app directory in its own variable (`TIGERCLI_APP_DIR`), declare console
structs `CharSet.Unicode`, read through the bottom of the visible window, type up to the common
prefix, and assert strict code-page preservation in a non-interactive host
(`CommandParserTest.Tests`).

**Prevented by:** the fixes in `windows-real-tab-check.ps1` and `linux-real-shell-check.sh`.

**Generalization candidate:** TigerWinLab and TigerLinuxLab (interactive console and terminal evidence)
