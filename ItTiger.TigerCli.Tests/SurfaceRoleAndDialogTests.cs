using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests
{
    /// <summary>
    /// Locks the surface-role model and the PanelSurface/DialogSurface relationship: tables use
    /// surface roles (Panel/Default/Alert), dialogs use the separate <see cref="ThemeStyle.DialogSurface"/>
    /// token, and the fallback chain DialogSurface -> PanelSurface -> Background holds.
    /// </summary>
    public class SurfaceRoleAndDialogTests : TestBase
    {
        private abstract class TestThemeBase : ThemeBase
        {
            protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Gray));
            protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
            protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.Cyan));
            protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
            protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
            protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
        }

        // Neither PanelSurface nor DialogSurface overridden.
        private sealed class BareTheme : TestThemeBase
        {
            public override string Name => "test-bare";
        }

        // PanelSurface overridden, DialogSurface not.
        private sealed class PanelOnlyTheme : TestThemeBase
        {
            public override string Name => "test-panel-only";
            protected override CliCellStyle? PanelSurface => new(new CliCharStyle(null, CliColor.DarkBlue));
        }

        // Both PanelSurface and DialogSurface overridden, to different colours.
        private sealed class PanelAndDialogTheme : TestThemeBase
        {
            public override string Name => "test-panel-and-dialog";
            protected override CliCellStyle? PanelSurface => new(new CliCharStyle(null, CliColor.DarkBlue));
            protected override CliCellStyle? DialogSurface => new(new CliCharStyle(null, CliColor.Magenta));
        }

        // Defines the Warning / Error accent inks but no explicit surfaces, so the warning/error
        // surfaces must be composed from those accents (foreground colour used as the surface background).
        private sealed class SemanticAccentTheme : TestThemeBase
        {
            public override string Name => "test-semantic-accent";
            protected override CliCellStyle? Warning => new(new CliCharStyle(CliColor.Yellow));
            protected override CliCellStyle? Error => new(new CliCharStyle(CliColor.Red));
        }

        // Overrides the warning/error surfaces directly: the explicit values win over composition.
        private sealed class SemanticSurfaceTheme : TestThemeBase
        {
            public override string Name => "test-semantic-surface";
            protected override CliCellStyle? WarningSurface => new(new CliCharStyle(CliColor.Black, CliColor.DarkYellow));
            protected override CliCellStyle? ErrorSurface => new(new CliCharStyle(CliColor.White, CliColor.DarkRed));
        }

        // ---- PanelSurface / DialogSurface fallback chain ----

        [Fact]
        public void PanelSurface_FallsBackToBackground_WhenNotOverridden()
        {
            var theme = new BareTheme();
            Assert.Equal(CliColor.Black, theme.Resolve(ThemeStyle.PanelSurface).CharStyle?.Background);
            Assert.Equal(CliColor.Black, theme.ResolveSurface(SurfaceRole.Panel).Background);
        }

        [Fact]
        public void DialogSurface_FallsBackToPanelSurface_WhenNotOverridden()
        {
            var theme = new PanelOnlyTheme();
            // DialogSurface is not set, so it inherits the PanelSurface.
            Assert.Equal(CliColor.DarkBlue, theme.Resolve(ThemeStyle.DialogSurface).CharStyle?.Background);
            Assert.Equal(CliColor.DarkBlue, theme.Resolve(ThemeStyle.PanelSurface).CharStyle?.Background);
        }

        [Fact]
        public void DialogSurface_CanBeOverridden_IndependentlyOfPanelSurface()
        {
            var theme = new PanelAndDialogTheme();
            Assert.Equal(CliColor.Magenta, theme.Resolve(ThemeStyle.DialogSurface).CharStyle?.Background);
            Assert.Equal(CliColor.DarkBlue, theme.Resolve(ThemeStyle.PanelSurface).CharStyle?.Background);
        }

        // ---- Warning / Error message-box surfaces ----

        [Fact]
        public void WarningAndErrorSurfaces_AreComposedFromAccents_WhenNotOverridden()
        {
            // No explicit surfaces: the warning/error surface backgrounds come from the Warning/Error
            // accent foregrounds, with readable foregrounds composed in.
            var theme = new SemanticAccentTheme();

            var warning = theme.Resolve(ThemeStyle.WarningSurface);
            Assert.Equal(CliColor.Yellow, warning.CharStyle?.Background);
            Assert.Equal(CliColor.Black, warning.CharStyle?.Foreground);

            var error = theme.Resolve(ThemeStyle.ErrorSurface);
            Assert.Equal(CliColor.Red, error.CharStyle?.Background);
            Assert.Equal(CliColor.White, error.CharStyle?.Foreground);
        }

        [Fact]
        public void WarningAndErrorSurfaces_RespectExplicitOverrides()
        {
            var theme = new SemanticSurfaceTheme();

            var warning = theme.Resolve(ThemeStyle.WarningSurface);
            Assert.Equal(CliColor.DarkYellow, warning.CharStyle?.Background);
            Assert.Equal(CliColor.Black, warning.CharStyle?.Foreground);

            var error = theme.Resolve(ThemeStyle.ErrorSurface);
            Assert.Equal(CliColor.DarkRed, error.CharStyle?.Background);
            Assert.Equal(CliColor.White, error.CharStyle?.Foreground);
        }

        [Theory]
        [InlineData(typeof(DarkTheme))]
        [InlineData(typeof(LightTheme))]
        [InlineData(typeof(TigerBlueTheme))]
        public void BuiltInThemes_DefineDistinctWarningAndErrorSurfaces(System.Type themeType)
        {
            var theme = (ThemeBase)Activator.CreateInstance(themeType)!;

            var warningBg = theme.Resolve(ThemeStyle.WarningSurface).CharStyle?.Background;
            var errorBg = theme.Resolve(ThemeStyle.ErrorSurface).CharStyle?.Background;
            var dialogBg = theme.Resolve(ThemeStyle.DialogSurface).CharStyle?.Background;

            // Each semantic surface is a real, attention-grabbing colour distinct from the plain dialog
            // surface and from each other.
            Assert.NotNull(warningBg);
            Assert.NotNull(errorBg);
            Assert.NotEqual(warningBg, errorBg);
            Assert.NotEqual(dialogBg, warningBg);
            Assert.NotEqual(dialogBg, errorBg);
        }

        [Fact]
        public void TigerBlue_UsesExpected256ColorRoles()
        {
            var theme = new TigerBlueTheme();

            Assert.Equal(CliColor.Gray31, theme.Resolve(ThemeStyle.Status).CharStyle?.Background);
            Assert.Equal(CliColor.Orange, theme.Resolve(ThemeStyle.WarningSurface).CharStyle?.Background);
            Assert.Equal(CliColor.White, theme.Resolve(ThemeStyle.Alert).CharStyle?.Foreground);
            Assert.Equal(CliColor.RoyalBlue, theme.Resolve(ThemeStyle.ButtonMarker).CharStyle?.Foreground);
            Assert.Equal(CliColor.Navy, theme.Resolve(ThemeStyle.PanelSurface).CharStyle?.Background);
        }

        // ---- Table styles use surface roles, not DialogSurface ----

        [Fact]
        public void TableStyle_UsesPanelSurface_NotDialog()
        {
            // DialogSurface is Magenta, PanelSurface is DarkBlue. Roma sits on the Panel surface, so
            // its body background must be the Panel colour — proving table styles never read DialogSurface.
            var theme = new PanelAndDialogTheme();
            var roma = CliTableStyleRecipe.Roma.Resolve(theme);

            Assert.Equal(CliColor.DarkBlue, roma.DefaultCellStyle?.CharStyle?.Background);
            Assert.NotEqual(CliColor.Magenta, roma.DefaultCellStyle?.CharStyle?.Background);
        }

        [Fact]
        public void DefaultSurface_TitleAndBody_ShareBaseBackground()
        {
            var theme = new BareTheme();
            // A default-surface recipe: body and title both resolve to the base background.
            var napoli = CliTableStyleRecipe.Napoli.Resolve(theme);
            Assert.Equal(CliColor.Black, napoli.DefaultCellStyle?.CharStyle?.Background);
            Assert.Equal(CliColor.Black, napoli.TitleStyle?.CharStyle?.Background);
        }
    }
}
