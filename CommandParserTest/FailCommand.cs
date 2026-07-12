using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

namespace CommandParserTest;

public sealed class FailSettings : TigerCliSettings
{
}

public sealed class FailCommand
    : TigerCliAsyncCommandHandler<FailSettings, ParserTestExitCode>
{
    public override Task<ParserTestExitCode> ExecuteAsync(FailSettings settings)
    {
        TigerConsole.MarkupErrorLine(settings.T("Intentional typed failure."));
        return Task.FromResult(ParserTestExitCode.IntentionalFailure);
    }
}
