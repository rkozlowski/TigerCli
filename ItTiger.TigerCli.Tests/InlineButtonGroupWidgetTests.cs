using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineButtonGroupWidgetTests : TestBase
{
    private static KeyEvent Key(ConsoleKey key) => new(key, ConsoleModifiers.None);

    private static InlineButtonGroupWidget Group(
        ICliAppShell shell,
        int? activeIndex = null,
        params (string label, DialogResultKind result, bool enabled)[] buttons)
    {
        var widgets = buttons
            .Select(b => new InlineButtonWidget(shell, b.label, b.result, b.enabled))
            .ToArray();
        // The group under test stands in for the dialog's focused button row (message boxes set
        // HasFocus = true on it), so its active button renders in the focused state.
        return new InlineButtonGroupWidget(shell, widgets, activeIndex) { HasFocus = true };
    }

    private static string Render(InlineButtonGroupWidget group)
        => string.Join("\n", TigerConsole.RenderGridToLines(group.ToGrid()));

    [Fact]
    public void RendersFocusedAndUnfocusedButtons()
    {
        var shell = new TestShell();
        var group = Group(shell, activeIndex: 0,
            ("Yes", DialogResultKind.Yes, true),
            ("No", DialogResultKind.No, true));

        var text = Render(group);

        Assert.Contains($"[{ConsoleSymbol.MarkerRight} Yes {ConsoleSymbol.MarkerLeft}]", text); // focused
        Assert.Contains("[  No  ]", text);                                                      // unfocused
    }

    [Fact]
    public void FocusedButton_UsesVisibleMarkers_NotOnlyColour()
    {
        var shell = new TestShell();
        var group = Group(shell, activeIndex: 0, ("Ok", DialogResultKind.Ok, true));

        // RenderGridToLines strips ANSI colour, so the markers must be visible as glyphs.
        var text = Render(group);

        Assert.Contains(ConsoleSymbol.MarkerRight, text);
        Assert.Contains(ConsoleSymbol.MarkerLeft, text);
    }

    [Fact]
    public void RightArrow_And_LeftArrow_MoveActiveButton()
    {
        var shell = new TestShell();
        var group = Group(shell, activeIndex: 0,
            ("Yes", DialogResultKind.Yes, true),
            ("No", DialogResultKind.No, true));

        Assert.True(group.HandleKey(Key(ConsoleKey.RightArrow)).IsHandled);
        Assert.Equal(1, group.ActiveIndex);
        Assert.Contains($"[{ConsoleSymbol.MarkerRight} No {ConsoleSymbol.MarkerLeft}]", Render(group));

        Assert.True(group.HandleKey(Key(ConsoleKey.LeftArrow)).IsHandled);
        Assert.Equal(0, group.ActiveIndex);
    }

    [Fact]
    public void ArrowKeys_ClampAtEnds()
    {
        var shell = new TestShell();
        var group = Group(shell, activeIndex: 0,
            ("Yes", DialogResultKind.Yes, true),
            ("No", DialogResultKind.No, true));

        group.HandleKey(Key(ConsoleKey.LeftArrow));
        Assert.Equal(0, group.ActiveIndex);

        group.HandleKey(Key(ConsoleKey.RightArrow));
        group.HandleKey(Key(ConsoleKey.RightArrow));
        Assert.Equal(1, group.ActiveIndex);
    }

    [Fact]
    public void HomeAndEnd_MoveToFirstAndLast()
    {
        var shell = new TestShell();
        var group = Group(shell, activeIndex: 1,
            ("Abort", DialogResultKind.Abort, true),
            ("Retry", DialogResultKind.Retry, true),
            ("Ignore", DialogResultKind.Ignore, true));

        Assert.True(group.HandleKey(Key(ConsoleKey.End)).IsHandled);
        Assert.Equal(2, group.ActiveIndex);

        Assert.True(group.HandleKey(Key(ConsoleKey.Home)).IsHandled);
        Assert.Equal(0, group.ActiveIndex);
    }

    [Fact]
    public void Enter_ActivatesActiveButton()
    {
        var shell = new TestShell();
        var group = Group(shell, activeIndex: 1,
            ("Yes", DialogResultKind.Yes, true),
            ("No", DialogResultKind.No, true));

        var result = group.HandleKey(Key(ConsoleKey.Enter));

        Assert.True(result.IsHandled);
        Assert.Equal(DialogResultKind.No, result.Result);
    }

    [Fact]
    public void Space_ActivatesActiveButton()
    {
        var shell = new TestShell();
        var group = Group(shell, activeIndex: 0,
            ("Ok", DialogResultKind.Ok, true),
            ("Cancel", DialogResultKind.Cancel, true));

        var result = group.HandleKey(Key(ConsoleKey.Spacebar));

        Assert.True(result.IsHandled);
        Assert.Equal(DialogResultKind.Ok, result.Result);
    }

    [Theory]
    [InlineData(0, DialogResultKind.Ok)]
    [InlineData(1, DialogResultKind.Yes)]
    [InlineData(2, DialogResultKind.No)]
    [InlineData(3, DialogResultKind.Cancel)]
    public void ReturnsExpectedResultForActiveButton(int activeIndex, DialogResultKind expected)
    {
        var shell = new TestShell();
        var group = Group(shell, activeIndex,
            ("Ok", DialogResultKind.Ok, true),
            ("Yes", DialogResultKind.Yes, true),
            ("No", DialogResultKind.No, true),
            ("Cancel", DialogResultKind.Cancel, true));

        var result = group.HandleKey(Key(ConsoleKey.Enter));

        Assert.Equal(expected, result.Result);
    }

    [Fact]
    public void DisabledActiveButton_ConsumesEnterAndSpace_WithoutResult()
    {
        var shell = new TestShell();
        // Navigate onto a disabled button, then try to activate it.
        var group = Group(shell, activeIndex: 0,
            ("Ok", DialogResultKind.Ok, true),
            ("Cancel", DialogResultKind.Cancel, false));

        group.HandleKey(Key(ConsoleKey.RightArrow));
        Assert.Equal(1, group.ActiveIndex);

        var enter = group.HandleKey(Key(ConsoleKey.Enter));
        Assert.True(enter.IsHandled);                          // consumed, so no Enter fallback fires
        Assert.Equal(DialogResultKind.NoResult, enter.Result); // but produces no result

        var space = group.HandleKey(Key(ConsoleKey.Spacebar));
        Assert.True(space.IsHandled);
        Assert.Equal(DialogResultKind.NoResult, space.Result);
    }

    [Fact]
    public void Escape_IsNotConsumed()
    {
        var shell = new TestShell();
        var group = Group(shell, activeIndex: 0,
            ("Ok", DialogResultKind.Ok, true),
            ("Cancel", DialogResultKind.Cancel, true));

        var result = group.HandleKey(Key(ConsoleKey.Escape));

        Assert.False(result.IsHandled);                        // left for the dialog's cancel fallback
        Assert.Equal(DialogResultKind.NoResult, result.Result);
    }

    [Fact]
    public void SingleOkButton_Activates()
    {
        var shell = new TestShell();
        var group = Group(shell, activeIndex: 0, ("OK", DialogResultKind.Ok, true));

        Assert.Contains($"[{ConsoleSymbol.MarkerRight} OK {ConsoleSymbol.MarkerLeft}]", Render(group));

        var result = group.HandleKey(Key(ConsoleKey.Enter));
        Assert.True(result.IsHandled);
        Assert.Equal(DialogResultKind.Ok, result.Result);
    }

    [Fact]
    public void DisabledButton_Activate_IsHandledWithoutResult()
    {
        var shell = new TestShell();
        var button = new InlineButtonWidget(shell, "Nope", DialogResultKind.Ok, enabled: false);

        var result = button.Activate();

        Assert.True(result.IsHandled);
        Assert.Equal(DialogResultKind.NoResult, result.Result);
        Assert.False(button.Focusable);
    }
}
