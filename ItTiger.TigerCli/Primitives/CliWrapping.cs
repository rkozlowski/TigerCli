using ItTiger.TigerCli.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Text wrapping and truncation policy for rendered cell content.
/// </summary>
/// <remarks>
/// Wrapping takes effect when the render pipeline has a width constraint from the cell, axis, grid,
/// or sink. Truncation is only allowed when <see cref="AllowTruncation"/> is <c>true</c>.
/// </remarks>
public sealed class CliWrapping(CliWrapMode mode, bool allowTruncation = false, string? truncationIndicator = null)
{
    /// <summary>
    /// Defines how the text is broken into lines: none, word, char, multiline, etc.
    /// </summary>
    public CliWrapMode Mode { get; init; } = mode;

    /// <summary>
    /// Whether text is allowed to be truncated to fit within the constraints.
    /// </summary>
    public bool AllowTruncation { get; init; } = allowTruncation;

    //public CliWrapping? Wrapping { get; set; }

    /// <summary>
    /// Optional indicator to show when text is truncated.
    /// Only used when <see cref="AllowTruncation"/> is true.
    /// </summary>
    public string? TruncationIndicator { get; init; } = truncationIndicator;


    /// <summary>Render as one line without truncation.</summary>
    public static CliWrapping SingleLine => new(CliWrapMode.SingleLine, allowTruncation: false);

    /// <summary>Render as one line and allow truncation with an ellipsis.</summary>
    public static CliWrapping SingleLineTruncate => new(CliWrapMode.SingleLine, allowTruncation: true, "…");

    /// <summary>Preserve explicit line breaks without truncation.</summary>
    public static CliWrapping Multiline => new(CliWrapMode.Multiline);

    /// <summary>Preserve explicit line breaks and allow truncation with an ellipsis.</summary>
    public static CliWrapping MultilineTruncate => new(CliWrapMode.Multiline, allowTruncation: true, "…");

    /// <summary>Wrap at word boundaries without truncation.</summary>
    public static CliWrapping WordWrap => new(CliWrapMode.WordWrap);

    /// <summary>Wrap at word boundaries and allow truncation with an ellipsis.</summary>
    public static CliWrapping WordWrapTruncate => new(CliWrapMode.WordWrap, allowTruncation: true, "…");

    /// <summary>Wrap at symbol boundaries without truncation.</summary>
    public static CliWrapping SymbolWrap => new(CliWrapMode.SymbolWrap);

    /// <summary>Wrap at symbol boundaries and allow truncation with an ellipsis.</summary>
    public static CliWrapping SymbolWrapTruncate => new(CliWrapMode.SymbolWrap, allowTruncation: true, "…");

    /// <summary>Wrap at character boundaries without truncation.</summary>
    public static CliWrapping CharWrap => new(CliWrapMode.CharWrap);
    
    /// <summary>Wrap at character boundaries and allow truncation with an ellipsis.</summary>
    public static CliWrapping CharWrapTruncate => new(CliWrapMode.CharWrap, allowTruncation: true, "…");

}
