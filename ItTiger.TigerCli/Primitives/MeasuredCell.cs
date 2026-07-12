using ItTiger.TigerCli.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Measured representation of a grid cell after formatting, markup parsing, and layout.
/// </summary>
public sealed class MeasuredCell
{
    /// <summary>Immutable clone of the cell's original measured line shape.</summary>
    public List<List<CliTextSegment>> InitialLines { get; }
    /// <summary>Effective style used to measure the cell.</summary>
    public CliCellStyle Style { get; }
    /// <summary>Current measured width.</summary>
    public int Width { get; private set; }
    /// <summary>Current measured height.</summary>
    public int Height { get; private set; }

    /// <summary>Whether this measured slot is covered by a spanning anchor cell.</summary>
    public bool IsCovered { get; internal set; } = false;

    /// <summary>Column span of the anchor cell.</summary>
    public int ColSpan { get; internal set; } = 1;

    /// <summary>Row span of the anchor cell.</summary>
    public int RowSpan { get; internal set; } = 1;

    /// <summary>Whether horizontal scrolling is active for this measured cell.</summary>
    public bool IsScrollXActive { get; internal set; } = false;
    /// <summary>Whether vertical scrolling is active for this measured cell.</summary>
    public bool IsScrollYActive { get; internal set; } = false;

    /// <summary>Working measured lines after wrapping, alignment, and viewport slicing.</summary>
    public List<List<CliTextSegment>> Lines { get; private set; }
    internal TextSegmentLinesSink? Sink { get; set; } = null;
    /// <summary>Total line count before viewport slicing.</summary>
    public int TotalLinesCount { get; internal set; }

    /// <summary>
    /// Creates a measured cell from segmented lines and the effective cell style.
    /// </summary>
    public MeasuredCell(
        List<List<CliTextSegment>> lines,
        CliCellStyle style)
    {
        InitialLines = CloneLines(lines);   // keep original shape
        Lines = CloneLines(lines);   // working copy
        Style = style;
        UpdateSize();
    }

    /// <summary>
    /// Replaces the working lines and recalculates measured size.
    /// </summary>
    public void UpdateLines(List<List<CliTextSegment>> lines)
    {
        Lines = CloneLines(lines);
        UpdateSize();
    }

    private void UpdateSize()
    {
        Height = Lines.Count;
        int width = 0;
        foreach (var line in Lines)
        {
            var w = CliTextSegment.Length(line);
            if (w > width)
                width = w;
        }
        Width = width;
    }

    internal static List<CliTextSegment> CloneLine(List<CliTextSegment> src)
    {
        var clone = new List<CliTextSegment>(src.Count);
        for (int i = 0; i < src.Count; i++)
            clone.Add(src[i]);
        return clone;
    }

    internal static List<List<CliTextSegment>> CloneLines(List<List<CliTextSegment>> src)
    {
        var clone = new List<List<CliTextSegment>>(src.Count);
        for (int i = 0; i < src.Count; i++)
            clone.Add(CloneLine(src[i]));
        return clone;
    }
}
