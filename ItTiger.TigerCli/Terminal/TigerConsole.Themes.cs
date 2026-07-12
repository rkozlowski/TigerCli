using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Terminal;

public static partial class TigerConsole
{
    /// <summary>One registry entry: the theme instance and whether it is framework-owned.</summary>
    private sealed record TigerConsoleThemeRegistration(ITheme Theme, bool IsFrameworkTheme);

    // The only theme-name string the framework hardcodes: a special alias for CurrentTheme.
    // Framework theme names themselves come from the registered theme instances' Name.
    private const string DefaultThemeName = "default";

    private static readonly StringComparer ThemeNameComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly object ThemeSync = new();

    // Stable framework theme instances (immutable from the registry's perspective).
    private static readonly DarkTheme FrameworkDarkTheme = new();
    private static readonly LightTheme FrameworkLightTheme = new();
    private static readonly TigerBlueTheme FrameworkTigerBlueTheme = new();

    // Framework and custom themes share one dictionary, keyed by theme.Name (ordinal-ignore-case).
    private static readonly Dictionary<string, TigerConsoleThemeRegistration> Themes =
        BuildFrameworkThemeRegistrations();

    private static ITheme _currentTheme = FrameworkDarkTheme;

    // Active app-scoped appearance registries. Defaults are empty (no built-in colour aliases, no
    // custom styles). The app builder's ConfigureThemes block populates an app's instances and the app
    // makes them active for the run; tests can save/restore these like CurrentTheme for isolation.
    private static TigerColorAliasRegistry _colorAliases = new();
    private static TigerCustomStyleRegistry _customStyles = new();

    /// <summary>
    /// The active raw colour alias registry consulted by markup in raw colour positions. Defaults to an
    /// empty registry — TigerCli ships no built-in colour aliases. Cannot be set to <c>null</c>.
    /// </summary>
    public static TigerColorAliasRegistry ColorAliases
    {
        get { lock (ThemeSync) return _colorAliases; }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (ThemeSync) _colorAliases = value;
        }
    }

    /// <summary>
    /// The active custom semantic style registry consulted by markup for single-token tags that are not
    /// framework semantic tokens. Defaults to an empty registry. Cannot be set to <c>null</c>.
    /// </summary>
    public static TigerCustomStyleRegistry CustomStyles
    {
        get { lock (ThemeSync) return _customStyles; }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (ThemeSync) _customStyles = value;
        }
    }

    /// <summary>
    /// Builds a markup style resolver for the active theme and custom styles. Markup call sites use this
    /// so framework semantic tokens and app custom styles resolve consistently.
    /// </summary>
    public static ThemeMarkupStyleResolver CreateMarkupStyleResolver()
        => new(CurrentTheme, CustomStyles);

    /// <summary>
    /// Builds a markup style resolver for an explicit <paramref name="theme"/> while still honouring the
    /// active custom style registry.
    /// </summary>
    public static ThemeMarkupStyleResolver CreateMarkupStyleResolver(ITheme theme)
        => new(theme, CustomStyles);

    private static Dictionary<string, TigerConsoleThemeRegistration> BuildFrameworkThemeRegistrations()
    {
        var map = new Dictionary<string, TigerConsoleThemeRegistration>(ThemeNameComparer);
        foreach (var theme in new ITheme[] { FrameworkDarkTheme, FrameworkLightTheme, FrameworkTigerBlueTheme })
            map[theme.Name] = new TigerConsoleThemeRegistration(theme, IsFrameworkTheme: true);
        return map;
    }

    /// <summary>
    /// The theme used for themed output when no explicit theme is passed. Defaults to the
    /// registered <see cref="DarkTheme"/>; cannot be set to <c>null</c>. May be set to any
    /// <see cref="ITheme"/>, including a custom theme that is not registered (lookup by name still
    /// only finds registered themes, but <c>GetTheme("default")</c> always returns this value).
    /// </summary>
    public static ITheme CurrentTheme
    {
        get { lock (ThemeSync) return _currentTheme; }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (ThemeSync) _currentTheme = value;
        }
    }

    /// <summary>
    /// Registers a custom theme by its <see cref="ITheme.Name"/>, replacing any existing custom
    /// theme with the same name (case-insensitive). Framework theme names and the reserved
    /// <c>"default"</c> alias cannot be registered.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The name is empty/whitespace, is <c>"default"</c>, or is framework-owned.</exception>
    public static void AddOrUpdateCustomTheme(ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var name = theme.Name;
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Theme name must be non-empty.", nameof(theme));

        if (ThemeNameComparer.Equals(name, DefaultThemeName))
            throw new ArgumentException(
                $"'{DefaultThemeName}' is a reserved alias and cannot be registered as a theme.", nameof(theme));

        lock (ThemeSync)
        {
            if (Themes.TryGetValue(name, out var existing) && existing.IsFrameworkTheme)
                throw new ArgumentException(
                    $"'{name}' is a framework theme and cannot be overridden by a custom theme.", nameof(theme));

            Themes[name] = new TigerConsoleThemeRegistration(theme, IsFrameworkTheme: false);
        }
    }

    /// <summary>
    /// Resolves a theme by name. <c>"default"</c> returns <see cref="CurrentTheme"/>; every other
    /// name is looked up in the shared framework+custom registry.
    /// </summary>
    /// <exception cref="ArgumentException">No theme is registered with the given name.</exception>
    public static ITheme GetTheme(string name)
    {
        if (TryGetTheme(name, out var theme))
            return theme;

        throw new ArgumentException($"No theme is registered with the name '{name}'.", nameof(name));
    }

    /// <summary>
    /// Attempts to resolve a theme by name. Returns <c>false</c> (without throwing) for an unknown,
    /// <c>null</c>, empty, or whitespace name. <c>"default"</c> resolves to <see cref="CurrentTheme"/>.
    /// </summary>
    public static bool TryGetTheme(string? name, [NotNullWhen(true)] out ITheme? theme)
    {
        theme = null;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (ThemeNameComparer.Equals(name, DefaultThemeName))
        {
            theme = CurrentTheme;
            return true;
        }

        lock (ThemeSync)
        {
            if (Themes.TryGetValue(name, out var registration))
            {
                theme = registration.Theme;
                return true;
            }
        }

        return false;
    }

    // Note: a custom theme is normally defined by subclassing a framework theme (e.g.
    // `class MyTheme : DarkTheme`) and overriding only the roles that differ — unoverridden roles
    // fall through to the base theme via ThemeBase's fallback chain. Deriving a registered theme by
    // string name at runtime is intentionally not provided; subclassing is the supported path.

    /// <summary>
    /// Returns the names of all selectable themes — framework and registered custom themes. Does not
    /// include the <c>"default"</c> alias (which is not a stored theme). Snapshot; no internal state leaks.
    /// </summary>
    public static IReadOnlyCollection<string> GetThemeNames()
    {
        lock (ThemeSync)
            return Themes.Keys.ToArray();
    }
}
