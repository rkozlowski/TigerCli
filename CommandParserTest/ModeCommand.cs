using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

namespace CommandParserTest;

public sealed class ModeSettings : TigerCliSettings
{
}

public sealed class ModeCommand : TigerCliAsyncCommandHandler<ModeSettings, ParserTestExitCode>
{
    public override Task<ParserTestExitCode> ExecuteAsync(ModeSettings settings)
    {
        TigerConsole.MarkupLine(settings.E("interaction-mode={0}", settings.InteractionMode));
        return Task.FromResult(ParserTestExitCode.Ok);
    }
}
