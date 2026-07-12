using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the CRUD semantic value roles (<see cref="ThemeStyle.Key"/>, <see cref="ThemeStyle.Value"/>,
/// <see cref="ThemeStyle.Path"/>, <see cref="ThemeStyle.Link"/>) across the built-in themes: each maps
/// to a base token unless a theme supplies a deliberate semantic override.
/// </summary>
public sealed class ThemeSemanticValueStyleTests
{
    public static IEnumerable<object[]> BuiltInThemes()
    {
        yield return new object[] { new DarkTheme() };
        yield return new object[] { new LightTheme() };
        yield return new object[] { new TigerBlueTheme() };
    }

    [Theory]
    [MemberData(nameof(BuiltInThemes))]
    public void Key_UsesExpectedForeground(ITheme theme)
    {
        var expected = theme switch
        {
            DarkTheme => CliColor.Green,
            TigerBlueTheme => CliColor.LawnGreen2,
            _ => theme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground,
        };

        Assert.Equal(
            expected,
            theme.Resolve(ThemeStyle.Key).CharStyle?.Foreground);
    }

    [Theory]
    [MemberData(nameof(BuiltInThemes))]
    public void Value_FallsBackToTextForeground(ITheme theme)
    {
        Assert.Equal(
            theme.Resolve(ThemeStyle.Text).CharStyle?.Foreground,
            theme.Resolve(ThemeStyle.Value).CharStyle?.Foreground);
    }

    [Theory]
    [MemberData(nameof(BuiltInThemes))]
    public void Path_UsesExpectedForeground(ITheme theme)
    {
        var expected = theme is TigerBlueTheme
            ? CliColor.SlateGray
            : theme.Resolve(ThemeStyle.MutedText).CharStyle?.Foreground;

        Assert.Equal(
            expected,
            theme.Resolve(ThemeStyle.Path).CharStyle?.Foreground);
    }

    [Theory]
    [MemberData(nameof(BuiltInThemes))]
    public void Link_UsesExpectedForegroundAndDecoration(ITheme theme)
    {
        var link = theme.Resolve(ThemeStyle.Link).CharStyle;
        var expectedForeground = theme is DarkTheme or TigerBlueTheme
            ? CliColor.Blue
            : theme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground;

        Assert.Equal(expectedForeground, link?.Foreground);
        Assert.True(link!.Value.Decorations.HasFlag(CliTextDecoration.Underline));
    }

    [Theory]
    [MemberData(nameof(BuiltInThemes))]
    public void Heading_IsAccentForeground_WithBold(ITheme theme)
    {
        var heading = theme.Resolve(ThemeStyle.Heading).CharStyle;

        Assert.Equal(theme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground, heading?.Foreground);
        Assert.True(heading!.Value.Decorations.HasFlag(CliTextDecoration.Bold));
    }

    [Theory]
    [MemberData(nameof(BuiltInThemes))]
    public void SemanticValueRoles_AreForegroundOnly(ITheme theme)
    {
        // Backgrounds stay null so the table/detail surface shows through.
        Assert.Null(theme.Resolve(ThemeStyle.Key).CharStyle?.Background);
        Assert.Null(theme.Resolve(ThemeStyle.Value).CharStyle?.Background);
        Assert.Null(theme.Resolve(ThemeStyle.Path).CharStyle?.Background);
        Assert.Null(theme.Resolve(ThemeStyle.Link).CharStyle?.Background);
        Assert.Null(theme.Resolve(ThemeStyle.Heading).CharStyle?.Background);
    }

    [Fact]
    public void SemanticValueRoles_AreThemeOverridable()
    {
        var theme = new CustomValueTheme();

        Assert.Equal(CliColor.Yellow, theme.Resolve(ThemeStyle.Key).CharStyle?.Foreground);
        Assert.Equal(CliColor.Magenta, theme.Resolve(ThemeStyle.Path).CharStyle?.Foreground);
        Assert.Equal(CliColor.Green, theme.Resolve(ThemeStyle.Heading).CharStyle?.Foreground);
    }

    // A theme that overrides the semantic value roles directly (not via fallback).
    private sealed class CustomValueTheme : ThemeBase
    {
        public override string Name => "custom-value";
        protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
        protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.Cyan));
        protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
        protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
        protected override CliCellStyle? Key => new(new CliCharStyle(CliColor.Yellow));
        protected override CliCellStyle? Path => new(new CliCharStyle(CliColor.Magenta));
        protected override CliCellStyle? Heading => new(new CliCharStyle(CliColor.Green));
    }
}
