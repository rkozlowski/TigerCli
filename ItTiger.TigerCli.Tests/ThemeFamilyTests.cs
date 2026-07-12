using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Theme families classify every theme as dark- or light-family contrast metadata (not inheritance),
/// which custom semantic styles use to pick a dark/light override.
/// </summary>
public sealed class ThemeFamilyTests
{
    [Fact]
    public void FrameworkThemes_DeclareTheirFamily()
    {
        Assert.Equal(TigerThemeFamily.Dark, new DarkTheme().Family);
        Assert.Equal(TigerThemeFamily.Light, new LightTheme().Family);
        // TigerBlue is a signature theme but belongs to the dark family.
        Assert.Equal(TigerThemeFamily.Dark, new TigerBlueTheme().Family);
    }

    private sealed class FamilylessTheme : ThemeBase
    {
        public override string Name => "familyless";
        protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
        protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.Cyan));
        protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
        protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
    }

    [Fact]
    public void CustomTheme_DefaultsToDarkFamily_WhenNotOverridden()
    {
        Assert.Equal(TigerThemeFamily.Dark, new FamilylessTheme().Family);
    }

    private sealed class LightFamilyCustomTheme : FamilylessThemeBase
    {
        public override string Name => "company-light";
        public override TigerThemeFamily Family => TigerThemeFamily.Light;
    }

    // Shared base for the light-family declaration test.
    private abstract class FamilylessThemeBase : ThemeBase
    {
        protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Black));
        protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
        protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.DarkBlue));
        protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.DarkGray));
        protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.White, CliColor.DarkBlue));
        protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.White));
    }

    [Fact]
    public void CustomTheme_CanDeclareLightFamily()
    {
        ITheme theme = new LightFamilyCustomTheme();
        Assert.Equal(TigerThemeFamily.Light, theme.Family);
    }
}
