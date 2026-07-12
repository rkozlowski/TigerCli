using ItTiger.TigerCli.Commands;

namespace RoiCities.Basic;

/// <summary>
/// The app factory. Program.cs and the app-boundary tests build the app through this one method,
/// so command registration, prompting policy, and metadata never drift between production and test
/// runs (see docs/guides/app-testing.md).
/// </summary>
public static class RoiCitiesApp
{
    public static TigerCliApp Create()
    {
        var store = new CityStore();

        return TigerCliApp.CreateBuilder()
            .SetApplicationName("roi-cities")
            .AddDescription("Cities of the Republic of Ireland.")
            .AddCommand("list", () => new ListCommand(store), "Lists the cities.")
            .AddCommand("show", () => new ShowCommand(store), "Shows details for one city.")
            .Build();
    }
}
