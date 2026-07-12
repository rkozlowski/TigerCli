namespace ItTiger.TigerCli.Enums;

/// <summary>
/// The confirmable dialog result kinds an <c>InlineDialog</c> can gate behind an "are you sure?"
/// confirmation. Only the user-/key-produced cancellation kinds are confirmable; the loop-produced
/// lifecycle kinds (<see cref="DialogResultKind.TokenCancel"/>, <see cref="DialogResultKind.Timeout"/>,
/// <see cref="DialogResultKind.SystemCancel"/>) are never confirmed.
/// </summary>
[Flags]
public enum DialogConfirmationKinds
{
    /// <summary>No dialog results require confirmation.</summary>
    None = 0,
    /// <summary>Confirm a user-requested cancellation.</summary>
    Cancel = 1,
    /// <summary>Confirm a user-requested abort.</summary>
    Abort = 2,
}
