using System.Globalization;

namespace RoiCities.Extended;

/// <summary>One city of the Republic of Ireland. Population figures are the 2022 census city counts.</summary>
public sealed record City(string Name, string County, string Province, int Population, string River)
{
    internal string Website { get; init; } = string.Empty;

    /// <summary>Culture-independent display form, so rendered output never depends on the host culture.</summary>
    public string PopulationDisplay => Population.ToString("N0", CultureInfo.InvariantCulture);
}

/// <summary>
/// The in-memory data source behind the commands and the "cities" provider. A real tool would
/// query a file, database, or service here.
/// </summary>
public sealed class CityStore
{
    private static readonly City[] Cities =
    [
        new("Dublin", "Dublin", "Leinster", 592_713, "Liffey")
            { Website = "https://www.dublincity.ie" },
        new("Cork", "Cork", "Munster", 222_526, "Lee")
            { Website = "https://www.corkcity.ie" },
        new("Limerick", "Limerick", "Munster", 102_287, "Shannon")
            { Website = "https://www.limerick.ie" },
        new("Waterford", "Waterford", "Munster", 60_079, "Suir")
            { Website = "https://www.waterfordcouncil.ie" },
        new("Galway", "Galway", "Connacht", 85_910, "Corrib")
            { Website = "https://www.galwaycity.ie" },
        new("Kilkenny", "Kilkenny", "Leinster", 27_184, "Nore")
            { Website = "https://www.kilkennycoco.ie" },
    ];

    public IReadOnlyList<City> All => Cities;

    public City? Find(string name) =>
        Cities.FirstOrDefault(city => string.Equals(city.Name, name, StringComparison.OrdinalIgnoreCase));
}
