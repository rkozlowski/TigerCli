using ItTiger.Core;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

namespace CommandParserTest;

[TigerText("Prompt smoke modes")]
public enum PromptSmokeMode
{
    [TigerText("Fast")]
    Fast,

    [TigerText("Normal")]
    Normal,

    [TigerText("Careful")]
    Careful
}

[Flags]
[TigerText("Prompt smoke features")]
public enum PromptSmokeFeatures
{
    [TigerText("None")]
    None = 0,

    [TigerText("Logging")]
    Logging = 1,

    [TigerText("Metrics")]
    Metrics = 2,

    [TigerText("Trace")]
    Trace = 4
}

public sealed class PromptSmokeSettings : TigerCliSettings
{
    [TigerCliArgument(0, Name = "name",
        Description = "Name to process.",
        DescriptionResourceKey = "Arg_PromptSmoke_Name_Description")]
    public string Name { get; set; } = string.Empty;

    [TigerCliOption("--mode", Required = true,
        Description = "Mode to use.",
        DescriptionResourceKey = "Opt_PromptSmoke_Mode_Description")]
    public PromptSmokeMode Mode { get; set; }

    [TigerCliOption("--features", Promptable = TigerCliPromptable.Normal,
        Description = "Optional features.",
        DescriptionResourceKey = "Opt_PromptSmoke_Features_Description")]
    public PromptSmokeFeatures Features { get; set; }
}

public sealed class PromptSmokeCommand
    : TigerCliAsyncCommandHandler<PromptSmokeSettings, ParserTestExitCode>
{
    public override Task<ParserTestExitCode> ExecuteAsync(PromptSmokeSettings settings)
    {
        TigerConsole.MarkupLine(settings.E(
            "name={0}; mode={1}; features={2}",
            settings.Name,
            settings.Mode,
            settings.Features));
        return Task.FromResult(ParserTestExitCode.Ok);
    }
}
