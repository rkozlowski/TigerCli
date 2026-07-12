using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Optional, reusable confirmation policy for an <see cref="InlineDialog"/>. When a confirmable
/// key-produced result (<see cref="DialogResultKind.Cancel"/> or <see cref="DialogResultKind.Abort"/>)
/// is requested and this policy includes that kind, the dialog enters an internal confirmation mode
/// (a Yes/No message box) before completing. Choosing <c>Yes</c> completes with the originally
/// requested kind; <c>No</c> (or Escape) dismisses the confirmation and resumes the original dialog.
/// Loop-/externally-produced kinds (TokenCancel, Timeout, SystemCancel) are never confirmed.
/// </summary>
public sealed record InlineDialogConfirmationPolicy
{
    /// <summary>The result kinds to confirm before completing. Defaults to none.</summary>
    public DialogConfirmationKinds Confirm { get; init; } = DialogConfirmationKinds.None;

    /// <summary>
    /// Optional per-kind confirmation message override. Receives the requested
    /// <see cref="DialogResultKind"/>; returning <c>null</c> (or leaving this unset) falls back to the
    /// localized default (<c>Tui_Confirm_Cancel_Message</c> / <c>Tui_Confirm_Abort_Message</c>).
    /// </summary>
    public Func<DialogResultKind, string?>? MessageProvider { get; init; }

    /// <summary>No confirmation — completes immediately for every result kind (current default behavior).</summary>
    public static InlineDialogConfirmationPolicy None { get; } = new();

    /// <summary>Confirm <see cref="DialogResultKind.Cancel"/> only.</summary>
    public static InlineDialogConfirmationPolicy ConfirmCancel { get; } =
        new() { Confirm = DialogConfirmationKinds.Cancel };

    /// <summary>Confirm <see cref="DialogResultKind.Abort"/> only.</summary>
    public static InlineDialogConfirmationPolicy ConfirmAbort { get; } =
        new() { Confirm = DialogConfirmationKinds.Abort };

    /// <summary>Confirm both <see cref="DialogResultKind.Cancel"/> and <see cref="DialogResultKind.Abort"/>.</summary>
    public static InlineDialogConfirmationPolicy ConfirmCancelAndAbort { get; } =
        new() { Confirm = DialogConfirmationKinds.Cancel | DialogConfirmationKinds.Abort };

    /// <summary>Whether <paramref name="kind"/> should be confirmed under this policy.</summary>
    internal bool ShouldConfirm(DialogResultKind kind) => kind switch
    {
        DialogResultKind.Cancel => (Confirm & DialogConfirmationKinds.Cancel) != 0,
        DialogResultKind.Abort => (Confirm & DialogConfirmationKinds.Abort) != 0,
        _ => false,
    };
}
