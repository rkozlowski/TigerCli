using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// Title content and style used by <see cref="CliTable"/> when materializing a table.
/// </summary>
public sealed class CliTableTitle
{
    /// <summary>Title content rendered above the table body.</summary>
    public object? Content { get; init; }

    /// <summary>Style used to format, align, and render the title cell.</summary>
    public CliCellStyle Style { get; init; }

    /// <summary>
    /// Creates a preformatted, markup-aware title with an optional alignment and character style.
    /// </summary>
    public CliTableTitle(string content, CliTextAlignment alignment = CliTextAlignment.Center, CliCharStyle? charStyle = null)
    {
        Content = content;
        Style = new CliCellStyle { FormattingMode = CliFormattingMode.Preformatted, HorizontalAlignment = alignment, CharStyle = charStyle };        
    }

    /// <summary>
    /// Creates a title from arbitrary content and an explicit title style.
    /// </summary>
    public CliTableTitle(object? content, CliCellStyle style)
    {
        Content = content;
        Style = style;        
    }
}
