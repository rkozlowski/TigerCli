using ItTiger.TigerCli.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItTiger.TigerCli.Primitives;


/// <summary>
/// Row definition for a <see cref="Rendering.CliGrid"/>, including row style.
/// </summary>
public sealed class CliGridRowDefinition(CliCellStyle? style) : CliGridAxisDefinition(style)
{
    internal CliGridRowDefinition() : this(null)
    {
    }
    /// <summary>Creates a deep copy of a row definition.</summary>
    public static CliGridRowDefinition Clone(CliGridRowDefinition src)
    {
        return new CliGridRowDefinition(CliCellStyle.Clone(src.Style))
        {            
            ScrollAxis = src.ScrollAxis,
            IsWidthLocked = src.IsWidthLocked,
            IsHeightLocked = src.IsHeightLocked
        };
    }
}

