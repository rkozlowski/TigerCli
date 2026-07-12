using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class DefaultOutputPresetTests
{
    private sealed class EmptySettings : TigerCliSettings { }

    private sealed record Device(string Id, string Name);

    private static readonly Device[] Devices =
    [
        new("d-1", "Front door"),
        new("d-2", "Garage"),
    ];

    private sealed class DefaultDetailsCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings)
        {
            TigerConsole.Render(CreateDetails());
            return Task.FromResult(0);
        }
    }

    private sealed class ExplicitDetailsCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings)
        {
            TigerConsole.Render(CreateDetails(CliTableStylePreset.Details));
            return Task.FromResult(0);
        }
    }

    private sealed class DefaultListCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings)
        {
            TigerConsole.Render(CreateList().Render(Devices));
            return Task.FromResult(0);
        }
    }

    private sealed class DefaultTableCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings)
        {
            TigerConsole.Render(CreateTable());
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task AppDefaultDetailsPreset_AppliesWhenDetailsHasNoExplicitPreset()
    {
        var app = CreateApp<DefaultDetailsCommand>(builder =>
            builder.SetDefaultOutputPresets(
                details: CliTableStylePreset.Milano,
                list: CliTableStylePreset.Default));

        var result = await RunAsync(app);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(RenderToString(CreateDetails(CliTableStylePreset.Milano)), result.StdOut);
    }

    [Fact]
    public async Task AppDefaultListPreset_AppliesWhenListHasNoExplicitPreset()
    {
        var app = CreateApp<DefaultListCommand>(builder =>
            builder.SetDefaultOutputPresets(
                details: CliTableStylePreset.Details,
                list: CliTableStylePreset.Parma));

        var result = await RunAsync(app);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(RenderToString(CreateList(CliTableStylePreset.Parma).Render(Devices)), result.StdOut);
    }

    [Fact]
    public async Task ExplicitDetailsPreset_WinsOverAppDefault()
    {
        var app = CreateApp<ExplicitDetailsCommand>(builder =>
            builder.SetDefaultOutputPresets(
                details: CliTableStylePreset.Milano,
                list: CliTableStylePreset.Parma));

        var result = await RunAsync(app);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(RenderToString(CreateDetails(CliTableStylePreset.Details)), result.StdOut);
    }

    [Fact]
    public async Task NoAppDefault_PreservesCurrentStructuredOutputDefaults()
    {
        var detailsApp = CreateApp<DefaultDetailsCommand>();
        var listApp = CreateApp<DefaultListCommand>();

        var details = await RunAsync(detailsApp);
        var list = await RunAsync(listApp);

        Assert.Equal(0, details.ExitCode);
        Assert.Equal(0, list.ExitCode);
        Assert.Equal(RenderToString(CreateDetails()), details.StdOut);
        Assert.Equal(RenderToString(CreateList().Render(Devices)), list.StdOut);
    }

    [Fact]
    public async Task AppDefaults_DoNotLeakBetweenApps()
    {
        var configured = CreateApp<DefaultDetailsCommand>(builder =>
            builder.SetDefaultOutputPresets(
                details: CliTableStylePreset.Milano,
                list: CliTableStylePreset.Parma));
        var unconfigured = CreateApp<DefaultDetailsCommand>();

        var configuredResult = await RunAsync(configured);
        var unconfiguredResult = await RunAsync(unconfigured);

        Assert.Equal(RenderToString(CreateDetails(CliTableStylePreset.Milano)), configuredResult.StdOut);
        Assert.Equal(RenderToString(CreateDetails()), unconfiguredResult.StdOut);
    }

    [Fact]
    public async Task AppDefaultTablePreset_AppliesToDirectTableWithoutExplicitStyle()
    {
        var app = CreateApp<DefaultTableCommand>(builder =>
            builder.SetDefaultOutputPresets(
                details: CliTableStylePreset.Details,
                list: CliTableStylePreset.Default,
                table: CliTableStylePreset.Milano));

        var result = await RunAsync(app);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(RenderToString(CreateTable().ApplyPreset(CliTableStylePreset.Milano)), result.StdOut);
    }

    private static TigerCliApp CreateApp<TCommand>(Action<TigerCliAppBuilder>? configure = null)
        where TCommand : class, new()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("output-presets")
            .SetInteractionMode(TigerCliInteractionMode.NonInteractive)
            .SetDefaultCommand<TCommand>();

        configure?.Invoke(builder);
        return builder.Build();
    }

    private static Task<TigerCliAppRunResult> RunAsync(TigerCliApp app) =>
        TigerCliAppTestHost
            .For(app)
            .RunAsync(TestContext.Current.CancellationToken);

    private static CliDetails CreateDetails(CliTableStylePreset? preset = null)
    {
        var details = new CliDetails()
            .Add("Name:", "prod")
            .Add("Region:", "eu-west-1");

        if (preset is not null)
            details.ApplyPreset(preset.Value);

        return details;
    }

    private static CliList<Device> CreateList(CliTableStylePreset? preset = null)
    {
        var list = new CliList<Device>()
            .AddColumn("Id", d => d.Id)
            .AddColumn("Name", d => d.Name);

        if (preset is not null)
            list.ApplyPreset(preset.Value);

        return list;
    }

    private static CliTable CreateTable() =>
        new CliTable()
            .AddHeader("Id", "Name")
            .AddRecord("d-1", "Front door")
            .AddRecord("d-2", "Garage");

    private static string RenderToString(CliRenderableComponent component) =>
        string.Concat(TigerConsole.RenderToLines(component).Select(line => line + Environment.NewLine));
}
