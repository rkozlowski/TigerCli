using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Rendering;


/// <summary>
/// Low-level dirty-cell console render buffer used by interactive rendering paths.
/// </summary>
/// <remarks>
/// The buffer tracks changed cells and writes only dirty rows/cells to <see cref="Console"/> during
/// <see cref="Flush"/>. Most app code should render through <see cref="Terminal.ICliRenderSink"/> and
/// <see cref="TigerConsole"/> instead.
/// </remarks>
public class CliRenderBuffer
{
    private readonly CliRenderCell[,] buffer;
    private bool[,] dirty;
    private int firstDirtyLine = int.MaxValue;
    private int lastDirtyLine = int.MinValue;

    /// <summary>The buffer width in console cells.</summary>
    public int Width { get; }

    /// <summary>The buffer height in console cells.</summary>
    public int Height { get; }

    /// <summary>
    /// Creates a render buffer. Missing dimensions are resolved through safe terminal capability
    /// helpers, so allocation works under redirected or headless output.
    /// </summary>
    public CliRenderBuffer(int? width, int? height)
    {
        // Reading Console.WindowWidth/Height can throw without an interactive console; fall back to
        // deterministic dimensions so buffer allocation never fails on a redirected/headless host.
        Width = width ?? TerminalCapabilities.GetSafeOutputWidth();
        Height = height ?? TerminalCapabilities.GetSafeOutputHeight() ?? TerminalCapabilities.DefaultOutputHeight;
        buffer = new CliRenderCell[Height, Width];
        dirty = new bool[Height, Width];
    }

    /// <summary>Writes a string at the current console cursor, updating dirty cells as it advances.</summary>
    public void Write(string s, ConsoleColor fg, ConsoleColor bg)
    {
        var x = Console.CursorLeft;
        var y = Console.CursorTop;
        var l = s.Length;
        for (var i = 0; i < l; i++)
        {
            var c = s[i];
            switch (c)
            {
                case '\r':
                    x = 0;
                    break;
                case '\n':
                    x = 0;
                    y += 1;
                    break;
                default:
                    Set(x, y, c, fg, bg);
                    x++;
                    if (x >= Width)
                    {
                        x = 0;
                        y++;
                    }
                    break;
            }
            if (y >= Height)
            {
                Flush();
                Console.CursorLeft = 0;
                Console.CursorTop = Height - 1;
                Console.WriteLine();
                y = Height - 1;
            }
        }
        Console.CursorLeft = x;
        Console.CursorTop = y;
    }

    /// <summary>Sets one buffer cell and marks it dirty.</summary>
    public void Set(int x, int y, char c, ConsoleColor fg, ConsoleColor bg)
    {
        Set (x, y, new CliRenderCell(c, fg, bg));
    }

    /// <summary>Sets one buffer cell and marks it dirty.</summary>
    public void Set(int x, int y, CliRenderCell cell)
    {
        buffer[y, x] = cell;
        dirty[y, x] = true;
        if (y < firstDirtyLine)
        {
            firstDirtyLine = y;
        }
        if (y > lastDirtyLine)
        {
            lastDirtyLine = y;
        }
    }

    /// <summary>Writes all dirty cells to the console and restores the previous console cursor and colours.</summary>
    public void Flush()
    {
        if (firstDirtyLine > lastDirtyLine)
        {
            return;
        }
        var oldX = Console.CursorLeft;
        var oldY = Console.CursorTop;        
        var oldFg = Console.ForegroundColor;
        var oldBg = Console.BackgroundColor;

        for (int y = firstDirtyLine; y <= lastDirtyLine; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!dirty[y, x]) 
                    continue;
                Console.SetCursorPosition(x, y);
                Console.ForegroundColor = buffer[y, x].Foreground;
                Console.BackgroundColor = buffer[y, x].Background;
                Console.Write(buffer[y, x].Character);
                dirty[y, x] = false;
            }
        }
        Console.SetCursorPosition(oldX, oldY);
        Console.ForegroundColor = oldFg;
        Console.BackgroundColor = oldBg;
        firstDirtyLine = int.MaxValue;
        lastDirtyLine = int.MinValue;
        dirty = new bool[Height, Width];
    }
}
