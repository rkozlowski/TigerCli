using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Configures a command group: a command-path prefix that owns a set of child
/// commands. The group name is chosen by the consuming app, and a reusable command
/// library can populate the same group via a static helper that takes a
/// <see cref="TigerCliCommandGroupBuilder"/>.
/// </summary>
/// <remarks>
/// A command registered on a group behaves exactly like a normal command whose path
/// is the group name followed by the command name: <c>group.AddCommand&lt;T&gt;("list")</c>
/// on a group named <c>connections</c> registers the path <c>connections list</c>.
/// The internal model is path-based, so nested groups (added via
/// <see cref="AddCommandGroup"/>) resolve, bind, and execute exactly like directly
/// registered full-path commands.
/// </remarks>
public sealed class TigerCliCommandGroupBuilder
{
    private readonly string _name;
    private readonly string[] _pathTokens;
    private string? _description;
    private string? _descriptionResourceKey;
    private TigerCliPromptMode? _promptMode;
    private CommandMenuMode _commandMenuMode = CommandMenuMode.Inherit;
    private readonly List<ITigerCliValueProvider> _providers = new();
    private readonly List<TigerCliCommandRegistration> _commands = new();
    private readonly List<TigerCliCommandGroupBuilder> _childGroups = new();

    internal TigerCliCommandGroupBuilder(string name)
        : this(SplitPath(name))
    {
    }

    private TigerCliCommandGroupBuilder(string[] pathTokens)
    {
        _pathTokens = pathTokens;
        _name = string.Join(' ', pathTokens);
    }

    /// <summary>
    /// Sets the group description. It is shown next to the group in top-level help
    /// and as the heading of the group's own help. The description may contain
    /// TigerCli markup.
    /// </summary>
    /// <param name="description">Fallback description used when no resource key is
    /// provided or when the key fails to resolve.</param>
    /// <param name="resourceKey">Optional resource key resolved through the app
    /// <c>ResourceManager</c> registered via <c>UseAppResources</c> against the active
    /// run culture. Missing keys silently fall back to <paramref name="description"/>.</param>
    public TigerCliCommandGroupBuilder SetDescription(string description, string? resourceKey = null)
    {
        _description = description ?? throw new ArgumentNullException(nameof(description));
        _descriptionResourceKey = resourceKey;
        return this;
    }

    /// <summary>
    /// Sets the prompt mode inherited by child commands that do not define their own
    /// command-level prompt mode.
    /// </summary>
    public TigerCliCommandGroupBuilder SetPromptMode(TigerCliPromptMode mode)
    {
        _promptMode = mode;
        return this;
    }

    /// <summary>
    /// Sets the group's command-menu opinion, inherited by child commands that do not set their own.
    /// <see cref="CommandMenuMode.Disabled"/> hides the whole group (and its children) from the menu;
    /// <see cref="CommandMenuMode.Enabled"/> opts the group's children in (unless a child overrides
    /// with <see cref="CommandMenuMode.Disabled"/>).
    /// </summary>
    public TigerCliCommandGroupBuilder CommandMenu(CommandMenuMode mode)
    {
        _commandMenuMode = mode;
        return this;
    }

    /// <summary>
    /// Registers an async group-level string provider inherited by child commands; each returned
    /// string is used as both value and label. Equivalent to
    /// <see cref="AddAsyncProvider(string, Func{TigerCliProviderContext, Task{IReadOnlyList{string}}}, IEnumerable{string}, Action{TigerCliProviderOptions})"/>,
    /// which is the preferred spelling for new code.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandGroupBuilder AddProvider(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<string>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        _providers.Add(new TigerCliStringValueProvider(
            key, _providers.Count, NormalizeDependsOn(dependsOn), provider,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
        return this;
    }

    /// <summary>
    /// Registers a sync group-level string provider inherited by child commands; each returned
    /// string is used as both value and label. Prefer the async overloads for slow or I/O-backed
    /// providers.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandGroupBuilder AddProvider(
        string key,
        Func<TigerCliProviderContext, IReadOnlyList<string>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        _providers.Add(new TigerCliStringValueProvider(
            key, _providers.Count, NormalizeDependsOn(dependsOn),
            context => Task.FromResult(provider(context)), isAsync: false,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
        return this;
    }

    /// <summary>
    /// Registers an async group-level named provider inherited by child commands, returning
    /// key/label pairs (<see cref="OptionItem{TValue}"/>). Equivalent to the
    /// <c>AddAsyncProvider</c> spelling, which is preferred for new code.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandGroupBuilder AddProvider<TValue>(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        _providers.Add(new TigerCliValueProvider<TValue>(
            key, _providers.Count, NormalizeDependsOn(dependsOn), provider,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
        return this;
    }

    /// <summary>
    /// Registers a sync group-level named provider inherited by child commands, returning
    /// key/label pairs (<see cref="OptionItem{TValue}"/>). Prefer the async overloads for slow or
    /// I/O-backed providers.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandGroupBuilder AddProvider<TValue>(
        string key,
        Func<TigerCliProviderContext, IReadOnlyList<OptionItem<TValue>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(provider);

        var options = TigerCliProviderOptions.Resolve(configure);
        _providers.Add(new TigerCliValueProvider<TValue>(
            key, _providers.Count, NormalizeDependsOn(dependsOn),
            context => Task.FromResult(provider(context)), isAsync: false,
            loadingMessage: options.LoadingMessage,
            emptyMessage: options.EmptyMessage));
        return this;
    }

    /// <summary>
    /// Registers an async group-level named provider that additionally receives the partially
    /// bound settings instance, letting the choices depend on values supplied earlier in the run.
    /// Inherited by child commands. Equivalent to the <c>AddAsyncProvider</c> spelling, which is
    /// preferred for new code.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandGroupBuilder AddProvider<TSettings, TValue>(
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
        return this;
    }

    /// <summary>
    /// Registers a sync group-level named provider that additionally receives the partially bound
    /// settings instance. Inherited by child commands. Prefer the async overloads for slow or
    /// I/O-backed providers.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public TigerCliCommandGroupBuilder AddProvider<TSettings, TValue>(
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
        return this;
    }

    /// <summary>
    /// Registers an async group-level string provider inherited by child commands. Prefer this over
    /// the sync
    /// <see cref="AddProvider(string, Func{TigerCliProviderContext, IReadOnlyList{string}}, IEnumerable{string}, Action{TigerCliProviderOptions})"/>
    /// for slow or I/O-backed providers; observe <see cref="TigerCliProviderContext.CancellationToken"/>
    /// to make the work cooperatively cancellable, and use <paramref name="configure"/> to customize the
    /// loading message.
    /// </summary>
    public TigerCliCommandGroupBuilder AddAsyncProvider(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<string>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        => AddProvider(key, provider, dependsOn, configure);

    /// <summary>
    /// Registers an async group-level named provider inherited by child commands. Prefer this over the
    /// sync overload for slow or I/O-backed providers; observe
    /// <see cref="TigerCliProviderContext.CancellationToken"/> to make the work cooperatively cancellable,
    /// and use <paramref name="configure"/> to customize the loading message.
    /// </summary>
    public TigerCliCommandGroupBuilder AddAsyncProvider<TValue>(
        string key,
        Func<TigerCliProviderContext, Task<IReadOnlyList<OptionItem<TValue>>>> provider,
        IEnumerable<string>? dependsOn = null,
        Action<TigerCliProviderOptions>? configure = null)
        => AddProvider<TValue>(key, provider, dependsOn, configure);

    /// <summary>
    /// Registers an async group-level named provider that can read partially bound settings. Prefer
    /// this over the sync overload for slow or I/O-backed providers; observe
    /// <see cref="TigerCliProviderContext.CancellationToken"/> to make the work cooperatively cancellable,
    /// and use <paramref name="configure"/> to customize the loading message.
    /// </summary>
    public TigerCliCommandGroupBuilder AddAsyncProvider<TSettings, TValue>(
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

    /// <summary>
    /// Registers a child command created with its parameterless constructor.
    /// <paramref name="name"/> is relative to the group; the resolved command path is
    /// the group name followed by <paramref name="name"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a single token —
    /// nest deeper paths under <see cref="AddCommandGroup"/> instead of a multi-token name.</exception>
    public TigerCliCommandGroupBuilder AddCommand<TCommand>(
        string name,
        string? description = null,
        string? descriptionResourceKey = null)
        where TCommand : class, new()
    {
        return AddCommand<TCommand>(name, configure: null, description, descriptionResourceKey);
    }

    /// <summary>
    /// Registers a child command created with its parameterless constructor and
    /// configures metadata for that command registration.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a single token —
    /// nest deeper paths under <see cref="AddCommandGroup"/> instead of a multi-token name.</exception>
    public TigerCliCommandGroupBuilder AddCommand<TCommand>(
        string name,
        Action<TigerCliCommandBuilder>? configure,
        string? description = null,
        string? descriptionResourceKey = null)
        where TCommand : class, new()
    {
        AddChild(typeof(TCommand), name, configure, description, descriptionResourceKey, factory: null);
        return this;
    }

    /// <summary>
    /// Registers a child command created by <paramref name="factory"/>. Command
    /// factories let reusable command libraries pass services or options into a command
    /// without requiring a DI container in the consuming app.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a single token —
    /// nest deeper paths under <see cref="AddCommandGroup"/> instead of a multi-token name.</exception>
    public TigerCliCommandGroupBuilder AddCommand<TCommand>(
        string name,
        Func<TCommand> factory,
        string? description = null,
        string? descriptionResourceKey = null)
        where TCommand : class
    {
        return AddCommand(name, factory, configure: null, description, descriptionResourceKey);
    }

    /// <summary>
    /// Registers a child command created by <paramref name="factory"/> and configures
    /// metadata for that command registration.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a single token —
    /// nest deeper paths under <see cref="AddCommandGroup"/> instead of a multi-token name.</exception>
    public TigerCliCommandGroupBuilder AddCommand<TCommand>(
        string name,
        Func<TCommand> factory,
        Action<TigerCliCommandBuilder>? configure,
        string? description = null,
        string? descriptionResourceKey = null)
        where TCommand : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        AddChild(typeof(TCommand), name, configure, description, descriptionResourceKey, () => factory());
        return this;
    }

    /// <summary>
    /// Registers a nested command group under this group. <paramref name="name"/> is
    /// relative to this group; the resolved group path is this group's path followed by
    /// <paramref name="name"/>. Commands and further subgroups added through
    /// <paramref name="configure"/> resolve, bind, prompt and execute exactly as if their
    /// full path had been registered directly — nesting is a help/menu and configuration
    /// convenience, not a separate dispatch path. Group-level prompt mode, providers and
    /// command-menu opinion cascade to descendants; a nearer group overrides a farther one.
    /// </summary>
    public TigerCliCommandGroupBuilder AddCommandGroup(
        string name,
        Action<TigerCliCommandGroupBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var childTokens = SplitPath(name);
        var childBuilder = new TigerCliCommandGroupBuilder([.. _pathTokens, .. childTokens]);
        configure(childBuilder);
        _childGroups.Add(childBuilder);
        return this;
    }

    private void AddChild(
        Type handlerType,
        string name,
        Action<TigerCliCommandBuilder>? configure,
        string? description,
        string? descriptionResourceKey,
        Func<object>? factory)
    {
        var childTokens = SplitPath(name);
        if (childTokens.Length != 1)
            throw new ArgumentException(
                "A command name added to a group must be a single token. Nest deeper paths under additional command groups rather than a multi-token name.",
                nameof(name));
        var fullName = string.Join(' ', _pathTokens.Concat(childTokens));
        var commandBuilder = new TigerCliCommandBuilder();
        configure?.Invoke(commandBuilder);
        _commands.Add(new TigerCliCommandRegistration(
            fullName,
            description,
            handlerType,
            descriptionResourceKey,
            factory,
            isGroupChild: true,
            promptMode: commandBuilder.PromptMode,
            providers: commandBuilder.Providers,
            editLoader: commandBuilder.EditLoader,
            titleAppend: commandBuilder.TitleAppend,
            titleSet: commandBuilder.TitleSet,
            commandMenuMode: commandBuilder.CommandMenuMode));
    }

    internal TigerCliCommandGroupRegistration BuildGroup() =>
        new(_name, _description, _descriptionResourceKey, _promptMode, _providers);

    /// <summary>
    /// Flattens this group and all nested subgroups into the app's path-based command and
    /// group lists. Group-level prompt mode, providers and command-menu opinion cascade to
    /// descendant commands, with a nearer group overriding a farther one; the resulting
    /// registrations are indistinguishable from directly registered full-path commands.
    /// </summary>
    internal void Collect(
        List<TigerCliCommandGroupRegistration> groups,
        List<TigerCliCommandRegistration> commands,
        IReadOnlyList<ITigerCliValueProvider> inheritedProviders,
        TigerCliPromptMode? inheritedPromptMode,
        CommandMenuMode inheritedCommandMenuMode)
    {
        var effectiveProviders = MergeProviders(inheritedProviders, _providers);
        var effectivePromptMode = _promptMode ?? inheritedPromptMode;
        var effectiveCommandMenuMode = CombineCommandMenuMode(inheritedCommandMenuMode, _commandMenuMode);

        groups.Add(BuildGroup());

        foreach (var command in _commands)
        {
            command.GroupPromptMode = effectivePromptMode;
            command.GroupProviders = effectiveProviders;
            command.GroupCommandMenuMode = effectiveCommandMenuMode;
            commands.Add(command);
        }

        foreach (var childGroup in _childGroups)
            childGroup.Collect(
                groups, commands, effectiveProviders, effectivePromptMode, effectiveCommandMenuMode);
    }

    private static IReadOnlyList<ITigerCliValueProvider> MergeProviders(
        IReadOnlyList<ITigerCliValueProvider> inherited,
        IReadOnlyList<ITigerCliValueProvider> own)
    {
        if (inherited.Count == 0)
            return own;
        if (own.Count == 0)
            return inherited;

        // A command inherits group providers from every ancestor group. The nearer group wins
        // per key; farther ancestors only fill keys the nearer levels did not define.
        var ownKeys = new HashSet<string>(own.Select(p => p.Key), StringComparer.OrdinalIgnoreCase);
        var merged = new List<ITigerCliValueProvider>(own);
        merged.AddRange(inherited.Where(p => !ownKeys.Contains(p.Key)));
        return merged;
    }

    private static CommandMenuMode CombineCommandMenuMode(CommandMenuMode outer, CommandMenuMode inner)
    {
        // Mirror CommandMenuEligibility's chain rule collapsed across group levels: a single
        // Disabled anywhere wins, otherwise any Enabled opts the subtree in, otherwise Inherit.
        if (outer == CommandMenuMode.Disabled || inner == CommandMenuMode.Disabled)
            return CommandMenuMode.Disabled;
        if (outer == CommandMenuMode.Enabled || inner == CommandMenuMode.Enabled)
            return CommandMenuMode.Enabled;
        return CommandMenuMode.Inherit;
    }

    private static string[] SplitPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command name must not be empty.", nameof(name));

        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Any(token => token.StartsWith('-')))
            throw new ArgumentException("Command path tokens must not start with '-'.", nameof(name));

        return tokens;
    }
}
