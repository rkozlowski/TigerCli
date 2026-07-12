using System;

namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Text decoration attributes that can be combined on a styled span. These are additive during
/// markup cascading/inheritance (nested scopes OR their flags onto the effective style), and are
/// rendered as ANSI SGR attributes by <c>AnsiSink</c> when ANSI output is active. Actual display
/// depends on terminal/font support.
/// </summary>
[Flags]
public enum CliTextDecoration
{
    /// <summary>No decoration.</summary>
    None = 0,

    /// <summary>Bold / increased intensity (SGR 1, off via 22).</summary>
    Bold = 1,

    /// <summary>Italic (SGR 3, off via 23).</summary>
    Italic = 2,

    /// <summary>Underline (SGR 4, off via 24).</summary>
    Underline = 4
}
