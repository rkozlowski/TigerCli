using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

namespace CommandParserTest;

public sealed class DefaultSettings : TigerCliSettings
{
    [TigerCliOption("-n|--name",
        Description = "Name to greet.",
        DescriptionResourceKey = "Opt_Default_Name_Description")]
    public string? Name { get; set; }
}
public sealed class DefaultCommand
    : TigerCliAsyncCommandHandler<DefaultSettings, ParserTestExitCode>
{
    public override Task<ParserTestExitCode> ExecuteAsync(DefaultSettings settings)
    {
        var name = string.IsNullOrEmpty(settings.Name)
            ? settings.T("World")
            : settings.Name;
        TigerConsole.MarkupLine(settings.E("Hello, [White]{0}[/]!", name));
        return Task.FromResult(ParserTestExitCode.Ok);
    }
}
