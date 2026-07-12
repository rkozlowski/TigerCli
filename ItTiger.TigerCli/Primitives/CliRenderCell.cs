namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Character and console colours stored in a render buffer cell.
/// </summary>
public readonly struct CliRenderCell(char character, ConsoleColor fg, ConsoleColor bg)
{
    /// <summary>Rendered character.</summary>
    public char Character { get; init; } = character;
    /// <summary>Foreground console colour.</summary>
    public ConsoleColor Foreground { get; init; } = fg;
    /// <summary>Background console colour.</summary>
    public ConsoleColor Background { get; init; } = bg;
}
