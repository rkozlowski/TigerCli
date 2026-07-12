using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace RoiCities.Basic;

public sealed class ListSettings : TigerCliSettings
{
}

public sealed class ListCommand(CityStore store) : TigerCliAsyncCommandHandler<ListSettings>
{
    public override Task<int> ExecuteAsync(ListSettings settings)
    {
        TigerConsole.Render(CityList().Render(store.All));
        return Task.FromResult(0);
    }

    /// <summary>The list view: one column per field, name styled as the identity key.
    /// Public so tests and documentation capture render exactly what the command renders.</summary>
    public static CliList<City> CityList() => new CliList<City>()
        .AddTitle("Cities of Ireland")
        .AddKeyColumn("Name", city => city.Name)
        .AddColumn("County", city => city.County)
        .AddColumn("Province", city => city.Province)
        .AddColumn("Population", city => city.PopulationDisplay)
        .AddColumn("River", city => city.River);
}
