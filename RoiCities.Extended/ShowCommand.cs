using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace RoiCities.Extended;

public sealed class ShowSettings : TigerCliSettings
{
    // Provider-backed selector: a missing value is prompted as a select over the store's cities
    // instead of a free-text input.
    [TigerCliArgument(0, Name = "city", Provider = "cities", Description = "City name, e.g. Galway.")]
    public string CityName { get; set; } = string.Empty;
}

public sealed class ShowCommand(CityStore store)
    : TigerCliAsyncCommandHandler<ShowSettings, RoiCitiesExitCode>
{
    public override Task<RoiCitiesExitCode> ExecuteAsync(ShowSettings settings)
    {
        var city = store.Find(settings.CityName);
        if (city is null)
        {
            TigerConsole.MarkupErrorLine(settings.E("[Error]Unknown city:[/] {0}", settings.CityName));
            return Task.FromResult(RoiCitiesExitCode.CityNotFound);
        }

        TigerConsole.Render(CityDetails(city));
        return Task.FromResult(RoiCitiesExitCode.Ok);
    }

    /// <summary>The details view: one record as labelled fields.
    /// Public so tests and documentation capture render exactly what the command renders.</summary>
    public static CliDetails CityDetails(City city) => new CliDetails()
        .AddTitle(city.Name)
        .AddKey("Name:", city.Name)
        .Add("County:", city.County)
        .Add("Province:", city.Province)
        .Add("Population:", city.PopulationDisplay)
        .Add("River:", city.River)
        .AddLink("Website:", city.Website)
        .AddPath("Path:", $"/{city.Province}/{city.County}");
}
