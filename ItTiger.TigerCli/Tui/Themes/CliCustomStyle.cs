using System;
using System.Collections.Generic;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Themes;

/// <summary>
/// An app-defined <b>custom semantic style</b>: a named markup token (e.g. <c>ConnectionName</c>,
/// <c>EnvironmentProd</c>) that resolves through the active theme like the framework semantic tokens.
/// A custom style always has a required framework <see cref="ThemeStyle"/> <see cref="BaseStyle"/> as a
/// safe fallback, plus optional dark/light family overrides and optional exact theme-name overrides.
/// <para>Resolution order (most specific first):</para>
/// <list type="number">
/// <item>exact theme-name override (matches <see cref="ITheme.Name"/>, case-insensitive);</item>
/// <item>dark/light family override (chosen by <see cref="ITheme.Family"/>);</item>
/// <item>the active theme's resolved <see cref="BaseStyle"/>.</item>
/// </list>
/// Custom styles are used as semantic markup tags only (single-token, e.g. <c>[ConnectionName]…[/]</c>);
/// like other semantic styles they are never valid inside a raw <c>on</c> colour expression.
/// </summary>
public sealed class CliCustomStyle
{
    private readonly Dictionary<string, CliCellStyle> _themeOverrides =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The custom style's markup token name (case-insensitive).</summary>
    public string Name { get; }

    /// <summary>The required framework fallback role resolved through the active theme.</summary>
    public ThemeStyle BaseStyle { get; }

    /// <summary>Optional override applied under dark-family themes.</summary>
    public CliCellStyle? DarkStyle { get; }

    /// <summary>Optional override applied under light-family themes.</summary>
    public CliCellStyle? LightStyle { get; }

    internal CliCustomStyle(string name, ThemeStyle baseStyle, CliCellStyle? darkStyle, CliCellStyle? lightStyle)
    {
        Name = name;
        BaseStyle = baseStyle;
        DarkStyle = darkStyle;
        LightStyle = lightStyle;
    }

    /// <summary>Adds or replaces an exact theme-name override (used by the registry).</summary>
    internal void SetThemeOverride(string themeName, CliCellStyle style)
        => _themeOverrides[themeName] = style;

    /// <summary>
    /// Resolves this custom style against <paramref name="theme"/> following the documented order and
    /// returns the effective character style. Never throws on a missing override — it always falls back
    /// to the active theme's <see cref="BaseStyle"/>.
    /// </summary>
    public CliCharStyle? Resolve(ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        // 1. Exact theme-name override.
        if (_themeOverrides.TryGetValue(theme.Name, out var exact))
            return exact.CharStyle;

        // 2. Dark/light family override.
        var familyOverride = theme.Family == TigerThemeFamily.Light ? LightStyle : DarkStyle;
        if (familyOverride is not null)
            return familyOverride.CharStyle;

        // 3. Active theme's resolved base ThemeStyle.
        return theme.Resolve(BaseStyle).CharStyle;
    }
}
