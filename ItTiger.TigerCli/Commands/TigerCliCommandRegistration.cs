using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Commands;

internal sealed class TigerCliCommandRegistration
{
    public string? Name { get; }
    public string[] PathTokens { get; }
    public string? Description { get; }
    public string? DescriptionResourceKey { get; }
    public Type HandlerType { get; }
    public Type SettingsType { get; }
    public Type? ExitCodeType { get; }
    public bool ResolveHandlerResultAsExitKind { get; }
    public TigerCliPromptMode? PromptMode { get; }
    public TigerCliPromptMode? GroupPromptMode { get; internal set; }
    public IReadOnlyList<ITigerCliValueProvider> Providers { get; }
    public IReadOnlyList<ITigerCliValueProvider> GroupProviders { get; internal set; } = [];
    public bool IsDefault { get; internal set; }
    public string? TitleAppend { get; }
    public string? TitleSet { get; }

    /// <summary>This command's own command-menu opinion (set via the command builder).</summary>
    public CommandMenuMode CommandMenuMode { get; }

    /// <summary>
    /// The owning group's command-menu opinion, stamped onto group children. Null for ungrouped
    /// commands (no group level in their chain).
    /// </summary>
    public CommandMenuMode? GroupCommandMenuMode { get; internal set; }

    /// <summary>
    /// True for the internal command-menu sentinel registration. Such commands are hidden from
    /// help and from the menu's own eligible list, and are intercepted before execution.
    /// </summary>
    public bool IsCommandMenu { get; }

    /// <summary>
    /// Optional edit-command loader. When set, the command is an edit command: the
    /// framework loads the existing object before prompting and merges its values into
    /// the bound settings for properties not supplied on the command line.
    /// </summary>
    public Func<TigerCliSettings, Task<TigerCliEditLoadResult>>? EditLoader { get; }

    /// <summary>True when this command was registered as an edit command via AsEdit.</summary>
    public bool IsEdit => EditLoader != null;

    /// <summary>
    /// Optional factory that produces the handler instance. When null, the handler
    /// is created with its parameterless constructor. Command factories let reusable
    /// command libraries supply services or options without a DI container.
    /// </summary>
    public Func<object>? Factory { get; }

    /// <summary>
    /// True when this command was registered through a <see cref="TigerCliCommandGroupBuilder"/>.
    /// Group children are excluded from the flat top-level help listing; their group
    /// represents them there instead.
    /// </summary>
    public bool IsGroupChild { get; }

    public TigerCliCommandRegistration(
        string? name,
        string? description,
        Type handlerType,
        string? descriptionResourceKey = null,
        Func<object>? factory = null,
        bool isGroupChild = false,
        TigerCliPromptMode? promptMode = null,
        TigerCliPromptMode? groupPromptMode = null,
        IReadOnlyList<ITigerCliValueProvider>? providers = null,
        IReadOnlyList<ITigerCliValueProvider>? groupProviders = null,
        Func<TigerCliSettings, Task<TigerCliEditLoadResult>>? editLoader = null,
        string? titleAppend = null,
        string? titleSet = null,
        CommandMenuMode commandMenuMode = CommandMenuMode.Inherit,
        CommandMenuMode? groupCommandMenuMode = null,
        bool isCommandMenu = false,
        bool resolveHandlerResultAsExitKind = false)
    {
        if (!string.IsNullOrWhiteSpace(titleAppend) && !string.IsNullOrWhiteSpace(titleSet))
            throw new InvalidOperationException("A command cannot configure both TitleAppend and TitleSet.");

        Name = name;
        PathTokens = name == null
            ? []
            : name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Description = description;
        DescriptionResourceKey = descriptionResourceKey;
        HandlerType = handlerType;
        Factory = factory;
        IsGroupChild = isGroupChild;
        PromptMode = promptMode;
        GroupPromptMode = groupPromptMode;
        Providers = providers ?? [];
        GroupProviders = groupProviders ?? [];
        EditLoader = editLoader;
        TitleAppend = string.IsNullOrWhiteSpace(titleAppend) ? null : titleAppend;
        TitleSet = string.IsNullOrWhiteSpace(titleSet) ? null : titleSet;
        CommandMenuMode = commandMenuMode;
        GroupCommandMenuMode = groupCommandMenuMode;
        IsCommandMenu = isCommandMenu;
        ResolveHandlerResultAsExitKind = resolveHandlerResultAsExitKind;
        (SettingsType, ExitCodeType) = ResolveHandlerTypes(handlerType);
    }

    private static (Type SettingsType, Type? ExitCodeType) ResolveHandlerTypes(Type handlerType)
    {
        var current = handlerType;
        while (current != null)
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(TigerCliAsyncCommandHandler<>))
            {
                return (current.GetGenericArguments()[0], null);
            }

            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(TigerCliAsyncCommandHandler<,>))
            {
                var args = current.GetGenericArguments();
                return (args[0], args[1]);
            }
            current = current.BaseType;
        }
        throw new InvalidOperationException(
            $"Type '{handlerType.Name}' must derive from TigerCliAsyncCommandHandler<TSettings> or TigerCliAsyncCommandHandler<TSettings, TExitCode>.");
    }
}
