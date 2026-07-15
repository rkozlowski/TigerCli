namespace ItTiger.TigerCli.Exceptions;

/// <summary>
/// Deliberate failure signal for dynamic value providers. A provider throws this when it cannot
/// produce its choices for a meaningful, user-facing reason — for example a reachable database with
/// an incompatible schema — so the run fails with the provider's message instead of behaving as if
/// no choices were available.
/// </summary>
/// <remarks>
/// The framework reports <see cref="Exception.Message"/> to the user (interactive and
/// non-interactive alike) and maps the run to
/// <see cref="ItTiger.TigerCli.Commands.TigerCliExitKind.ProviderError"/>, which rolls up to
/// <see cref="ItTiger.TigerCli.Commands.TigerCliExitCategory.Execution"/>. This differs from any
/// other exception escaping a provider, which is treated as
/// <see cref="ItTiger.TigerCli.Commands.TigerCliExitKind.UnhandledException"/>. Cancellation is
/// never expressed with this type — a provider observing a cancelled
/// <see cref="ItTiger.TigerCli.Commands.TigerCliProviderContext.CancellationToken"/> should throw
/// <see cref="OperationCanceledException"/> as usual.
/// </remarks>
public sealed class TigerCliProviderException : Exception
{
    /// <summary>Creates a provider failure with a user-facing message.</summary>
    /// <param name="message">The user-facing failure reason; must not be null, empty, or whitespace.</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is null, empty, or whitespace.</exception>
    public TigerCliProviderException(string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
    }

    /// <summary>Creates a provider failure with a user-facing message and the underlying cause.</summary>
    /// <param name="message">The user-facing failure reason; must not be null, empty, or whitespace.</param>
    /// <param name="innerException">The underlying exception that caused the failure.</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is null, empty, or whitespace.</exception>
    public TigerCliProviderException(string message, Exception? innerException)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
    }
}
