using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;

namespace ItTiger.TigerCli.Tui;

public static partial class TigerTui
{
    /// <summary>
    /// Shows a generic, framework-level loading dialog (spinner + a localized <paramref name="message"/>)
    /// that stays open until <paramref name="watched"/> completes or the user/caller/system cancels.
    /// It does <em>not</em> run any work: the caller owns <paramref name="watched"/> and awaits it for the
    /// real result/exception once this returns. Used by the prompt pipeline to cover a slow provider's
    /// choice resolution; intentionally internal so providers never see or render UI.
    /// </summary>
    internal static async Task<DialogResult> RunProviderLoadingAsync(
        ICliAppShell shell,
        Task watched,
        string message,
        TimeSpan? timeout = default,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(watched);
        ArgumentNullException.ThrowIfNull(message);

        var control = new InlineLoadingControl(shell, watched, message);
        var dialog = new InlineDialog(shell, title: null, control);
        return await shell.RunModalAsync(dialog, timeout, ct).ConfigureAwait(false);
    }
}
