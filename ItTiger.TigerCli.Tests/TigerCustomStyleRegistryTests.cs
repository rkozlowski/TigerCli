using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Custom semantic styles: app-defined markup tokens with a required framework <see cref="ThemeStyle"/>
/// base fallback, optional dark/light family overrides, and optional exact theme-name overrides.
/// Custom styles resolve through the active theme and are used as single-token semantic tags.
/// </summary>
public sealed class TigerCustomStyleRegistryTests
{
    private static (CliColor? fg, CliColor? bg, CliTextDecoration deco) Resolve(
        TigerCustomStyleRegistry registry, ITheme theme, string name)
    {
        var resolver = new ThemeMarkupStyleResolver(theme, registry);
        Assert.True(resolver.TryResolve(name, out var fg, out var bg, out var deco), $"'{name}' should resolve.");
        return (fg, bg, deco);
    }

    // ---- Validation ----

    [Theory]
    [InlineData("Accent")]   // framework semantic token
    [InlineData("Panel")]
    [InlineData("Alert")]
    [InlineData("Heading")]  // CRUD/structured-output role tokens
    [InlineData("Key")]
    [InlineData("Value")]
    [InlineData("Path")]
    [InlineData("Link")]
    public void Register_RejectsFrameworkTokenNames(string reserved)
    {
        Assert.Throws<ArgumentException>(
            () => new TigerCustomStyleRegistry().Register(reserved, ThemeStyle.Accent));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Connection Name")]
    [InlineData("on")]
    [InlineData("b")]   // standalone short decoration aliases are reserved
    [InlineData("i")]
    [InlineData("u")]
    public void Register_RejectsInvalidNames(string name)
    {
        Assert.Throws<ArgumentException>(
            () => new TigerCustomStyleRegistry().Register(name, ThemeStyle.Accent));
    }

    [Fact]
    public void RegisterForTheme_RequiresExistingStyle()
    {
        var registry = new TigerCustomStyleRegistry();
        Assert.Throws<ArgumentException>(
            () => registry.RegisterForTheme("ConnectionName", "dark", new CliCellStyle(new CliCharStyle(CliColor.Red))));
    }

    // ---- Resolution via the base ThemeStyle (no overrides) ----

    [Fact]
    public void CustomStyle_ResolvesViaBaseThemeStyle_PerTheme()
    {
        var registry = new TigerCustomStyleRegistry().Register("ConnectionName", ThemeStyle.Accent);

        // DarkTheme Accent is Cyan; LightTheme Accent is DarkBlue.
        Assert.Equal(CliColor.Cyan, Resolve(registry, new DarkTheme(), "ConnectionName").fg);
        Assert.Equal(CliColor.DarkBlue, Resolve(registry, new LightTheme(), "ConnectionName").fg);
    }

    [Fact]
    public void CustomStyle_BaseStyleForegroundOnly_LeavesBackgroundNull()
    {
        var registry = new TigerCustomStyleRegistry().Register("ConnectionName", ThemeStyle.Accent);

        var (_, bg, _) = Resolve(registry, new DarkTheme(), "ConnectionName");
        Assert.Null(bg); // Accent is foreground-only, so the surrounding background is preserved.
    }

    // ---- Dark/light family overrides ----

    [Fact]
    public void CustomStyle_FamilyOverrides_ChosenByThemeFamily()
    {
        var registry = new TigerCustomStyleRegistry().Register(
            "ConnectionName",
            ThemeStyle.Accent,
            darkStyle: new CliCellStyle(new CliCharStyle(CliColor.Green)),
            lightStyle: new CliCellStyle(new CliCharStyle(CliColor.Magenta)));

        Assert.Equal(CliColor.Green, Resolve(registry, new DarkTheme(), "ConnectionName").fg);
        Assert.Equal(CliColor.Magenta, Resolve(registry, new LightTheme(), "ConnectionName").fg);
        // TigerBlue is dark-family, so it uses the dark override.
        Assert.Equal(CliColor.Green, Resolve(registry, new TigerBlueTheme(), "ConnectionName").fg);
    }

    [Fact]
    public void CustomStyle_FallsBackToBase_WhenNoFamilyOverrideForThatFamily()
    {
        // Only a dark override; a light-family theme falls back to the base ThemeStyle.
        var registry = new TigerCustomStyleRegistry().Register(
            "ConnectionName",
            ThemeStyle.Accent,
            darkStyle: new CliCellStyle(new CliCharStyle(CliColor.Green)));

        Assert.Equal(CliColor.Green, Resolve(registry, new DarkTheme(), "ConnectionName").fg);
        Assert.Equal(CliColor.DarkBlue, Resolve(registry, new LightTheme(), "ConnectionName").fg); // base Accent
    }

    // ---- Exact theme-name override wins over family override ----

    [Fact]
    public void ExactThemeOverride_WinsOver_FamilyOverride()
    {
        var registry = new TigerCustomStyleRegistry()
            .Register(
                "ConnectionName",
                ThemeStyle.Accent,
                darkStyle: new CliCellStyle(new CliCharStyle(CliColor.Green)))
            .RegisterForTheme(
                "ConnectionName",
                "tiger-blue",
                new CliCellStyle(new CliCharStyle(CliColor.Yellow)));

        // tiger-blue is dark-family, but the exact theme override wins over the dark override.
        Assert.Equal(CliColor.Yellow, Resolve(registry, new TigerBlueTheme(), "ConnectionName").fg);
        // A different dark-family theme still uses the dark family override.
        Assert.Equal(CliColor.Green, Resolve(registry, new DarkTheme(), "ConnectionName").fg);
    }

    // ---- Decorations ----

    [Fact]
    public void CustomStyle_MayIncludeTextDecorations()
    {
        var registry = new TigerCustomStyleRegistry().Register(
            "DangerZone",
            ThemeStyle.Error,
            darkStyle: new CliCellStyle(new CliCharStyle(
                CliColor.Red, decorations: CliTextDecoration.Bold | CliTextDecoration.Underline)));

        var (_, _, deco) = Resolve(registry, new DarkTheme(), "DangerZone");
        Assert.Equal(CliTextDecoration.Bold | CliTextDecoration.Underline, deco);
    }

    [Fact]
    public void CustomStyle_ForegroundBackground_FromOverride()
    {
        var registry = new TigerCustomStyleRegistry().Register(
            "EnvironmentProd",
            ThemeStyle.Alert,
            darkStyle: new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkRed)));

        var (fg, bg, _) = Resolve(registry, new DarkTheme(), "EnvironmentProd");
        Assert.Equal(CliColor.White, fg);
        Assert.Equal(CliColor.DarkRed, bg);
    }

    // ---- Markup integration: single-token semantic tag ----

    [Fact]
    public void CustomStyle_WorksAsSingleTokenMarkupTag()
    {
        var registry = new TigerCustomStyleRegistry().Register("ConnectionName", ThemeStyle.Accent);
        var resolver = new ThemeMarkupStyleResolver(new DarkTheme(), registry);

        var seg = Assert.Single(CliMarkupParser.Parse("[ConnectionName]Local[/]", baseStyle: null, resolver));
        Assert.Equal("Local", seg.Text);
        Assert.Equal(CliColor.Cyan, seg.Style.Foreground);
    }

    [Fact]
    public void CustomStyle_ComposesUnderSurface()
    {
        // [Panel][ConnectionName]Local[/][/] — Panel background kept, ConnectionName foreground applied.
        var registry = new TigerCustomStyleRegistry().Register("ConnectionName", ThemeStyle.Accent);
        var resolver = new ThemeMarkupStyleResolver(new TigerBlueTheme(), registry);

        var seg = Assert.Single(
            CliMarkupParser.Parse("[Panel][ConnectionName]Local[/][/]", baseStyle: null, resolver));
        Assert.Equal(CliColor.Cyan, seg.Style.Foreground);     // ConnectionName -> Accent
        Assert.Equal(CliColor.Navy, seg.Style.Background);     // Panel surface preserved
    }

    // ---- Custom styles are semantic, never raw colours ----

    [Fact]
    public void CustomStyle_OnColour_IsInvalid()
    {
        var registry = new TigerCustomStyleRegistry().Register("ConnectionName", ThemeStyle.Accent);
        var resolver = new ThemeMarkupStyleResolver(new DarkTheme(), registry);

        Assert.Throws<FormatException>(
            () => CliMarkupParser.Parse("[ConnectionName on Blue]x[/]", baseStyle: null, resolver));
    }

    [Fact]
    public void CustomStyleName_IsNotARawColour()
    {
        // Without a resolver, a custom style name is just an unknown raw colour.
        Assert.Throws<FormatException>(() => CliMarkupParser.Parse("[ConnectionName]x[/]"));
    }
}
