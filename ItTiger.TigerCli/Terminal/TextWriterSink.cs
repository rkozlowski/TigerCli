using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Terminal;

// Plain-text sink over an arbitrary writer. It is generic (and therefore unbounded) by default,
// because most of its callers write to a string, a file, or a capture stream. The colour-disabled
// console path is the exception: it writes to a real terminal and says so with a terminal target,
// so no-colour output wraps in the layout exactly like the coloured paths do.
internal sealed class TextWriterSink : ICliRenderSink
{
    private readonly TextWriter _writer;
    private readonly CliSinkTarget _target;

    public TextWriterSink(TextWriter writer, CliSinkTarget target = CliSinkTarget.Buffer)
    {
        _writer = writer;
        _target = target;
    }

    public string? WindowTitle { get; private set; }
    public void NewLine() => _writer.WriteLine();
    public void Flush() => _writer.Flush();

    public int? SoftMaxWidth => TerminalCapabilities.SoftWidthFor(_target);
    public int? SoftMaxHeight => TerminalCapabilities.SoftHeightFor(_target);

    public int? MaxWidth => null;
    public int? MaxHeight => null;

    public void Write(CliTextSegment segment)
    {
        _writer.Write(segment.Text);
    }

    public void Reset()
    {        
    }

    public void SetWindowTitle(string title)
    {
        WindowTitle = AnsiSgr.SanitizeControlString(title);
    }
}
