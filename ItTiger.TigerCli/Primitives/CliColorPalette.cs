using ItTiger.TigerCli.Enums;
using System;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// RGB source of truth for <see cref="CliColor"/> across the full ANSI 0–255 palette, plus the
/// down-conversion used to degrade a 256-color value to the nearest standard 0–15 color until a
/// 256-color (ANSI) sink exists. Because a <see cref="CliColor"/> value equals its ANSI palette
/// index, every RGB is derived directly from that index (no per-name table to keep in sync).
/// </summary>
public static class CliColorPalette
{
    /// <summary>Count of directly renderable standard colors (ANSI 0–15).</summary>
    public const int StandardColorCount = 16;

    // Standard 16 console-compatible RGBs (indexes 0–15), in CliColor/ConsoleColor order.
    private static readonly (byte R, byte G, byte B)[] Standard16 =
    {
        (0x00, 0x00, 0x00), (0x00, 0x00, 0x80), (0x00, 0x80, 0x00), (0x00, 0x80, 0x80),
        (0x80, 0x00, 0x00), (0x80, 0x00, 0x80), (0x80, 0x80, 0x00), (0xC0, 0xC0, 0xC0),
        (0x80, 0x80, 0x80), (0x00, 0x00, 0xFF), (0x00, 0xFF, 0x00), (0x00, 0xFF, 0xFF),
        (0xFF, 0x00, 0x00), (0xFF, 0x00, 0xFF), (0xFF, 0xFF, 0x00), (0xFF, 0xFF, 0xFF),
    };

    // 6×6×6 cube component levels (indexes 16–231).
    private static readonly int[] CubeLevels = { 0, 95, 135, 175, 215, 255 };

    /// <summary>True when the color is a directly renderable standard color (ANSI 0–15).</summary>
    public static bool IsStandard(CliColor color) => (int)color is >= 0 and < StandardColorCount;

    /// <summary>Resolves the concrete RGB for any ANSI 0–255 <see cref="CliColor"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside ANSI 0–255.</exception>
    public static (byte R, byte G, byte B) GetRgb(CliColor color)
    {
        int i = (int)color;
        if (i is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(color), color, "CliColor must be an ANSI index 0–255.");

        if (i < 16)
            return Standard16[i];

        if (i < 232)
        {
            int n = i - 16;
            return ((byte)CubeLevels[(n / 36) % 6], (byte)CubeLevels[(n / 6) % 6], (byte)CubeLevels[n % 6]);
        }

        byte v = (byte)(8 + 10 * (i - 232));
        return (v, v, v);
    }

    /// <summary>
    /// Maps any ANSI 0–255 <see cref="CliColor"/> to the nearest standard 0–15 color by Euclidean RGB
    /// distance. Standard colors map to themselves. Used to degrade 256-color values explicitly for
    /// sinks that cannot yet emit 256-color escape sequences.
    /// </summary>
    public static CliColor ToNearestStandard(CliColor color)
    {
        if (IsStandard(color))
            return color;

        var (r, g, b) = GetRgb(color);
        int best = 0, bestDist = int.MaxValue;
        for (int k = 0; k < Standard16.Length; k++)
        {
            var s = Standard16[k];
            int dr = r - s.R, dg = g - s.G, db = b - s.B;
            int d = dr * dr + dg * dg + db * db;
            if (d < bestDist)
            {
                bestDist = d;
                best = k;
            }
        }
        return (CliColor)best;
    }
}
