using System;
using System.Linq;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the ANSI 0–255 <see cref="CliColor"/> contract: 0–15 stay ConsoleColor-compatible,
/// 16–255 equal their ANSI palette index, names are unique and collision-free, and 16–255 are
/// guarded (degraded, never cast to an invalid ConsoleColor) in the conversion helper.
/// </summary>
public sealed class CliColorPaletteTests
{
    // ---- 0–15 preserved exactly ----

    [Theory]
    [InlineData(CliColor.Black, 0)]
    [InlineData(CliColor.DarkBlue, 1)]
    [InlineData(CliColor.DarkGreen, 2)]
    [InlineData(CliColor.DarkCyan, 3)]
    [InlineData(CliColor.DarkRed, 4)]
    [InlineData(CliColor.DarkMagenta, 5)]
    [InlineData(CliColor.DarkYellow, 6)]
    [InlineData(CliColor.Gray, 7)]
    [InlineData(CliColor.DarkGray, 8)]
    [InlineData(CliColor.Blue, 9)]
    [InlineData(CliColor.Green, 10)]
    [InlineData(CliColor.Cyan, 11)]
    [InlineData(CliColor.Red, 12)]
    [InlineData(CliColor.Magenta, 13)]
    [InlineData(CliColor.Yellow, 14)]
    [InlineData(CliColor.White, 15)]
    public void StandardColors_KeepConsoleColorValues(CliColor color, int value)
    {
        Assert.Equal(value, (int)color);
        // Round-trips through ConsoleColor unchanged.
        Assert.Equal((ConsoleColor)value, CliColorMapper.ToConsoleColor(color));
    }

    // ---- IsStandard boundary is half-open: 0–15 standard, 16+ extended ----

    [Fact]
    public void IsStandard_TrueForLastStandardColor()
    {
        // 15 (White) is the last directly renderable standard color.
        Assert.True(CliColorPalette.IsStandard((CliColor)15));
    }

    [Fact]
    public void IsStandard_FalseForFirstExtendedColor()
    {
        // 16 (Black1) is the first 256-palette color; it must NOT index the 16-entry classic table.
        Assert.False(CliColorPalette.IsStandard((CliColor)16));
    }

    // ---- selected 16–255 numeric values ----

    [Theory]
    [InlineData(CliColor.Navy, 17)]
    [InlineData(CliColor.OceanBlue, 24)]
    [InlineData(CliColor.OceanBlue2, 25)]
    [InlineData(CliColor.TealBlue, 31)]
    [InlineData(CliColor.BlueGray, 60)]
    [InlineData(CliColor.Mint2, 72)]
    [InlineData(CliColor.SoftTeal, 73)]
    [InlineData(CliColor.OldRose, 95)]
    [InlineData(CliColor.OliveGray, 101)]
    [InlineData(CliColor.SeafoamGray, 109)]
    [InlineData(CliColor.Mint, 115)]
    [InlineData(CliColor.Sand2, 221)]
    [InlineData(CliColor.Gray50, 244)]
    [InlineData(CliColor.Gray85, 253)]
    [InlineData(CliColor.Gray93, 255)]
    public void ExtendedColors_EqualAnsiIndex(CliColor color, int index) =>
        Assert.Equal(index, (int)color);

    // ---- exact duplicates use BaseName1 ----

    [Theory]
    [InlineData(CliColor.Black1, 16, 0x00, 0x00, 0x00)]
    [InlineData(CliColor.Blue1, 21, 0x00, 0x00, 0xFF)]
    [InlineData(CliColor.Green1, 46, 0x00, 0xFF, 0x00)]
    [InlineData(CliColor.Cyan1, 51, 0x00, 0xFF, 0xFF)]
    [InlineData(CliColor.Red1, 196, 0xFF, 0x00, 0x00)]
    [InlineData(CliColor.Magenta1, 201, 0xFF, 0x00, 0xFF)]
    [InlineData(CliColor.Yellow1, 226, 0xFF, 0xFF, 0x00)]
    [InlineData(CliColor.White1, 231, 0xFF, 0xFF, 0xFF)]
    public void ExactDuplicates_UseBaseName1_AndMatchStandardRgb(CliColor color, int index, int r, int g, int b)
    {
        Assert.Equal(index, (int)color);
        Assert.Equal(((byte)r, (byte)g, (byte)b), CliColorPalette.GetRgb(color));
    }

    // ---- all values unique, 16–255 names don't collide with 0–15 ----

    [Fact]
    public void AllEnumValues_AreUnique()
    {
        var values = Enum.GetValues<CliColor>();
        Assert.Equal(values.Length, values.Distinct().Count());
        Assert.Equal(256, values.Length);
        // Every ANSI index 0–255 is represented exactly once.
        Assert.Equal(Enumerable.Range(0, 256), values.Select(v => (int)v).OrderBy(x => x));
    }

    [Fact]
    public void ExtendedNames_DoNotCollideWith_StandardNames()
    {
        var standard = new[]
        {
            "Black","DarkBlue","DarkGreen","DarkCyan","DarkRed","DarkMagenta","DarkYellow","Gray",
            "DarkGray","Blue","Green","Cyan","Red","Magenta","Yellow","White"
        };
        foreach (var name in Enum.GetNames<CliColor>())
        {
            var value = (int)Enum.Parse<CliColor>(name);
            // A standard name may only appear at its standard index (0–15).
            if (standard.Contains(name))
                Assert.True(value < 16, $"Standard name '{name}' must stay in 0–15.");
            else
                Assert.True(value >= 16, $"Extended name '{name}' must be in 16–255.");
        }
    }

    // ---- RGB derivation is structural ----

    [Theory]
    [InlineData(CliColor.OceanBlue, 0x00, 0x5F, 0x87)]
    [InlineData(CliColor.OldRose, 0x87, 0x5F, 0x5F)]
    [InlineData(CliColor.Sand2, 0xFF, 0xD7, 0x5F)]
    [InlineData(CliColor.Gray85, 0xDA, 0xDA, 0xDA)]
    public void GetRgb_MatchesAnsiCubeAndRamp(CliColor color, int r, int g, int b) =>
        Assert.Equal(((byte)r, (byte)g, (byte)b), CliColorPalette.GetRgb(color));

    // ---- 16–255 guarded in the ConsoleColor conversion ----

    [Fact]
    public void ToConsoleColor_DegradesExtendedColors_ToValidStandardConsoleColor()
    {
        foreach (var color in Enum.GetValues<CliColor>())
        {
            var cc = CliColorMapper.ToConsoleColor(color);
            // Must always be a defined ConsoleColor (0–15), never an out-of-range cast.
            Assert.True(Enum.IsDefined(cc), $"{color} produced invalid ConsoleColor {(int)cc}.");
        }
    }

    [Fact]
    public void ToNearestStandard_Identity_ForStandardColors()
    {
        for (int i = 0; i < 16; i++)
        {
            var c = (CliColor)i;
            Assert.Equal(c, CliColorPalette.ToNearestStandard(c));
        }
    }

    [Fact]
    public void ToNearestStandard_DegradesExtendedColor_ToANearbyStandard()
    {
        // Red1 (#FF0000) degrades to the standard Red; pure-black cube vertex to Black.
        Assert.Equal(CliColor.Red, CliColorPalette.ToNearestStandard(CliColor.Red1));
        Assert.Equal(CliColor.Black, CliColorPalette.ToNearestStandard(CliColor.Black1));
    }

    // ---- CliColorMapper parses the new names ----

    [Theory]
    [InlineData("OceanBlue", CliColor.OceanBlue)]
    [InlineData("BlueGray", CliColor.BlueGray)]
    [InlineData("SoftTeal", CliColor.SoftTeal)]
    [InlineData("OldRose", CliColor.OldRose)]
    [InlineData("Sand2", CliColor.Sand2)]
    [InlineData("Gray85", CliColor.Gray85)]
    [InlineData("oceanblue", CliColor.OceanBlue)] // case-insensitive
    public void CliColorMapper_ParsesNewNames(string name, CliColor expected)
    {
        Assert.True(CliColorMapper.TryParse(name, out var color));
        Assert.Equal(expected, color);
    }

    [Fact]
    public void CliColorMapper_EnumNamesStillResolve()
    {
        Assert.True(CliColorMapper.TryParse("Red", out var red));
        Assert.Equal(CliColor.Red, red);
    }

    [Theory]
    // The old hardcoded Spectre/CSS-style color alias *mappings* are no longer shipped by TigerCli
    // core. Some of these names now exist as distinct extended-palette enum names (e.g. "Navy"), so the
    // assertion is that the legacy mapping (alias -> old target color) is gone — not that the name is
    // entirely unparseable.
    [InlineData("aqua", CliColor.Cyan)]
    [InlineData("navy", CliColor.DarkBlue)]
    [InlineData("teal", CliColor.DarkCyan)]
    [InlineData("maroon", CliColor.DarkRed)]
    [InlineData("purple", CliColor.DarkMagenta)]
    [InlineData("olive", CliColor.DarkYellow)]
    [InlineData("silver", CliColor.Gray)]
    [InlineData("lime", CliColor.Green)]
    [InlineData("fuchsia", CliColor.Magenta)]
    [InlineData("orange", CliColor.DarkYellow)]
    [InlineData("pink", CliColor.Magenta)]
    [InlineData("indigo", CliColor.DarkBlue)]
    [InlineData("brown", CliColor.DarkRed)]
    public void CliColorMapper_LegacyHardcodedAliasMappings_AreGone(string legacyAlias, CliColor oldTarget)
    {
        var resolvedToOldTarget = CliColorMapper.TryParse(legacyAlias, out var color) && color == oldTarget;
        Assert.False(resolvedToOldTarget);
    }

    // ---- markup raw color parsing accepts the new names ----

    [Fact]
    public void Markup_RawColor_AcceptsNewEnumNames()
    {
        var ocean = Assert.Single(CliMarkupParser.Parse("[OceanBlue]text[/]"));
        Assert.Equal("text", ocean.Text);
        Assert.Equal(CliColor.OceanBlue, ocean.Style.Foreground);

        var gray = Assert.Single(CliMarkupParser.Parse("[Gray85]x[/]"));
        Assert.Equal(CliColor.Gray85, gray.Style.Foreground);
    }

    [Fact]
    public void Markup_ExistingRawColors_StillWork()
    {
        var red = Assert.Single(CliMarkupParser.Parse("[Red]x[/]"));
        Assert.Equal(CliColor.Red, red.Style.Foreground);

        var both = Assert.Single(CliMarkupParser.Parse("[Yellow on Blue]x[/]"));
        Assert.Equal(CliColor.Yellow, both.Style.Foreground);
        Assert.Equal(CliColor.Blue, both.Style.Background);
    }

    [Fact]
    public void Markup_SemanticToken_StillWinsOverRawColor()
    {
        // [Accent] resolves through the active theme, not as a raw color, even though raw color
        // parsing now knows hundreds more names.
        var styles = new ThemeMarkupStyleResolver(TigerConsole.CurrentTheme);
        var seg = Assert.Single(CliMarkupParser.Parse("[Accent]x[/]", baseStyle: null, styles));
        Assert.Equal("x", seg.Text);
        // Accent foreground comes from the theme; it is not the raw "Accent"... which isn't a color anyway.
        Assert.NotNull(seg.Style.Foreground);
    }
}
