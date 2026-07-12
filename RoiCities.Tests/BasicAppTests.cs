using ItTiger.TigerCli.Testing;
using RoiCities.Basic;

namespace RoiCities.Tests;

/// <summary>
/// App-boundary tests for the basic ROI Cities app (docs/getting-started.md): the same
/// <see cref="RoiCitiesApp.Create"/> factory that Program.cs runs, driven through
/// <see cref="TigerCliAppTestHost"/>.
/// </summary>
public sealed class BasicAppTests
{
    [Fact]
    public async Task List_WritesAllCities()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("list")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Cities of Ireland", result.StdOut);
        Assert.Contains("Dublin", result.StdOut);
        Assert.Contains("Galway", result.StdOut);
        Assert.Contains("Kilkenny", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task ShowGalway_WritesDetails()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("show", "Galway")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Galway", result.StdOut);
        Assert.Contains("Connacht", result.StdOut);
        Assert.Contains("Corrib", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Show_CityNameIsCaseInsensitive()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("show", "galway")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Connacht", result.StdOut);
    }

    [Fact]
    public async Task Show_MissingCity_PromptsWithTextInput()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("show")
            .WithTextInput("Galway")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Corrib", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Show_MissingCity_NonInteractive_FailsCleanly()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("show", "--non-interactive")
            .WithTextInput("unused")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("city", result.StdErr);
        Assert.DoesNotContain("Corrib", result.StdOut);
    }

    [Fact]
    public async Task Show_UnknownCity_FailsWithAppError()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("show", "Atlantis")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown city", result.StdErr);
        Assert.Empty(result.StdOut);
    }

    [Fact]
    public async Task Help_ListsCommands()
    {
        var result = await TigerCliAppTestHost
            .For(RoiCitiesApp.Create())
            .WithArgs("--help")
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("roi-cities", result.StdOut);
        Assert.Contains("list", result.StdOut);
        Assert.Contains("show", result.StdOut);
        Assert.Empty(result.StdErr);
    }
}
