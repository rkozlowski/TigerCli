using System.Globalization;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Proves a third-party can build a custom <see cref="InlineControlBase"/>, wrap it in an
/// <see cref="InlineDialog"/>, and run it through the public <see cref="TigerTui"/> facade
/// (<see cref="TigerTui.RunControlAsync(InlineControlBase, string?, InlineDialogConfirmationPolicy?, TimeSpan?, CancellationToken)"/>
/// / <see cref="TigerTui.RunDialogAsync(ICliDialog, TimeSpan?, CancellationToken)"/>) and the public
/// <see cref="ICliAppShell"/> contract — without touching any internal TigerCli type. Timeout and
/// cancellation behaviour is asserted to match the built-in prompts.
/// </summary>
public sealed class CustomInlineControlPublicApiTests
{
    private static TestShell NewShell() => new(culture: CultureInfo.GetCultureInfo("en-US"));

    /// <summary>
    /// A minimal third-party control: renders one line and completes on Enter (Ok, carrying a payload)
    /// or Escape (Cancel). Built only from public types.
    /// </summary>
    private sealed class EchoControl : InlineControlBase
    {
        private readonly string _text;

        public EchoControl(ICliAppShell shell, string text) : base(shell) => _text = text;

        public override object? Payload => _text;

        public override CliGrid ToGrid()
        {
            var grid = new CliGrid(1, 1);
            grid.Set(0, 0, _text);
            return grid;
        }

        public override InlineKeyResult HandleKey(KeyEvent key)
        {
            if (key.Mods == ConsoleModifiers.None && key.Key == ConsoleKey.Enter)
                return InlineKeyResult.WithResult(DialogResultKind.Ok);
            if (key.Mods == ConsoleModifiers.None && key.Key == ConsoleKey.Escape)
                return InlineKeyResult.WithResult(DialogResultKind.Cancel);
            return InlineKeyResult.NotHandled;
        }
    }

    [Fact]
    public void DefaultShell_IsAPublicCliAppShell()
    {
        // The real console shell is reachable as ICliAppShell without any internal type.
        ICliAppShell shell = TigerTui.DefaultShell;
        Assert.NotNull(shell);
    }

    [Fact]
    public async Task RunControlAsync_HostsCustomControl_ReturnsResultAndPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var control = new EchoControl(shell, "hello");

        var run = TigerTui.RunControlAsync(shell, control, "Custom", ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);

        Assert.Equal(DialogResultKind.Ok, result.Kind);
        Assert.Equal("hello", result.Payload);
    }

    [Fact]
    public async Task RunDialogAsync_RunsCustomInlineDialog_ReturnsResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var control = new EchoControl(shell, "world");

        // A caller-constructed InlineDialog run through the public facade.
        var dialog = new InlineDialog(shell, "Custom", control);
        var run = TigerTui.RunDialogAsync(shell, dialog, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);

        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }

    [Fact]
    public async Task RunControlAsync_Timeout_CompletesWithTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var control = new EchoControl(shell, "waiting"); // never completes without a key

        var run = TigerTui.RunControlAsync(
            shell, control, "Custom", timeout: TimeSpan.FromMilliseconds(20), ct: ct);

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);

        Assert.Equal(DialogResultKind.Timeout, result.Kind);
    }

    [Fact]
    public async Task RunControlAsync_CallerTokenCancelled_CompletesWithTokenCancel()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.Cancel();
        var shell = NewShell();
        var control = new EchoControl(shell, "waiting");

        var run = TigerTui.RunControlAsync(shell, control, "Custom",
            timeout: TimeSpan.FromSeconds(10), ct: cts.Token);

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(DialogResultKind.TokenCancel, result.Kind);
    }

    [Fact]
    public async Task RunControlAsync_ConfirmationPolicy_GatesCancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = NewShell();
        var control = new EchoControl(shell, "guarded");

        // Pass a public confirmation policy through the facade; Escape should raise a Yes/No confirmation
        // (the control's Cancel is gated) rather than completing immediately.
        var run = TigerTui.RunControlAsync(shell, control, "Custom",
            confirmation: InlineDialogConfirmationPolicy.ConfirmCancel, ct: ct);

        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), ct);
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        // The confirmation prompt is now shown; the run has not completed.
        await shell.Terminal.WaitForReadCountAsync(1, TimeSpan.FromSeconds(1), ct);
        Assert.False(run.IsCompleted);

        // Confirm (No is default-focused -> move to Yes -> Enter) to finish the run cleanly.
        shell.Terminal.EnqueueKey(ConsoleKey.LeftArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(DialogResultKind.Cancel, result.Kind);
    }
}
