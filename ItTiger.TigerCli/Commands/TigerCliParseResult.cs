namespace ItTiger.TigerCli.Commands;

internal sealed class TigerCliParseResult
{
    public TigerCliCommandRegistration? ResolvedCommand { get; init; }
    public Dictionary<TigerCliArgumentMetadata, string> ArgumentValues { get; init; } = new();
    public Dictionary<TigerCliOptionMetadata, List<string>> OptionValues { get; init; } = new();
    public bool HelpRequested { get; init; }
    public string? ErrorResourceKey { get; init; }
    public object[]? ErrorArgs { get; init; }
    public TigerCliExitKind ErrorExitKind { get; init; } = TigerCliExitKind.InvalidArguments;
}
