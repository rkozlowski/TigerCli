using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

namespace CommandParserTest;

public sealed class ProjectsSpAddSettings : TigerCliSettings
{
    [TigerCliArgument(0, Name = "connection",
        Description = "Connection name.",
        DescriptionResourceKey = "Arg_ProjectsSpAdd_Connection_Description")]
    public string ConnectionName { get; set; } = string.Empty;

    [TigerCliArgument(1, Name = "project",
        Description = "Project name.",
        DescriptionResourceKey = "Arg_ProjectsSpAdd_Project_Description")]
    public string ProjectName { get; set; } = string.Empty;

    [TigerCliOption("--schema",
        Description = "Schema name.",
        DescriptionResourceKey = "Opt_ProjectsSpAdd_Schema_Description")]
    public string Schema { get; set; } = "dbo";
}

public sealed class ProjectsSpAddCommand
    : TigerCliAsyncCommandHandler<ProjectsSpAddSettings, ParserTestExitCode>
{
    public override Task<ParserTestExitCode> ExecuteAsync(ProjectsSpAddSettings settings)
    {
        TigerConsole.MarkupLine(settings.E(
            "projects sp-add connection={0} project={1} schema={2}",
            settings.ConnectionName,
            settings.ProjectName,
            settings.Schema));
        return Task.FromResult(ParserTestExitCode.Ok);
    }
}
