using ItTiger.TigerCli.Testing;
using RoiCities.Extended;

namespace RoiCities.Tests;

/// <summary>
/// App-boundary tests for the extended ROI Cities app (docs/getting-started.md): the
/// provider-backed selector, the command menu, and the typed exit-code mapping.
/// </summary>
public sealed class ExtendedAppTests
{
    // Store order: Dublin, Cork, Limerick, Waterford, Galway, Kilkenny.
    private const int GalwayChoiceIndex = 4;

    [Fact]
    public async Task Show_MissingCity_PromptsWithProviderSelect()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("show")
            .WithSelectIndex(GalwayChoiceIndex)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)RoiCitiesExitCode.Ok, result.ExitCode);
        Assert.Contains("Galway", result.StdOut);
        Assert.Contains("Corrib", result.StdOut);
        Assert.Contains("https://www.galwaycity.ie", result.StdOut);
        Assert.Contains("/Connacht/Galway", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task CommandMenu_FirstEntry_RunsList()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithSelectIndex(0)
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)RoiCitiesExitCode.Ok, result.ExitCode);
        Assert.Contains("Cities of Ireland", result.StdOut);
    }

    [Fact]
    public async Task CommandMenu_ShowFlowsIntoProviderPrompt()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithSelectIndex(1)                  // menu: pick "show"
            .WithSelectIndex(GalwayChoiceIndex)  // provider select: pick Galway
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)RoiCitiesExitCode.Ok, result.ExitCode);
        Assert.Contains("Corrib", result.StdOut);
    }

    [Fact]
    public async Task CommandMenu_NonInteractive_FailsWithMappedExitCode()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("--non-interactive")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)RoiCitiesExitCode.InteractiveNotAllowed, result.ExitCode);
        Assert.Empty(result.StdOut);
    }

    [Fact]
    public async Task Show_MissingCity_NonInteractive_FailsWithMappedExitCode()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("show", "--non-interactive")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)RoiCitiesExitCode.MissingRequiredArgument, result.ExitCode);
        Assert.Contains("city", result.StdErr);
    }

    [Fact]
    public async Task Show_UnknownCity_FailsFrameworkProviderValidation()
    {
        // The provider-backed <city> selector makes the provider's choices authoritative for
        // supplied values, so an unknown city fails framework validation before the handler runs.
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("show", "Atlantis")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)RoiCitiesExitCode.ValidationError, result.ExitCode);
        Assert.Contains("Atlantis", result.StdErr);
        Assert.Contains("not an available choice", result.StdErr);
        Assert.Empty(result.StdOut);
    }

    [Fact]
    public async Task Show_SuppliedCity_MatchesCaseInsensitively_AndBindsCanonicalName()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("show", "galway")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)RoiCitiesExitCode.Ok, result.ExitCode);
        Assert.Contains("Galway", result.StdOut); // canonical provider key, not "galway"
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Version_WritesDisplayNameAndVersion()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("--version")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)RoiCitiesExitCode.Ok, result.ExitCode);
        Assert.Contains("ROI Cities version 0.8.0", result.StdOut);
    }

    [Fact]
    public async Task VersionFull_WritesSharedInformationalVersion()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("--version-full")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)RoiCitiesExitCode.Ok, result.ExitCode);
        Assert.Contains("ROI Cities product version 0.8.0+", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task HelpErrors_DocumentsTypedExitCodes()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("--help-errors")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)RoiCitiesExitCode.Ok, result.ExitCode);
        Assert.Contains("City not found", result.StdOut);
        Assert.Contains("Interactive input not allowed", result.StdOut);
        Assert.Empty(result.StdErr);
    }
}
