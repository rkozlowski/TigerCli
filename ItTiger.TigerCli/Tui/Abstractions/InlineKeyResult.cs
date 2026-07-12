using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Tui.Abstractions;

/// <summary>
/// The outcome of an inline control/widget handling a key. Beyond "handled / not handled" it can
/// request a dialog result, so a focused widget (e.g. a button) can complete the hosting dialog
/// directly instead of relying on the dialog's Enter/Escape fallback.
/// </summary>
public readonly record struct InlineKeyResult
{
    /// <summary>True when the control/widget consumed the key.</summary>
    public bool IsHandled { get; init; }

    /// <summary>The dialog result requested by the handler, or <see cref="DialogResultKind.NoResult"/>.</summary>
    public DialogResultKind Result { get; init; }

    /// <summary>The key was not consumed; the caller may apply its own fallback.</summary>
    public static InlineKeyResult NotHandled { get; } = new() { IsHandled = false, Result = DialogResultKind.NoResult };

    /// <summary>The key was consumed with no dialog result requested.</summary>
    public static InlineKeyResult Handled { get; } = new() { IsHandled = true, Result = DialogResultKind.NoResult };

    /// <summary>The key was consumed and requests the given dialog result.</summary>
    public static InlineKeyResult WithResult(DialogResultKind result) => new() { IsHandled = true, Result = result };
}
