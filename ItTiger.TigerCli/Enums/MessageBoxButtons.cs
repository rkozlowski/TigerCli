namespace ItTiger.TigerCli.Enums;

/// <summary>
/// The button set shown by a message-box style inline dialog. It describes the buttons only — not the
/// severity/icon of the message. Each value maps to a fixed set of buttons and <c>DialogResultKind</c>
/// results (see <c>InlineMessageBoxControl</c> / <c>TigerTui.MessageBoxAsync</c>).
/// </summary>
public enum MessageBoxButtons
{
    /// <summary>A single <c>OK</c> button → <c>DialogResultKind.Ok</c>.</summary>
    Ok,

    /// <summary><c>OK</c> → <c>Ok</c>, <c>Cancel</c> → <c>Cancel</c>.</summary>
    OkCancel,

    /// <summary><c>Yes</c> → <c>Yes</c>, <c>No</c> → <c>No</c>.</summary>
    YesNo,

    /// <summary><c>Yes</c> → <c>Yes</c>, <c>No</c> → <c>No</c>, <c>Cancel</c> → <c>Cancel</c>.</summary>
    YesNoCancel,

    /// <summary><c>Abort</c> → <c>Abort</c>, <c>Retry</c> → <c>Retry</c>, <c>Ignore</c> → <c>Ignore</c>.</summary>
    AbortRetryIgnore
}
