using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Terminal;


internal sealed class TextSegmentLinesSink : ICliRenderSink
{
    private readonly List<List<CliTextSegment>> _lines = new();
    private List<CliTextSegment> _current = new();

    public List<List<CliTextSegment>> Lines => _lines;
    public string? WindowTitle { get; private set; }

    public int? SoftMaxWidth { get; set; }
    public int? SoftMaxHeight { get; set; }

    public int? MaxWidth { get; set; }

    public int? MaxHeight { get; set; }

    public void Reset()
    {
        _lines.Clear();
        _current.Clear();
    }

    public void NewLine()
    {
        _lines.Add(_current);
        _current = new();
    }

    public void Flush()
    {
        if (_current.Count > 0)
        {
            _lines.Add(_current);
            _current = new();
        }
    }

    public void Write(CliTextSegment segment)
    {
        _current.Add(segment);
    }

    public void SetWindowTitle(string title)
    {
        WindowTitle = AnsiSgr.SanitizeControlString(title);
    }
}

