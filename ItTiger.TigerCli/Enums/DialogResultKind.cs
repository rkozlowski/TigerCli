namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Result kind returned by modal and inline dialog interactions.
/// </summary>
public enum DialogResultKind
{
    /// <summary>No result has been produced.</summary>
    NoResult = 0,
    /// <summary>The caller's cancellation token cancelled the interaction.</summary>
    TokenCancel = 1,
    /// <summary>User accepted with OK.</summary>
    Ok,
    /// <summary>User cancelled or dismissed the interaction.</summary>
    Cancel,
    /// <summary>The interaction timed out.</summary>
    Timeout,
    /// <summary>Interaction was requested while interaction was not allowed.</summary>
    InteractionNotAllowed,

    // Message-box style results. Appended after the existing members so the established
    // values (Ok = 2, Cancel = 3, etc.) are preserved.
    /// <summary>User chose Yes.</summary>
    Yes,
    /// <summary>User chose No.</summary>
    No,
    /// <summary>User chose Abort.</summary>
    Abort,
    /// <summary>User chose Retry.</summary>
    Retry,
    /// <summary>User chose Ignore.</summary>
    Ignore,

    // Process/system cancellation request (Ctrl-C / SIGINT / SIGTERM / SIGQUIT). Appended last so the
    // established values are preserved. Kept distinct from Cancel (user dismissal), TokenCancel
    // (caller token), and Timeout (inactivity); it must never be collapsed into any of those.
    /// <summary>
    /// Process/system cancellation request, distinct from user cancel, token cancellation, and timeout.
    /// </summary>
    SystemCancel
}
