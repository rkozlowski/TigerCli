using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Configures metadata for a single command registration.
/// </summary>
public sealed class TigerCliCommandBuilder
{
    internal TigerCliCommandBuilder() { }

    internal TigerCliPromptMode? PromptMode { get; private set; }
    internal List<ITigerCliValueProvider> Providers { get; } = new();
    internal Func<TigerCliSettings, Task<TigerCliEditLoadResult>>? EditLoader { get; private set; }
    internal string? TitleAppend { get; private set; }
    internal string? TitleSet { get; private set; }
    internal CommandMenuMode CommandMenuMode { get; private set; } = CommandMenuMode.Inherit;

    /// <summary>
    /// Sets the prompt mode override for this command registration.
    /// </summary>
    public TigerCliCommandBuilder SetPromptMode(TigerCliPromptMode mode)
    {
        PromptMode = mode;
        return this;
    }

    /// <summary>
    /// Sets this command's command-menu opinion. Combined with the app and group levels, a command
    /// is shown in the menu when the chain has at least one <see cref="CommandMenuMode.Enabled"/>
    /// and no <see cref="CommandMenuMode.Disabled"/>. Use <see cref="CommandMenuMode.Disabled"/> to
    /// hide an otherwise-eligible command from the menu.
    /// </summary>
    public TigerCliCommandBuilder CommandMenu(CommandMenuMode mode)
    {
        CommandMenuMode = mode;
        return this;
    }

    /// <summary>
    /// Appends command-specific text to the app title for this command, using
    /// <c>{AppTitle} - {titleAppend}</c>. Cannot be combined with <see cref="SetTitle"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="titleAppend"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="SetTitle"/> was already configured.</exception>
    public TigerCliCommandBuilder AppendTitle(string titleAppend)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titleAppend);
        if (TitleSet != null)
            throw new InvalidOperationException("A command cannot configure both TitleAppend and TitleSet.");

        TitleAppend = titleAppend;
        return this;
    }

    /// <summary>
    /// Replaces the app title entirely while this command is active. Cannot be combined with
    /// <see cref="AppendTitle"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="titleSet"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="AppendTitle"/> was already configured.</exception>
    public TigerCliCommandBuilder SetTitle(string titleSet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titleSet);
        if (TitleAppend != null)
            throw new InvalidOperationException("A command cannot configure both TitleAppend and TitleSet.");

        TitleSet = titleSet;
        return this;
    }

    /// <summary>
    /// Marks this command as an edit command. Before prompting, the framework invokes
    /// <paramref name="loader"/> with the already-bound settings (selector arguments and
    /// command-line overrides applied). The loader returns
    /// <see cref="TigerCliEditLoad{TSettings}.Found"/> with the existing object's values,
    /// or <see cref="TigerCliEditLoad{TSettings}.NotFound"/>. Existing values seed every
    /// property that was not supplied on the command line; command-line values always win.
    /// The command handler still owns saving the edited object.
    /// </summary>
    public TigerCliCommandBuilder AsEdit<TSettings>(
        Func<TSettings, Task<TigerCliEditLoad<TSettings>>> loader)
        where TSettings : TigerCliSettings
    {
        ArgumentNullException.ThrowIfNull(loader);

        EditLoader = async settings =>
        {
            var typed = (TSettings)settings;
            var result = await loader(typed).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Edit loader returned null.");
            return new TigerCliEditLoadResult(result.IsFound, result.Existing);
        };
        return this;
    }

    /// <summary>
    /// Synchronous overload of
    /// <see cref="AsEdit{TSettings}(Func{TSettings, Task{TigerCliEditLoad{TSettings}}})"/>.
    /// </summary>
    public TigerCliCommandBuilder AsEdit<TSettings>(
        Func<TSettings, TigerCliEditLoad<TSettings>> loader)
        where TSettings : TigerCliSettings
    {
        ArgumentNullException.ThrowIfNull(loader);

        return AsEdit<TSettings>(settings => Task.FromResult(loader(settings)));
    }

    /// <summary>
    /// Registers an async command-level string provider; each returned string is used as both
    /// value and label. Equivalent to
    /// <see cref="AddAsyncProvider(string, Func{TigerCliProviderContext, Task{IReadOnlyList{string}}}, IEnumerable{string}, Action{TigerCliProviderOptions})"/>,
    /// which is the preferred spelling for new code.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandBuilder AddProvider(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<string>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        Providers.Add(new TigerCliStringValueProvider(
            key, Providers.Count, NormalizeDependsOn(dependsOn), provider,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
        return this;
    }

    /// <summary>
    /// Registers a sync command-level string provider; each returned string is used as both value
    /// and label. Prefer the async overloads for slow or I/O-backed providers.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandBuilder AddProvider(
        string key,
        Func<TigerCliProviderContext, IReadOnlyList<string>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        Providers.Add(new TigerCliStringValueProvider(
            key, Providers.Count, NormalizeDependsOn(dependsOn),
            context => Task.FromResult(provider(context)), isAsync: false,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
        return this;
    }

    /// <summary>
    /// Registers an async command-level named provider returning key/label pairs
    /// (<see cref="OptionItem{TValue}"/>). Equivalent to the <c>AddAsyncProvider</c> spelling,
    /// which is preferred for new code.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandBuilder AddProvider<TValue>(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        Providers.Add(new TigerCliValueProvider<TValue>(
            key, Providers.Count, NormalizeDependsOn(dependsOn), provider,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
        return this;
    }

    /// <summary>
    /// Registers a sync command-level named provider returning key/label pairs
    /// (<see cref="OptionItem{TValue}"/>). Prefer the async overloads for slow or I/O-backed
    /// providers.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandBuilder AddProvider<TValue>(
        string key,
        Func<TigerCliProviderContext, IReadOnlyList<OptionItem<TValue>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        Providers.Add(new TigerCliValueProvider<TValue>(
            key, Providers.Count, NormalizeDependsOn(dependsOn),
            context => Task.FromResult(provider(context)), isAsync: false,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
        return this;
    }

    /// <summary>
    /// Registers an async command-level named provider that additionally receives the partially
    /// bound settings instance, letting the choices depend on values supplied earlier in the run.
    /// Equivalent to the <c>AddAsyncProvider</c> spelling, which is preferred for new code.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandBuilder AddProvider<TSettings, TValue>(
        string key,
        Func<TSettings, TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        where TSettings : TigerCliSettings
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        Providers.Add(new TigerCliValueProvider<TSettings, TValue>(
            key,
            Providers.Count,
            NormalizeDependsOn(dependsOn),
            provider,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
        return this;
    }

    /// <summary>
    /// Registers a sync command-level named provider that additionally receives the partially
    /// bound settings instance. Prefer the async overloads for slow or I/O-backed providers.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandBuilder AddProvider<TSettings, TValue>(
        string key,
        Func<TSettings, TigerCliProviderContext, IReadOnlyList<OptionItem<TValue>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        where TSettings : TigerCliSettings
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        Providers.Add(new TigerCliValueProvider<TSettings, TValue>(
            key,
            Providers.Count,
            NormalizeDependsOn(dependsOn),
            (settings, context) => Task.FromResult(provider(settings, context)),
            isAsync: false,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
        return this;
    }

    /// <summary>
    /// Registers an async command-level string provider. Prefer this over the sync
    /// <see cref="AddProvider(string, Func{TigerCliProviderContext, IReadOnlyList{string}}, IEnumerable{string}, Action{TigerCliProviderOptions})"/>
    /// for slow or I/O-backed providers; observe <see cref="TigerCliProviderContext.CancellationToken"/>
    /// to make the work cooperatively cancellable, and use <paramref name="configure"/> to customize the
    /// loading message.
    /// </summary>
    public TigerCliCommandBuilder AddAsyncProvider(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<string>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        => AddProvider(key, provider, dependsOn, configure);

    /// <summary>
    /// Registers an async command-level named provider. Prefer this over the sync overload for slow or
    /// I/O-backed providers; observe <see cref="TigerCliProviderContext.CancellationToken"/> to make
    /// the work cooperatively cancellable, and use <paramref name="configure"/> to customize the loading
    /// message.
    /// </summary>
    public TigerCliCommandBuilder AddAsyncProvider<TValue>(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        => AddProvider<TValue>(key, provider, dependsOn, configure);

    /// <summary>
    /// Registers an async command-level named provider that can read partially bound settings. Prefer
    /// this over the sync overload for slow or I/O-backed providers; observe
    /// <see cref="TigerCliProviderContext.CancellationToken"/> to make the work cooperatively cancellable,
    /// and use <paramref name="configure"/> to customize the loading message.
    /// </summary>
    public TigerCliCommandBuilder AddAsyncProvider<TSettings, TValue>(
        string key,
        Func<TSettings, TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        where TSettings : TigerCliSettings
        => AddProvider<TSettings, TValue>(key, provider, dependsOn, configure);

    private static IReadOnlyList<string> NormalizeDependsOn(IEnumerable<string>? dependsOn) =>
        dependsOn?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray()
        ?? [];
}
