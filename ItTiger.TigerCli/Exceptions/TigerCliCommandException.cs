using ItTiger.TigerCli.Commands;

namespace ItTiger.TigerCli.Exceptions;

/// <summary>
/// Classified command failure for handlers — especially reusable command libraries — that want to
/// express what kind of failure occurred using TigerCli concepts without owning the application's
/// numeric exit-code scheme. Throwing this from a command handler reports
/// <see cref="Exception.Message"/> as a framework error and resolves the process exit code through
/// the app's exit-code policy for <see cref="ExitKind"/>, instead of the
/// <see cref="TigerCliExitKind.UnhandledException"/> mapping every other escaping exception gets.
/// </summary>
/// <remarks>
/// The application keeps full ownership of numeric codes: it maps the thrown kind (or its category)
/// with the existing <c>UseExitCodes</c> / <c>ExitKind</c> / <c>ExitCategory</c> / <c>ExitRange</c>
/// builder configuration. An optional <see cref="ErrorId"/> carries a stable, library-defined
/// identifier (for example <c>"TQ0007"</c>) that is appended to the reported message so scripts,
/// logs, and tests can match the failure without parsing prose.
/// </remarks>
public class TigerCliCommandException : Exception
{
    /// <summary>
    /// The framework classification for this failure. Resolved to the process exit code through the
    /// app's exit-code policy (kind → range → category → outcome baseline).
    /// </summary>
    public TigerCliExitKind ExitKind { get; }

    /// <summary>
    /// Optional stable, library-defined failure identifier (for example <c>"TQ0007"</c>).
    /// When present it is appended to the reported error message.
    /// </summary>
    public string? ErrorId { get; }

    /// <summary>
    /// Creates a classified command failure. <paramref name="exitKind"/> defaults to
    /// <see cref="TigerCliExitKind.GenericFail"/> and must be an error kind: success kinds
    /// (<see cref="TigerCliExitKind.Success"/>, <see cref="TigerCliExitKind.HelpShown"/>) and
    /// <see cref="TigerCliExitKind.Cancelled"/> are rejected — express cancellation with
    /// <see cref="OperationCanceledException"/> instead.
    /// </summary>
    /// <param name="message">The user-facing failure reason; must not be null, empty, or whitespace.</param>
    /// <param name="exitKind">The framework classification; must be a defined error kind.</param>
    /// <param name="errorId">Optional stable, library-defined failure identifier.</param>
    /// <param name="innerException">Optional underlying exception that caused the failure.</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="exitKind"/> is not a defined <see cref="TigerCliExitKind"/>, is a success
    /// kind, or is <see cref="TigerCliExitKind.Cancelled"/>.
    /// </exception>
    public TigerCliCommandException(
        string message,
        TigerCliExitKind exitKind = TigerCliExitKind.GenericFail,
        string? errorId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!Enum.IsDefined(exitKind))
            throw new ArgumentOutOfRangeException(nameof(exitKind), exitKind, "Exit kind is not a defined TigerCliExitKind.");
        if (exitKind is TigerCliExitKind.Success or TigerCliExitKind.HelpShown or TigerCliExitKind.Cancelled)
            throw new ArgumentOutOfRangeException(nameof(exitKind), exitKind, "Exit kind must be an error kind; success and cancellation kinds cannot be thrown as command failures.");

        ExitKind = exitKind;
        ErrorId = errorId;
    }
}
