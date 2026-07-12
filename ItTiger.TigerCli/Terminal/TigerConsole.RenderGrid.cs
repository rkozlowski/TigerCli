using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using Microsoft.Extensions.Logging;

namespace ItTiger.TigerCli.Terminal;

public static partial class TigerConsole
{

    /// <summary>
    /// Measures the grid when needed and renders it through the supplied sink.
    /// </summary>
    public static void RenderGrid(CliGrid grid, ICliRenderSink sink)
    {
        if (!grid.IsMeasured)
            grid.Measure(sink);

        for (int r = 0; r < grid.RowCount; r++)
        {
            int rowHeight = grid.GetMeasuredRowHeight(r) ?? 0;
            for (int lineIdx = 0; lineIdx < rowHeight; lineIdx++)
            {
                for (int c = 0; c < grid.ColumnCount; c++)
                {
                    var cell = grid.GetMeasuredCell(c, r);
                    if (cell is null) 
                        continue;
                    var lines = cell.Lines;
                    var line = (lineIdx < lines.Count) ? lines[lineIdx] : new List<CliTextSegment>();

                    for (int i = 0; i < line.Count; i++)
                    {
                        sink.Write(line[i]);
                    }
                }
                sink.NewLine();
            }
        }
        sink.Flush();
    }

    // Console (non-interactive). The sink is chosen by TigerConsole.ColorMode and, under Auto,
    // detected terminal capability (ConsoleSink or AnsiSink). The live TUI path (ConsoleTerminal)
    // resolves its sink through the same ConsoleSinkFactory policy.
    /// <summary>
    /// Renders a grid to stdout using the current TigerConsole sink policy.
    /// </summary>
    public static void RenderGrid(CliGrid grid)
        => RenderGrid(grid, GetOutputSink());

    // For tests: list of lines
    /// <summary>
    /// Renders a grid to deterministic plain-text lines.
    /// </summary>
    public static List<string> RenderGridToLines(CliGrid grid)
    {
        var sink = new StringLinesSink();
        RenderGrid(grid, sink);
        return sink.Lines;
    }

    internal static List<List<CliTextSegment>> RenderGridToSegmentedLines(CliGrid grid, TextSegmentLinesSink sink)
    {
        sink.Reset();
        RenderGrid(grid, sink);
        return sink.Lines;
    }

    // To a TextWriter (files, MemoryStream via StreamWriter)
    /// <summary>
    /// Renders a grid as plain text to a <see cref="TextWriter"/>.
    /// </summary>
    public static void RenderGrid(TextWriter writer, CliGrid grid)
        => RenderGrid(grid, new TextWriterSink(writer));

    // To an ANSI SGR string (faithful 0–255 colour); useful for tests, docs, and generated examples.
    /// <summary>
    /// Renders a grid to an ANSI SGR string via <see cref="AnsiSink"/>.
    /// </summary>
    public static string RenderGridToAnsi(CliGrid grid)
    {
        var writer = new StringWriter();
        RenderGrid(grid, new AnsiSink(writer));
        return writer.ToString();
    }

    // To a deterministic HTML string (<pre class="tigercli"> + styled spans/anchors); useful for
    // snapshot tests and for generating documentation examples from real rendering. Opt-in: no
    // existing console/ANSI/text path is affected. See HtmlSink / HtmlSinkOptions.
    /// <summary>
    /// Renders a grid to deterministic HTML via <see cref="HtmlSink"/>.
    /// </summary>
    public static string RenderGridToHtml(CliGrid grid, HtmlSinkOptions? options = null)
    {
        var writer = new StringWriter();
        RenderGrid(grid, new HtmlSink(writer, options));
        return writer.ToString();
    }


    // interactive

    /// <summary>
    /// Renders a grid at a terminal coordinate using a render buffer.
    /// </summary>
    public static void RenderGrid(int x, int y, CliGrid grid)
    {
        CliRenderBuffer buffer = new(null, null);
        RenderGrid(x, y, buffer, grid);
        buffer.Flush();
    }
    /// <summary>
    /// Reserved interactive render-buffer overload.
    /// </summary>
    /// <exception cref="NotImplementedException">This overload is not implemented.</exception>
    public static void RenderGrid(int x, int y, CliRenderBuffer buffer, CliGrid grid)
    {
        throw new NotImplementedException();
    }
}
