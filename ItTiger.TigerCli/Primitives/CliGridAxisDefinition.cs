using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Primitives;


/// <summary>
/// Base definition for a grid row or column axis.
/// </summary>
public abstract class CliGridAxisDefinition(CliCellStyle? style)
{
    /// <summary>Style applied along this row or column axis.</summary>
    public CliCellStyle? Style { get; internal set; } = style;

    /// <summary>Whether this axis is pinned or scrollable when used by internal scroll layout.</summary>
    public CliScrollAxis ScrollAxis { get; internal set; } = CliScrollAxis.Pinned;

    /// <summary>Whether width has been locked by frame/layout initialization.</summary>
    public bool IsWidthLocked { get; internal set; } = false;

    /// <summary>Whether height has been locked by frame/layout initialization.</summary>
    public bool IsHeightLocked { get; internal set; } = false;

}
