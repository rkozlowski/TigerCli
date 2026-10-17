# TigerCli Unit Testing

TigerCli tests fall into two broad groups:

- **Non-interactive rendering tests**: render components or grids to deterministic lines and assert output.
- **Semi-interactive TUI tests**: run inline controls through the real modal loop using fake terminal input/output.

Use the simpler non-interactive style whenever the behavior does not require keyboard input or modal timing.

## Non-Interactive Rendering Tests

Non-interactive tests verify rendering and layout as pure output. They do not run a keyboard loop, modal shell, or terminal state restoration.

Typical coverage:

- `CliGrid` layout and measurement.
- wrapping, truncation, padding, alignment, frames, and overlays.
- tables and table framing.
- style cascade and formatting behavior.
- subgrids and scrollable cells where the behavior can be asserted from rendered lines.
- invalid usage and exception behavior.

Most tests should use `TestBase.AssertSnapshot(...)`, which renders through:

- `TigerConsole.RenderToLines(component)`
- `TigerConsole.RenderGridToLines(grid)`

Example:

```csharp
var grid = new CliGrid(1, 1);
grid.Set(0, 0, "Hello");

AssertSnapshot(grid, "Hello");
```

These tests should usually assert:

- rendered lines.
- measured output where needed.
- thrown exceptions for invalid layout/API usage.

They should not enqueue keys, start `InlineShell`, or depend on terminal cursor state.

## Semi-Interactive TUI Tests

Semi-interactive tests verify inline behavior over time. Use them when behavior depends on input, re-rendering, dialog result state, or the real modal loop.

Use:

- `ItTiger.TigerCli.Tui.Testing.TestShell`
- `ItTiger.TigerCli.Tui.Testing.TestTerminal`

`TestShell` implements `ICliAppShell` and delegates to the real `InlineShell.RunModalAsync`. `TestTerminal` implements `ICliTerminal` without using `System.Console`.

Typical coverage:

- confirm and cancel behavior.
- select navigation.
- empty states.
- disabled confirmation through `CanConfirm`.
- scrolling behavior after key input.
- text input editing and validation behavior.

Example:

```csharp
var shell = new TestShell();
var select = new InlineSelect(shell, ["Red", "Green", "Blue"]);
var dialog = new InlineDialog(shell, "Pick one", select);
var modalTask = shell.RunModalAsync(dialog, TestContext.Current.CancellationToken);

await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

shell.Terminal.EnqueueKeys(ConsoleKey.DownArrow, ConsoleKey.DownArrow);
await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

Assert.Equal(2, select.Payload);
Assert.Contains("Blue", shell.Terminal.LastRenderedText);

shell.Terminal.EnqueueKey(ConsoleKey.Enter);
var result = await modalTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

Assert.Equal(DialogResultKind.Ok, result.Kind);
Assert.Equal(2, result.Payload);
```

Assert the returned [DialogResultKind](https://rkozlowski.github.io/TigerCli/api/ItTiger.TigerCli.Enums.DialogResultKind.html) as well as the payload when a direct prompt or modal is the test subject.

## TestTerminal and TestShell

`TestShell` is the test-facing shell. It exposes:

- `Terminal`
- `Theme`
- `Viewport`
- `RunModalAsync(...)`

`TestTerminal` provides:

- `EnqueueKey(...)` and `EnqueueKeys(...)`.
- `RenderCount`, `ReadCount`, and `ClearCount`.
- `LastRenderedGrid`.
- `LastRenderedLines`.
- `LastRenderedText`.
- wait helpers such as `WaitForRenderCountAsync(...)`, `WaitForReadCountAsync(...)`, and `WaitForInputDrainedAsync(...)`.

### Input Draining

`WaitForInputDrainedAsync(...)` does not mean only that the internal key queue is empty.

Input is considered drained only after `InlineShell.RunModalAsync` returns to its polling point and calls `Terminal.KeyAvailable`, and `KeyAvailable` observes an empty queue for the current input generation. At that point:

- queued keys have been read.
- key handling has completed.
- any post-key re-render has completed.
- it is safe to inspect `LastRenderedLines`, `LastRenderedText`, `LastRenderedGrid`, render counts, and control state.

Use `WaitForInputDrainedAsync(...)` for non-terminating input batches, such as arrow keys. For terminating keys such as `Enter` or `Escape`, await the modal task instead.

## What To Unit Test

Prefer automated tests for:

- pure rendering and layout behavior.
- wrapping, truncation, frames, tables, style cascade, subgrids, and scrollable-cell behavior.
- inline control state transitions.
- dialog result behavior.
- empty-state behavior.
- cancellation behavior.
- text input editing behavior.
- validation and `CanConfirm` behavior.
- status/hint row behavior.

## Shell Completion Tests

Completion has two layers of tests:

- **Engine and protocol tests** (`ItTiger.TigerCli.Tests/TigerCliShellCompletionTests.cs`) send
  `--tigercli-complete` requests through `TigerCliAppTestHost` and assert candidates, fallbacks,
  and the generated scripts' shape. They run everywhere.
- **Real-host PowerShell tests** (`CommandParserTest.Tests/PowerShellCompletionTests.cs`) evaluate
  the generated script for the built `parser-test` executable inside Windows PowerShell 5.1, and
  inside PowerShell 7 when `pwsh` is on `PATH`. They assert the completer's session contract:
  decoded descriptions, `$LASTEXITCODE`, `$Error`, the console code page, Constrained Language Mode,
  and an uninstalled executable. They also send a raw completion request, which must leave the
  console code page alone.

## Windows Console Tests

`CommandParserTest.Tests/WindowsConsoleProbe.ps1` runs inside a real PowerShell host under OEM
console code pages and writes what it observed to a JSON result file. `WindowsConsoleProbe.cs` starts
it once per host and language mode, with its own hidden console and its standard handles left on
that console, so the app it starts writes to a genuine console. Besides the completion scenarios
above, `ConsoleOutputEncodingTests.cs` asserts the
[console output encoding](../reference/console-output-encoding.md) contract for normal execution:
non-ASCII output, errors, and themed help read back from the screen buffer; redirected output and
errors as PowerShell decodes them; and an unchanged console code page after each run. On other
platforms these tests have no data and are skipped.

To put an app on a genuine console from a harness, let it inherit console handles. A `cmd`
redirection to `CONOUT$` opens a write-only handle, which .NET reports as redirected.

When checking non-ASCII completion text by hand, compare code points rather than reading console
output: two mismatched decodings can cancel out on the way to your terminal and make broken text
look correct. `chcp` reports the console's input code page, not its output code page.

## What To Keep Manual Or Smoke-Tested

Some behavior still needs human review or manual harnesses:

- actual console visual feel.
- terminal resize behavior.
- font-specific and terminal-specific glyph rendering.
- scrollback/history artifacts.
- full end-to-end TUI checks in a real terminal.

Public samples such as `RoiCities.Extended` and `FolderCopy` are useful for these manual checks, but they should not replace focused unit tests for deterministic behavior.
