namespace ItTiger.TigerCli.Enums;

/// <summary>
/// The terminal outcome of a rich activity dialog run. Mapped 1:1 from the modal
/// <see cref="DialogResultKind"/> plus the background operation's own result/exception. Distinguishes
/// successful completion from the cancellation flavours and operation failure so callers can branch
/// without re-deriving meaning from a collapsed value.
/// </summary>
public enum ActivityOutcome
{
    /// <summary>The operation ran to completion and produced a value.</summary>
    Completed,

    /// <summary>The user cancelled (Esc / Cancel), or the caller token cancelled the run.</summary>
    Cancelled,

    /// <summary>The user aborted via an explicit Abort action.</summary>
    Aborted,

    /// <summary>The inactivity timeout expired.</summary>
    TimedOut,

    /// <summary>Process/system cancellation (Ctrl-C / SIGTERM / shutdown).</summary>
    SystemCancelled,

    /// <summary>The operation threw; the exception is carried on the result.</summary>
    Failed,
}
