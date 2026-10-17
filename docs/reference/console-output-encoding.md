# Console Output Encoding

How a TigerCli app encodes the text it writes to standard output and standard error, and why it
never changes the console's code page on Windows.

## The Rule

| Platform and streams | Encoding | How it reaches the reader |
|---|---|---|
| Windows, stdout and stderr both on the interactive console | UTF-16 | .NET writes it with `WriteConsoleW`. Every character renders, whatever the console code page is. |
| Windows, stdout or stderr redirected (pipe, file, captured by a shell) | The console's output code page, unchanged | It is the encoding the invoking shell or parent process decodes the output with. |
| Linux, macOS | UTF-8 | Assigning it changes nothing in the terminal. |

TigerCli applies the rule when an app run starts and when it first writes to the console, never during
type initialization. Code that only reads TigerCli metadata, such as a
[shell completion](../guides/shell-completion.md) request, gets no console setup.

## Why Not UTF-8 Everywhere

On Windows, assigning any encoding other than UTF-16 to `Console.OutputEncoding` calls
`SetConsoleOutputCP`. That changes the code page of the console, which the shell and every other
process attached to it share, and it stays changed after the app exits. An app that did this would
switch the user's console to code page 65001 on every run, even for a shell completion request.
That setting belongs to the console's owner, not to the app.

The rule above renders the same characters without that side effect:

- **Interactive output** uses UTF-16. The console receives characters, not bytes, so the console code
  page plays no part.
- **Redirected output** follows the console code page. Windows PowerShell 5.1 and PowerShell 7 decode
  a program's redirected output with it, as does .NET's `Process` by default, and `cmd` displays
  redirected files in it. Matching it is what makes redirected non-ASCII text arrive intact. UTF-8
  output decoded with an OEM code page arrives garbled (`Świecie` becomes `┼Üwiecie`).
- UTF-16 is not used for redirected output: .NET applies one encoding to both standard streams, and a
  redirected stream would receive UTF-16 bytes.

`CommandParserTest.Tests/ConsoleOutputEncodingTests.cs` runs the real `parser-test` executable in
Windows PowerShell 5.1 and PowerShell 7 under OEM code pages. It checks interactive output, errors,
and themed help on the screen, and redirected output and errors as PowerShell decodes them. It also
checks that the console code page is unchanged afterwards. The
[real-shell checks](../contributing/unit-testing.md#real-shell-checks) repeat this with real
keypresses in interactive PowerShell sessions, and in Bash, Zsh, and Fish on Linux.

In an interactive PowerShell session, PSReadLine sets the console to UTF-8 while it edits a line and
restores the previous encoding before a command runs. An app changing the code page is therefore
hidden at a PowerShell prompt. It shows wherever nothing resets it, `cmd.exe` among them: one run of
an app that assigned UTF-8 left a `cmd` console at code page 65001.

## Limitations

- **Redirected output keeps only what the code page can represent.** Characters the console's code
  page lacks are best-fit or replaced: under code page 437, Polish `Ś` becomes `S`, and a glyph with
  no equivalent becomes `?`. The common box-drawing characters exist in the usual OEM code pages.
  For full fidelity, the consumer selects UTF-8 before starting the app (`chcp 65001`, or in
  PowerShell `[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)`). The app then writes
  UTF-8 because the console code page is 65001. The generated
  [PowerShell completer](shell-completion-installation.md) does exactly this around each request.
- **One redirected stream degrades both.** When only one of stdout and stderr is redirected, both use
  the console code page, so the interactive one is also limited to it.
- **Interactive glyphs still depend on the console font.** UTF-16 delivers every character; the font
  decides how it looks.

## Choosing An Encoding Explicitly

An app that needs a specific encoding sets `TigerConsole.OutputEncoding` before TigerCli writes,
typically at the top of `Main`:

```csharp
TigerConsole.OutputEncoding = Encoding.UTF8;
```

The value is assigned to `Console.OutputEncoding` immediately, and TigerCli's own choice is skipped.
On Windows any encoding other than UTF-16 changes the shared console code page again, so treat it as
a deliberate decision about the user's console, not a rendering setting.

## Other Console State

On an interactive Windows console, the [ANSI](ansi-sink.md) path enables virtual-terminal processing
on the console output handle when it is not already enabled, because legacy console hosts need it
to interpret colour sequences. Like the code page, that console mode is shared, and TigerCli does not
restore it on exit. Windows Terminal and PowerShell 7 already run with it enabled. The 16-colour
fallback sink sets console colours while it writes and restores the original colours at each line
end and when it flushes.
