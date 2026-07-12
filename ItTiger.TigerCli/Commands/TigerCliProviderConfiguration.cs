using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Registers app-level named dynamic value providers, configured through
/// <c>TigerCliAppBuilder.ConfigureProviders(...)</c>. A provider is referenced by key from
/// <see cref="TigerCliOptionAttribute.Provider"/> / <see cref="TigerCliArgumentAttribute.Provider"/>
/// (or matched implicitly by property name for options) and supplies the choices used by
/// provider-backed prompting and by validation of supplied values. String providers use each
/// string as both value and label; named providers return key/label pairs. On every overload the
/// optional <c>dependsOn</c> names values that should be resolved first, and the trailing
/// <c>configure</c> callback receives a <see cref="TigerCliProviderOptions"/> for provider-specific
/// framework text (loading message, empty message) — omit it for generic localized defaults.
/// </summary>
public sealed class TigerCliProviderConfiguration
{
    private readonly List<ITigerCliValueProvider> _providers;

    internal TigerCliProviderConfiguration(List<ITigerCliValueProvider> providers)
    {
        _providers = providers;
    }

    /// <summary>
    /// Registers an async app-level string provider; each returned string is used as both value
    /// and label. Equivalent to <see cref="AddAsync(string, Func{TigerCliProviderContext,
    /// Task{IReadOnlyList{string}}}, IEnumerable{string}, Action{TigerCliProviderOptions})"/>,
    /// which is the preferred spelling for new code.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public void Add(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<string>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        _providers.Add(new TigerCliStringValueProvider(
            key,
            _providers.Count,
            NormalizeDependsOn(dependsOn),
            provider,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
    }

    /// <summary>
    /// Registers a sync app-level string provider; each returned string is used as both value and
    /// label. Prefer the async overloads for slow or I/O-backed providers.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public void Add(
        string key,
        Func<TigerCliProviderContext, IReadOnlyList<string>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        _providers.Add(new TigerCliStringValueProvider(
            key,
            _providers.Count,
            NormalizeDependsOn(dependsOn),
            context => Task.FromResult(provider(context)),
            isAsync: false,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
    }

    /// <summary>
    /// Registers an async app-level string provider. Prefer this over the sync <see cref="Add(string,
    /// Func{TigerCliProviderContext, IReadOnlyList{string}}, IEnumerable{string}, Action{TigerCliProviderOptions})"/>
    /// for slow or I/O-backed providers; observe <see cref="TigerCliProviderContext.CancellationToken"/>
    /// to make the work cooperatively cancellable, and use <paramref name="configure"/> to customize the
    /// loading message.
    /// </summary>
    public void AddAsync(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<string>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        => Add(key, provider, dependsOn, configure);

    /// <summary>
    /// Registers an async app-level named provider returning key/label pairs
    /// (<see cref="OptionItem{TValue}"/>). The key is the bound value; the label is display text.
    /// Equivalent to the <c>AddAsync</c> spelling, which is preferred for new code.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public void Add<TValue>(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        _providers.Add(new TigerCliValueProvider<TValue>(
            key,
            _providers.Count,
            NormalizeDependsOn(dependsOn),
            provider,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
    }

    /// <summary>
    /// Registers a sync app-level named provider returning key/label pairs
    /// (<see cref="OptionItem{TValue}"/>). Prefer the async overloads for slow or I/O-backed
    /// providers.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public void Add<TValue>(
        string key,
        Func<TigerCliProviderContext, IReadOnlyList<OptionItem<TValue>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        _providers.Add(new TigerCliValueProvider<TValue>(
            key,
            _providers.Count,
            NormalizeDependsOn(dependsOn),
            context => Task.FromResult(provider(context)),
            isAsync: false,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
    }

    /// <summary>
    /// Registers an async app-level named provider. Prefer this over the sync overload for slow or
    /// I/O-backed providers; observe <see cref="TigerCliProviderContext.CancellationToken"/> to make the
    /// work cooperatively cancellable, and use <paramref name="configure"/> to customize the loading
    /// message.
    /// </summary>
    public void AddAsync<TValue>(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        => Add<TValue>(key, provider, dependsOn, configure);

    /// <summary>
    /// Registers an async app-level named provider that additionally receives the partially bound
    /// settings instance, letting the choices depend on values supplied earlier in the run.
    /// Equivalent to the <c>AddAsync</c> spelling, which is preferred for new code.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public void Add<TSettings, TValue>(
        string key,
        Func<TSettings, TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        where TSettings : TigerCliSettings
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        _providers.Add(new TigerCliValueProvider<TSettings, TValue>(
            key,
            _providers.Count,
            NormalizeDependsOn(dependsOn),
            provider,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
    }

    /// <summary>
    /// Registers a sync app-level named provider that additionally receives the partially bound
    /// settings instance. Prefer the async overloads for slow or I/O-backed providers.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public void Add<TSettings, TValue>(
        string key,
        Func<TSettings, TigerCliProviderContext, IReadOnlyList<OptionItem<TValue>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        where TSettings : TigerCliSettings
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        _providers.Add(new TigerCliValueProvider<TSettings, TValue>(
            key,
            _providers.Count,
            NormalizeDependsOn(dependsOn),
            (settings, context) => Task.FromResult(provider(settings, context)),
            isAsync: false,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
    }

    /// <summary>
    /// Registers an async app-level named provider that can read partially bound settings. Prefer this
    /// over the sync overload for slow or I/O-backed providers; observe
    /// <see cref="TigerCliProviderContext.CancellationToken"/> to make the work cooperatively cancellable,
    /// and use <paramref name="configure"/> to customize the loading message.
    /// </summary>
    public void AddAsync<TSettings, TValue>(
        string key,
        Func<TSettings, TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        where TSettings : TigerCliSettings
        => Add<TSettings, TValue>(key, provider, dependsOn, configure);

    private static IReadOnlyList<string> NormalizeDependsOn(IEnumerable<string>? dependsOn) =>
        dependsOn?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray()
        ?? [];
}
