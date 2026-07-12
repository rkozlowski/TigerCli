using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace RoiCities.Basic;

public sealed class ShowSettings : TigerCliSettings
{
    // The selector: identifies which city the command works with. Missing selectors are
    // prompted in semi-interactive mode and fail cleanly under --non-interactive.
    [TigerCliArgument(0, Name = "city", Description = "City name, e.g. Galway.")]
    public string CityName { get; set; } = string.Empty;
}

public sealed class ShowCommand(CityStore store) : TigerCliAsyncCommandHandler<ShowSettings>
{
    public override Task<int> ExecuteAsync(ShowSettings settings)
    {
        var city = store.Find(settings.CityName);
        if (city is null)
        {
            TigerConsole.MarkupErrorLine(settings.E("[Error]Unknown city:[/] {0}", settings.CityName));
            return Task.FromResult(1);
        }

        TigerConsole.Render(CityDetails(city));
        return Task.FromResult(0);
    }

    /// <summary>The details view: one record as labelled fields.
    /// Public so tests and documentation capture render exactly what the command renders.</summary>
    public static CliDetails CityDetails(City city) => new CliDetails()
        .AddTitle(city.Name)
        .AddKey("Name:", city.Name)
        .Add("County:", city.County)
        .Add("Province:", city.Province)
        .Add("Population:", city.PopulationDisplay)
        .Add("River:", city.River);
}
