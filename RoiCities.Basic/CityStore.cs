using System.Globalization;

namespace RoiCities.Basic;

/// <summary>One city of the Republic of Ireland. Population figures are the 2022 census city counts.</summary>
public sealed record City(string Name, string County, string Province, int Population, string River)
{
    /// <summary>Culture-independent display form, so rendered output never depends on the host culture.</summary>
    public string PopulationDisplay => Population.ToString("N0", CultureInfo.InvariantCulture);
}

/// <summary>
/// The in-memory data source behind the commands. A real tool would query a file, database, or
/// service here; the commands only see <see cref="All"/> and <see cref="Find"/>.
/// </summary>
public sealed class CityStore
{
    private static readonly City[] Cities =
    [
        new("Dublin", "Dublin", "Leinster", 592_713, "Liffey"),
        new("Cork", "Cork", "Munster", 222_526, "Lee"),
        new("Limerick", "Limerick", "Munster", 102_287, "Shannon"),
        new("Waterford", "Waterford", "Munster", 60_079, "Suir"),
        new("Galway", "Galway", "Connacht", 85_910, "Corrib"),
        new("Kilkenny", "Kilkenny", "Leinster", 27_184, "Nore"),
    ];

    public IReadOnlyList<City> All => Cities;

    public City? Find(string name) =>
        Cities.FirstOrDefault(city => string.Equals(city.Name, name, StringComparison.OrdinalIgnoreCase));
}
