namespace ItTiger.TigerCli.Enums;

/// <summary>
/// The single source of truth for built-in table style presets (see <see cref="Rendering.CliTableStyles"/>).
///
/// <para>The low range (0–99) holds boring intent/alias names; the 100+ range holds the exact "city"
/// recipes they map to. The grouping keeps the alias/city distinction visible in code and leaves room
/// to extend either range. The menu order is intentional — do not reorder.</para>
/// </summary>
public enum CliTableStylePreset
{
    // Boring aliases / intent names (canonicalize to a city preset).
    /// <summary>Default table preset; canonicalizes to <see cref="Roma"/>.</summary>
    Default = 0,

    /// <summary>Light table preset; canonicalizes to <see cref="Milano"/>.</summary>
    Light,

    /// <summary>Grid table preset; canonicalizes to <see cref="Napoli"/>.</summary>
    Grid,

    /// <summary>Alert table preset; canonicalizes to <see cref="Palermo"/>.</summary>
    Alert,

    /// <summary>Condensed vertical table preset; canonicalizes to <see cref="Parma"/>.</summary>
    Condensed,

    /// <summary>Details table preset; canonicalizes to <see cref="Lucca"/>.</summary>
    Details,

    /// <summary>Condensed details table preset; canonicalizes to <see cref="Verona"/>.</summary>
    DetailsCondensed,

    /// <summary>List table preset; canonicalizes to <see cref="Milano"/>.</summary>
    List,

    // City recipes (the actual presets).
    /// <summary>Double outer frame with header rule and column separators on a panel surface.</summary>
    Roma = 100,

    /// <summary>Single-line boxed grid on a panel surface.</summary>
    Milano,

    /// <summary>Full single-line grid with record separators on the default surface.</summary>
    Napoli,

    /// <summary>Frameless outer table with header rule and column separators.</summary>
    Torino,

    /// <summary>Tight single-line boxed grid with no padding.</summary>
    Genova,

    /// <summary>Roma-style framing on the default surface.</summary>
    Bologna,

    /// <summary>Attention-style table using the alert surface.</summary>
    Palermo,

    /// <summary>Compact vertical list style; vertical-only.</summary>
    Parma,

    /// <summary>Condensed detail-view style; horizontal-only.</summary>
    Verona,

    /// <summary>Boxed detail-view style; horizontal-only.</summary>
    Lucca
}
