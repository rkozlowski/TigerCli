using ItTiger.TigerCli.Primitives;
using System.Text;

namespace ItTiger.TigerCli.Terminal;


internal sealed class StringLinesSink : ICliRenderSink
{
    private List<string> _lines = new();
    private StringBuilder _sb = new();

    public List<string> Lines => _lines;
    public string? WindowTitle { get; private set; }

    public int? SoftMaxWidth => null;
    public int? SoftMaxHeight => null;

    public int? MaxWidth => null;
    public int? MaxHeight => null;

    public void NewLine()
    {
        _lines.Add(_sb.ToString());
        _sb.Clear();
    }
    public void Flush()
    {
        if (_sb.Length > 0) 
        { 
            _lines.Add(_sb.ToString()); 
            _sb.Clear(); 
        }
    }

    public void Write(CliTextSegment segment)
    {
        _sb.Append(segment.Text);
    }

    public void Reset()
    {
        _lines = new();
        _sb = new();
    }

    public void SetWindowTitle(string title)
    {
        WindowTitle = AnsiSgr.SanitizeControlString(title);
    }
}

