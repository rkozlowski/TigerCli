using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Terminal;

internal sealed class TextWriterSink : ICliRenderSink
{
    private readonly TextWriter _writer;
    public TextWriterSink(TextWriter writer) => _writer = writer;
    public string? WindowTitle { get; private set; }
    public void NewLine() => _writer.WriteLine();
    public void Flush() => _writer.Flush();

    public int? SoftMaxWidth => null;
    public int? SoftMaxHeight => null;

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
