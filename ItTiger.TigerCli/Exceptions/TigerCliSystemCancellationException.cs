namespace ItTiger.TigerCli.Exceptions;

/// <summary>
/// Thrown by the simple (collapsing) <c>TigerTui</c> prompt helpers when the underlying modal completed
/// with <see cref="Enums.DialogResultKind.SystemCancel"/> — a process/system cancellation request such
/// as Ctrl-C / SIGINT / SIGTERM / SIGQUIT.
/// </summary>
/// <remarks>
/// Simple APIs collapse a modal outcome to value-or-<c>null</c> (or <c>bool?</c>) and cannot represent
/// process/system cancellation safely: returning <c>null</c> would masquerade as an ordinary user
/// cancel. They therefore surface it as this exception. The rich <c>*ResultAsync</c> APIs return
/// <see cref="Enums.DialogResultKind.SystemCancel"/> as an ordinary result kind instead and never throw.
/// Derives from <see cref="OperationCanceledException"/> so idiomatic cancellation handling still
/// applies, while the concrete type keeps system cancellation distinguishable from token cancellation.
/// </remarks>
public sealed class TigerCliSystemCancellationException : OperationCanceledException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TigerCliSystemCancellationException"/> class
    /// with the default system-cancellation message.
    /// </summary>
    public TigerCliSystemCancellationException()
        : base("The operation was cancelled by a process/system cancellation request (e.g. Ctrl-C / SIGINT / SIGTERM).")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TigerCliSystemCancellationException"/> class
    /// with a specified message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TigerCliSystemCancellationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TigerCliSystemCancellationException"/> class
    /// with a specified message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public TigerCliSystemCancellationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
