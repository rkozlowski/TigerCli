using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Terminal;


internal sealed class ConsoleSink : ICliRenderSink
{
    private ConsoleColor _origFg = Console.ForegroundColor;
    private ConsoleColor _origBg = Console.BackgroundColor;

    // Console.WindowWidth/Height are a terminal capability, not a guaranteed value: reading them
    // when stdout is redirected/piped/captured can throw ("The handle is invalid."). Route through
    // the safe helper so structured output falls back to a deterministic width and unbounded height.
    public int? SoftMaxWidth => TerminalCapabilities.GetSafeOutputWidth();

    public int? SoftMaxHeight => TerminalCapabilities.GetSafeOutputHeight();

    public int? MaxWidth => null;
    public int? MaxHeight => null;

    public void SetStyle(CliCharStyle style)
    {
        if (style.Foreground.HasValue)
        {
            Console.ForegroundColor = CliColorMapper.ToConsoleColor(style.Foreground.Value);
        }
        if (style.Background.HasValue)
        {
            Console.BackgroundColor = CliColorMapper.ToConsoleColor(style.Background.Value);
        }
    }
    public void NewLine()
    {
        // Reset before the newline so any end-of-line fill (Windows console paints the
        // remainder of the row with the current background) uses default colors instead
        // of bleeding the last cell's background past the grid's right edge.
        Console.ForegroundColor = _origFg;
        Console.BackgroundColor = _origBg;        
        Console.WriteLine();
    }



    public void Flush()
    {
        Console.ForegroundColor = _origFg;
        Console.BackgroundColor = _origBg;
    }

    public void Write(CliTextSegment segment)
    {
        if (segment.Style.Foreground.HasValue) 
            Console.ForegroundColor = CliColorMapper.ToConsoleColor(segment.Style.Foreground.Value);
        if (segment.Style.Background.HasValue) 
            Console.BackgroundColor = CliColorMapper.ToConsoleColor(segment.Style.Background.Value);
        Console.Write(segment.Text);
    }

    public void Reset()
    {
        _origFg = Console.ForegroundColor;
        _origBg = Console.BackgroundColor;
    }

    public void SetWindowTitle(string title)
    {
    }
}
