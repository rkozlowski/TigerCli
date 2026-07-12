using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

namespace CommandParserTest;

public sealed class RawSettings : TigerCliSettings
{
    [TigerCliOption("--code", Required = true,
        Description = "Raw integer code to return.",
        DescriptionResourceKey = "Opt_Raw_Code_Description")]
    public int Code { get; set; }
}

public sealed class RawCommand
    : TigerCliAsyncCommandHandler<RawSettings>
{
    public override Task<int> ExecuteAsync(RawSettings settings)
    {
        TigerConsole.MarkupLine(settings.E("Returning raw code {0}", settings.Code));
        return Task.FromResult(settings.Code);
    }
}
