using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// A contiguous run of text with a single character style.
/// </summary>
public sealed class CliTextSegment(string text, CliCharStyle style)
{
    /// <summary>The text content in this run.</summary>
    public string Text { get; } = text;

    /// <summary>The character style for this run.</summary>
    public CliCharStyle Style { get; } = style;

    /// <summary>
    /// Returns the total text length of a line represented as segments.
    /// </summary>
    public static int Length(IReadOnlyList<CliTextSegment> line)
    {
        int sum = 0;
        for (int i = 0; i < line.Count; i++)
            sum += line[i].Text.Length;
        return sum;
    }
}
