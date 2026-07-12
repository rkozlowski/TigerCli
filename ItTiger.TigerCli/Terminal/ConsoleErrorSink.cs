using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Terminal;


internal sealed class ConsoleErrorSink : ICliRenderSink
{
    private ConsoleColor _origFg = Console.ForegroundColor;
    private ConsoleColor _origBg = Console.BackgroundColor;

    // See ConsoleSink: Console.WindowWidth/Height can throw under redirection; gate on stderr.
    public int? SoftMaxWidth => TerminalCapabilities.GetSafeOutputWidth(forError: true);

    public int? SoftMaxHeight => TerminalCapabilities.GetSafeOutputHeight(forError: true);

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
        // Reset before the newline so any end-of-line fill uses default colors instead
        // of bleeding the last segment's background past the right edge.
        Console.ForegroundColor = _origFg;
        Console.BackgroundColor = _origBg;
        Console.Error.WriteLine();
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
        Console.Error.Write(segment.Text);
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
