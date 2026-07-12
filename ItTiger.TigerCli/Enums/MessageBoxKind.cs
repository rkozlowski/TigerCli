namespace ItTiger.TigerCli.Enums;

/// <summary>
/// The semantic severity of a message-box style inline dialog. It describes the message's meaning —
/// not its buttons (see <see cref="MessageBoxButtons"/>) — and selects the dialog surface/style the
/// hosting <c>InlineDialog</c> renders (see <c>InlineMessageBoxControl</c> / <c>TigerTui.MessageBoxAsync</c>).
/// The surface is resolved through the active theme (<c>ThemeStyle.DialogSurface</c> /
/// <c>WarningSurface</c> / <c>ErrorSurface</c>), so kinds stay theme-driven rather than hard-coded.
/// </summary>
public enum MessageBoxKind
{
    /// <summary>An informational message on the normal dialog surface (<c>ThemeStyle.DialogSurface</c>).</summary>
    Normal,

    /// <summary>A warning on the yellow/orange warning surface (<c>ThemeStyle.WarningSurface</c>).</summary>
    Warning,

    /// <summary>An error on the red error surface (<c>ThemeStyle.ErrorSurface</c>).</summary>
    Error
}
