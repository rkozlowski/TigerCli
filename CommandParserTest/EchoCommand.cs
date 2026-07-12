using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

namespace CommandParserTest;

public sealed class EchoSettings : TigerCliSettings
{
    [TigerCliOption("-m|--message",
        Required = true,
        Description = "Message to echo.",
        DescriptionResourceKey = "Opt_Echo_Message_Description")]
    public string Message { get; set; } = string.Empty;
}

public sealed class EchoCommand
    : TigerCliAsyncCommandHandler<EchoSettings, ParserTestExitCode>
{
    public override Task<ParserTestExitCode> ExecuteAsync(EchoSettings settings)
    {
        TigerConsole.MarkupLine(settings.Message);
        return Task.FromResult(ParserTestExitCode.Ok);
    }
}




