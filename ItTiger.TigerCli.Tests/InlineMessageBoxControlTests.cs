using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers <see cref="InlineMessageBoxControl"/>: button-set mapping, widget composition, focus, and
/// key routing to the button group. The button group is the top-level interactive widget; individual
/// buttons are not separate dialog widgets.
/// </summary>
public sealed class InlineMessageBoxControlTests : TestBase
{
    private static KeyEvent Key(ConsoleKey key) => new(key, ConsoleModifiers.None);

    private static InlineMessageBoxControl Control(
        ICliAppShell shell, MessageBoxButtons buttons, DialogResultKind? defaultButton = null)
        => new(shell, "Message text", buttons, defaultButton);

    // ------------------------------------------------------------------
    // Button-set mapping
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(MessageBoxButtons.Ok, new[] { DialogResultKind.Ok })]
    [InlineData(MessageBoxButtons.OkCancel, new[] { DialogResultKind.Ok, DialogResultKind.Cancel })]
    [InlineData(MessageBoxButtons.YesNo, new[] { DialogResultKind.Yes, DialogResultKind.No })]
    [InlineData(MessageBoxButtons.YesNoCancel, new[] { DialogResultKind.Yes, DialogResultKind.No, DialogResultKind.Cancel })]
    [InlineData(MessageBoxButtons.AbortRetryIgnore, new[] { DialogResultKind.Abort, DialogResultKind.Retry, DialogResultKind.Ignore })]
    public void ButtonSet_MapsToExpectedResults(MessageBoxButtons buttons, DialogResultKind[] expected)
    {
        var shell = new TestShell();
        var control = Control(shell, buttons);

        var results = control.ButtonGroup.Buttons.Select(b => b.Result).ToArray();

        Assert.Equal(expected, results);
    }

    [Theory]
    // Default active button is the first button for every set (OK / Yes / Abort all sit at index 0).
    [InlineData(MessageBoxButtons.Ok, DialogResultKind.Ok)]
    [InlineData(MessageBoxButtons.OkCancel, DialogResultKind.Ok)]
    [InlineData(MessageBoxButtons.YesNo, DialogResultKind.Yes)]
    [InlineData(MessageBoxButtons.YesNoCancel, DialogResultKind.Yes)]
    [InlineData(MessageBoxButtons.AbortRetryIgnore, DialogResultKind.Abort)]
    public void DefaultActiveButton_IsFirstButton(MessageBoxButtons buttons, DialogResultKind expected)
    {
        var shell = new TestShell();
        var control = Control(shell, buttons);

        Assert.Equal(0, control.ButtonGroup.ActiveIndex);
        Assert.Equal(expected, control.ButtonGroup.ActiveButton!.Result);
    }

    [Fact]
    public void DefaultButtonOverride_SelectsMatchingButton()
    {
        var shell = new TestShell();
        var control = Control(shell, MessageBoxButtons.YesNo, defaultButton: DialogResultKind.No);

        Assert.Equal(1, control.ButtonGroup.ActiveIndex);
        Assert.Equal(DialogResultKind.No, control.ButtonGroup.ActiveButton!.Result);
    }

    // ------------------------------------------------------------------
    // Widget composition
    // ------------------------------------------------------------------

    [Fact]
    public void GetWidgets_ReturnsMessageScrollable_AndButtonGroupInFrame()
    {
        var shell = new TestShell();
        var control = Control(shell, MessageBoxButtons.Ok);

        var widgets = control.GetWidgets();

        Assert.Equal(2, widgets.Count);

        var message = widgets[0];
        Assert.Equal(InlineDialogArea.InFrameScrollable, message.Area);
        Assert.False(message.IsFocused);

        var buttonGroup = widgets[1];
        Assert.Equal(InlineDialogArea.InFrame, buttonGroup.Area);
        Assert.True(buttonGroup.IsFocused);
    }

    [Fact]
    public void ButtonGroup_IsFocusedWidgetInitially()
    {
        var shell = new TestShell();
        var control = Control(shell, MessageBoxButtons.YesNo);

        Assert.True(control.ButtonGroup.HasFocus);
        var focused = control.GetWidgets().Single(w => w.IsFocused);
        Assert.Equal(InlineDialogArea.InFrame, focused.Area);
    }

    // ------------------------------------------------------------------
    // Key routing to the button group
    // ------------------------------------------------------------------

    [Fact]
    public void Enter_ActivatesFocusedButton()
    {
        var shell = new TestShell();
        var control = Control(shell, MessageBoxButtons.Ok);

        var result = control.HandleKey(Key(ConsoleKey.Enter));

        Assert.True(result.IsHandled);
        Assert.Equal(DialogResultKind.Ok, result.Result);
    }

    [Fact]
    public void Space_ActivatesFocusedButton()
    {
        var shell = new TestShell();
        var control = Control(shell, MessageBoxButtons.YesNo);

        var result = control.HandleKey(Key(ConsoleKey.Spacebar));

        Assert.True(result.IsHandled);
        Assert.Equal(DialogResultKind.Yes, result.Result);
    }

    [Fact]
    public void RightAndLeft_ChangeActiveButton()
    {
        var shell = new TestShell();
        var control = Control(shell, MessageBoxButtons.YesNo);

        Assert.True(control.HandleKey(Key(ConsoleKey.RightArrow)).IsHandled);
        Assert.Equal(DialogResultKind.No, control.HandleKey(Key(ConsoleKey.Enter)).Result);

        control.HandleKey(Key(ConsoleKey.LeftArrow));
        Assert.Equal(DialogResultKind.Yes, control.HandleKey(Key(ConsoleKey.Enter)).Result);
    }

    [Fact]
    public void Escape_IsNotHandled_ForDialogCancelFallback()
    {
        var shell = new TestShell();
        var control = Control(shell, MessageBoxButtons.Ok);

        var result = control.HandleKey(Key(ConsoleKey.Escape));

        Assert.False(result.IsHandled);
        Assert.Equal(DialogResultKind.NoResult, result.Result);
    }

    [Fact]
    public void Control_DoesNotConfirmViaFallback()
    {
        var shell = new TestShell();
        var control = Control(shell, MessageBoxButtons.Ok);

        // The focused button drives the result; the control must not let the dialog's Enter-confirm
        // fallback fire.
        Assert.False(control.CanConfirm);
    }

    [Fact]
    public void Hint_IsProvided()
    {
        var shell = new TestShell();
        var control = Control(shell, MessageBoxButtons.Ok);

        Assert.False(string.IsNullOrWhiteSpace(control.Hint));
    }

    // ------------------------------------------------------------------
    // Message-box kind → dialog surface
    // ------------------------------------------------------------------

    [Fact]
    public void Kind_DefaultsToNormal_AndUsesDialogSurface()
    {
        var shell = new TestShell();
        var control = Control(shell, MessageBoxButtons.Ok);

        Assert.Equal(MessageBoxKind.Normal, control.Kind);
        Assert.Equal(ThemeStyle.DialogSurface, control.DialogSurfaceStyle);
    }

    [Theory]
    [InlineData(MessageBoxKind.Normal, ThemeStyle.DialogSurface)]
    [InlineData(MessageBoxKind.Warning, ThemeStyle.WarningSurface)]
    [InlineData(MessageBoxKind.Error, ThemeStyle.ErrorSurface)]
    public void Kind_SelectsSemanticDialogSurface(MessageBoxKind kind, ThemeStyle expected)
    {
        var shell = new TestShell();
        var control = new InlineMessageBoxControl(shell, "Message text", MessageBoxButtons.Ok, kind: kind);

        Assert.Equal(kind, control.Kind);
        Assert.Equal(expected, control.DialogSurfaceStyle);
    }
}
