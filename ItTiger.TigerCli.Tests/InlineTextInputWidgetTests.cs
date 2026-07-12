using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineTextInputWidgetTests : TestBase
{
    private const string PolishLetters = "ĄĆĘŁŃÓŚŹŻąćęłńóśźż";

    [Fact]
    public void PrintableInput_InsertsAndAdvancesCursor()
    {
        var shell = new TestShell();
        var widget = new InlineTextInputWidget(shell);

        Assert.True(widget.HandleKey(Key(ConsoleKey.A, 'a')).IsHandled);
        Assert.True(widget.HandleKey(Key(ConsoleKey.D1, '1')).IsHandled);
        Assert.True(widget.HandleKey(Key(ConsoleKey.OemPeriod, '.')).IsHandled);

        Assert.Equal("a1.", widget.Text);
        Assert.Equal(3, widget.CursorIndex);
    }

    [Fact]
    public void UnicodeAndAltGrPrintableInput_UsesKeyChar()
    {
        var shell = new TestShell();
        var widget = new InlineTextInputWidget(shell);

        foreach (char ch in PolishLetters)
            Assert.True(widget.HandleKey(new KeyEvent(ConsoleKey.None, ConsoleModifiers.Control | ConsoleModifiers.Alt, ch)).IsHandled);

        Assert.Equal(PolishLetters, widget.Text);
        Assert.Equal(PolishLetters.Length, widget.CursorIndex);
    }

    [Fact]
    public void AltOrControlWithoutPrintableKeyChar_IsNotHandled()
    {
        var shell = new TestShell();
        var widget = new InlineTextInputWidget(shell, "abc");

        Assert.False(widget.HandleKey(new KeyEvent(ConsoleKey.LeftArrow, ConsoleModifiers.Alt)).IsHandled);
        Assert.Equal("abc", widget.Text);
        Assert.Equal(3, widget.CursorIndex);
    }

    [Fact]
    public void BackspaceDeleteAndCursorMovement_EditAtCursor()
    {
        var shell = new TestShell();
        var widget = new InlineTextInputWidget(shell, "abcd");

        Assert.True(widget.HandleKey(Key(ConsoleKey.LeftArrow)).IsHandled);
        Assert.True(widget.HandleKey(Key(ConsoleKey.Backspace)).IsHandled);
        Assert.True(widget.HandleKey(Key(ConsoleKey.X, 'X')).IsHandled);
        Assert.True(widget.HandleKey(Key(ConsoleKey.Delete)).IsHandled);

        Assert.Equal("abX", widget.Text);
        Assert.Equal(3, widget.CursorIndex);
    }

    [Fact]
    public void HomeAndEnd_MoveCursor()
    {
        var shell = new TestShell();
        var widget = new InlineTextInputWidget(shell, "bd");

        Assert.True(widget.HandleKey(Key(ConsoleKey.Home)).IsHandled);
        Assert.True(widget.HandleKey(Key(ConsoleKey.A, 'a')).IsHandled);
        Assert.True(widget.HandleKey(Key(ConsoleKey.End)).IsHandled);
        Assert.True(widget.HandleKey(Key(ConsoleKey.C, 'c')).IsHandled);

        Assert.Equal("abdc", widget.Text);
        Assert.Equal(4, widget.CursorIndex);
    }

    [Fact]
    public void SecretMode_RendersBulletsButKeepsRealText()
    {
        var shell = new TestShell();
        var widget = new InlineTextInputWidget(shell, "hide", isSecret: true)
        {
            HasFocus = true
        };

        var text = string.Join("\n", TigerConsole.RenderGridToLines(widget.ToGrid()));

        Assert.Equal("hide", widget.Text);
        Assert.Contains(new string(ConsoleSymbol.Bullet, 4), text);
        Assert.DoesNotContain("hide", text);
    }

    [Fact]
    public void Focus_ControlsCursorVisibility()
    {
        var shell = new TestShell();
        var widget = new InlineTextInputWidget(shell, "abc");

        Assert.Equal(CursorMode.Hidden, widget.ToGrid().CursorMode);

        widget.HasFocus = true;

        Assert.Equal(CursorMode.Normal, widget.ToGrid().CursorMode);
    }

    [Fact]
    public void RenderingMatchesInlineTextInputWrapper()
    {
        var shell = new TestShell();
        var widget = new InlineTextInputWidget(shell, "server")
        {
            HasFocus = true
        };
        var input = new InlineTextInput(shell, "server");

        Assert.Equal(
            TigerConsole.RenderGridToLines(input.ToGrid()),
            TigerConsole.RenderGridToLines(widget.ToGrid()));
    }

    [Fact]
    public void WrapperGetWidgets_ReturnsFocusedInFrameWithIndicatorsWidget()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell, "abc");

        var widget = Assert.Single(input.GetWidgets());

        Assert.True(widget.IsFocused);
        Assert.Equal(InlineDialogArea.InFrameWithIndicators, widget.Area);
        Assert.Equal(input.ControlDecoration, widget.Decoration);
        Assert.Equal(input.ScrollMode, widget.ScrollMode);
        Assert.Equal(input.ThumbMode, widget.ThumbMode);
        Assert.Same(input.ToGrid(), widget.Grid);
    }

    [Fact]
    public void WrapperHandleKey_DelegatesEditingToWidget()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell);

        Assert.True(input.HandleKey(Key(ConsoleKey.A, 'a')).IsHandled);

        Assert.Equal("a", input.Payload);
        Assert.Contains("a", string.Join("\n", TigerConsole.RenderGridToLines(input.ToGrid())));
    }

    private static KeyEvent Key(ConsoleKey key, char keyChar = '\0')
        => new(key, ConsoleModifiers.None, keyChar);
}
