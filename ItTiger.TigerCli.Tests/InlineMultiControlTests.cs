using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineMultiControlTests
{
    private sealed class StubWidget : InlineWidget
    {
        private readonly string _text;
        private readonly InlineKeyResult _result;

        public StubWidget(ICliAppShell shell, string text, InlineKeyResult? result = null, bool focusable = true)
            : base(shell)
        {
            _text = text;
            _result = result ?? InlineKeyResult.Handled;
            FocusableOverride = focusable;
        }

        public int HandleCount { get; private set; }
        public bool FocusableOverride { get; }
        public override bool Focusable => FocusableOverride;
        public override string? Hint => $"{_text} hint";

        public override InlineKeyResult HandleKey(KeyEvent key)
        {
            HandleCount++;
            return _result;
        }

        public override CliGrid ToGrid()
        {
            var g = ToGrid(1, 1);
            g.Set(0, 0, _text);
            return g;
        }
    }

    private sealed class StubMultiControl : InlineMultiControl
    {
        public StubMultiControl(ICliAppShell shell)
            : base(shell)
        {
        }

        public override object? Payload => null;
        public int FocusIndex => FocusedWidgetIndex;

        public int Add(StubWidget widget, string? hint = null)
        {
            return AddWidget(widget, InlineDialogArea.InFrame, hint: hint);
        }
    }

    private static KeyEvent Key(ConsoleKey key, ConsoleModifiers mods = ConsoleModifiers.None)
        => new(key, mods);

    [Fact]
    public void Tab_MovesToNextFocusableWidget()
    {
        var shell = new TestShell();
        var control = new StubMultiControl(shell);
        control.Add(new StubWidget(shell, "one"));
        control.Add(new StubWidget(shell, "two"));

        Assert.Equal(0, control.FocusIndex);

        Assert.True(control.HandleKey(Key(ConsoleKey.Tab)).IsHandled);

        Assert.Equal(1, control.FocusIndex);
        Assert.True(control.GetWidgets()[1].IsFocused);
    }

    [Fact]
    public void ShiftTab_MovesToPreviousFocusableWidget()
    {
        var shell = new TestShell();
        var control = new StubMultiControl(shell);
        control.Add(new StubWidget(shell, "one"));
        control.Add(new StubWidget(shell, "two"));
        control.HandleKey(Key(ConsoleKey.Tab));

        Assert.True(control.HandleKey(Key(ConsoleKey.Tab, ConsoleModifiers.Shift)).IsHandled);

        Assert.Equal(0, control.FocusIndex);
    }

    [Fact]
    public void FocusTraversal_SkipsNonFocusableWidgets()
    {
        var shell = new TestShell();
        var control = new StubMultiControl(shell);
        control.Add(new StubWidget(shell, "one"));
        control.Add(new StubWidget(shell, "skip", focusable: false));
        control.Add(new StubWidget(shell, "two"));

        control.HandleKey(Key(ConsoleKey.Tab));

        Assert.Equal(2, control.FocusIndex);
    }

    [Fact]
    public void FocusedWidgetReceivesKeys_UnfocusedWidgetsDoNot()
    {
        var shell = new TestShell();
        var first = new StubWidget(shell, "one");
        var second = new StubWidget(shell, "two");
        var control = new StubMultiControl(shell);
        control.Add(first);
        control.Add(second);

        control.HandleKey(Key(ConsoleKey.A));
        control.HandleKey(Key(ConsoleKey.Tab));
        control.HandleKey(Key(ConsoleKey.B));

        Assert.Equal(1, first.HandleCount);
        Assert.Equal(1, second.HandleCount);
    }

    [Fact]
    public void GetWidgets_MarksExactlyOneWidgetFocused_AndUpdatesAfterTraversal()
    {
        var shell = new TestShell();
        var control = new StubMultiControl(shell);
        control.Add(new StubWidget(shell, "one"));
        control.Add(new StubWidget(shell, "two"));

        Assert.Single(control.GetWidgets(), w => w.IsFocused);

        control.HandleKey(Key(ConsoleKey.Tab));
        var widgets = control.GetWidgets();

        Assert.Single(widgets, w => w.IsFocused);
        Assert.False(widgets[0].IsFocused);
        Assert.True(widgets[1].IsFocused);
    }

    [Fact]
    public void Hint_FollowsFocusedWidgetOrSlot()
    {
        var shell = new TestShell();
        var control = new StubMultiControl(shell);
        control.Add(new StubWidget(shell, "one"));
        control.Add(new StubWidget(shell, "two"), hint: "slot hint");

        Assert.Equal("one hint", control.Hint);

        control.HandleKey(Key(ConsoleKey.Tab));

        Assert.Equal("slot hint", control.Hint);
    }

    [Fact]
    public void HandledNoResult_PreventsDialogEnterFallback()
    {
        var shell = new TestShell();
        var control = new StubMultiControl(shell);
        control.Add(new StubWidget(shell, "one", InlineKeyResult.Handled));
        var dialog = new InlineDialog(shell, title: null, control);

        Assert.True(dialog.HandleKey(Key(ConsoleKey.Enter)));

        Assert.Equal(DialogResultKind.NoResult, dialog.Result);
    }

    [Fact]
    public void WidgetResult_CompletesDialog()
    {
        var shell = new TestShell();
        var control = new StubMultiControl(shell);
        control.Add(new StubWidget(shell, "one", InlineKeyResult.WithResult(DialogResultKind.Ok)));
        var dialog = new InlineDialog(shell, title: null, control);

        Assert.True(dialog.HandleKey(Key(ConsoleKey.Spacebar)));

        Assert.Equal(DialogResultKind.Ok, dialog.Result);
    }
}
