using System.Globalization;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Context passed to prompt and provider callbacks while TigerCli is resolving provider-backed values.
/// </summary>
/// <remarks>
/// The context exposes the effective shell, interaction mode, timeout, cancellation token, culture,
/// and values already available from earlier binding or prompting. Value lookup is intended for
/// dependent providers that need to read selector values or earlier option answers.
/// </remarks>
public sealed class TigerCliPromptContext
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    internal TigerCliPromptContext(
        ICliAppShell? shell,
        TigerCliInteractionMode interactionMode,
        TimeSpan? timeout,
        CancellationToken cancellationToken,
        CultureInfo culture,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        Shell = shell;
        InteractionMode = interactionMode;
        Timeout = timeout;
        CancellationToken = cancellationToken;
        Culture = culture;
        _values = values ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// The injected prompt shell for the current run, or <c>null</c> when TigerCli is using its default
    /// console-backed shell.
    /// </summary>
    public ICliAppShell? Shell { get; }

    /// <summary>
    /// The effective interaction mode for the command being resolved.
    /// </summary>
    public TigerCliInteractionMode InteractionMode { get; }

    /// <summary>
    /// The optional prompt inactivity timeout passed to the run pipeline.
    /// </summary>
    public TimeSpan? Timeout { get; }

    /// <summary>
    /// The effective cancellation token for prompt/provider work. Provider callbacks should observe it
    /// cooperatively; an <see cref="OperationCanceledException"/> thrown while this token is cancelled
    /// is treated as cancellation rather than provider failure.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Returns a copy of this context with <see cref="CancellationToken"/> replaced. Used by the prompt
    /// pipeline to hand a slow provider a token it can cancel independently (e.g. when the interactive
    /// loading UI is dismissed) without disturbing the caller's original token.
    /// </summary>
    internal TigerCliPromptContext WithCancellationToken(CancellationToken cancellationToken) =>
        new(Shell, InteractionMode, Timeout, cancellationToken, Culture, _values);

    /// <summary>
    /// The active run culture as resolved by the framework. Provider callbacks can
    /// use this to return localized labels without mutating
    /// <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    public CultureInfo Culture { get; }

    internal IReadOnlyDictionary<string, object?> Values => _values;

    /// <summary>
    /// Attempts to read an already-bound or already-prompted value by option alias, normalized alias,
    /// argument/display name, normalized display name, or settings property name.
    /// </summary>
    /// <param name="name">The lookup name. For options this may be an alias such as <c>--file</c> or the property name.</param>
    /// <param name="value">The typed value when present and assignable to <typeparamref name="TValue"/>.</param>
    /// <returns><c>true</c> when a matching value exists and is assignable to <typeparamref name="TValue"/>.</returns>
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
    /// Reads an already-bound or already-prompted value by name, returning <c>default</c> when no
    /// matching value exists or the value is not assignable to <typeparamref name="TValue"/>.
    /// </summary>
    /// <param name="name">The lookup name. For options this may be an alias such as <c>--file</c> or the property name.</param>
    public TValue? GetOptionValue<TValue>(string name)
    {
        return TryGetValue<TValue>(name, out var value)
            ? value
            : default;
    }
}
