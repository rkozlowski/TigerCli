using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Determines whether a row style or a column style wins when both contribute the same cell style
/// property. Cell-specific style is applied after this choice.
/// </summary>
public enum CliStylePrecedence
{
    /// <summary>
    /// Column style takes precedence over row style.
    /// </summary>
    ColumnOverRow,

    /// <summary>
    /// Row style takes precedence over column style.
    /// </summary>
    RowOverColumn
}
