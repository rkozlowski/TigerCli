using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Tui.Themes;

/// <summary>
/// App-scoped registry of <b>custom semantic styles</b> (see <see cref="CliCustomStyle"/>). Custom
/// styles extend the curated framework semantic tokens with app-defined roles such as
/// <c>ConnectionName</c> or <c>EnvironmentProd</c>; they resolve through the active theme and are used
/// as single-token markup tags.
/// <para>
/// Instances are app-local: construct one per app/test, configure it, then make it active via
/// <c>TigerConsole.CustomStyles</c> so the markup call sites resolve custom tokens. Nothing is
/// process-global.
/// </para>
/// </summary>
public sealed class TigerCustomStyleRegistry
{
    private readonly Dictionary<string, CliCustomStyle> _styles =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when no custom styles are registered.</summary>
    public bool IsEmpty => _styles.Count == 0;

    /// <summary>The registered custom style names (case-insensitive). Snapshot.</summary>
    public IReadOnlyCollection<string> Names => _styles.Keys.ToArray();

    /// <summary>
    /// Registers (or replaces) a custom style. <paramref name="baseStyle"/> is the required framework
    /// fallback; <paramref name="darkStyle"/>/<paramref name="lightStyle"/> are optional family
    /// overrides. Re-registering the same name replaces the whole definition (including any exact
    /// theme-name overrides previously added through <see cref="RegisterForTheme"/>).
    /// </summary>
    /// <exception cref="ArgumentException">The name is invalid or collides with a framework semantic token.</exception>
    public TigerCustomStyleRegistry Register(
        string name,
        ThemeStyle baseStyle,
        CliCellStyle? darkStyle = null,
        CliCellStyle? lightStyle = null)
    {
        var validated = ValidateName(name);
        _styles[validated] = new CliCustomStyle(validated, baseStyle, darkStyle, lightStyle);
        return this;
    }

    /// <summary>
    /// Adds an exact theme-name override to an already-registered custom style. Theme-name overrides
    /// win over dark/light family overrides. The custom style must already be registered (the required
    /// base style is established by <see cref="Register"/>).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="styleName"/> is not registered, or <paramref name="themeName"/> is empty.</exception>
    public TigerCustomStyleRegistry RegisterForTheme(string styleName, string themeName, CliCellStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (string.IsNullOrWhiteSpace(themeName))
            throw new ArgumentException("Theme name must be non-empty.", nameof(themeName));
        if (styleName is null || !_styles.TryGetValue(styleName.Trim(), out var definition))
            throw new ArgumentException(
                $"Custom style '{styleName}' must be registered before adding a theme override.", nameof(styleName));

        definition.SetThemeOverride(themeName.Trim(), style);
        return this;
    }

    /// <summary>Removes a custom style if present. Returns <c>true</c> when one was removed.</summary>
    public bool Remove(string name)
        => name is not null && _styles.Remove(name.Trim());

    /// <summary>True when a custom style with this name is registered (case-insensitive).</summary>
    public bool Contains(string name)
        => name is not null && _styles.ContainsKey(name.Trim());

    /// <summary>Tries to get a registered custom style by name (case-insensitive).</summary>
    public bool TryGet(string name, [NotNullWhen(true)] out CliCustomStyle? style)
    {
        if (name is not null && _styles.TryGetValue(name.Trim(), out style))
            return true;
        style = null;
        return false;
    }

    // Reserved markup keywords. Includes the composed-expression keywords (on/bold/italic/underline)
    // and the standalone short decoration aliases (b/i/u) the parser intercepts before custom-style
    // resolution — a custom style with one of these names would be unreachable via markup.
    private static readonly HashSet<string> ReservedTokens =
        new(StringComparer.OrdinalIgnoreCase) { "on", "bold", "italic", "underline", "b", "i", "u" };

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Custom style name must be non-empty.", nameof(name));

        var trimmed = name.Trim();
        if (!trimmed.All(char.IsLetterOrDigit))
            throw new ArgumentException(
                $"Custom style name '{trimmed}' must contain only letters and digits.", nameof(name));

        if (ReservedTokens.Contains(trimmed))
            throw new ArgumentException(
                $"'{trimmed}' is a reserved markup keyword and cannot be a custom style name.", nameof(name));

        if (ThemeMarkupStyleResolver.IsFrameworkToken(trimmed))
            throw new ArgumentException(
                $"'{trimmed}' is a framework semantic token and cannot be redefined as a custom style.", nameof(name));

        return trimmed;
    }
}
