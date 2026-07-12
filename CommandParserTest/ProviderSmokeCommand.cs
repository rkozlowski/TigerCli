using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

namespace CommandParserTest;

public sealed class ProviderSmokeSettings : TigerCliSettings
{
    [TigerCliArgument(0, Name = "connection",
        Provider = "connections",
        Description = "Connection to use.",
        DescriptionResourceKey = "Arg_ProviderSmoke_Connection_Description")]
    public string ConnectionName { get; set; } = string.Empty;

    [TigerCliArgument(1, Name = "project",
        Provider = "projects",
        Description = "Project to use.",
        DescriptionResourceKey = "Arg_ProviderSmoke_Project_Description")]
    public string ProjectName { get; set; } = string.Empty;
}

public sealed class ProviderSmokeCommand
    : TigerCliAsyncCommandHandler<ProviderSmokeSettings, ParserTestExitCode>
{
    public override Task<ParserTestExitCode> ExecuteAsync(ProviderSmokeSettings settings)
    {
        TigerConsole.MarkupLine(settings.E(
            "connection={0}; project={1}",
            settings.ConnectionName,
            settings.ProjectName));
        return Task.FromResult(ParserTestExitCode.Ok);
    }
}
