using ItTiger.TigerCli.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItTiger.TigerCli.Primitives;


/// <summary>
/// Column definition for a <see cref="Rendering.CliGrid"/>, including style and sizing mode.
/// </summary>
public sealed class CliGridColumnDefinition(CliCellStyle? style) : CliGridAxisDefinition(style)
{
    /// <summary>How this column participates in width allocation. Defaults to <see cref="CliColumnSizing.Auto"/>.</summary>
    public CliColumnSizing Sizing { get; set; } = CliColumnSizing.Auto;

    internal CliGridColumnDefinition(): this(null)
    {
    }
    /// <summary>Creates a deep copy of a column definition.</summary>
    public static CliGridColumnDefinition Clone(CliGridColumnDefinition src)
    {
        return new CliGridColumnDefinition(CliCellStyle.Clone(src.Style))
        {
            ScrollAxis = src.ScrollAxis,
            IsWidthLocked = src.IsWidthLocked,
            IsHeightLocked = src.IsHeightLocked,
            Sizing = src.Sizing
        };
    }
}
