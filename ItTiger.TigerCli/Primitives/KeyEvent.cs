namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Keyboard event captured by a terminal or test terminal.
/// </summary>
public readonly record struct KeyEvent(ConsoleKey Key, ConsoleModifiers Mods)
{
    /// <summary>
    /// Creates a key event with an explicit character value.
    /// </summary>
    public KeyEvent(ConsoleKey key, ConsoleModifiers mods, char keyChar)
        : this(key, mods)
    {
        KeyChar = keyChar;
    }

    /// <summary>Character associated with the key event, or <c>'\0'</c> when none was supplied.</summary>
    public char KeyChar { get; init; } = '\0';
}
