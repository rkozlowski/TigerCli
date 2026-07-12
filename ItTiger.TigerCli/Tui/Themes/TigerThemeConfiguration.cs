using System;
using System.Collections.Generic;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Themes;

/// <summary>
/// App-scoped theme, style, and colour-alias policy configured through the app builder's
/// <c>ConfigureThemes</c> block. The application owns this policy: framework themes are available by
/// default, custom themes and styles are added explicitly, library packages register through this
/// object (referencing a library never mutates TigerCli), and apps may disable framework themes they
/// do not want. Everything configured here is applied to the active <c>TigerConsole</c> appearance for
/// the run; nothing is registered merely because a type or library is referenced.
/// </summary>
public sealed class TigerThemeConfiguration
{
    private readonly List<ITheme> _customThemes = new();
    private readonly HashSet<string> _disabledThemes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The app's raw colour aliases (empty by default — no built-in aliases).</summary>
    public TigerColorAliasRegistry ColorAliases { get; } = new();

    /// <summary>The app's custom semantic styles (empty by default).</summary>
    public TigerCustomStyleRegistry CustomStyles { get; } = new();

    internal IReadOnlyList<ITheme> CustomThemes => _customThemes;
    internal IReadOnlyCollection<string> DisabledThemes => _disabledThemes;

    /// <summary>
    /// Registers a custom theme by its <see cref="ITheme.Name"/>. The name must not be a framework theme
    /// name or the reserved <c>"default"</c> alias (validated when applied to the run).
    /// </summary>
    public TigerThemeConfiguration AddTheme(ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        _customThemes.Add(theme);
        return this;
    }

    /// <summary>
    /// Disables a registered theme for this app. A disabled theme behaves like an unknown theme:
    /// <c>--theme</c> and <c>TIGERCLI_THEME</c> reject it and it is omitted from help. Disabling a name
    /// that is not registered is a harmless no-op.
    /// </summary>
    public TigerThemeConfiguration DisableTheme(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Theme name must be non-empty.", nameof(name));
        _disabledThemes.Add(name.Trim());
        return this;
    }

    /// <summary>Registers a raw colour alias (see <see cref="TigerColorAliasRegistry.Register"/>).</summary>
    public TigerThemeConfiguration RegisterColorAlias(string alias, CliColor color)
    {
        ColorAliases.Register(alias, color);
        return this;
    }

    /// <summary>Registers a custom semantic style (see <see cref="TigerCustomStyleRegistry.Register"/>).</summary>
    public TigerThemeConfiguration RegisterCustomStyle(
        string name,
        ThemeStyle baseStyle,
        CliCellStyle? darkStyle = null,
        CliCellStyle? lightStyle = null)
    {
        CustomStyles.Register(name, baseStyle, darkStyle, lightStyle);
        return this;
    }

    /// <summary>
    /// Registers an exact theme-name override for a custom style (see
    /// <see cref="TigerCustomStyleRegistry.RegisterForTheme"/>). The custom style must already be
    /// registered via <see cref="RegisterCustomStyle"/>.
    /// </summary>
    public TigerThemeConfiguration RegisterCustomStyleForTheme(string styleName, string themeName, CliCellStyle style)
    {
        CustomStyles.RegisterForTheme(styleName, themeName, style);
        return this;
    }
}
