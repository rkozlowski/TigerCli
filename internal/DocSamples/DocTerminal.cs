using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.PngSink;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Testing;

namespace DocSamples;

/// <summary>
/// The single definition of the terminal every documentation artifact is captured from, per
/// <c>docs/design/doc-artifacts.md</c>: a <see cref="Columns"/>-column window with
/// <see cref="Chrome"/> window chrome. HTML, PNG, and animated WebP artifacts all use it, so a
/// reader sees one consistent terminal across the whole documentation set.
/// <para>Two rules follow from that and are enforced here rather than restated per generator:</para>
/// <list type="bullet">
/// <item>Capture shells are created at <see cref="Columns"/>. The semi-interactive modal loop
/// measures each grid at <c>Viewport.Width</c>, and a dialog's status row stretches to fill it, so a
/// narrower shell renders a status bar that stops short of the window's right edge.</item>
/// <item>Every PNG canvas — standalone or an animation frame — is <see cref="Columns"/> wide, with
/// height derived from the rendered content. Content wider than the canvas fails loudly via
/// <see cref="EnsureFits"/>; nothing is clipped silently.</item>
/// </list>
/// </summary>
internal static class DocTerminal
{
    /// <summary>The emulated terminal width every artifact is captured at.</summary>
    public const int Columns = 120;

    /// <summary>The window chrome every rendered image carries.</summary>
    public const PngWindowChrome Chrome = PngWindowChrome.FrameAndTitle;

    /// <summary>A capture shell at the documentation terminal width. Height stays a per-capture
    /// concern: a storyboard may deliberately use a short viewport to force scrolling.</summary>
    public static TestShell CreateShell(int viewportHeight = 24, bool useManualClock = false)
        => new(viewportWidth: Columns, viewportHeight: viewportHeight, useManualClock: useManualClock);

    /// <summary>Sink options for one rendered frame of the documentation terminal.</summary>
    public static PngSinkOptions FrameOptions(int rows, string? title) => new()
    {
        Columns = Columns,
        Rows = rows,
        Chrome = Chrome,
        Title = title,
    };

    /// <summary>HTML sink options measuring at the documentation terminal width — the HTML
    /// equivalent of the PNG canvas, so a paired HTML fragment and PNG show the same layout.</summary>
    public static HtmlSinkOptions Html(HtmlHyperlinkMode hyperlinkMode = HtmlHyperlinkMode.Text) => new()
    {
        SoftMaxWidth = Columns,
        HyperlinkMode = hyperlinkMode,
    };

    /// <summary>
    /// Asserts that rendered lines fit the canvas, and returns their count (the frame's row count).
    /// Raw line length — including trailing pad spaces — is what the sink receives per line.
    /// </summary>
    public static int EnsureFits(string what, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            throw new InvalidOperationException($"{what}: the grid rendered no lines.");

        int contentWidth = lines.Max(static l => l.Length);
        if (contentWidth > Columns)
            throw new InvalidOperationException(
                $"{what}: content is {contentWidth} columns wide and exceeds the {Columns}-column canvas. "
                + "Constrain the artifact's width — the documentation terminal is a fixed size.");

        return lines.Count;
    }
}
