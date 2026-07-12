using ItTiger.TigerCli.Commands;

namespace CommandParserTest;

public sealed class ThrowSettings : TigerCliSettings
{
}

public sealed class ThrowCommand
    : TigerCliAsyncCommandHandler<ThrowSettings, ParserTestExitCode>
{
    public override Task<ParserTestExitCode> ExecuteAsync(ThrowSettings settings)
    {
        throw new InvalidOperationException("Intentional exception from parser test app.");
    }
}


