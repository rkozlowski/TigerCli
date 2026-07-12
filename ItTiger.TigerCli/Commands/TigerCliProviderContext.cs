using System.Globalization;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Run-time context passed to dynamic value-provider callbacks. Exposes the effective cancellation
/// token, the active run culture, and read access to option values that were already bound or
/// prompted earlier in the run (declare ordering with the registration's <c>dependsOn</c> hint).
/// </summary>
public sealed class TigerCliProviderContext
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    internal TigerCliProviderContext(
        CancellationToken cancellationToken,
        CultureInfo culture,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        CancellationToken = cancellationToken;
        Culture = culture;
        _values = values ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// The effective run/prompt cancellation token threaded from the pipeline. Observing it is
    /// optional and cooperative: a provider that throws <see cref="OperationCanceledException"/>
    /// while this token is cancelled is treated as a cancellation — prompt-backed providers map to
    /// the standard prompt-cancellation outcome, validation-time providers propagate the
    /// cancellation — never as a provider failure.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// The active run culture as resolved by the framework. Provider callbacks can
    /// use this to return localized labels without mutating
    /// <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Reads an already-bound or already-prompted option value. <paramref name="name"/> may be an
    /// alias token (<c>--env</c>), a normalized alias (<c>env</c>), or the settings property name.
    /// Returns <c>false</c> when the value is not available yet or is not assignable to
    /// <typeparamref name="TValue"/>.
    /// </summary>
    /// <param name="name">Alias token, normalized alias, or property name; must not be null/whitespace.</param>
    /// <param name="value">The typed value when found; otherwise <c>default</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    public bool TryGetValue<TValue>(string name, out TValue? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_values.TryGetValue(name, out var rawValue)
            && rawValue is TValue typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Returns an already-bound or already-prompted option value when available and assignable to
    /// <typeparamref name="TValue"/>; otherwise <c>default</c>. Convenience form of
    /// <see cref="TryGetValue{TValue}(string, out TValue)"/>.
    /// </summary>
    /// <param name="name">Alias token, normalized alias, or property name; must not be null/whitespace.</param>
    public TValue? GetOptionValue<TValue>(string name)
    {
        return TryGetValue<TValue>(name, out var value)
            ? value
            : default;
    }
}
