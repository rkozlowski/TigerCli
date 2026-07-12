using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Windowing;

namespace ItTiger.TigerCli.Tui;

public static partial class TigerTui
{
    /// <summary>
    /// The default semi-interactive shell — the real console-backed <see cref="ICliAppShell"/> that the
    /// built-in <c>TigerTui.*</c> prompts run on. Advanced callers can use it to host their own custom
    /// controls/dialogs through the same modal loop the framework uses. <see cref="ICliAppShell"/> is the
    /// public shell contract; the concrete implementation behind this property is an internal detail.
    /// </summary>
    public static ICliAppShell DefaultShell => InlineShell.Instance;

    /// <summary>
    /// Runs a fully-constructed dialog on the <see cref="DefaultShell"/> and returns its
    /// <see cref="DialogResult"/>. Use this to run a custom <see cref="ICliDialog"/> (e.g. an
    /// <see cref="InlineDialog"/>) without depending on internal shell types. Timeout and cancellation
    /// behave exactly as for the built-in prompts.
    /// </summary>
    public static Task<DialogResult> RunDialogAsync(
        ICliDialog dialog,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunDialogAsync(InlineShell.Instance, dialog, timeout, ct);

    /// <summary>Runs a fully-constructed dialog against an explicit <paramref name="shell"/>.</summary>
    public static Task<DialogResult> RunDialogAsync(
        ICliAppShell shell,
        ICliDialog dialog,
        TimeSpan? timeout = default,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(dialog);
        return shell.RunModalAsync(dialog, timeout, ct);
    }

    /// <summary>
    /// Hosts a custom <see cref="InlineControlBase"/> in an <see cref="InlineDialog"/> and runs it on the
    /// <see cref="DefaultShell"/>, returning its <see cref="DialogResult"/> (the control's
    /// <see cref="InlineControlBase.Payload"/> is carried on <see cref="DialogResult.Payload"/>). This is
    /// the public path for running a custom control without touching internal shell types; the built-in
    /// prompts are themselves thin adapters over the same composition.
    /// </summary>
    public static Task<DialogResult> RunControlAsync(
        InlineControlBase control,
        string? title = null,
        InlineDialogConfirmationPolicy? confirmation = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default) =>
        RunControlAsync(InlineShell.Instance, control, title, confirmation, timeout, ct);

    /// <summary>Hosts and runs a custom control against an explicit <paramref name="shell"/>.</summary>
    public static Task<DialogResult> RunControlAsync(
        ICliAppShell shell,
        InlineControlBase control,
        string? title = null,
        InlineDialogConfirmationPolicy? confirmation = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(control);

        var dialog = new InlineDialog(shell, title, control, confirmation: confirmation);
        return shell.RunModalAsync(dialog, timeout, ct);
    }
}
