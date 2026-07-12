using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Metadata for a command alias: an alternate root-level entry point that maps a single-token
/// name to an existing command path (for example <c>import</c> → <c>card ingest</c>). An alias
/// owns no handler, settings, or providers — it only redirects resolution to its
/// <see cref="Target"/>, which owns parsing, prompting, validation, and execution. An alias may
/// own its own help/menu presentation (description, help visibility, command-menu opinion).
/// </summary>
internal sealed class TigerCliCommandAliasRegistration
{
    public string Name { get; }
    public string[] PathTokens { get; }
    public string TargetPath { get; }
    public string[] TargetPathTokens { get; }
    public string? Description { get; }
    public string? DescriptionResourceKey { get; }

    /// <summary>This alias's own command-menu opinion. Combined only with the app level (aliases
    /// are root-level, so there is no group node and the target's eligibility is not inherited).</summary>
    public CommandMenuMode CommandMenuMode { get; }

    /// <summary>When true, the alias is omitted from generated help; it still resolves and runs.</summary>
    public bool HiddenFromHelp { get; }

    /// <summary>
    /// The command this alias redirects to. Linked at app-construction time by
    /// <see cref="TigerCliApp"/> after the full command tree (and the command-menu sentinel) is known.
    /// </summary>
    public TigerCliCommandRegistration Target { get; private set; } = default!;

    public TigerCliCommandAliasRegistration(
        string name,
        string targetPath,
        string? description,
        string? descriptionResourceKey,
        CommandMenuMode commandMenuMode,
        bool hiddenFromHelp)
    {
        Name = name;
        PathTokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        TargetPath = targetPath;
        TargetPathTokens = targetPath.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Description = description;
        DescriptionResourceKey = descriptionResourceKey;
        CommandMenuMode = commandMenuMode;
        HiddenFromHelp = hiddenFromHelp;
    }

    internal void LinkTarget(TigerCliCommandRegistration target) => Target = target;
}
