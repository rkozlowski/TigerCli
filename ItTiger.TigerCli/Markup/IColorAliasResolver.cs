using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Markup;

/// <summary>
/// Resolves an application-defined <b>raw colour alias</b> (e.g. <c>BrandOrange</c>) to a concrete
/// <see cref="CliColor"/>. Used by <see cref="CliMarkupParser"/> in raw colour positions only —
/// foreground, and the background after <c>on</c>. Colour aliases are a different concept from
/// semantic styles (<see cref="IMarkupStyleResolver"/>): an alias is just a name for a colour and is
/// never a theme role. Keeping this an abstraction lets the parser stay pure — it depends on the
/// resolver, never on a global alias table or console state.
/// </summary>
public interface IColorAliasResolver
{
    /// <summary>
    /// Tries to resolve <paramref name="name"/> (case-insensitive) to a registered colour alias.
    /// Returns <c>false</c> for unregistered names so the parser can fall back to <see cref="CliColor"/>
    /// enum names. Registered aliases take precedence over enum names (see <c>TigerColorAliasRegistry</c>).
    /// </summary>
    bool TryResolve(string name, out CliColor color);
}
