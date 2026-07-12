using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Tui.Activity;

/// <summary>
/// Immutable column definition for an <see cref="ActivityDialogSpec"/>: an optional fixed
/// <see cref="Width"/> (mutually exclusive with <see cref="CliColumnSizing.Star"/>), default cell
/// <see cref="Align"/>ment, and an optional default <see cref="Style"/>. The activity control maps each
/// column onto a real <c>CliGrid</c> column; <c>CliGrid</c> owns the actual width resolution.
/// </summary>
public sealed class ActivityColumnSpec
{
    internal ActivityColumnSpec(
        int? width,
        CliColumnSizing sizing,
        CliTextAlignment? align,
        ThemeStyle? style,
        CliCellPadding? padding = null)
    {
        Width = width;
        Sizing = sizing;
        Align = align;
        Style = style;
        Padding = padding;
    }

    /// <summary>Fixed column width in cells, or <c>null</c> for content/star sizing.</summary>
    public int? Width { get; }

    /// <summary>How the column is sized after content sizing (<see cref="CliColumnSizing.Star"/> absorbs the remainder).</summary>
    public CliColumnSizing Sizing { get; }

    /// <summary>
    /// Default horizontal alignment for cells in this column, or <c>null</c> when unset (so the spec
    /// default, then the built-in fallback, applies).
    /// </summary>
    public CliTextAlignment? Align { get; }

    /// <summary>Optional default theme style for cells in this column.</summary>
    public ThemeStyle? Style { get; }

    /// <summary>
    /// Optional default cell padding for this column, or <c>null</c> when unset. A cell's own padding
    /// overrides this. Resolved through the normal <c>CliGrid</c> cell-style cascade.
    /// </summary>
    public CliCellPadding? Padding { get; }
}
