using System;
using System.Collections.Generic;
using System.Linq;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;

namespace ItTiger.TigerCli.Tui.Themes;

/// <summary>
/// App-scoped registry of <b>raw colour aliases</b>: application-defined names for concrete
/// <see cref="CliColor"/> values (e.g. <c>BrandOrange</c> → <see cref="CliColor.Orange"/>). Aliases
/// are valid only in raw colour positions of markup (<c>[BrandOrange]</c>, <c>[White on CompanyBlue]</c>)
/// and are a different concept from semantic styles.
/// <para>
/// TigerCli ships <b>no</b> built-in aliases — only the <see cref="CliColor"/> enum names are known by
/// default. Apps (or opt-in libraries) register the aliases they want. A registered alias takes
/// precedence over a <see cref="CliColor"/> enum name of the same name: registering an alias is a
/// deliberate app policy, so the alias wins and the enum value remains the fallback when no alias is
/// registered.
/// </para>
/// Instances are app-local: construct one per app/test, configure it, then make it active via
/// <c>TigerConsole.ColorAliases</c>. Nothing is process-global.
/// </summary>
public sealed class TigerColorAliasRegistry : IColorAliasResolver
{
    private readonly Dictionary<string, CliColor> _aliases =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when no aliases are registered.</summary>
    public bool IsEmpty => _aliases.Count == 0;

    /// <summary>The registered alias names (case-insensitive), in no particular order. Snapshot.</summary>
    public IReadOnlyCollection<string> Names => _aliases.Keys.ToArray();

    /// <summary>
    /// Registers (or overwrites) a colour alias. Alias names are case-insensitive and must be a single
    /// markup token: letters and digits only, no whitespace, and not one of the reserved raw-expression
    /// keywords (<c>on</c>, <c>bold</c>, <c>italic</c>, <c>underline</c>) which the parser interprets
    /// specially and could never reach an alias.
    /// </summary>
    /// <exception cref="ArgumentException">The name is null/empty/whitespace, contains non-alphanumeric characters, or is reserved.</exception>
    public TigerColorAliasRegistry Register(string alias, CliColor color)
    {
        _aliases[ValidateName(alias)] = color;
        return this;
    }

    /// <summary>Removes an alias if present. Returns <c>true</c> when an alias was removed.</summary>
    public bool Remove(string alias)
        => alias is not null && _aliases.Remove(alias.Trim());

    /// <summary>True when an alias with this name is registered (case-insensitive).</summary>
    public bool Contains(string alias)
        => alias is not null && _aliases.ContainsKey(alias.Trim());

    /// <inheritdoc />
    public bool TryResolve(string name, out CliColor color)
    {
        if (name is not null && _aliases.TryGetValue(name.Trim(), out color))
            return true;
        color = default;
        return false;
    }

    private static readonly HashSet<string> ReservedTokens =
        new(StringComparer.OrdinalIgnoreCase) { "on", "bold", "italic", "underline" };

    private static string ValidateName(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Colour alias name must be non-empty.", nameof(alias));

        var trimmed = alias.Trim();
        if (!trimmed.All(char.IsLetterOrDigit))
            throw new ArgumentException(
                $"Colour alias name '{trimmed}' must contain only letters and digits.", nameof(alias));

        if (ReservedTokens.Contains(trimmed))
            throw new ArgumentException(
                $"'{trimmed}' is a reserved markup keyword and cannot be a colour alias.", nameof(alias));

        return trimmed;
    }
}
