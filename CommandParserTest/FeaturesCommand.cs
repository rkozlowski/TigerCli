using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

namespace CommandParserTest;

public sealed class FeaturesSettings : TigerCliSettings
{
    // Dynamic multi-select over keyed long values. The provider ("features") returns
    // OptionItem<long>(bitValue, "Name (0xN)"), TigerCli binds the selected keys, and the
    // command ORs them into a combined flag mask — the same key/label pattern a real app
    // (e.g. TigerWrap language options) uses.
    [TigerCliOption("--features",
        Provider = "feature-flags",
        Promptable = TigerCliPromptable.Normal,
        Description = "Feature bit flags to enable (comma-separated or repeated).")]
    [TigerCliMultiSelect]
    public long[]? Features { get; set; }
}

public sealed class FeaturesCommand
    : TigerCliAsyncCommandHandler<FeaturesSettings, ParserTestExitCode>
{
    public override Task<ParserTestExitCode> ExecuteAsync(FeaturesSettings settings)
    {
        var selected = settings.Features ?? [];
        var combined = selected.Aggregate(0L, (acc, value) => acc | value);

        TigerConsole.MarkupLine(settings.E(
            "features={0}; combined=0x{1}",
            string.Join(",", selected),
            combined.ToString("X")));

        return Task.FromResult(ParserTestExitCode.Ok);
    }
}
