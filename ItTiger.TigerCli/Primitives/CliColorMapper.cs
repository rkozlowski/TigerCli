using ItTiger.TigerCli.Enums;
using System;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Converts between TigerCli's <see cref="CliColor"/> palette and the legacy
/// <see cref="ConsoleColor"/> API, and parses raw color names used by markup.
/// </summary>
/// <remarks>
/// The mapper intentionally knows only the built-in <see cref="CliColor"/> enum names.
/// Application color aliases are resolved by the markup layer before this fallback.
/// </remarks>
public static class CliColorMapper
{
    /// <summary>
    /// Parses a raw colour name into a <see cref="CliColor"/>. Only <see cref="CliColor"/> enum names
    /// are recognized (case-insensitive); TigerCli ships no built-in colour aliases. Application- or
    /// library-defined aliases are an app-scoped concern (see <c>TigerColorAliasRegistry</c>) and are
    /// applied by <see cref="Markup.CliMarkupParser"/> before this fallback — referencing a library
    /// never mutates the colour vocabulary here.
    /// </summary>
    public static bool TryParse(string name, out CliColor color)
        => Enum.TryParse(name.Trim(), ignoreCase: true, out color);

    /// <summary>
    /// Converts a <see cref="CliColor"/> to a <see cref="ConsoleColor"/>. The legacy console only
    /// supports the standard 0–15 colors, so ANSI 16–255 values are degraded explicitly to the
    /// nearest standard color (<see cref="CliColorPalette.ToNearestStandard"/>) rather than cast to an
    /// out-of-range <see cref="ConsoleColor"/> (which throws when assigned). 0–15 are unchanged.
    /// </summary>
    public static ConsoleColor ToConsoleColor(CliColor color)
    => (ConsoleColor)(int)CliColorPalette.ToNearestStandard(color);

    /// <summary>Converts a standard <see cref="ConsoleColor"/> value to the matching <see cref="CliColor"/>.</summary>
    public static CliColor ToCliColor(ConsoleColor color)
    => (CliColor)(int)color;

    /// <summary>
    /// Converts a <see cref="ConsoleColor"/> read from the current console state to a
    /// <see cref="CliColor"/>, returning <c>null</c> when the value is not a valid standard color.
    /// <para>Only the 16 standard console colors (numeric 0–15) map to a <see cref="CliColor"/>.
    /// <see cref="Console.ForegroundColor"/>/<see cref="Console.BackgroundColor"/> can report an
    /// out-of-range sentinel (e.g. <c>-1</c>) when the console color is unavailable — typically on
    /// Linux with redirected/captured output. Such a value must be treated as "no color" rather than
    /// cast blindly into an invalid <see cref="CliColor"/> that later throws in the palette.</para>
    /// </summary>
    public static CliColor? FromConsoleColorOrNull(ConsoleColor color)
    {
        int value = (int)color;
        return value is >= 0 and < CliColorPalette.StandardColorCount ? (CliColor)value : null;
    }
}
