using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the curated Phase 1 semantic markup tokens: channel modes (foreground-only vs
/// foreground/background), Panel/Dialog surface resolution, per-theme differences, and safe
/// fallbacks for Error/Alert under a minimal theme.
/// </summary>
public sealed class ThemeMarkupStyleResolverTests
{
    // Minimal theme: only the required base tokens. No Error/Alert/Panel/Dialog/AlertSurface.
    private sealed class MinimalTheme : ThemeBase
    {
        public override string Name => "markup-minimal";
        protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
        protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.Cyan));
        protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
        protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
    }

    // PanelSurface and DialogSurface overridden to different colours.
    private sealed class DistinctSurfaceTheme : ThemeBase
    {
        public override string Name => "markup-distinct-surface";
        protected override CliCellStyle Text => new(new CliCharStyle(CliColor.White));
        protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
        protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.Cyan));
        protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
        protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
        protected override CliCellStyle? PanelSurface => new(new CliCharStyle(null, CliColor.DarkBlue));
        protected override CliCellStyle? DialogSurface => new(new CliCharStyle(null, CliColor.Magenta));
    }

    // Theme whose Accent ink carries a decoration, proving a resolved semantic style can include
    // bold/italic/underline alongside its colours.
    private sealed class DecoratedAccentTheme : ThemeBase
    {
        public override string Name => "markup-decorated-accent";
        protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
        protected override CliCellStyle Accent =>
            new(new CliCharStyle(CliColor.Cyan, decorations: CliTextDecoration.Bold | CliTextDecoration.Underline));
        protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
        protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
    }

    private static (CliColor? fg, CliColor? bg) Resolve(IMarkupStyleResolver r, string name)
    {
        Assert.True(r.TryResolve(name, out var fg, out var bg, out _), $"'{name}' should resolve.");
        return (fg, bg);
    }

    [Fact]
    public void SemanticStyle_CanCarryDecorations()
    {
        var r = new ThemeMarkupStyleResolver(new DecoratedAccentTheme());

        Assert.True(r.TryResolve("Accent", out var fg, out _, out var decorations));
        Assert.Equal(CliColor.Cyan, fg);
        Assert.Equal(CliTextDecoration.Bold | CliTextDecoration.Underline, decorations);
    }

    [Fact]
    public void SemanticStyle_WithoutDecorations_ReturnsNone()
    {
        var r = new ThemeMarkupStyleResolver(new DarkTheme());
        Assert.True(r.TryResolve("Accent", out _, out _, out var decorations));
        Assert.Equal(CliTextDecoration.None, decorations);
    }

    // ---- Channel modes ----

    [Fact]
    public void ForegroundOnlyTokens_ReturnForeground_AndNullBackground()
    {
        var r = new ThemeMarkupStyleResolver(new DarkTheme());

        Assert.Equal(((CliColor?)CliColor.Cyan, (CliColor?)null), Resolve(r, "Accent"));
        Assert.Equal(((CliColor?)CliColor.DarkGray, (CliColor?)null), Resolve(r, "Muted"));
        Assert.Equal(((CliColor?)CliColor.Red, (CliColor?)null), Resolve(r, "Error"));
    }

    [Fact]
    public void ForegroundBackgroundTokens_ReturnBoth()
    {
        var r = new ThemeMarkupStyleResolver(new DarkTheme());

        Assert.Equal(((CliColor?)CliColor.White, (CliColor?)CliColor.DarkRed), Resolve(r, "Alert"));
        Assert.Equal(((CliColor?)CliColor.Black, (CliColor?)CliColor.Green), Resolve(r, "Selected"));
    }

    // ---- Panel / Dialog surface resolution ----

    [Fact]
    public void Panel_UsesPanelSurface_WithTextForeground()
    {
        var theme = new TigerBlueTheme();
        var r = new ThemeMarkupStyleResolver(theme);

        var (fg, bg) = Resolve(r, "Panel");
        Assert.Equal(theme.Resolve(ThemeStyle.PanelSurface).CharStyle?.Background, bg); // DarkBlue
        Assert.Equal(theme.Resolve(ThemeStyle.Text).CharStyle?.Foreground, fg);         // Gray (surface has no fg)
    }

    [Fact]
    public void Dialog_UsesDialogSurface()
    {
        var theme = new DistinctSurfaceTheme();
        var r = new ThemeMarkupStyleResolver(theme);

        var (_, bg) = Resolve(r, "Dialog");
        Assert.Equal(CliColor.Magenta, bg); // DialogSurface, not PanelSurface (DarkBlue)
    }

    [Fact]
    public void DialogMarkup_CanDifferFromPanelMarkup_WhenDialogSurfaceOverriddenIndependently()
    {
        var r = new ThemeMarkupStyleResolver(new DistinctSurfaceTheme());

        var (_, panelBg) = Resolve(r, "Panel");
        var (_, dialogBg) = Resolve(r, "Dialog");

        Assert.Equal(CliColor.DarkBlue, panelBg);
        Assert.Equal(CliColor.Magenta, dialogBg);
        Assert.NotEqual(panelBg, dialogBg);
    }

    [Fact]
    public void Dialog_FallsBackToPanelSurface_WhenDialogSurfaceNotOverridden()
    {
        // TigerBlue overrides PanelSurface (Navy) but not DialogSurface.
        var r = new ThemeMarkupStyleResolver(new TigerBlueTheme());

        Assert.Equal((CliColor?)CliColor.Navy, Resolve(r, "Panel").bg);
        Assert.Equal((CliColor?)CliColor.Navy, Resolve(r, "Dialog").bg);
    }

    // ---- Per-theme differences ----

    [Fact]
    public void SameToken_ResolvesDifferently_PerTheme()
    {
        var dark = new ThemeMarkupStyleResolver(new DarkTheme());
        var tigerBlue = new ThemeMarkupStyleResolver(new TigerBlueTheme());

        // PanelSurface falls back to Background (Black) on dark, but is Navy on tiger-blue.
        Assert.Equal((CliColor?)CliColor.Black, Resolve(dark, "Panel").bg);
        Assert.Equal((CliColor?)CliColor.Navy, Resolve(tigerBlue, "Panel").bg);
    }

    // ---- Fallbacks under a minimal theme ----

    [Fact]
    public void Error_And_Alert_ResolveWithoutThrowing_UnderMinimalTheme()
    {
        var r = new ThemeMarkupStyleResolver(new MinimalTheme());

        // Error falls back to Warning -> Accent (Cyan); foreground-only.
        Assert.Equal(((CliColor?)CliColor.Cyan, (CliColor?)null), Resolve(r, "Error"));

        // Alert is composed: Background bg (Black) + Accent fg (Cyan) since no AlertSurface/Error.
        Assert.Equal(((CliColor?)CliColor.Cyan, (CliColor?)CliColor.Black), Resolve(r, "Alert"));
    }

    // ---- Unknown / casing ----

    [Fact]
    public void UnknownName_ReturnsFalse()
    {
        var r = new ThemeMarkupStyleResolver(new DarkTheme());
        Assert.False(r.TryResolve("DefinitelyNotAToken", out _, out _, out _));
    }

    [Fact]
    public void Resolution_IsCaseInsensitive()
    {
        var r = new ThemeMarkupStyleResolver(new DarkTheme());
        Assert.Equal(((CliColor?)CliColor.Cyan, (CliColor?)null), Resolve(r, "accent"));
        Assert.Equal(((CliColor?)CliColor.Cyan, (CliColor?)null), Resolve(r, "ACCENT"));
    }

    // ---- Semantic CRUD/structured-output role tokens (Heading/Key/Value/Path/Link) ----

    [Fact]
    public void SemanticRoleTokens_ResolveThroughTheme()
    {
        var theme = new DarkTheme();
        var r = new ThemeMarkupStyleResolver(theme);

        Assert.Equal((theme.Resolve(ThemeStyle.Key).CharStyle?.Foreground, (CliColor?)null), Resolve(r, "Key"));
        Assert.Equal((theme.Resolve(ThemeStyle.Value).CharStyle?.Foreground, (CliColor?)null), Resolve(r, "Value"));
        Assert.Equal((theme.Resolve(ThemeStyle.Path).CharStyle?.Foreground, (CliColor?)null), Resolve(r, "Path"));
        Assert.Equal((theme.Resolve(ThemeStyle.Link).CharStyle?.Foreground, (CliColor?)null), Resolve(r, "Link"));
        Assert.Equal((theme.Resolve(ThemeStyle.Heading).CharStyle?.Foreground, (CliColor?)null), Resolve(r, "Heading"));
    }

    [Fact]
    public void LinkToken_CarriesUnderline_HeadingToken_CarriesBold()
    {
        var r = new ThemeMarkupStyleResolver(new DarkTheme());

        Assert.True(r.TryResolve("Link", out _, out _, out var linkDeco));
        Assert.True(linkDeco.HasFlag(CliTextDecoration.Underline));

        Assert.True(r.TryResolve("Heading", out _, out _, out var headingDeco));
        Assert.True(headingDeco.HasFlag(CliTextDecoration.Bold));
    }

    [Fact]
    public void SemanticRoleTokens_AreDeveloperOverridable()
    {
        // The name is framework-known but the visual meaning is theme/configurable.
        var r = new ThemeMarkupStyleResolver(new OverriddenRolesTheme());

        Assert.Equal((CliColor?)CliColor.Yellow, Resolve(r, "Key").fg);
        Assert.Equal((CliColor?)CliColor.Magenta, Resolve(r, "Path").fg);
        Assert.Equal((CliColor?)CliColor.Green, Resolve(r, "Heading").fg);
        Assert.Equal((CliColor?)CliColor.Blue, Resolve(r, "Link").fg);
    }

    [Fact]
    public void SemanticRoleTokens_AreCaseInsensitive()
    {
        var r = new ThemeMarkupStyleResolver(new DarkTheme());
        Assert.True(r.TryResolve("heading", out _, out _, out _));
        Assert.True(r.TryResolve("KEY", out _, out _, out _));
        Assert.True(r.TryResolve("Path", out _, out _, out _));
    }

    // A theme overriding the CRUD/structured-output roles directly (proving they are configurable).
    private sealed class OverriddenRolesTheme : ThemeBase
    {
        public override string Name => "markup-overridden-roles";
        protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
        protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.Cyan));
        protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
        protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
        protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
        protected override CliCellStyle? Key => new(new CliCharStyle(CliColor.Yellow));
        protected override CliCellStyle? Path => new(new CliCharStyle(CliColor.Magenta));
        protected override CliCellStyle? Heading => new(new CliCharStyle(CliColor.Green));
        protected override CliCellStyle? Link => new(new CliCharStyle(CliColor.Blue));
    }
}

/// <summary>
/// Integration: the theme-backed resolver is wired into the markup call sites, so semantic tokens
/// resolve through the current theme instead of throwing as unknown colours.
/// </summary>
public sealed class ThemeAwareMarkupIntegrationTests
{
    [Fact]
    public void MarkupLine_AcceptsSemanticTokens_UnderCurrentTheme()
    {
        var (stdout, _) = WithTheme(new DarkTheme(),
            () => CaptureConsole(() => TigerConsole.MarkupLine("[Accent]Name[/]: value")));

        Assert.Equal("Name: value" + Environment.NewLine, stdout);
    }

    [Fact]
    public void MarkupErrorLine_AcceptsSemanticTokens_UnderCurrentTheme()
    {
        var (_, stderr) = WithTheme(new DarkTheme(),
            () => CaptureConsole(() => TigerConsole.MarkupErrorLine("[Alert]Error![/] failed")));

        Assert.Equal("Error! failed" + Environment.NewLine, stderr);
    }

    [Fact]
    public void Grid_PreformattedMarkup_UsesSemanticTokens()
    {
        var original = TigerConsole.CurrentTheme;
        try
        {
            TigerConsole.CurrentTheme = new TigerBlueTheme();

            var grid = new CliGrid(1, 1);
            grid.Set(0, 0, "[Success]ok[/]", new CliCellStyle { FormattingMode = CliFormattingMode.Preformatted });

            var text = string.Join("\n", TigerConsole.RenderGridToLines(grid));
            Assert.Contains("ok", text);       // tags consumed by the semantic resolver
            Assert.DoesNotContain("Success", text);
        }
        finally
        {
            TigerConsole.CurrentTheme = original;
        }
    }

    [Fact]
    public void Grid_PreformattedMarkup_UnknownTag_StillThrows()
    {
        var grid = new CliGrid(1, 1);
        grid.Set(0, 0, "[Bogus]x[/]", new CliCellStyle { FormattingMode = CliFormattingMode.Preformatted });

        Assert.Throws<FormatException>(() => TigerConsole.RenderGridToLines(grid));
    }

    [Fact]
    public void MarkupLine_SemanticRoleTokens_AreAccepted_UnderCurrentTheme()
    {
        var (stdout, _) = WithTheme(new DarkTheme(),
            () => CaptureConsole(() => TigerConsole.MarkupLine("[Heading]Devices[/] [Key]cam-1[/] [Path]/media[/]")));

        Assert.Equal("Devices cam-1 /media" + Environment.NewLine, stdout);
    }

    [Fact]
    public void MarkupLine_Link_KeepsVisibleCopyableUrl()
    {
        var (stdout, _) = WithTheme(new DarkTheme(),
            () => CaptureConsole(() => TigerConsole.MarkupLine("[Link]https://example.com[/]")));

        Assert.Equal("https://example.com" + Environment.NewLine, stdout);
    }

    [Fact]
    public void MarkupLine_ShortDecorationAliases_AreAccepted()
    {
        var (stdout, _) = WithTheme(new DarkTheme(),
            () => CaptureConsole(() => TigerConsole.MarkupLine("[b]A[/][i]B[/][u]C[/]")));

        Assert.Equal("ABC" + Environment.NewLine, stdout);
    }

    private static T WithTheme<T>(ItTiger.TigerCli.Tui.Abstractions.ITheme theme, Func<T> action)
    {
        var original = TigerConsole.CurrentTheme;
        try
        {
            TigerConsole.CurrentTheme = theme;
            return action();
        }
        finally
        {
            TigerConsole.CurrentTheme = original;
        }
    }

    private static (string Stdout, string Stderr) CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            action();
            return (stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
