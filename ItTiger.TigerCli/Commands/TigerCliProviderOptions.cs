namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Optional per-registration provider options, supplied through the trailing <c>configure</c> callback on
/// the provider registration APIs (<c>AddProvider</c>, <c>AddAsyncProvider</c>, <c>Add</c>, <c>AddAsync</c>).
/// Carries provider-specific framework text such as the interactive slow-provider loading message and
/// the required-prompt empty message. Providers never render UI — this only describes text the
/// framework may show while handling provider choices.
/// </summary>
public sealed class TigerCliProviderOptions
{
    private string? _loadingMessageText;
    private string? _loadingMessageResourceKey;
    private string? _emptyMessageText;
    private string? _emptyMessageResourceKey;

    /// <summary>
    /// Sets a literal loading message shown (interactive only) while a slow provider resolves its choices,
    /// replacing the generic default. Has no effect in non-interactive mode.
    /// </summary>
    public TigerCliProviderOptions LoadingMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _loadingMessageText = message;
        return this;
    }

    /// <summary>
    /// Sets a localized loading message resolved from the app
    /// <see cref="System.Resources.ResourceManager"/> (registered via <c>UseAppResources</c>) for the
    /// active run culture, with an optional literal <paramref name="fallback"/> used when the key does not
    /// resolve. Has no effect in non-interactive mode.
    /// </summary>
    public TigerCliProviderOptions LoadingMessageResource(string resourceKey, string? fallback = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        _loadingMessageResourceKey = resourceKey;
        if (fallback is not null)
            _loadingMessageText = fallback;
        return this;
    }

    /// <summary>
    /// Sets a literal message used when a required provider-backed prompt has no selectable outcome.
    /// Optional nullable prompts that can offer <c>(None)</c> do not use this message.
    /// </summary>
    public TigerCliProviderOptions EmptyMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _emptyMessageText = message;
        return this;
    }

    /// <summary>
    /// Sets a localized empty message resolved from the app
    /// <see cref="System.Resources.ResourceManager"/> (registered via <c>UseAppResources</c>) for the
    /// active run culture, with an optional literal <paramref name="fallback"/> used when the key does
    /// not resolve.
    /// </summary>
    public TigerCliProviderOptions EmptyMessageResource(string resourceKey, string? fallback = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        _emptyMessageResourceKey = resourceKey;
        if (fallback is not null)
            _emptyMessageText = fallback;
        return this;
    }

    // Runs the caller's configuration (if any) once and captures the resulting provider UX metadata.
    internal static TigerCliResolvedProviderOptions Resolve(Action<TigerCliProviderOptions>? configure)
    {
        if (configure is null)
            return TigerCliResolvedProviderOptions.Empty;

        var options = new TigerCliProviderOptions();
        configure(options);

        var loadingMessage = options._loadingMessageText is null && options._loadingMessageResourceKey is null
            ? (TigerCliProviderLoadingMessage?)null
            : new TigerCliProviderLoadingMessage(options._loadingMessageText, options._loadingMessageResourceKey);

        var emptyMessage = options._emptyMessageText is null && options._emptyMessageResourceKey is null
            ? (TigerCliProviderEmptyMessage?)null
            : new TigerCliProviderEmptyMessage(options._emptyMessageText, options._emptyMessageResourceKey);

        return new TigerCliResolvedProviderOptions(loadingMessage, emptyMessage);
    }
}

// Immutable captured loading-message metadata: a literal fallback and/or an app resource key, resolved at
// prompt time via TigerCliAppText.Resolve (the same literal-or-resource-key shape descriptions use).
internal readonly record struct TigerCliProviderLoadingMessage(string? Text, string? ResourceKey);

// Immutable captured empty-message metadata: a literal fallback and/or an app resource key, resolved at
// prompt time via TigerCliAppText.Resolve. Missing resource keys fall back to Text; if both are absent
// after resolution, the framework's generic no-choices message is used.
internal readonly record struct TigerCliProviderEmptyMessage(string? Text, string? ResourceKey);

internal readonly record struct TigerCliResolvedProviderOptions(
    TigerCliProviderLoadingMessage? LoadingMessage,
    TigerCliProviderEmptyMessage? EmptyMessage)
{
    public static TigerCliResolvedProviderOptions Empty { get; } = new(null, null);
}
