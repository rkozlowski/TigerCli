using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineTextInputTests : TestBase
{
    private const string PolishLetters = "ĄĆĘŁŃÓŚŹŻąćęłńóśźż";

    [Fact]
    public void InitialValue_RendersText_AndSetsActivePoint()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell, "server");
        var grid = input.ToGrid();

        Assert.Equal("server", input.Payload);
        Assert.Equal(CursorMode.Normal, grid.CursorMode);
        Assert.NotNull(grid.ActivePoint);
        Assert.Equal(6, grid.ActivePoint!.Offset);
        Assert.Contains("server", string.Join("\n", TigerConsole.RenderGridToLines(grid)));
    }

    [Fact]
    public void HandleKey_Space_InsertsAndAdvancesCursor()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell);

        Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.Spacebar, ConsoleModifiers.None, ' ')).IsHandled);

        var grid = input.ToGrid();
        TigerConsole.RenderGridToLines(grid);

        Assert.Equal(" ", input.Payload);
        Assert.Equal(1, grid.ActivePoint!.Offset);
        Assert.NotNull(grid.MeasuredActivePoint);
        Assert.Equal(1, grid.MeasuredActivePoint!.OffsetInLine);
    }

    [Fact]
    public void HandleKey_HomeSpaceX_PreservesSpaceAndCursorPosition()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell, "Abc");

        Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.Home, ConsoleModifiers.None)).IsHandled);
        Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.Spacebar, ConsoleModifiers.None, ' ')).IsHandled);
        Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.X, ConsoleModifiers.None, 'X')).IsHandled);

        var grid = input.ToGrid();
        var lines = TigerConsole.RenderGridToLines(grid);

        Assert.Equal(" XAbc", input.Payload);
        Assert.Contains(" XAbc", string.Join("\n", lines));
        Assert.Equal(2, grid.ActivePoint!.Offset);
        Assert.NotNull(grid.MeasuredActivePoint);
        Assert.Equal(2, grid.MeasuredActivePoint!.OffsetInLine);
    }

    [Fact]
    public void HandleKey_BackspaceDeleteAtBoundariesAreNoOps()
    {
        var shell = new TestShell();
        var empty = new InlineTextInput(shell);

        Assert.True(empty.HandleKey(new KeyEvent(ConsoleKey.Backspace, ConsoleModifiers.None)).IsHandled);
        Assert.True(empty.HandleKey(new KeyEvent(ConsoleKey.Delete, ConsoleModifiers.None)).IsHandled);

        var emptyGrid = empty.ToGrid();
        TigerConsole.RenderGridToLines(emptyGrid);

        Assert.Equal(string.Empty, empty.Payload);
        Assert.Equal(0, emptyGrid.ActivePoint!.Offset);
        Assert.Equal(0, emptyGrid.MeasuredActivePoint!.OffsetInLine);

        var input = new InlineTextInput(shell, "abc");

        Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.Home, ConsoleModifiers.None)).IsHandled);
        Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.Backspace, ConsoleModifiers.None)).IsHandled);
        Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.End, ConsoleModifiers.None)).IsHandled);
        Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.Delete, ConsoleModifiers.None)).IsHandled);

        var grid = input.ToGrid();
        TigerConsole.RenderGridToLines(grid);

        Assert.Equal("abc", input.Payload);
        Assert.Equal(3, grid.ActivePoint!.Offset);
        Assert.Equal(3, grid.MeasuredActivePoint!.OffsetInLine);
    }

    [Fact]
    public void HandleKey_PrintableDigitAndPunctuation_InsertAndAdvanceCursor()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell);

        Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.D1, ConsoleModifiers.None, '1')).IsHandled);
        Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.OemPeriod, ConsoleModifiers.None, '.')).IsHandled);

        var grid = input.ToGrid();
        TigerConsole.RenderGridToLines(grid);

        Assert.Equal("1.", input.Payload);
        Assert.Equal(2, grid.ActivePoint!.Offset);
        Assert.NotNull(grid.MeasuredActivePoint);
        Assert.Equal(2, grid.MeasuredActivePoint!.OffsetInLine);
    }

    [Fact]
    public void HandleKey_PrintablePolishCharacters_InsertFromKeyChar()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell);

        foreach (char ch in PolishLetters)
            Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.None, ConsoleModifiers.Control | ConsoleModifiers.Alt, ch)).IsHandled);

        var grid = input.ToGrid();
        var lines = TigerConsole.RenderGridToLines(grid);

        Assert.Equal(PolishLetters, input.Payload);
        Assert.Contains(PolishLetters, string.Join("\n", lines));
    }

    [Fact]
    public void HandleKey_PrintablePolishCharacters_AdvanceActivePoint()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell);

        foreach (char ch in PolishLetters)
            Assert.True(input.HandleKey(new KeyEvent(ConsoleKey.None, ConsoleModifiers.Control | ConsoleModifiers.Alt, ch)).IsHandled);

        var grid = input.ToGrid();
        TigerConsole.RenderGridToLines(grid);

        Assert.Equal(PolishLetters.Length, grid.ActivePoint!.Offset);
        Assert.NotNull(grid.MeasuredActivePoint);
        Assert.Equal(PolishLetters.Length, grid.MeasuredActivePoint!.OffsetInLine);
    }

    [Fact]
    public void Dialog_UsesTextInputContentStyle_ForHostedContentArea()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell);
        var dialog = new InlineDialog(shell, title: null, input, "Name");

        var grid = dialog.ToGrid();
        var style = grid.GetCellStyle(2, 2);

        Assert.NotNull(input.ContentStyle);
        Assert.Equal(CliColor.White, style.CharStyle?.Foreground);
        Assert.Equal(CliColor.DarkGray, style.CharStyle?.Background);
    }

    [Fact]
    public void Dialog_WidthClamp_WithVeryNarrowViewport_DoesNotThrowAndUsesSafeWidth()
    {
        var shell = new TestShell(viewportWidth: 5);
        var input = new InlineTextInput(shell, width: 200);
        var dialog = new InlineDialog(shell, title: null, input, "Name");

        var lines = TigerConsole.RenderGridToLines(dialog.ToGrid());

        Assert.True(lines.Count > 2);
        Assert.InRange(InputContentWidth(lines[2]), 1, 4);
    }

    [Fact]
    public void Validation_DisablesConfirmAndShowsHint()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell, validator: value =>
            value.Length >= 3 ? null : "Name must be at least 3 characters.");

        Assert.False(input.CanConfirm);
        Assert.Equal("Name must be at least 3 characters.", input.Hint);

        input.HandleKey(new KeyEvent(ConsoleKey.A, ConsoleModifiers.None, 'a'));
        input.HandleKey(new KeyEvent(ConsoleKey.B, ConsoleModifiers.None, 'b'));
        Assert.False(input.CanConfirm);

        input.HandleKey(new KeyEvent(ConsoleKey.C, ConsoleModifiers.None, 'c'));
        Assert.True(input.CanConfirm);
        Assert.Equal("Type text   Enter Confirm   Esc Cancel", input.Hint);
    }

    [Fact]
    public void Validation_CanBecomeFalseAgainAfterInputChanges()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell, validator: value =>
            value.Length is > 0 and <= 2 ? null : "Use 1 or 2 characters.");

        input.HandleKey(new KeyEvent(ConsoleKey.A, ConsoleModifiers.None, 'a'));
        Assert.True(input.CanConfirm);

        input.HandleKey(new KeyEvent(ConsoleKey.B, ConsoleModifiers.None, 'b'));
        Assert.True(input.CanConfirm);

        input.HandleKey(new KeyEvent(ConsoleKey.C, ConsoleModifiers.None, 'c'));
        Assert.False(input.CanConfirm);
        Assert.Equal("Use 1 or 2 characters.", input.Hint);

        input.HandleKey(new KeyEvent(ConsoleKey.Backspace, ConsoleModifiers.None));
        Assert.True(input.CanConfirm);
    }

    [Fact]
    public async Task ModalFlow_ValidationPreventsEnterUntilValid()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(
            shell,
            ct,
            validator: value => value.Length >= 3 ? null : "Name must be at least 3 characters.");
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        EnqueueText(shell.Terminal, "ab");
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.False(runTask.IsCompleted);
        Assert.Contains("Name must be at least 3 characters.", shell.Terminal.LastRenderedText);

        EnqueueText(shell.Terminal, "c");
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal("abc", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_SecretValidationMasksValueAndShowsSafeHint()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(
            shell,
            ct,
            isSecret: true,
            validator: value => value.Length >= 5 ? null : "Secret must be at least 5 characters.");
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        EnqueueText(shell.Terminal, "hide");
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.False(runTask.IsCompleted);
        Assert.Contains("Secret must be at least 5 characters.", shell.Terminal.LastRenderedText);
        Assert.Contains(new string(ConsoleSymbol.Bullet, 4), shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("hide", shell.Terminal.LastRenderedText);

        EnqueueText(shell.Terminal, "n");
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal("hiden", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_ShortInput_PlacesTerminalCursorAtInputCell()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(shell, ct, "abc", width: 10);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        Assert.Equal(5, shell.Terminal.CursorLeft);
        Assert.Equal(2, shell.Terminal.CursorTop);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);
    }

    [Fact]
    public async Task ModalFlow_HorizontallyScrolledInput_PlacesTerminalCursorInViewport()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(shell, ct, "abcdefghij", width: 6);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        Assert.Equal(7, shell.Terminal.CursorLeft);
        Assert.Equal(2, shell.Terminal.CursorTop);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);
    }

    [Fact]
    public async Task ModalFlow_TypingTextAndEnter_ReturnsPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        EnqueueText(shell.Terminal, "abc");
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunInputAsync(shell, ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal("abc", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_PolishCharacters_ReturnsExactPayloadAndRendersText()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(shell, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        EnqueueText(shell.Terminal, PolishLetters, ConsoleModifiers.Control | ConsoleModifiers.Alt);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Contains(PolishLetters, shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal(PolishLetters, result.Payload);
    }

    [Fact]
    public async Task ModalFlow_EmptyInputEnter_ReturnsEmptyString()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunInputAsync(shell, ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal(string.Empty, result.Payload);
    }

    [Fact]
    public async Task ModalFlow_LeftRightInsertion_InsertsAtCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        EnqueueText(shell.Terminal, "ac");
        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow);
        EnqueueText(shell.Terminal, "b");
        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow);
        EnqueueText(shell.Terminal, "d");
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunInputAsync(shell, ct);

        Assert.Equal("abcd", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_BackspaceAndDelete_RemoveExpectedCharacters()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
        shell.Terminal.EnqueueKey(ConsoleKey.Delete);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunInputAsync(shell, ct, "abcd");

        Assert.Equal("ad", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_HomeAndEnd_MoveCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Home);
        EnqueueText(shell.Terminal, "a");
        shell.Terminal.EnqueueKey(ConsoleKey.End);
        EnqueueText(shell.Terminal, "c");
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunInputAsync(shell, ct, "bd");

        Assert.Equal("abdc", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_HomeSpaceX_PreservesSpacePayloadAndCursorPosition()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(shell, ct, "Abc", width: 10);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.Home);
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.X, keyChar: 'X');
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Contains(" XAbc", shell.Terminal.LastRenderedText);
        Assert.Equal(4, shell.Terminal.CursorLeft);
        Assert.Equal(2, shell.Terminal.CursorTop);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal(" XAbc", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_TrailingSpaceBeforeEnter_IsPreservedInPayloadAndMeasurement()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(shell, ct, width: 10);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        EnqueueText(shell.Terminal, "abc ");
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        var activePoint = shell.Terminal.LastRenderedGrid?.MeasuredActivePoint;
        Assert.NotNull(activePoint);
        Assert.Equal(4, activePoint!.OffsetInLine);
        Assert.Equal(6, shell.Terminal.CursorLeft);
        Assert.Equal(2, shell.Terminal.CursorTop);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal("abc ", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_LongInputTyping_KeepsCursorVisibleHorizontally()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(shell, ct, width: 6);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        EnqueueText(shell.Terminal, "abcdefghij");
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        var info = HorizontalInfo(shell);
        var activePoint = shell.Terminal.LastRenderedGrid?.MeasuredActivePoint;

        Assert.True(info.visible);
        Assert.Equal(5, info.offset);
        Assert.Equal(6, info.viewport);
        Assert.Equal(11, info.total);
        Assert.Equal(5, info.maxOffset);
        Assert.NotNull(activePoint);
        Assert.InRange(activePoint!.OffsetInLine, 0, info.viewport - 1);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal("abcdefghij", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_KeyPressBeforeTimeoutResetsTimer()
    {
        // Virtual time keeps this deterministic: the timeout deadline only moves when
        // the test advances the manual clock, so no wall-clock race can flip the result.
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(useManualClock: true);
        var input = new InlineTextInput(shell);
        var dialog = new InlineDialog(shell, title: null, input, "Name");
        var timeout = TimeSpan.FromSeconds(1);
        var runTask = shell.RunModalAsync(dialog, timeout, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        // Move to just before the original deadline (0.6s < 1s), then type a key.
        // The key press must reset the inactivity timer to "now + 1s" = 1.6s.
        shell.AdvanceTime(TimeSpan.FromMilliseconds(600));
        shell.Terminal.EnqueueKey(ConsoleKey.A, keyChar: 'a');
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        // Move past the ORIGINAL deadline (1.2s > 1s) but before the reset deadline
        // (1.2s < 1.6s). The dialog must still be alive to accept the Enter.
        shell.AdvanceTime(TimeSpan.FromMilliseconds(600));
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal("a", result.Payload);
        Assert.Equal(2, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ModalFlow_HorizontalScroll_CountsSpaces()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(shell, ct, "abc def ghi", width: 6);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        var info = HorizontalInfo(shell);
        var activePoint = shell.Terminal.LastRenderedGrid?.MeasuredActivePoint;

        Assert.True(info.visible);
        Assert.Equal(6, info.offset);
        Assert.Equal(6, info.viewport);
        Assert.Equal(12, info.total);
        Assert.Equal(6, info.maxOffset);
        Assert.NotNull(activePoint);
        Assert.Equal(5, activePoint!.OffsetInLine);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal("abc def ghi", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_LeftRight_AdjustHorizontalOffsetMinimally()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(shell, ct, "abcdefghij", width: 6);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        Assert.Equal(5, HorizontalInfo(shell).offset);

        shell.Terminal.EnqueueKeys(
            ConsoleKey.LeftArrow,
            ConsoleKey.LeftArrow,
            ConsoleKey.LeftArrow,
            ConsoleKey.LeftArrow,
            ConsoleKey.LeftArrow);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(5, HorizontalInfo(shell).offset);

        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(4, HorizontalInfo(shell).offset);

        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(4, HorizontalInfo(shell).offset);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);
    }

    [Fact]
    public async Task ModalFlow_HomeAndEnd_UpdateHorizontalOffset()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(shell, ct, "abcdefghij", width: 6);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        Assert.Equal(5, HorizontalInfo(shell).offset);

        shell.Terminal.EnqueueKey(ConsoleKey.Home);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        var homeInfo = HorizontalInfo(shell);
        Assert.Equal(0, homeInfo.offset);
        Assert.Contains(ConsoleSymbol.TriangleRight, shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.End);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        var endInfo = HorizontalInfo(shell);
        Assert.Equal(5, endInfo.offset);
        Assert.Contains(ConsoleSymbol.TriangleLeft, shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);
    }

    [Fact]
    public async Task ModalFlow_SecretMode_HorizontalScrollMasksRenderAndReturnsRealPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var input = new InlineTextInput(shell, isSecret: true, width: 6);
        var dialog = new InlineDialog(shell, title: null, input, "Password");
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        EnqueueText(shell.Terminal, "abcdefghij");
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        var info = HorizontalInfo(shell);
        Assert.True(info.visible);
        Assert.Equal(5, info.offset);
        Assert.Contains(new string(ConsoleSymbol.Bullet, 5), shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("abcdefghij", shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal("abcdefghij", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_SecretMode_PolishCharactersMasksRenderAndReturnsRealPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var input = new InlineTextInput(shell, isSecret: true);
        var dialog = new InlineDialog(shell, title: null, input, "Password");
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        EnqueueText(shell.Terminal, PolishLetters, ConsoleModifiers.Control | ConsoleModifiers.Alt);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Contains(new string(ConsoleSymbol.Bullet, PolishLetters.Length), shell.Terminal.LastRenderedText);
        Assert.DoesNotContain(PolishLetters, shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal(PolishLetters, result.Payload);
    }

    [Fact]
    public async Task ModalFlow_SecretMode_EditingKeysReturnEditedPayloadAndStayMasked()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var input = new InlineTextInput(shell, "abcd", isSecret: true, width: 10);
        var dialog = new InlineDialog(shell, title: null, input, "Password");
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
        shell.Terminal.EnqueueKey(ConsoleKey.X, keyChar: 'X');
        shell.Terminal.EnqueueKey(ConsoleKey.Delete);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Contains(new string(ConsoleSymbol.Bullet, 3), shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("abcd", shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("abX", shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal("abX", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_HorizontalIndicators_ShowOverflowDirection()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var runTask = RunInputAsync(shell, ct, "abcdefghij", width: 6);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        Assert.Contains(ConsoleSymbol.TriangleLeft, shell.Terminal.LastRenderedText);
        Assert.DoesNotContain(ConsoleSymbol.TriangleRight, shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Home);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), ct);

        Assert.DoesNotContain(ConsoleSymbol.TriangleLeft, shell.Terminal.LastRenderedText);
        Assert.Contains(ConsoleSymbol.TriangleRight, shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);
    }

    [Fact]
    public async Task ModalFlow_EscapeCancels()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await RunInputAsync(shell, ct, "abc");

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
        Assert.Equal("abc", result.Payload);
    }

    [Fact]
    public async Task ModalFlow_NoKeyBeforeTimeoutReturnsTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var input = new InlineTextInput(shell);
        var dialog = new InlineDialog(shell, title: null, input, "Name");

        var result = await shell.RunModalAsync(dialog, TimeSpan.FromMilliseconds(20), ct)
            .WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Timeout, result.Kind);
        Assert.Null(result.Payload);
    }

    [Fact]
    public async Task ModalFlow_ExternalCancellation_ReturnsTokenCancel()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var shell = new TestShell();
        var runTask = RunInputAsync(shell, cts.Token, "abc");

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(DialogResultKind.TokenCancel, result.Kind);
        Assert.Null(result.Payload);
    }

    [Fact]
    public async Task ModalFlow_SecretMode_MasksRenderButReturnsRealPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell();
        var input = new InlineTextInput(shell, "hide", isSecret: true);
        var dialog = new InlineDialog(shell, title: null, input, "Password");
        var runTask = shell.RunModalAsync(dialog, ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);

        Assert.Contains(new string(ConsoleSymbol.Bullet, 4), shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("hide", shell.Terminal.LastRenderedText);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(1), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal("hide", result.Payload);
    }

    private static Task<DialogResult> RunInputAsync(
        TestShell shell,
        CancellationToken ct,
        string? initialValue = null,
        int? width = null,
        bool isSecret = false,
        Func<string, string?>? validator = null)
    {
        var input = new InlineTextInput(shell, initialValue, isSecret, width, validator);
        var dialog = new InlineDialog(shell, title: null, input, "Name");
        return shell.RunModalAsync(dialog, ct);
    }

    private static void EnqueueText(
        TestTerminal terminal,
        string text,
        ConsoleModifiers modifiers = ConsoleModifiers.None)
    {
        foreach (char ch in text)
        {
            var key = ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
                ? Enum.Parse<ConsoleKey>(char.ToUpperInvariant(ch).ToString())
                : ConsoleKey.Spacebar;
            if (key == ConsoleKey.Spacebar && ch != ' ')
                key = ConsoleKey.None;

            terminal.EnqueueKey(key, modifiers, ch);
        }
    }

    private static (bool visible, int offset, int viewport, int total, int maxOffset) HorizontalInfo(TestShell shell)
    {
        var info = shell.Terminal.LastRenderedGrid?.GetHorizontalScrollInfo();
        Assert.NotNull(info);
        return info!.Value;
    }

    private static int InputContentWidth(string line)
    {
        int left = line.IndexOf(ConsoleSymbol.DoubleV);
        int right = line.IndexOf(ConsoleSymbol.DoubleV, left + 1);

        Assert.True(left >= 0);
        Assert.True(right > left);

        return right - left - 3;
    }
}
