namespace ItTiger.TigerCli.Commands;

/// <summary>
/// The middle layer of the layered exit model: a small, stable grouping of
/// <see cref="TigerCliExitKind"/> values. Categories let an app map a whole family of framework
/// failures to one exit code without enumerating every kind.
/// </summary>
public enum TigerCliExitCategory
{
    /// <summary>Successful outcomes (<see cref="TigerCliExitKind.Success"/>, <see cref="TigerCliExitKind.HelpShown"/>).</summary>
    Success,

    /// <summary>The caller invoked the tool incorrectly (bad/missing arguments, no command, interactive not allowed).</summary>
    Usage,

    /// <summary>Input was well-formed but failed validation (<see cref="TigerCliExitKind.ValidationError"/>).</summary>
    Validation,

    /// <summary>
    /// The command could not complete its work, including generic and provider failures and
    /// portable application outcomes such as <see cref="TigerCliExitKind.NotFound"/>,
    /// <see cref="TigerCliExitKind.AlreadyExists"/>, <see cref="TigerCliExitKind.Conflict"/>, and
    /// <see cref="TigerCliExitKind.NotSupported"/>.
    /// </summary>
    Execution,

    /// <summary>An unexpected, unhandled error escaped (<see cref="TigerCliExitKind.UnhandledException"/>).</summary>
    Unexpected,

    /// <summary>
    /// The user cancelled an interactive prompt (<see cref="TigerCliExitKind.Cancelled"/>). This is a
    /// deliberate, distinct category — cancellation is never classified as <see cref="Usage"/> or
    /// <see cref="Validation"/>. It still rolls up to <see cref="TigerCliExitOutcome.Error"/> by
    /// default, but apps may map it to success if they want Escape to be neutral.
    /// </summary>
    Cancelled
}
