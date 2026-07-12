using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// Options for <see cref="HtmlSink"/>. Defaults are conservative and documentation-friendly: output is
/// wrapped in <c>&lt;pre class="tigercli"&gt;</c>, links render as visible text (no anchors), and layout
/// is unbounded (no emulated terminal width).
/// </summary>
public sealed class HtmlSinkOptions
{
    private readonly int? _softMaxWidth;

    /// <summary>
    /// When <c>true</c> (default), the whole render is wrapped in <c>&lt;pre class="tigercli"&gt;…&lt;/pre&gt;</c>
    /// so whitespace and line breaks are preserved by the browser. When <c>false</c>, only the inner
    /// HTML is emitted (for embedding inside a caller-supplied container).
    /// </summary>
    public bool WrapInPre { get; init; } = true;

    /// <summary>
    /// How hyperlink targets are rendered. Defaults to <see cref="HtmlHyperlinkMode.Text"/>.
    /// </summary>
    public HtmlHyperlinkMode HyperlinkMode { get; init; } = HtmlHyperlinkMode.Text;

    /// <summary>
    /// Optional soft layout width in text columns — the HTML equivalent of a terminal width. When set,
    /// the sink reports it as <see cref="HtmlSink.SoftMaxWidth"/>, so a grid measured against the sink
    /// wraps and truncates as if the terminal were this many columns wide. Defaults to <c>null</c>
    /// (unbounded), preserving the documentation-friendly "never wrap because of the sink" behavior.
    /// <para>Standard measure-pass resolution applies: a grid's own <c>SoftMaxWidth</c> takes precedence
    /// over the sink's, and an already-measured grid is rendered as measured (this value only affects
    /// the measure pass). It has no effect on segment-level helpers such as
    /// <c>TigerConsole.MarkupToHtml</c>, which perform no measure pass. Must be positive when set.</para>
    /// </summary>
    public int? SoftMaxWidth
    {
        get => _softMaxWidth;
        init
        {
            if (value is <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "SoftMaxWidth must be positive when set.");
            _softMaxWidth = value;
        }
    }
}
