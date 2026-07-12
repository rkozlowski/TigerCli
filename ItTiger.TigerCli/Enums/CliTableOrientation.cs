using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItTiger.TigerCli.Enums;


/// <summary>
/// Specifies the orientation of a CLI table.
/// </summary>
public enum CliTableOrientation
{
    /// <summary>
    /// Default layout: headers are rendered as the first row,
    /// and each subsequent row represents a single data record.
    /// </summary>
    Vertical = 0,

    /// <summary>
    /// Rotated layout: headers are rendered as the first column,
    /// and each subsequent column represents a single data record.
    /// </summary>
    Horizontal
}
