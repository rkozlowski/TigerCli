using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace RoiCities.Extended;

/// <summary>
/// The extended ROI Cities app: the same list/show commands and city store as RoiCities.Basic,
/// with TigerCli's richer UX turned on — a provider-backed selector for show, an opt-in command
/// menu, typed exit-code mapping, and version metadata.
/// </summary>
public static class RoiCitiesApp
{
    public static TigerCliApp Create()
    {
        var store = new CityStore();

        // App identity, display name, version, and description come from the project file
        // (RoiCities.Extended.csproj) and are imported here — the normal executable-app pattern.
        return TigerCliApp.CreateBuilder()
            .UseAssemblyMetadata(typeof(RoiCitiesApp).Assembly)
            .UseExitCodes(RoiCitiesExitCode.Ok, RoiCitiesExitCode.InternalError)
                .ExitKind(TigerCliExitKind.InvalidArguments, RoiCitiesExitCode.InvalidArguments)
                .ExitKind(TigerCliExitKind.MissingRequiredArgument, RoiCitiesExitCode.MissingRequiredArgument)
                .ExitKind(TigerCliExitKind.ValidationError, RoiCitiesExitCode.ValidationError)
                .ExitKind(TigerCliExitKind.InteractiveNotAllowed, RoiCitiesExitCode.InteractiveNotAllowed)
                .ExitKind(TigerCliExitKind.Cancelled, RoiCitiesExitCode.Cancelled)
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand("list", () => new ListCommand(store), "Lists the cities.")
            .AddCommand("show", () => new ShowCommand(store), "Shows details for one city.")
            .ConfigureProviders(providers => providers.Add<string>("cities", _ =>
                store.All
                    .Select(city => new OptionItem<string>(city.Name, $"{city.Name} ({city.Province})"))
                    .ToArray()))
            .Build();
    }
}
