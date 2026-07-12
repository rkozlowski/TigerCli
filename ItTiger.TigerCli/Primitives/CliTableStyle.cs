using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Resolved, renderable table defaults — the output of resolving a
/// <see cref="Rendering.CliTableStyleRecipe"/> against an <see cref="Tui.Abstractions.ITheme"/>.
/// Carries the resulting orientation, framing, and styles so <see cref="Rendering.CliTable"/> can
/// apply them via <see cref="Rendering.CliTable.ApplyStyle"/>. It is a defaults container, not a
/// renderer — applying it does not lock the table.
/// </summary>
public sealed class CliTableStyle
{
    /// <summary>Layout orientation the style resolves to.</summary>
    public CliTableOrientation Orientation { get; set; } = CliTableOrientation.Vertical;

    /// <summary>Which orientations this style supports; locked styles clamp orientation changes.</summary>
    public CliTableStyleOrientationSupport OrientationSupport { get; set; } = CliTableStyleOrientationSupport.Both;

    /// <summary>Outer frame, separators, join style, and frame character style.</summary>
    public CliTableFrameConfig FrameConfig { get; set; } = new();

    /// <summary>Default style for a table title, when one is shown. Titles are app-provided.</summary>
    public CliCellStyle? TitleStyle { get; set; }

    /// <summary>Default header band style (alignment and colors).</summary>
    public CliCellStyle? HeaderStyle { get; set; }

    /// <summary>Default cell style applied to the whole table.</summary>
    public CliCellStyle? DefaultCellStyle { get; set; }

    /// <summary>Default record style, when the theme distinguishes record cells from <see cref="DefaultCellStyle"/>.</summary>
    public CliCellStyle? DataStyle { get; set; }

    /// <summary>Default alternate-record style for zebra striping, when the theme supplies one.</summary>
    public CliCellStyle? DataAltStyle { get; set; }

    /// <summary>Whether <see cref="DataAltStyle"/> should be used by default when rendering alternate records.</summary>
    public bool AlternateRecordsEnabled { get; set; }
}
