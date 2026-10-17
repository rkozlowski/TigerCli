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

**Cause:** the TigerCli app writes UTF-8. PowerShell decoded it with its cached OEM
`[Console]::OutputEncoding`, then the probe printed the mojibake back out in that same OEM
encoding, which re-created the original UTF-8 bytes, so the outer terminal showed correct text. Two
mismatched decodings cancelled out. Related traps found the same day: `chcp` reports the console's
*input* code page, not its output code page, and a TigerCli app changes the console's output code
page to UTF-8 as a side effect of running.

**Do not:** judge encoding correctness from text a child process printed, or read the console code
page with `chcp`.

**Use instead:** compare code points inside the process that decoded the text, or write results to a
file with an explicit encoding. Read the output code page with `GetConsoleOutputCP`.

**Prevented by:** `CommandParserTest.Tests/PowerShellCompletionTests.cs`. Its probe writes the
descriptions the PowerShell host decoded to a JSON file, and the test compares them exactly.

**Generalization candidate:** TigerWinLab (evidence collection from Windows console processes)
