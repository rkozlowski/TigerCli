using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Configures a command alias registered via
/// <see cref="TigerCliAppBuilder.AddCommandAlias(string, string, Action{TigerCliCommandAliasBuilder}?)"/>.
/// An alias is an alternate entry point into the existing command tree: it owns no handler,
/// settings, or providers, and the target command owns parsing, prompting, validation, and
/// execution. The alias may own only its help/menu presentation.
/// </summary>
/// <remarks>
/// At resolution time real commands win over aliases; a matched alias resolves to its target
/// command, so parsing, prompting, providers, validation, prompt-mode inheritance, and execution
/// all run on the target unchanged. <c>&lt;app&gt; &lt;alias&gt; --help</c> shows the alias name
/// and description plus an <c>Alias for: &lt;target&gt;</c> note, while the arguments and options
/// come from the target command's settings. Aliases are root-level single-token names mapping to
/// an existing command path; multi-token alias paths are not supported.
/// </remarks>
public sealed class TigerCliCommandAliasBuilder
{
    internal TigerCliCommandAliasBuilder() { }

    internal string? Description { get; private set; }
    internal string? DescriptionResourceKey { get; private set; }
    internal CommandMenuMode CommandMenuMode { get; private set; } = CommandMenuMode.Inherit;
    internal bool HiddenFromHelp { get; private set; }

    /// <summary>
    /// Sets the alias's own description, shown in the help "Aliases" section and the alias's command
    /// help. When omitted, the alias falls back to the target command's description. The description
    /// may contain TigerCli markup.
    /// </summary>
    /// <param name="description">Fallback description used when no resource key is provided or when
    /// the key fails to resolve.</param>
    /// <param name="resourceKey">Optional resource key resolved through the app
    /// <c>ResourceManager</c> registered via <c>UseAppResources</c> against the active run culture.
    /// Missing keys silently fall back to <paramref name="description"/>.</param>
    public TigerCliCommandAliasBuilder SetDescription(string description, string? resourceKey = null)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
        DescriptionResourceKey = resourceKey;
        return this;
    }

    /// <summary>
    /// Sets the alias's command-menu opinion. The alias has its own eligibility chain (app level →
    /// alias) and does not inherit the target's menu eligibility, so a target hidden from the menu
    /// can still be reached through a visible alias.
    /// </summary>
    public TigerCliCommandAliasBuilder CommandMenu(CommandMenuMode mode)
    {
        CommandMenuMode = mode;
        return this;
    }

    /// <summary>
    /// Hides the alias from generated help. The alias still resolves on the command line and may
    /// still appear in the command menu (controlled separately via <see cref="CommandMenu"/>).
    /// </summary>
    public TigerCliCommandAliasBuilder HideFromHelp()
    {
        HiddenFromHelp = true;
        return this;
    }
}
