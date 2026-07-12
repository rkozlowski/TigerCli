using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Line-breaking strategy used by <see cref="Primitives.CliWrapping"/>.
/// </summary>
public enum CliWrapMode
{
    /// <summary>
    /// Render on a single line. No wrapping or multiline support.
    /// </summary>
    SingleLine,

    /// <summary>
    /// Text can include line breaks (e.g. '\n') and span multiple lines, 
    /// but will not wrap.
    /// </summary>
    Multiline,

    /// <summary>
    /// Wrap on white characters where possible. Allows multiline.
    /// </summary>
    WordWrap,

    /// <summary>
    /// Wrap on non-alphanumeric characters where possible. Allows multiline.
    /// </summary>
    SymbolWrap,

    /// <summary>
    /// Wrap on any character if needed. Allows multiline.
    /// </summary>
    CharWrap
}
