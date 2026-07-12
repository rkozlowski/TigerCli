using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItTiger.TigerCli.Enums;


/// <summary>
/// Controls whether cell content is treated as raw data or preformatted markup-aware text.
/// </summary>
public enum CliFormattingMode
{
    /// <summary>
    /// Records are raw objects; formatter, format string, null display, and styles apply.
    /// </summary>
    Raw,

    /// <summary>
    /// Records are already formatted strings containing markup; no formatting or styling is applied.
    /// </summary>
    Preformatted
}
