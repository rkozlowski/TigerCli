using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Structured help rendering: composes help output as a document of separate frameless
/// <see cref="CliGrid"/> blocks rendered through the normal measure/render pipeline. This slice
/// covers the title block (app/command title plus optional description) and heading-plus-lines
/// sections (Usage); later sections migrate onto the same block shapes.
/// </summary>
/// <remarks>
/// All inputs are trusted, already-composed markup strings (escaped by the caller where needed),
/// resolved against the active theme exactly like <see cref="TigerConsole.MarkupLine(string)"/>
/// output. Blocks render through the ambient output sink, so color/no-color policy and test capture
/// scopes behave the same as line-based output. Grid alignment pads short lines to the block width;
/// the trimming sink drops that trailing whitespace so the plain-text shape matches the legacy
/// line-oriented help.
/// </remarks>
internal static class TigerCliHelpRenderer
{
    // Two-space indent under a heading, matching the legacy help text layout.
    private const int IndentWidth = 2;

    /// <summary>
    /// Renders the title block: the app/command title line plus an optional indented description.
    /// </summary>
    public static void RenderTitleBlock(string titleMarkup, string? descriptionMarkup)
        => RenderBlock(titleMarkup, descriptionMarkup is null ? [] : [descriptionMarkup]);

    /// <summary>
    /// Renders a section: an accent-styled heading followed by indented body lines.
    /// </summary>
    public static void RenderSection(string headingMarkup, IReadOnlyList<string> lineMarkups)
        => RenderBlock(headingMarkup, lineMarkups);

    private static void RenderBlock(string headMarkup, IReadOnlyList<string> indentedLineMarkups)
    {
        var sink = new TrailingWhitespaceTrimmingSink(TigerConsole.GetOutputSink());

        // The heading/title is its own single-cell grid so its width never couples to the body
        // lines (no colSpan wrapping surprises), and the body is a two-column grid whose first
        // column is a fixed structural indent — indentation must be structural because the measure
        // pass trims leading/trailing whitespace from cell content lines.
        var head = new CliGrid(1, 1) { DefaultCellStyle = PreformattedStyle() };
        head.Set(0, 0, headMarkup);
        TigerConsole.RenderGrid(head, sink);

        if (indentedLineMarkups.Count == 0)
            return;

        var body = new CliGrid(2, indentedLineMarkups.Count) { DefaultCellStyle = PreformattedStyle() };
        body.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle
        {
            Width = IndentWidth,
            MinWidth = IndentWidth
        }));
        for (int i = 0; i < indentedLineMarkups.Count; i++)
            body.Set(1, i, indentedLineMarkups[i]);
        TigerConsole.RenderGrid(body, sink);
    }

    private static CliCellStyle PreformattedStyle() => new()
    {
        FormattingMode = CliFormattingMode.Preformatted
    };

    /// <summary>
    /// Sink decorator that trims trailing whitespace from each rendered line. Grid alignment pads
    /// every line of a left-aligned cell to the resolved column width; for frameless document-style
    /// blocks that padding is pure trailing whitespace, and dropping it keeps the plain-text shape
    /// identical to line-oriented output. Alignment/padding fill carries no decorations or
    /// hyperlink targets (the measure pass strips them from fill), so nothing meaningful is lost.
    /// </summary>
    private sealed class TrailingWhitespaceTrimmingSink(ICliRenderSink inner) : ICliRenderSink
    {
        private readonly List<CliTextSegment> _line = [];

        public int? SoftMaxWidth => inner.SoftMaxWidth;
        public int? SoftMaxHeight => inner.SoftMaxHeight;
        public int? MaxWidth => inner.MaxWidth;
        public int? MaxHeight => inner.MaxHeight;

        public void Write(CliTextSegment segment) => _line.Add(segment);

        public void NewLine()
        {
            FlushLine();
            inner.NewLine();
        }

        public void Flush()
        {
            FlushLine();
            inner.Flush();
        }

        public void Reset()
        {
            _line.Clear();
            inner.Reset();
        }

        public void SetWindowTitle(string title) => inner.SetWindowTitle(title);

        private void FlushLine()
        {
            // Drop whitespace-only segments from the tail, then trim the end of the last kept one.
            int end = _line.Count;
            while (end > 0 && string.IsNullOrWhiteSpace(_line[end - 1].Text))
                end--;

            for (int i = 0; i < end; i++)
            {
                var segment = _line[i];
                if (i == end - 1)
                {
                    var trimmed = segment.Text.TrimEnd();
                    if (trimmed.Length != segment.Text.Length)
                        segment = new CliTextSegment(trimmed, segment.Style);
                }

                inner.Write(NeutralizeDefaultColors(segment));
            }

            _line.Clear();
        }

        // The grid cascade always injects the framework-global fallback colours (CliGrid's
        // default char style) as the base of every cell, but line-oriented help output never
        // carried explicit colours for plain text. Strip exactly those fallback colours so plain
        // help text keeps inheriting the terminal defaults, while deliberate semantic span
        // colours (and decorations/hyperlinks) pass through untouched.
        private static CliTextSegment NeutralizeDefaultColors(CliTextSegment segment)
        {
            var fallback = CliGrid.GlobalDefaultCharStyle;
            var style = segment.Style;
            bool changed = false;

            if (style.Foreground == fallback.Foreground)
            {
                style.Foreground = null;
                changed = true;
            }

            if (style.Background == fallback.Background)
            {
                style.Background = null;
                changed = true;
            }

            return changed ? new CliTextSegment(segment.Text, style) : segment;
        }
    }
}
