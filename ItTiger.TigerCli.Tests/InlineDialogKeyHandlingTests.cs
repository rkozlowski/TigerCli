using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Proves the fallback contract for <see cref="InlineDialog.HandleKey"/>:
///
///   Control gets the first chance at every key.
///   The dialog handles Enter/Escape only when the control did not handle them.
///
/// Existing controls (InlineTextInput, InlineSelect, InlineMultiSelect) do not consume Enter or
/// Escape, so the dialog's confirm/cancel fallback still fires for them.
/// </summary>
public sealed class InlineDialogKeyHandlingTests : TestBase
{
    // Minimal control that returns whichever result the function selects, so we can prove the
    // dialog respects the control's handled/result decision.
    private sealed class KeyConsumingControl : InlineControlBase
    {
        private readonly Func<KeyEvent, InlineKeyResult> _handle;

        public KeyConsumingControl(ICliAppShell shell, Func<KeyEvent, bool> consume) : base(shell)
            => _handle = key => consume(key) ? InlineKeyResult.Handled : InlineKeyResult.NotHandled;

        public KeyConsumingControl(ICliAppShell shell, Func<KeyEvent, InlineKeyResult> handle) : base(shell)
            => _handle = handle;

        public override InlineKeyResult HandleKey(KeyEvent key) => _handle(key);
        public override object? Payload => null;

        public override CliGrid ToGrid()
        {
            var g = ToGrid(1, 1);
            g.Set(0, 0, "x");
            return g;
        }
    }

    private static KeyEvent Enter() => new(ConsoleKey.Enter, ConsoleModifiers.None);
    private static KeyEvent Escape() => new(ConsoleKey.Escape, ConsoleModifiers.None);

    private static InlineSelect Select(ICliAppShell shell)
        => new(shell, new[] { "Alpha", "Beta", "Gamma" }, preselectIndex: 0);

    private static InlineMultiSelect MultiSelect(ICliAppShell shell)
        => new(shell, new[] { "Alpha", "Beta", "Gamma" });

    // ------------------------------------------------------------------
    // Dialog fallback fires when the control does not handle Enter/Escape
    // ------------------------------------------------------------------

    [Fact]
    public void TextInput_DoesNotHandleEnter_DialogConfirms()
    {
        var shell = new TestShell();
        var dialog = new InlineDialog(shell, "Title", new InlineTextInput(shell, "hello"));

        bool handled = dialog.HandleKey(Enter());

        Assert.True(handled);
        Assert.Equal(DialogResultKind.Ok, dialog.Result);
    }

    [Fact]
    public void Select_DoesNotHandleEnter_DialogConfirms()
    {
        var shell = new TestShell();
        var dialog = new InlineDialog(shell, "Title", Select(shell));

        bool handled = dialog.HandleKey(Enter());

        Assert.True(handled);
        Assert.Equal(DialogResultKind.Ok, dialog.Result);
    }

    [Fact]
    public void MultiSelect_DoesNotHandleEnter_DialogConfirms()
    {
        var shell = new TestShell();
        var dialog = new InlineDialog(shell, "Title", MultiSelect(shell));

        bool handled = dialog.HandleKey(Enter());

        Assert.True(handled);
        Assert.Equal(DialogResultKind.Ok, dialog.Result);
    }

    [Fact]
    public void Escape_NotHandledByControl_DialogCancels()
    {
        var shell = new TestShell();
        var dialog = new InlineDialog(shell, "Title", Select(shell));

        bool handled = dialog.HandleKey(Escape());

        Assert.True(handled);
        Assert.Equal(DialogResultKind.Cancel, dialog.Result);
    }

    // ------------------------------------------------------------------
    // Control gets first chance: consuming the key blocks the fallback
    // ------------------------------------------------------------------

    [Fact]
    public void Control_ConsumesEnter_DialogDoesNotConfirm()
    {
        var shell = new TestShell();
        var control = new KeyConsumingControl(shell, k => k.Key == ConsoleKey.Enter);
        var dialog = new InlineDialog(shell, "Title", control);

        bool handled = dialog.HandleKey(Enter());

        Assert.True(handled);                                  // the control handled it
        Assert.Equal(DialogResultKind.NoResult, dialog.Result); // dialog did not confirm
    }

    [Fact]
    public void Control_ConsumesEscape_DialogDoesNotCancel()
    {
        var shell = new TestShell();
        var control = new KeyConsumingControl(shell, k => k.Key == ConsoleKey.Escape);
        var dialog = new InlineDialog(shell, "Title", control);

        bool handled = dialog.HandleKey(Escape());

        Assert.True(handled);                                  // the control handled it
        Assert.Equal(DialogResultKind.NoResult, dialog.Result); // dialog did not cancel
    }

    // ------------------------------------------------------------------
    // Fallback still consumes Enter (no-op) when confirmation is not allowed
    // ------------------------------------------------------------------

    [Fact]
    public void Enter_WhenControlCannotConfirm_IsConsumedButDoesNotConfirm()
    {
        var shell = new TestShell();
        // Empty select cannot confirm (CanConfirm == false) and does not handle Enter.
        var emptySelect = new InlineSelect(shell, Array.Empty<string>());
        var dialog = new InlineDialog(shell, "Title", emptySelect);

        bool handled = dialog.HandleKey(Enter());

        Assert.True(handled);                                   // Enter is still consumed
        Assert.Equal(DialogResultKind.NoResult, dialog.Result); // but no confirmation committed
    }

    // ------------------------------------------------------------------
    // Control-supplied dialog results (InlineKeyResult.WithResult)
    // ------------------------------------------------------------------

    [Fact]
    public void Control_ReturnsResult_DialogAppliesIt()
    {
        var shell = new TestShell();
        var control = new KeyConsumingControl(
            shell, k => k.Key == ConsoleKey.Y ? InlineKeyResult.WithResult(DialogResultKind.Yes) : InlineKeyResult.NotHandled);
        var dialog = new InlineDialog(shell, "Title", control);

        bool handled = dialog.HandleKey(new KeyEvent(ConsoleKey.Y, ConsoleModifiers.None, 'y'));

        Assert.True(handled);
        Assert.Equal(DialogResultKind.Yes, dialog.Result);
    }

    [Fact]
    public void Control_HandledWithNoResult_PreventsDialogFallback()
    {
        var shell = new TestShell();
        // Handled but with NoResult: the dialog must not apply its Enter-confirm fallback.
        var control = new KeyConsumingControl(shell, _ => InlineKeyResult.Handled);
        var dialog = new InlineDialog(shell, "Title", control);

        bool handled = dialog.HandleKey(Enter());

        Assert.True(handled);
        Assert.Equal(DialogResultKind.NoResult, dialog.Result);
    }
}
