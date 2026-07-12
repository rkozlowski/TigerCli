using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Metadata for a command group: a command-path prefix that owns a set of child
/// commands. A group is purely a path prefix plus help metadata — its children are
/// regular command registrations whose path begins with <see cref="PathTokens"/>,
/// so parsing, binding and execution stay on the same pipeline as ungrouped commands.
/// </summary>
internal sealed class TigerCliCommandGroupRegistration
{
    public string Name { get; }
    public string[] PathTokens { get; }
    public string? Description { get; }
    public string? DescriptionResourceKey { get; }
    public TigerCliPromptMode? PromptMode { get; }
    public IReadOnlyList<ITigerCliValueProvider> Providers { get; }

    public TigerCliCommandGroupRegistration(
        string name,
        string? description,
        string? descriptionResourceKey,
        TigerCliPromptMode? promptMode,
        IReadOnlyList<ITigerCliValueProvider>? providers)
    {
        Name = name;
        PathTokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Description = description;
        DescriptionResourceKey = descriptionResourceKey;
        PromptMode = promptMode;
        Providers = providers ?? [];
    }
}
