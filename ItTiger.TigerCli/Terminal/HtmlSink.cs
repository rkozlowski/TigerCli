using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// An <see cref="ICliRenderSink"/> that renders TigerCli text segments to deterministic HTML — for
/// snapshot tests (internal and external) and for generating documentation examples from real
/// rendering. It is <em>not</em> a browser UI framework: output is plain
/// <c>&lt;pre class="tigercli"&gt;</c> + <c>&lt;span&gt;</c> (and optionally <c>&lt;a&gt;</c>) markup.
/// <para>This sink renders the <see cref="CliCharStyle"/> it receives; it does not reconstruct the
/// original semantic token (e.g. <c>[Heading]</c>) once it has been resolved to a concrete style.
/// Text decorations and the link role map to stable CSS classes (<c>tc-bold</c>, <c>tc-italic</c>,
/// <c>tc-underline</c>, <c>tc-link</c>); foreground/background colours map to a deterministic inline
/// <c>#RRGGBB</c> hex derived from <see cref="CliColorPalette"/> (the 256-colour palette has no stable
/// CSS class names). Text content is HTML-escaped, attribute values are attribute-escaped, and
/// whitespace/line breaks are preserved via the <c>&lt;pre&gt;</c> wrapper. No ANSI is ever emitted.</para>
/// <para>Like the ANSI sink, link text is always written visibly and copyably. In
/// <see cref="HtmlHyperlinkMode.Anchor"/> a non-empty, safe <see cref="CliCharStyle.HyperlinkTarget"/>
/// is additionally wrapped in <c>&lt;a href="…"&gt;</c>; an empty or unsafe target (control characters,
/// or a <c>javascript:</c>/<c>vbscript:</c>/<c>data:</c> scheme) falls back to a styled span.</para>
/// </summary>
public sealed class HtmlSink : ICliRenderSink
{
    private readonly TextWriter _writer;
    private readonly HtmlSinkOptions _options;

    // The <pre> wrapper is opened lazily before the first output and closed on flush, so even an
    // empty render produces a well-formed (possibly empty) wrapper deterministically.
    private bool _opened;
    private bool _closed;

    /// <summary>
    /// Creates an HTML sink writing to <paramref name="writer"/>. When <paramref name="options"/> is
    /// <c>null</c>, the conservative defaults (<see cref="HtmlSinkOptions"/>) are used.
    /// </summary>
    public HtmlSink(TextWriter writer, HtmlSinkOptions? options = null)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _options = options ?? new HtmlSinkOptions();
    }

    // HTML output has no real terminal, so layout is unbounded unless the caller opts in to an
    // emulated terminal width via HtmlSinkOptions.SoftMaxWidth (for width-faithful doc artifacts).
    /// <summary>The optional emulated terminal width used during grid measurement.</summary>
    public int? SoftMaxWidth => _options.SoftMaxWidth;
    /// <inheritdoc/>
    public int? SoftMaxHeight => null;
    /// <inheritdoc/>
    public int? MaxWidth => null;
    /// <inheritdoc/>
    public int? MaxHeight => null;

    /// <summary>The most recently captured, sanitized window title.</summary>
    public string? WindowTitle { get; private set; }

    /// <summary>Writes a styled text segment as escaped HTML.</summary>
    /// <param name="segment">The styled text segment to write.</param>
    public void Write(CliTextSegment segment)
    {
        EnsureOpen();
        WriteSegment(segment);
    }

    /// <summary>Writes a deterministic line feed to the HTML output.</summary>
    public void NewLine()
    {
        EnsureOpen();
        // Deterministic '\n' (not Environment.NewLine): inside <pre> it is a line break and the
        // surrounding whitespace is preserved verbatim, so output is identical across platforms.
        _writer.Write('\n');
    }

    /// <summary>Closes the optional wrapper and flushes the underlying writer.</summary>
    public void Flush()
    {
        EnsureOpen();
        Close();
        _writer.Flush();
    }

    /// <summary>Resets sink state; this sink has no buffered style state to clear.</summary>
    public void Reset()
    {
        // No buffered style state to clear; a fresh sink per render is the supported usage, matching
        // the other TextWriter-backed sinks.
    }

    /// <summary>Captures a sanitized window title without writing terminal controls.</summary>
    /// <param name="title">The window title to capture.</param>
    public void SetWindowTitle(string title)
    {
        WindowTitle = AnsiSgr.SanitizeControlString(title);
    }

    private void EnsureOpen()
    {
        if (_opened)
            return;
        _opened = true;
        if (_options.WrapInPre)
            _writer.Write("<pre class=\"tigercli\">");
    }

    private void Close()
    {
        if (_closed)
            return;
        _closed = true;
        if (_options.WrapInPre)
            _writer.Write("</pre>");
    }

    private void WriteSegment(CliTextSegment segment)
    {
        var text = segment.Text;
        if (string.IsNullOrEmpty(text))
            return;

        var style = segment.Style;
        var classAttr = BuildClassAttribute(style);
        var styleAttr = BuildStyleAttribute(style);
        var escaped = EncodeText(text);

        // Anchor only when enabled, a target is present, and it survives sanitization as safe; the
        // visible text is never changed or hidden, regardless of the outcome.
        if (_options.HyperlinkMode == HtmlHyperlinkMode.Anchor
            && TryBuildSafeHref(style.HyperlinkTarget, out var href))
        {
            _writer.Write("<a");
            WriteAttr("class", classAttr);
            WriteAttr("href", href); // already attribute-escaped
            WriteAttr("style", styleAttr);
            _writer.Write('>');
            _writer.Write(escaped);
            _writer.Write("</a>");
            return;
        }

        if (classAttr is not null || styleAttr is not null)
        {
            _writer.Write("<span");
            WriteAttr("class", classAttr);
            WriteAttr("style", styleAttr);
            _writer.Write('>');
            _writer.Write(escaped);
            _writer.Write("</span>");
            return;
        }

        _writer.Write(escaped);
    }

    private void WriteAttr(string name, string? value)
    {
        if (value is null)
            return;
        _writer.Write(' ');
        _writer.Write(name);
        _writer.Write("=\"");
        _writer.Write(value);
        _writer.Write('"');
    }

    // Stable, documentable class set: decorations first (bold, italic, underline) in a fixed order,
    // then the link role. Colours are not classed (the 256-colour palette has no stable names).
    private static string? BuildClassAttribute(CliCharStyle style)
    {
        var classes = new List<string>(4);
        var d = style.Decorations;
        if ((d & CliTextDecoration.Bold) != 0) classes.Add("tc-bold");
        if ((d & CliTextDecoration.Italic) != 0) classes.Add("tc-italic");
        if ((d & CliTextDecoration.Underline) != 0) classes.Add("tc-underline");
        if (!string.IsNullOrWhiteSpace(style.HyperlinkTarget)) classes.Add("tc-link");
        return classes.Count == 0 ? null : string.Join(' ', classes);
    }

    // Deterministic inline colour: "color:#RRGGBB" then "background-color:#RRGGBB", joined by "; ".
    private static string? BuildStyleAttribute(CliCharStyle style)
    {
        var fg = style.Foreground is { } f ? ToHex(f) : null;
        var bg = style.Background is { } b ? ToHex(b) : null;
        if (fg is null && bg is null)
            return null;

        var sb = new StringBuilder(40);
        if (fg is not null)
            sb.Append("color:").Append(fg);
        if (bg is not null)
        {
            if (sb.Length > 0)
                sb.Append("; ");
            sb.Append("background-color:").Append(bg);
        }
        return sb.ToString();
    }

    private static string ToHex(CliColor color)
    {
        var (r, g, b) = CliColorPalette.GetRgb(color);
        // "X2" hex formatting is culture-independent.
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    // PCDATA escaping: &, <, > only (quotes are not significant in element content). Allocation-free
    // for the common no-special-character case.
    private static string EncodeText(string text)
    {
        StringBuilder? sb = null;
        for (int i = 0; i < text.Length; i++)
        {
            var rep = text[i] switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                _ => null
            };
            if (rep is null)
            {
                sb?.Append(text[i]);
            }
            else
            {
                sb ??= new StringBuilder(text.Length + 8).Append(text, 0, i);
                sb.Append(rep);
            }
        }
        return sb?.ToString() ?? text;
    }

    // Attribute escaping: also escapes the quote characters that could close the attribute.
    private static string EncodeAttribute(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&#39;"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    // Produces a safe, attribute-escaped href, or returns false to signal "fall back to span/text".
    // The visible text is never affected by this — only whether an anchor is emitted.
    private static bool TryBuildSafeHref(string? rawTarget, out string href)
    {
        href = string.Empty;
        if (string.IsNullOrWhiteSpace(rawTarget))
            return false;

        // Strip control characters (C0/C1/DEL), matching the OSC 8 sanitizer, so a malformed value
        // cannot inject a newline/quote sequence or otherwise break out of the attribute.
        var sanitized = StripControlChars(rawTarget);
        if (string.IsNullOrWhiteSpace(sanitized))
            return false;

        if (IsDangerousScheme(sanitized))
            return false;

        href = EncodeAttribute(sanitized);
        return true;
    }

    private static string StripControlChars(string value)
    {
        StringBuilder? sb = null;
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsControl(value[i]))
            {
                sb ??= new StringBuilder(value.Length).Append(value, 0, i);
            }
            else
            {
                sb?.Append(value[i]);
            }
        }
        return sb?.ToString() ?? value;
    }

    // Blocks the classic script-injection schemes for anchors. Relative URLs, fragments, and the
    // common safe schemes (http/https/mailto/file/ftp/…) are left to render as anchors.
    private static bool IsDangerousScheme(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }
}
