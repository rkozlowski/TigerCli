using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// Shared resolution of a semantic <see cref="ThemeStyle"/> into a value-only data style used by the
/// CRUD-oriented builders (<see cref="CliList{T}"/> and <see cref="CliDetails"/>). Only the foreground
/// and decorations of the resolved theme token are carried over; the background is intentionally left
/// <c>null</c> so the table/detail surface (panel/zebra/etc.) shows through unchanged. This keeps
/// semantic value styling theme-aware without commands hardcoding colours, and consistent across both
/// builders.
/// </summary>
internal static class CliSemanticValueStyle
{
    /// <summary>
    /// Resolves <paramref name="style"/> through <paramref name="theme"/> into a foreground/decoration
    /// data style, or returns <c>null</c> when <paramref name="style"/> is <c>null</c> (so the preset's
    /// own body styling is preserved by the style cascade).
    /// </summary>
    public static CliCellStyle? Resolve(ITheme theme, ThemeStyle? style)
    {
        if (style is null)
            return null;

        var charStyle = theme.Resolve(style.Value).CharStyle;
        return new CliCellStyle(new CliCharStyle(
            charStyle?.Foreground,
            background: null,
            charStyle?.Decorations ?? CliTextDecoration.None));
    }
}
