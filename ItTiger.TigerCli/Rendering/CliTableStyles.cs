using System.Linq;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// Convenience facade over the predefined table style presets. <see cref="CliTableStylePreset"/> is
/// the source of truth: aliases canonicalize to city presets, and each city preset maps to a
/// <see cref="CliTableStyleRecipe"/>. Every factory resolves a recipe through an <see cref="ITheme"/>
/// (the <see cref="TigerConsole.CurrentTheme"/> by default) and returns a fresh, fully-resolved
/// <see cref="CliTableStyle"/> that can be applied with <see cref="CliTable.ApplyStyle"/>.
///
/// <para>These are presets, not a separate styling system: a recipe defines structure and theme roles
/// only, and the active theme supplies the colours. The result is a plain <see cref="CliTableStyle"/>,
/// so callers keep full customization freedom afterwards. For a custom preset, derive a
/// <see cref="CliTableStyleRecipe"/> with a <c>with</c> expression and resolve it directly.</para>
///
/// <para>String-based overloads are a name boundary only (config/plugins/help): they parse to a
/// <see cref="CliTableStylePreset"/> and delegate to the enum-based methods.</para>
/// </summary>
public static class CliTableStyles
{
    // Presets at or above this value are city recipes; below it are boring aliases.
    private const int CityThreshold = 100;

    /// <summary>All presets, in menu order (aliases first, then cities).</summary>
    public static IReadOnlyList<CliTableStylePreset> Presets { get; } = Enum.GetValues<CliTableStylePreset>();

    /// <summary>The nine city presets (the actual recipes).</summary>
    public static IReadOnlyList<CliTableStylePreset> CityPresets { get; } =
        Presets.Where(p => (int)p >= CityThreshold).ToArray();

    /// <summary>The boring alias presets (each canonicalizes to a city preset).</summary>
    public static IReadOnlyList<CliTableStylePreset> AliasPresets { get; } =
        Presets.Where(p => (int)p < CityThreshold).ToArray();

    /// <summary>Names of all presets (derived from the enum; for docs/help/config).</summary>
    public static IReadOnlyList<string> PresetNames { get; } = Presets.Select(p => p.ToString()).ToArray();

    /// <summary>Names of the city presets (derived from the enum).</summary>
    public static IReadOnlyList<string> CityNames { get; } = CityPresets.Select(p => p.ToString()).ToArray();

    /// <summary>Names of the alias presets (derived from the enum).</summary>
    public static IReadOnlyList<string> AliasNames { get; } = AliasPresets.Select(p => p.ToString()).ToArray();

    /// <summary>Each alias preset mapped to the city preset it canonicalizes to (e.g. Default → Roma).</summary>
    public static IReadOnlyDictionary<CliTableStylePreset, CliTableStylePreset> AliasMap { get; } =
        AliasPresets.ToDictionary(p => p, Canonicalize);

    // ---- Enum-based core (the main path) ----

    /// <summary>
    /// Canonicalizes a preset: alias presets map to their target city preset; city presets are returned
    /// unchanged.
    /// </summary>
    public static CliTableStylePreset Canonicalize(CliTableStylePreset preset) => preset switch
    {
        CliTableStylePreset.Default => CliTableStylePreset.Roma,
        CliTableStylePreset.Light => CliTableStylePreset.Milano,
        CliTableStylePreset.Grid => CliTableStylePreset.Napoli,
        CliTableStylePreset.Alert => CliTableStylePreset.Palermo,
        CliTableStylePreset.Condensed => CliTableStylePreset.Parma,
        CliTableStylePreset.Details => CliTableStylePreset.Lucca,
        CliTableStylePreset.DetailsCondensed => CliTableStylePreset.Verona,
        CliTableStylePreset.List => CliTableStylePreset.Milano,
        _ => preset
    };

    /// <summary>
    /// Returns the <see cref="CliTableStyleRecipe"/> for a preset, canonicalizing aliases first.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined preset.</exception>
    public static CliTableStyleRecipe GetRecipe(CliTableStylePreset preset) => Canonicalize(preset) switch
    {
        CliTableStylePreset.Roma => CliTableStyleRecipe.Roma,
        CliTableStylePreset.Milano => CliTableStyleRecipe.Milano,
        CliTableStylePreset.Napoli => CliTableStyleRecipe.Napoli,
        CliTableStylePreset.Torino => CliTableStyleRecipe.Torino,
        CliTableStylePreset.Genova => CliTableStyleRecipe.Genova,
        CliTableStylePreset.Bologna => CliTableStyleRecipe.Bologna,
        CliTableStylePreset.Palermo => CliTableStyleRecipe.Palermo,
        CliTableStylePreset.Parma => CliTableStyleRecipe.Parma,
        CliTableStylePreset.Verona => CliTableStyleRecipe.Verona,
        CliTableStylePreset.Lucca => CliTableStyleRecipe.Lucca,
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown table style preset.")
    };

    /// <summary>
    /// Builds a table style for a preset, resolving its recipe through <paramref name="theme"/>
    /// (the <see cref="TigerConsole.CurrentTheme"/> when <c>null</c>). The requested
    /// <paramref name="orientation"/> is honoured for universal presets and ignored for
    /// orientation-locked ones (Parma, Verona).
    /// </summary>
    public static CliTableStyle Create(CliTableStylePreset preset, ITheme? theme = null,
        CliTableOrientation orientation = CliTableOrientation.Vertical)
        => GetRecipe(preset).Resolve(theme, orientation);

    /// <summary>Returns which orientations a preset supports (aliases canonicalize first).</summary>
    public static CliTableStyleOrientationSupport OrientationSupport(CliTableStylePreset preset)
        => GetRecipe(preset).OrientationSupport;

    // ---- String boundary (config/plugins/help; parse then delegate to the enum path) ----

    /// <summary>
    /// Builds a table style by preset name. Accepts any <see cref="CliTableStylePreset"/> name
    /// (city or alias), case-insensitively.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not a known preset (e.g. a removed tasting variant).</exception>
    public static CliTableStyle Create(string presetName, ITheme? theme = null,
        CliTableOrientation orientation = CliTableOrientation.Vertical)
        => Create(Parse(presetName), theme, orientation);

    /// <summary>Returns the recipe for a preset name (city or alias), case-insensitively.</summary>
    /// <exception cref="ArgumentException">The name is not a known preset.</exception>
    public static CliTableStyleRecipe GetRecipe(string presetName)
        => GetRecipe(Parse(presetName));

    /// <summary>Returns which orientations a preset name supports (city or alias), case-insensitively.</summary>
    /// <exception cref="ArgumentException">The name is not a known preset.</exception>
    public static CliTableStyleOrientationSupport OrientationSupport(string presetName)
        => OrientationSupport(Parse(presetName));

    /// <summary>
    /// Parses a preset name to a <see cref="CliTableStylePreset"/>, case-insensitively. Numeric input
    /// and unknown names (including removed tasting variants such as Venezia/Firenze/Pisa) are rejected.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not a known preset.</exception>
    public static CliTableStylePreset Parse(string presetName)
    {
        ArgumentNullException.ThrowIfNull(presetName);
        var trimmed = presetName.Trim();

        // Names only: reject numeric / flag-combo input so only real preset names resolve.
        var firstIsLetter = trimmed.Length > 0 && char.IsLetter(trimmed[0]);
        if (firstIsLetter
            && Enum.TryParse<CliTableStylePreset>(trimmed, ignoreCase: true, out var preset)
            && Enum.IsDefined(preset))
        {
            return preset;
        }

        throw new ArgumentException(
            $"'{presetName}' is not a known table style preset. Use one of CliTableStyles.PresetNames.",
            nameof(presetName));
    }
}
