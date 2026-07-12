using System.Linq.Expressions;
using System.Reflection;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// App-level compatibility surface for configuring property-scoped prompt providers for a settings
/// type.
/// </summary>
/// <remarks>
/// Instances are created by <see cref="TigerCliAppBuilder.ConfigurePrompts{TSettings}"/>. New code
/// should usually prefer named providers registered through <see cref="TigerCliAppBuilder.ConfigureProviders"/>
/// or the group/command provider APIs, but this type remains supported for provider-backed prompts
/// that are naturally tied to a single settings property.
/// </remarks>
/// <typeparam name="TSettings">The settings type whose prompt providers are being configured.</typeparam>
public sealed class TigerCliPromptConfiguration<TSettings>
    where TSettings : TigerCliSettings
{
    private readonly List<ITigerCliValueProvider> _providers;

    internal TigerCliPromptConfiguration(List<ITigerCliValueProvider> providers)
    {
        _providers = providers;
    }

    /// <summary>
    /// Targets a readable and writable settings property for provider-backed prompt customization.
    /// </summary>
    /// <param name="property">An expression that directly selects a settings property.</param>
    /// <returns>A property builder used to register the property's choice provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="property"/> does not directly target a readable and writable settings property.
    /// </exception>
    public TigerCliPromptPropertyBuilder<TSettings, TValue> For<TValue>(
        Expression<Func<TSettings, TValue>> property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var propertyInfo = ResolveProperty(property);
        return new TigerCliPromptPropertyBuilder<TSettings, TValue>(_providers, propertyInfo);
    }

    private static PropertyInfo ResolveProperty<TValue>(Expression<Func<TSettings, TValue>> property)
    {
        if (property.Body is not MemberExpression memberExpression ||
            memberExpression.Member is not PropertyInfo propertyInfo)
        {
            throw new ArgumentException("Prompt configuration must target a settings property.", nameof(property));
        }

        if (propertyInfo.GetMethod == null)
            throw new ArgumentException("Prompt configuration must target a readable settings property.", nameof(property));

        if (propertyInfo.SetMethod == null)
            throw new ArgumentException("Prompt configuration must target a writable settings property.", nameof(property));

        return propertyInfo;
    }
}

/// <summary>
/// Registers a provider-backed prompt for one property selected from
/// <see cref="TigerCliPromptConfiguration{TSettings}"/>.
/// </summary>
/// <remarks>
/// The registered provider is keyed to the targeted property name. When the property is prompted,
/// TigerCli invokes the provider with the current settings instance and a
/// <see cref="TigerCliPromptContext"/> containing previously bound or prompted values. The returned
/// <see cref="OptionItem{TValue}"/> keys are the values bound back to the property; labels are display text.
/// Provider choices must not be <c>null</c>, contain <c>null</c> keys, or contain duplicate keys.
/// </remarks>
/// <typeparam name="TSettings">The command settings type.</typeparam>
/// <typeparam name="TValue">The provider key and target property value type.</typeparam>
public sealed class TigerCliPromptPropertyBuilder<TSettings, TValue>
    where TSettings : TigerCliSettings
{
    private readonly List<ITigerCliValueProvider> _providers;
    private readonly PropertyInfo _property;

    internal TigerCliPromptPropertyBuilder(
        List<ITigerCliValueProvider> providers,
        PropertyInfo property)
    {
        _providers = providers;
        _property = property;
    }

    /// <summary>
    /// Registers an asynchronous key/label choice provider for the targeted property.
    /// </summary>
    /// <param name="provider">
    /// Provider invoked during prompt/provider validation. It receives the current settings and prompt
    /// context, and returns selectable values for the property.
    /// </param>
    /// <param name="dependsOn">
    /// Optional prompt-order hints naming other options by alias or property name. These affect prompt
    /// ordering only; they do not make this property required and they do not perform validation.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public void SelectFrom(
        Func<TSettings, TigerCliPromptContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        IEnumerable<string>? dependsOn = null)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _providers.Add(new TigerCliPromptValueProvider<TSettings, TValue>(
            _property,
            _property.Name,
            _providers.Count,
            NormalizeDependsOn(dependsOn),
            provider));
    }

    /// <summary>
    /// Registers a synchronous key/label choice provider for the targeted property.
    /// </summary>
    /// <param name="provider">
    /// Provider invoked during prompt/provider validation. It receives the current settings and prompt
    /// context, and returns selectable values for the property.
    /// </param>
    /// <param name="dependsOn">
    /// Optional prompt-order hints naming other options by alias or property name. These affect prompt
    /// ordering only; they do not make this property required and they do not perform validation.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public void SelectFrom(
        Func<TSettings, TigerCliPromptContext, IReadOnlyList<OptionItem<TValue>>> provider,
        IEnumerable<string>? dependsOn = null)
    {
        ArgumentNullException.ThrowIfNull(provider);

        // A sync registration: wrap in a completed task for the uniform async invocation path, but
        // record it as sync so a later interactive loading stage can tell it apart from an async one.
        _providers.Add(new TigerCliPromptValueProvider<TSettings, TValue>(
            _property,
            _property.Name,
            _providers.Count,
            NormalizeDependsOn(dependsOn),
            (settings, context) => Task.FromResult(provider(settings, context)),
            isAsync: false));
    }

    private static IReadOnlyList<string> NormalizeDependsOn(IEnumerable<string>? dependsOn) =>
        dependsOn?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray()
        ?? [];
}

internal interface ITigerCliValueProvider
{
    string Key { get; }
    Type SettingsType { get; }
    string? PropertyName { get; }
    int Order { get; }
    IReadOnlyList<string> DependsOn { get; }

    /// <summary>
    /// Whether the provider was registered through an async (<c>Task</c>-returning) overload. Sync
    /// registrations are wrapped in a completed task at registration time and report <c>false</c>.
    /// Invocation is uniformly async regardless; this flag only records provenance so a later
    /// interactive loading stage can decide whether a provider may need offloading to stay responsive.
    /// </summary>
    bool IsAsync { get; }

    /// <summary>
    /// Optional custom message for the interactive slow-provider loading UI, captured at registration.
    /// <c>null</c> means the framework uses its generic localized default. Resolution (literal vs. app
    /// resource key) happens at prompt time against the active culture; providers never render UI.
    /// </summary>
    TigerCliProviderLoadingMessage? LoadingMessage { get; }

    /// <summary>
    /// Optional custom message for a required provider-backed prompt with no selectable outcome.
    /// </summary>
    TigerCliProviderEmptyMessage? EmptyMessage { get; }

    Task<IReadOnlyList<TigerCliPromptChoice>> GetChoicesAsync(
        TigerCliSettings settings,
        TigerCliPromptContext context);
}

internal readonly record struct TigerCliPromptChoice(object Key, string Label);

internal sealed class TigerCliPromptProviderConfigurationException : Exception
{
    public TigerCliPromptProviderConfigurationException(string message)
        : base(message)
    {
    }
}

internal sealed class TigerCliPromptValueProvider<TSettings, TValue> : ITigerCliValueProvider
    where TSettings : TigerCliSettings
{
    private readonly PropertyInfo _property;
    private readonly Func<TSettings, TigerCliPromptContext, Task<IReadOnlyList<OptionItem<TValue>>>> _provider;

    public TigerCliPromptValueProvider(
        PropertyInfo property,
        string key,
        int order,
        IReadOnlyList<string>? dependsOn,
        Func<TSettings, TigerCliPromptContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        bool isAsync = true,
        TigerCliProviderLoadingMessage? loadingMessage = null,
        TigerCliProviderEmptyMessage? emptyMessage = null)
    {
        _property = property;
        Key = key;
        Order = order;
        DependsOn = dependsOn ?? [];
        _provider = provider;
        IsAsync = isAsync;
        LoadingMessage = loadingMessage;
        EmptyMessage = emptyMessage;
    }

    public string Key { get; }
    public Type SettingsType => typeof(TSettings);
    public string PropertyName => _property.Name;
    public int Order { get; }
    public IReadOnlyList<string> DependsOn { get; }
    public bool IsAsync { get; }
    public TigerCliProviderLoadingMessage? LoadingMessage { get; }
    public TigerCliProviderEmptyMessage? EmptyMessage { get; }

    public async Task<IReadOnlyList<TigerCliPromptChoice>> GetChoicesAsync(
        TigerCliSettings settings,
        TigerCliPromptContext context)
    {
        var typedSettings = (TSettings)settings;
        var items = await _provider(typedSettings, context).ConfigureAwait(false)
            ?? throw new TigerCliPromptProviderConfigurationException(
                $"Prompt value provider for {PropertyName} returned null.");

        var seenKeys = new HashSet<TValue>();
        var choices = new List<TigerCliPromptChoice>(items.Count);
        foreach (var item in items)
        {
            if (item.Key == null)
                throw new TigerCliPromptProviderConfigurationException(
                    $"Prompt value provider for {PropertyName} returned a null key.");

            if (!seenKeys.Add(item.Key))
                throw new TigerCliPromptProviderConfigurationException(
                    $"Prompt value provider for {PropertyName} returned duplicate key '{item.Key}'.");

            choices.Add(new TigerCliPromptChoice(item.Key, item.Label));
        }

        return choices;
    }
}

internal sealed class TigerCliValueProvider<TValue> : ITigerCliValueProvider
{
    private readonly Func<TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> _provider;

    public TigerCliValueProvider(
        string key,
        int order,
        IReadOnlyList<string>? dependsOn,
        Func<TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        bool isAsync = true,
        TigerCliProviderLoadingMessage? loadingMessage = null,
        TigerCliProviderEmptyMessage? emptyMessage = null)
    {
        Key = key;
        Order = order;
        DependsOn = dependsOn ?? [];
        _provider = provider;
        IsAsync = isAsync;
        LoadingMessage = loadingMessage;
        EmptyMessage = emptyMessage;
    }

    public string Key { get; }
    public Type SettingsType => typeof(TigerCliSettings);
    public string? PropertyName => null;
    public int Order { get; }
    public IReadOnlyList<string> DependsOn { get; }
    public bool IsAsync { get; }
    public TigerCliProviderLoadingMessage? LoadingMessage { get; }
    public TigerCliProviderEmptyMessage? EmptyMessage { get; }

    public async Task<IReadOnlyList<TigerCliPromptChoice>> GetChoicesAsync(
        TigerCliSettings settings,
        TigerCliPromptContext context)
    {
        var providerContext = new TigerCliProviderContext(context.CancellationToken, context.Culture, context.Values);
        var items = await _provider(providerContext).ConfigureAwait(false)
            ?? throw new TigerCliPromptProviderConfigurationException(
                $"Provider for {Key} returned null.");

        return BuildChoices(items, Key);
    }

    internal static IReadOnlyList<TigerCliPromptChoice> BuildChoices(
        IReadOnlyList<OptionItem<TValue>> items,
        string key)
    {
        var seenKeys = new HashSet<TValue>();
        var choices = new List<TigerCliPromptChoice>(items.Count);
        foreach (var item in items)
        {
            if (item.Key == null)
                throw new TigerCliPromptProviderConfigurationException(
                    $"Provider for {key} returned a null key.");

            if (!seenKeys.Add(item.Key))
                throw new TigerCliPromptProviderConfigurationException(
                    $"Provider for {key} returned duplicate key '{item.Key}'.");

            choices.Add(new TigerCliPromptChoice(item.Key, item.Label));
        }

        return choices;
    }
}

internal sealed class TigerCliStringValueProvider : ITigerCliValueProvider
{
    private readonly Func<TigerCliProviderContext, Task<IReadOnlyList<string>>> _provider;

    public TigerCliStringValueProvider(
        string key,
        int order,
        IReadOnlyList<string>? dependsOn,
        Func<TigerCliProviderContext, Task<IReadOnlyList<string>>> provider,
        bool isAsync = true,
        TigerCliProviderLoadingMessage? loadingMessage = null,
        TigerCliProviderEmptyMessage? emptyMessage = null)
    {
        Key = key;
        Order = order;
        DependsOn = dependsOn ?? [];
        _provider = provider;
        IsAsync = isAsync;
        LoadingMessage = loadingMessage;
        EmptyMessage = emptyMessage;
    }

    public string Key { get; }
    public Type SettingsType => typeof(TigerCliSettings);
    public string? PropertyName => null;
    public int Order { get; }
    public IReadOnlyList<string> DependsOn { get; }
    public bool IsAsync { get; }
    public TigerCliProviderLoadingMessage? LoadingMessage { get; }
    public TigerCliProviderEmptyMessage? EmptyMessage { get; }

    public async Task<IReadOnlyList<TigerCliPromptChoice>> GetChoicesAsync(
        TigerCliSettings settings,
        TigerCliPromptContext context)
    {
        var providerContext = new TigerCliProviderContext(context.CancellationToken, context.Culture, context.Values);
        var items = await _provider(providerContext).ConfigureAwait(false)
            ?? throw new TigerCliPromptProviderConfigurationException(
                $"Provider for {Key} returned null.");

        return BuildChoices(items, Key);
    }

    internal static IReadOnlyList<TigerCliPromptChoice> BuildChoices(
        IReadOnlyList<string> items,
        string key)
    {
        var seenKeys = new HashSet<string>();
        var choices = new List<TigerCliPromptChoice>(items.Count);
        foreach (var item in items)
        {
            if (item == null)
                throw new TigerCliPromptProviderConfigurationException(
                    $"Provider for {key} returned a null key.");

            if (!seenKeys.Add(item))
                throw new TigerCliPromptProviderConfigurationException(
                    $"Provider for {key} returned duplicate key '{item}'.");

            choices.Add(new TigerCliPromptChoice(item, item));
        }

        return choices;
    }
}

internal sealed class TigerCliValueProvider<TSettings, TValue> : ITigerCliValueProvider
    where TSettings : TigerCliSettings
{
    private readonly Func<TSettings, TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> _provider;

    public TigerCliValueProvider(
        string key,
        int order,
        IReadOnlyList<string>? dependsOn,
        Func<TSettings, TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        bool isAsync = true,
        TigerCliProviderLoadingMessage? loadingMessage = null,
        TigerCliProviderEmptyMessage? emptyMessage = null)
    {
        Key = key;
        Order = order;
        DependsOn = dependsOn ?? [];
        _provider = provider;
        IsAsync = isAsync;
        LoadingMessage = loadingMessage;
        EmptyMessage = emptyMessage;
    }

    public string Key { get; }
    public Type SettingsType => typeof(TSettings);
    public string? PropertyName => null;
    public int Order { get; }
    public IReadOnlyList<string> DependsOn { get; }
    public bool IsAsync { get; }
    public TigerCliProviderLoadingMessage? LoadingMessage { get; }
    public TigerCliProviderEmptyMessage? EmptyMessage { get; }

    public async Task<IReadOnlyList<TigerCliPromptChoice>> GetChoicesAsync(
        TigerCliSettings settings,
        TigerCliPromptContext context)
    {
        var typedSettings = (TSettings)settings;
        var providerContext = new TigerCliProviderContext(context.CancellationToken, context.Culture, context.Values);
        var items = await _provider(typedSettings, providerContext).ConfigureAwait(false)
            ?? throw new TigerCliPromptProviderConfigurationException(
                $"Provider for {Key} returned null.");

        return TigerCliValueProvider<TValue>.BuildChoices(items, Key);
    }
}
