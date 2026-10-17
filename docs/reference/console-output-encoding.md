# Console Output Encoding

How a TigerCli app encodes the text it writes to standard output and standard error.

## The Contract

| Where the stream goes | Encoding |
|---|---|
| Redirected: a file, a pipe, a capturing process, on every platform | UTF-8, without a byte-order mark |
| The interactive Windows console | UTF-16, which .NET writes with `WriteConsoleW` |
| A Linux or macOS terminal | UTF-8 |

- **Unicode is preserved.** Redirected output keeps every character, including those no legacy
  Windows code page has (`€`, `╭─╮`, `✓`, CJK, emoji). An interactive Windows console receives
  characters rather than bytes, so the console code page plays no part in what is rendered.
- **The console code page is never changed.** On Windows the code page belongs to the console, which
  the shell and every process attached to it share, and a change outlives the app. .NET calls
  `SetConsoleOutputCP` for any `Console.OutputEncoding` except UTF-16, so TigerCli assigns only
  UTF-16 and gives redirected streams UTF-8 writers instead.
- **Installed writers are kept.** A writer an app or test installed with `Console.SetOut` or
  `Console.SetError` is never replaced, whether it was installed before or after the app was built.
  Output TigerCli renders, and anything a handler writes to `Console.Out`, goes to that writer.
- **Each stream is decided on its own.** With stdout redirected and stderr on the console (or the
  other way round), the redirected stream is UTF-8 and the console stream still renders every
  character.

TigerCli applies the contract once per process, when an app run starts or when TigerCli first writes
to the console, and never during type initialization. A [shell completion](../guides/shell-completion.md)
request sets up nothing else: it only selects the encoding of its answer, which is UTF-8 for Bash,
Zsh, and Fish. A PowerShell answer is written in the console code page instead, because PowerShell
decodes it with that code page in every language mode; the generated script selects UTF-8 for each
request where it can.

## Reading TigerCli Output From A Shell

Most consumers read UTF-8 directly: files opened by editors and tools, .NET processes that capture
with UTF-8, Linux tools reading a Windows executable through WSL, and Linux shells.

Windows PowerShell 5.1 and PowerShell 7 decode a program's output with `[Console]::OutputEncoding`,
which defaults to the console's OEM code page. That is a shell setting, so to capture non-ASCII
output intact the caller selects UTF-8 first:

```powershell
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$lines = my-tool list
```

The same applies to `cmd`'s `type` and to a .NET `Process` that captures without setting
`StandardOutputEncoding`: both use the console code page. Any UTF-8-writing program behaves this way
in those consumers.

## How TigerCli Owns Its Writers

To give a redirected stream a UTF-8 writer, TigerCli has to replace `Console.Out` or `Console.Error`,
and it may only do so when the current writer is .NET's own. .NET has no documented way to tell, and
no point in an app's start-up reliably comes before every capture: apps and tests install writers both
before and after building the app. TigerCli therefore uses one narrowly isolated compatibility
mechanism (`PlatformStandardWriters`, Windows only).

- **What it relies on.** Assigning `Console.OutputEncoding` discards .NET's own writers (they are
  recreated on next use) and leaves writers installed with `SetOut`/`SetError` in place. Every .NET
  Core and .NET release behaves this way, but it is not documented. TigerCli assigns UTF-16 and
  compares each writer before and after.
- **Guards.** `ItTiger.TigerCli.Tests/ConsoleOutputEncodingTests.cs` checks that installed writers
  survive the probe and keep receiving output.
  `CommandParserTest.Tests/RedirectedOutputEncodingTests.cs` checks that a real process's redirected
  stdout, stderr, and completion answers are exact UTF-8, which only happens when .NET's own writers
  are recognized. A .NET upgrade that changed the behavior fails one of them.
- **Fallback.** If .NET stopped recreating its writers, every writer would look installed. TigerCli
  would keep the writers it found, and redirected output would stay in the console code page (lossy
  outside it), with no wrong bytes and no replaced capture. TigerCli creates the writers before the
  probe so that an unrecreated writer keeps the encoding it was created with.

## Compatibility

On Linux and macOS nothing changed: output was UTF-8 before and is UTF-8 now.

On Windows, output matches what TigerCli 0.9.4 produced, but the console code page is no longer
changed. Consequences of not changing it:

- **`Console.OutputEncoding` reports UTF-16** once TigerCli has applied the contract. Code that
  encodes its own bytes for a standard stream should choose UTF-8 explicitly instead of reading it.
- **Raw bytes written to `Console.OpenStandardOutput()` on an interactive console are read as
  UTF-16.** Write text through `Console.Out`, or through TigerCli, instead. Raw bytes on redirected
  streams are unaffected.
- **Child processes see the console code page unchanged.** A child that relied on TigerCli switching
  the console to UTF-8 now renders in the console's own code page.
- **Writers taken before TigerCli's first output keep the console code page.** A reference to
  `Console.Out` stored before the app run starts still points at .NET's original writer.

## Limitations

- **Classic console host and emoji.** Windows' classic console host stores characters outside the
  Basic Multilingual Plane as U+FFFD in its screen buffer; Windows Terminal shows them. TigerCli
  writes them correctly either way.
- **Glyphs depend on the console font.** UTF-16 delivers every character; the font decides how it
  looks.

## Choosing An Encoding Explicitly

An app that needs a specific encoding sets `TigerConsole.OutputEncoding` at the start of `Main`, before
TigerCli writes:

```csharp
TigerConsole.OutputEncoding = Encoding.Latin1;
```

The value is assigned to `Console.OutputEncoding` immediately, as in earlier TigerCli versions, and
replaces TigerCli's own choice for every stream. On Windows any encoding other than UTF-16 changes the
shared console code page, so treat it as a deliberate decision about the user's console, not a
rendering setting. Its default value, UTF-8, describes TigerCli's redirected output.

## Verification

`CommandParserTest.Tests/ConsoleOutputEncodingTests.cs` runs the real `parser-test` executable in
Windows PowerShell 5.1 and PowerShell 7 under OEM code pages. It checks:

- interactive output, errors, and themed help on the screen;
- redirected output and errors in files and in PowerShell;
- both mixed cases (one stream redirected, the other on the console);
- that the console code page is unchanged after every run.

The [real-shell checks](../contributing/unit-testing.md#real-shell-checks) repeat this with real
keypresses in interactive PowerShell sessions, in Bash, Zsh, and Fish on Linux, and through WSL
interoperability.

In an interactive PowerShell session, PSReadLine sets the console to UTF-8 while it edits a line and
restores the previous encoding before a command runs. An app that changed the code page is therefore
hidden at a PowerShell prompt. It shows wherever nothing resets it, `cmd.exe` among them.

## Other Console State

On an interactive Windows console, the [ANSI](ansi-sink.md) path enables virtual-terminal processing
on the console output handle when it is not already enabled, because legacy console hosts need it
to interpret colour sequences. That console mode is shared, and TigerCli does not restore it on exit.
Windows Terminal and PowerShell 7 already run with it enabled. The 16-colour fallback sink sets
console colours while it writes and restores the original colours at each line end and when it
flushes.
