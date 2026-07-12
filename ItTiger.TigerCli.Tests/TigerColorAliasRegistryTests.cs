using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Raw colour aliases: an app-scoped registry of names for concrete <see cref="CliColor"/> values.
/// Aliases are valid only in raw colour positions of markup, take precedence over enum names, and are
/// never shipped by TigerCli core.
/// </summary>
public sealed class TigerColorAliasRegistryTests
{
    // ---- Registry basics / validation ----

    [Fact]
    public void Register_And_TryResolve_AreCaseInsensitive()
    {
        var registry = new TigerColorAliasRegistry().Register("BrandOrange", CliColor.Sand2);

        Assert.True(registry.TryResolve("brandorange", out var color));
        Assert.Equal(CliColor.Sand2, color);
        Assert.True(registry.Contains("BRANDORANGE"));
    }

    [Fact]
    public void DefaultRegistry_IsEmpty()
    {
        Assert.True(new TigerColorAliasRegistry().IsEmpty);
    }

    [Fact]
    public void Remove_DeletesAlias()
    {
        var registry = new TigerColorAliasRegistry().Register("CompanyBlue", CliColor.Blue1);
        Assert.True(registry.Remove("companyblue"));
        Assert.False(registry.TryResolve("CompanyBlue", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Brand Orange")]   // whitespace
    [InlineData("Brand-Orange")]   // punctuation
    [InlineData("on")]             // reserved keyword
    [InlineData("Bold")]           // reserved keyword
    public void Register_RejectsInvalidNames(string alias)
    {
        Assert.Throws<ArgumentException>(() => new TigerColorAliasRegistry().Register(alias, CliColor.Red));
    }

    // ---- Markup integration: foreground / background positions ----

    [Fact]
    public void Alias_WorksInForegroundPosition()
    {
        var aliases = new TigerColorAliasRegistry().Register("BrandOrange", CliColor.Sand2);

        var seg = Assert.Single(
            CliMarkupParser.Parse("[BrandOrange]x[/]", baseStyle: null, styles: null, aliases));
        Assert.Equal(CliColor.Sand2, seg.Style.Foreground);
    }

    [Fact]
    public void Alias_WorksInBackgroundPosition()
    {
        var aliases = new TigerColorAliasRegistry().Register("CompanyBlue", CliColor.Blue1);

        var seg = Assert.Single(
            CliMarkupParser.Parse("[White on CompanyBlue]x[/]", baseStyle: null, styles: null, aliases));
        Assert.Equal(CliColor.White, seg.Style.Foreground);
        Assert.Equal(CliColor.Blue1, seg.Style.Background);
    }

    [Fact]
    public void Alias_WorksInBothPositions()
    {
        var aliases = new TigerColorAliasRegistry()
            .Register("BrandOrange", CliColor.Sand2)
            .Register("CompanyBlue", CliColor.Blue1);

        var seg = Assert.Single(
            CliMarkupParser.Parse("[BrandOrange on CompanyBlue]x[/]", baseStyle: null, styles: null, aliases));
        Assert.Equal(CliColor.Sand2, seg.Style.Foreground);
        Assert.Equal(CliColor.Blue1, seg.Style.Background);
    }

    // ---- Precedence: registered alias wins over CliColor enum name ----

    [Fact]
    public void Alias_TakesPrecedenceOver_CliColorEnumName()
    {
        // An app deliberately redefines "Red" as a brand colour; the alias wins.
        var aliases = new TigerColorAliasRegistry().Register("Red", CliColor.Sand2);

        var seg = Assert.Single(
            CliMarkupParser.Parse("[Red]x[/]", baseStyle: null, styles: null, aliases));
        Assert.Equal(CliColor.Sand2, seg.Style.Foreground);
    }

    [Fact]
    public void WithoutAlias_CliColorEnumName_IsTheFallback()
    {
        var aliases = new TigerColorAliasRegistry(); // empty

        var seg = Assert.Single(
            CliMarkupParser.Parse("[Red]x[/]", baseStyle: null, styles: null, aliases));
        Assert.Equal(CliColor.Red, seg.Style.Foreground);
    }

    // ---- Aliases are colours only, never semantic-style positions ----

    [Fact]
    public void UnknownAlias_StillThrows()
    {
        var aliases = new TigerColorAliasRegistry();
        Assert.Throws<FormatException>(
            () => CliMarkupParser.Parse("[NotAColour]x[/]", baseStyle: null, styles: null, aliases));
    }

    // ---- Isolation: two registries do not share state ----

    [Fact]
    public void Registries_AreIndependent()
    {
        var a = new TigerColorAliasRegistry().Register("BrandOrange", CliColor.Sand2);
        var b = new TigerColorAliasRegistry();

        Assert.True(a.Contains("BrandOrange"));
        Assert.False(b.Contains("BrandOrange"));
    }
}
