using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests
{
    /// <summary>
    /// Locks the predefined city table styles (<see cref="CliTableStyles"/>) and their boring aliases
    /// against accidental drift: which styles exist, that removed tasting variants are not exposed,
    /// alias targets, orientation applicability, the title base-background rule, alt-only data styling,
    /// and theme-token fallback.
    /// </summary>
    public class CliTableStylesTests : TestBase
    {
        private static readonly ITheme Blue = new TigerBlueTheme();

        // A theme that overrides only the required base tokens and none of the new table/semantic
        // tokens, so the fallback chain in ThemeBase.Resolve is exercised.
        private sealed class FallbackTheme : ThemeBase
        {
            public override string Name => "test-fallback";
            protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Gray));
            protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
            protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.Cyan));
            protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
            protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
            protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
        }

        // ---- Existence / removed variants ----

        [Fact]
        public void CityNames_AreExactlyTheTenExpectedStyles()
        {
            string[] expected =
                ["Roma", "Milano", "Napoli", "Torino", "Genova", "Bologna", "Palermo", "Parma", "Verona", "Lucca"];

            Assert.Equal(expected.OrderBy(n => n), CliTableStyles.CityNames.OrderBy(n => n));
        }

        [Fact]
        public void EveryCityName_BuildsWithoutError()
        {
            foreach (var name in CliTableStyles.CityNames)
                Assert.NotNull(CliTableStyles.Create(name, Blue));
        }

        [Theory]
        [InlineData("Venezia")]
        [InlineData("Firenze")]
        [InlineData("Pisa")]
        public void RemovedTastingVariants_AreNotExposedAsStyles(string removed)
        {
            Assert.DoesNotContain(removed, CliTableStyles.CityNames);
            Assert.Throws<ArgumentException>(() => CliTableStyles.Create(removed, Blue));
            Assert.Throws<ArgumentException>(() => CliTableStyles.OrientationSupport(removed));
        }

        // ---- Aliases ----

        [Fact]
        public void Aliases_MapToIntendedCityStyles()
        {
            Assert.Equal(CliTableStylePreset.Roma, CliTableStyles.Canonicalize(CliTableStylePreset.Default));
            Assert.Equal(CliTableStylePreset.Milano, CliTableStyles.Canonicalize(CliTableStylePreset.Light));
            Assert.Equal(CliTableStylePreset.Napoli, CliTableStyles.Canonicalize(CliTableStylePreset.Grid));
            Assert.Equal(CliTableStylePreset.Palermo, CliTableStyles.Canonicalize(CliTableStylePreset.Alert));
            Assert.Equal(CliTableStylePreset.Parma, CliTableStyles.Canonicalize(CliTableStylePreset.Condensed));
            Assert.Equal(CliTableStylePreset.Lucca, CliTableStyles.Canonicalize(CliTableStylePreset.Details));
            Assert.Equal(CliTableStylePreset.Verona, CliTableStyles.Canonicalize(CliTableStylePreset.DetailsCondensed));
            Assert.Equal(CliTableStylePreset.Milano, CliTableStyles.Canonicalize(CliTableStylePreset.List));
            Assert.Equal(8, CliTableStyles.AliasMap.Count);
        }

        [Fact]
        public void Alias_ProducesSameRecipeAsTargetCity()
        {
            AssertSameRecipe(CliTableStyles.Create(CliTableStylePreset.Default, Blue), CliTableStyles.Create(CliTableStylePreset.Roma, Blue));
            AssertSameRecipe(CliTableStyles.Create(CliTableStylePreset.Light, Blue), CliTableStyles.Create(CliTableStylePreset.Milano, Blue));
            AssertSameRecipe(CliTableStyles.Create(CliTableStylePreset.Grid, Blue), CliTableStyles.Create(CliTableStylePreset.Napoli, Blue));
            AssertSameRecipe(CliTableStyles.Create(CliTableStylePreset.Alert, Blue), CliTableStyles.Create(CliTableStylePreset.Palermo, Blue));
            AssertSameRecipe(CliTableStyles.Create(CliTableStylePreset.Condensed, Blue), CliTableStyles.Create(CliTableStylePreset.Parma, Blue));
            AssertSameRecipe(CliTableStyles.Create(CliTableStylePreset.Details, Blue), CliTableStyles.Create(CliTableStylePreset.Lucca, Blue));
            AssertSameRecipe(CliTableStyles.Create(CliTableStylePreset.DetailsCondensed, Blue), CliTableStyles.Create(CliTableStylePreset.Verona, Blue));
        }

        private static void AssertSameRecipe(CliTableStyle alias, CliTableStyle city)
        {
            Assert.Equal(city.Orientation, alias.Orientation);
            Assert.Equal(city.OrientationSupport, alias.OrientationSupport);
            Assert.Equal(city.AlternateRecordsEnabled, alias.AlternateRecordsEnabled);
            Assert.Equal(city.FrameConfig.OuterFrame.Style, alias.FrameConfig.OuterFrame.Style);
            Assert.Equal(city.FrameConfig.BetweenElements.Style, alias.FrameConfig.BetweenElements.Style);
            Assert.Equal(city.FrameConfig.CharStyle?.Foreground, alias.FrameConfig.CharStyle?.Foreground);
            Assert.Equal(city.DefaultCellStyle?.CharStyle?.Background, alias.DefaultCellStyle?.CharStyle?.Background);
            Assert.Equal(city.TitleStyle?.CharStyle?.Foreground, alias.TitleStyle?.CharStyle?.Foreground);
            Assert.Equal(city.DataAltStyle?.CharStyle?.Background, alias.DataAltStyle?.CharStyle?.Background);
        }

        // ---- Orientation applicability ----

        [Fact]
        public void UniversalStyles_SupportBothOrientations_AndHonourRequest()
        {
            Assert.Equal(CliTableStyleOrientationSupport.Both, CliTableStyles.OrientationSupport("Roma"));

            Assert.Equal(CliTableOrientation.Vertical,
                CliTableStyles.Create(CliTableStylePreset.Roma, Blue, CliTableOrientation.Vertical).Orientation);
            Assert.Equal(CliTableOrientation.Horizontal,
                CliTableStyles.Create(CliTableStylePreset.Roma, Blue, CliTableOrientation.Horizontal).Orientation);
        }

        [Fact]
        public void Parma_IsVerticalOnly_AndClampsToVertical()
        {
            Assert.Equal(CliTableStyleOrientationSupport.VerticalOnly, CliTableStyles.OrientationSupport("Parma"));

            // Even when a horizontal orientation is requested, Parma resolves to vertical.
            Assert.Equal(CliTableOrientation.Vertical,
                CliTableStyles.Create("Parma", Blue, CliTableOrientation.Horizontal).Orientation);
            Assert.Equal(CliTableOrientation.Vertical, CliTableStyles.Create(CliTableStylePreset.Parma, Blue).Orientation);

            // Parma's hallmark: a single-space column separator, no frames.
            var parma = CliTableStyles.Create(CliTableStylePreset.Parma, Blue);
            Assert.Equal(CliFrameSegmentStyle.Space, parma.FrameConfig.BetweenElements.Style);
            Assert.Equal(CliFrameSegmentStyle.None, parma.FrameConfig.OuterFrame.Style);
        }

        [Fact]
        public void Verona_IsHorizontalOnly_AndClampsToHorizontal()
        {
            Assert.Equal(CliTableStyleOrientationSupport.HorizontalOnly, CliTableStyles.OrientationSupport("Verona"));

            Assert.Equal(CliTableOrientation.Horizontal,
                CliTableStyles.Create("Verona", Blue, CliTableOrientation.Vertical).Orientation);
            Assert.Equal(CliTableOrientation.Horizontal, CliTableStyles.Create(CliTableStylePreset.Verona, Blue).Orientation);

            // Horizontal detail views read better left-aligned.
            Assert.Equal(CliTextAlignment.Left, CliTableStyles.Create(CliTableStylePreset.Verona, Blue).HeaderStyle?.HorizontalAlignment);
        }

        [Fact]
        public void Aliases_InheritOrientationApplicability()
        {
            Assert.Equal(CliTableStyleOrientationSupport.VerticalOnly, CliTableStyles.OrientationSupport("Condensed"));
            Assert.Equal(CliTableStyleOrientationSupport.HorizontalOnly, CliTableStyles.OrientationSupport("DetailsCondensed"));
        }

        // ---- Title background rule (titles sit outside the surface) ----

        [Fact]
        public void EveryCityStyle_PinsTitleBackgroundToBaseBackground()
        {
            var baseBg = Blue.Resolve(ThemeStyle.Background).CharStyle?.Background;

            foreach (var name in CliTableStyles.CityNames)
            {
                var style = CliTableStyles.Create(name, Blue);
                Assert.Equal(baseBg, style.TitleStyle?.CharStyle?.Background);
            }
        }

        [Fact]
        public void TitleBackground_StaysBase_EvenWhenSurfaceIsNotBase()
        {
            var baseBg = Blue.Resolve(ThemeStyle.Background).CharStyle?.Background; // Black

            // Roma uses the dialog surface (DarkBlue) for the body, but the title stays on base.
            var roma = CliTableStyles.Create(CliTableStylePreset.Roma, Blue);
            Assert.NotEqual(baseBg, roma.DefaultCellStyle?.CharStyle?.Background); // body on dialog surface
            Assert.Equal(baseBg, roma.TitleStyle?.CharStyle?.Background);

            // Palermo uses the alert surface (DarkRed); the title still stays on base.
            var palermo = CliTableStyles.Create(CliTableStylePreset.Palermo, Blue);
            Assert.NotEqual(baseBg, palermo.DefaultCellStyle?.CharStyle?.Background);
            Assert.Equal(baseBg, palermo.TitleStyle?.CharStyle?.Background);
        }

        // ---- Reference colours (TigerBlue) ----

        [Fact]
        public void Roma_TigerBlue_ResolvesExpectedColoursAndStructure()
        {
            var s = CliTableStyles.Create(CliTableStylePreset.Roma, Blue);

            Assert.Equal(CliFrameSegmentStyle.DoubleFrame, s.FrameConfig.OuterFrame.Style);
            Assert.Equal(CliFrameSegmentStyle.SingleFrame, s.FrameConfig.AfterHeader.Style);
            Assert.Equal(CliFrameSegmentStyle.SingleFrame, s.FrameConfig.BetweenElements.Style);
            Assert.Equal(CliFrameSegmentStyle.None, s.FrameConfig.BetweenRecords.Style);

            Assert.Equal(CliColor.Navy, s.DefaultCellStyle?.CharStyle?.Background);     // dialog surface
            Assert.Equal(CliColor.Cyan, s.TitleStyle?.CharStyle?.Foreground);          // accent title
            Assert.Equal(CliColor.DarkGreen, s.DataAltStyle?.CharStyle?.Background);    // dialog zebra family
        }

        [Fact]
        public void Palermo_TigerBlue_UsesAlertSurfaceWarningFrameAndWhiteAltText()
        {
            var s = CliTableStyles.Create(CliTableStylePreset.Palermo, Blue);

            Assert.Equal(CliColor.DarkRed, s.DefaultCellStyle?.CharStyle?.Background); // alert surface
            Assert.Equal(CliColor.Yellow, s.FrameConfig.CharStyle?.Foreground);        // warning frame
            Assert.Equal(CliColor.Yellow, s.TitleStyle?.CharStyle?.Foreground);        // warning title
            Assert.Equal(CliColor.DarkRed, s.DataAltStyle?.CharStyle?.Background);      // alert zebra keeps red
            Assert.Equal(CliColor.White, s.DataAltStyle?.CharStyle?.Foreground);       // alert zebra fg = white
        }

        [Fact]
        public void DefaultSurfaceStyles_TigerBlue_UseDarkGrayZebra()
        {
            // Napoli/Torino/Bologna/Parma/Verona use the default surface family => DarkGray zebra.
            foreach (var name in new[] { "Napoli", "Torino", "Bologna", "Parma", "Verona" })
            {
                var s = CliTableStyles.Create(name, Blue);
                Assert.Equal(CliColor.DarkGray, s.DataAltStyle?.CharStyle?.Background);
                Assert.Equal(CliColor.Green, s.TitleStyle?.CharStyle?.Foreground); // success title
            }
        }

        [Fact]
        public void BuiltInPresets_CarryAltStyle_ButDoNotEnableAlternateRecordsByDefault()
        {
            foreach (var preset in CliTableStyles.Presets)
            {
                var style = CliTableStyles.Create(preset, Blue);

                Assert.NotNull(style.DataAltStyle);
                Assert.False(style.AlternateRecordsEnabled);
            }
        }

        [Fact]
        public void ApplyPreset_SetsAlternateRecordsToPresetDefault()
        {
            var table = new CliTable()
                .UseAlternateRecords()
                .ApplyPreset(CliTableStylePreset.Default, Blue);

            Assert.False(table.AlternateRecordsEnabled);
            Assert.NotNull(table.DataAltStyle);
        }

        // ---- Alt style isolation: data cells only ----

        [Fact]
        public void AltStyle_AffectsDataCellsOnly_NotTitleHeaderOrFrame()
        {
            // The alt (zebra) background must differ from, and not leak into, the surface used by the
            // frame/header, nor the base background used by the title.
            var roma = CliTableStyles.Create(CliTableStylePreset.Roma, Blue);
            var altBg = roma.DataAltStyle?.CharStyle?.Background; // DarkGreen

            Assert.NotNull(roma.DataAltStyle);
            Assert.NotEqual(altBg, roma.FrameConfig.CharStyle?.Background);   // frame on dialog surface
            Assert.NotEqual(altBg, roma.HeaderStyle?.CharStyle?.Background);  // header on dialog surface
            Assert.NotEqual(altBg, roma.TitleStyle?.CharStyle?.Background);   // title on base background
        }

        [Fact]
        public void ApplyStyle_SetsAltOnTable_WithoutTouchingTitle()
        {
            var style = CliTableStyles.Create(CliTableStylePreset.Roma, Blue);
            var table = new CliTable().ApplyStyle(style);

            // ApplyStyle carries the data/alt styles but does not force a title (titles are app-provided).
            Assert.NotNull(table.DataAltStyle);
            Assert.Equal(CliColor.DarkGreen, table.DataAltStyle?.CharStyle?.Background);
            Assert.Null(table.Title);
        }

        // ---- AddTitle convenience ----

        [Fact]
        public void AddTitle_UsesAppliedTitleStyle_OnBaseBackground()
        {
            var style = CliTableStyles.Create(CliTableStylePreset.Roma, Blue);
            var table = new CliTable().ApplyStyle(style).AddTitle("Connections");

            Assert.NotNull(table.Title);
            Assert.Equal(CliColor.Cyan, table.Title!.Style.CharStyle?.Foreground);
            Assert.Equal(CliColor.Black, table.Title!.Style.CharStyle?.Background); // base background
            Assert.Equal(CliFormattingMode.Preformatted, table.Title!.Style.FormattingMode);
        }

        // ---- Theme fallback ----

        [Fact]
        public void NewThemeTokens_FallBackToExistingDefaults_WhenNotOverridden()
        {
            var theme = new FallbackTheme();
            var accent = theme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground;     // Cyan
            var baseBg = theme.Resolve(ThemeStyle.Background).CharStyle?.Background;  // Black

            // Semantic accents fall back to Accent.
            Assert.Equal(accent, theme.Resolve(ThemeStyle.Success).CharStyle?.Foreground);
            Assert.Equal(accent, theme.Resolve(ThemeStyle.Warning).CharStyle?.Foreground);

            // Surfaces fall back: Panel -> Background, Alert -> Background; no zebra without overrides.
            Assert.Equal(baseBg, theme.ResolveSurface(SurfaceRole.Default).Background);
            Assert.Equal(baseBg, theme.ResolveSurface(SurfaceRole.Panel).Background);
            Assert.Equal(baseBg, theme.ResolveSurface(SurfaceRole.Alert).Background);
            Assert.Null(theme.ResolveSurface(SurfaceRole.Panel).AltBackground);
            Assert.Null(theme.ResolveSurface(SurfaceRole.Alert).AltBackground);
        }

        [Fact]
        public void CityStyles_BuildSensibly_UnderAThemeWithoutNewTokenOverrides()
        {
            var theme = new FallbackTheme();

            // Success title falls back to Accent (Cyan).
            Assert.Equal(CliColor.Cyan, CliTableStyles.Create(CliTableStylePreset.Napoli, theme).TitleStyle?.CharStyle?.Foreground);
            // Alert surface falls back to base background (Black) — no crash, sensible look.
            Assert.Equal(CliColor.Black, CliTableStyles.Create(CliTableStylePreset.Palermo, theme).DefaultCellStyle?.CharStyle?.Background);

            // All styles build without throwing under the fallback theme.
            foreach (var name in CliTableStyles.CityNames)
                Assert.NotNull(CliTableStyles.Create(name, theme));
        }

        // ---- Milano title and header accent colours (TigerBlue) ----

        [Fact]
        public void Milano_TigerBlue_TitleAccentIsSuccess_Green()
        {
            var s = CliTableStyles.Create(CliTableStylePreset.Milano, Blue);

            Assert.Equal(CliColor.Green, s.TitleStyle?.CharStyle?.Foreground);
        }

        [Fact]
        public void Milano_TigerBlue_HeaderAccentIsWarning_Yellow()
        {
            var s = CliTableStyles.Create(CliTableStylePreset.Milano, Blue);

            Assert.Equal(CliColor.Yellow, s.HeaderStyle?.CharStyle?.Foreground);
        }

        [Fact]
        public void OtherPresets_TigerBlue_HeaderIsNotYellow()
        {
            // Roma/Genova use Default header accent; their header stays White (TableHeader in TigerBlue).
            var roma = CliTableStyles.Create(CliTableStylePreset.Roma, Blue);
            Assert.Equal(CliColor.White, roma.HeaderStyle?.CharStyle?.Foreground);

            var napoli = CliTableStyles.Create(CliTableStylePreset.Napoli, Blue);
            Assert.Equal(CliColor.White, napoli.HeaderStyle?.CharStyle?.Foreground);
        }

        // ---- Lucca structure ----

        [Fact]
        public void Lucca_IsHorizontalOnly_AndClampsToHorizontal()
        {
            Assert.Equal(CliTableStyleOrientationSupport.HorizontalOnly,
                CliTableStyles.OrientationSupport(CliTableStylePreset.Lucca));

            Assert.Equal(CliTableOrientation.Horizontal,
                CliTableStyles.Create(CliTableStylePreset.Lucca, Blue, CliTableOrientation.Vertical).Orientation);
            Assert.Equal(CliTableOrientation.Horizontal,
                CliTableStyles.Create(CliTableStylePreset.Lucca, Blue).Orientation);
        }

        [Fact]
        public void Lucca_HasNoBetweenElementsFrame()
        {
            var s = CliTableStyles.Create(CliTableStylePreset.Lucca, Blue);

            Assert.Equal(CliFrameSegmentStyle.None, s.FrameConfig.BetweenElements.Style);
        }

        [Fact]
        public void Lucca_HasMilanoLikeOuterAndAfterHeaderFrames()
        {
            var milano = CliTableStyles.Create(CliTableStylePreset.Milano, Blue);
            var lucca = CliTableStyles.Create(CliTableStylePreset.Lucca, Blue);

            Assert.Equal(milano.FrameConfig.OuterFrame.Style, lucca.FrameConfig.OuterFrame.Style);
            Assert.Equal(milano.FrameConfig.AfterHeader.Style, lucca.FrameConfig.AfterHeader.Style);
        }

        [Fact]
        public void Lucca_IsOnPanelSurface_TigerBlue()
        {
            var s = CliTableStyles.Create(CliTableStylePreset.Lucca, Blue);

            Assert.Equal(CliColor.Navy, s.DefaultCellStyle?.CharStyle?.Background);
        }

        [Fact]
        public void Lucca_TigerBlue_TitleIsGreen_HeaderIsYellow()
        {
            var s = CliTableStyles.Create(CliTableStylePreset.Lucca, Blue);

            Assert.Equal(CliColor.Green, s.TitleStyle?.CharStyle?.Foreground);
            Assert.Equal(CliColor.Yellow, s.HeaderStyle?.CharStyle?.Foreground);
        }

        [Fact]
        public void Details_AliasMapsToLucca_AndIsHorizontalOnly()
        {
            Assert.Equal(CliTableStyleOrientationSupport.HorizontalOnly,
                CliTableStyles.OrientationSupport("Details"));
            Assert.Equal(CliTableOrientation.Horizontal,
                CliTableStyles.Create("Details", Blue).Orientation);
        }

        [Fact]
        public void DetailsCondensed_StillMapsToVerona()
        {
            Assert.Equal(CliTableStylePreset.Verona,
                CliTableStyles.Canonicalize(CliTableStylePreset.DetailsCondensed));
            Assert.Equal(CliTableStyleOrientationSupport.HorizontalOnly,
                CliTableStyles.OrientationSupport("DetailsCondensed"));
        }

        [Theory]
        [InlineData("Lucca")]
        [InlineData("lucca")]
        [InlineData("Details")]
        [InlineData("details")]
        public void Parse_AcceptsLuccaAndDetails_CaseInsensitive(string name)
        {
            var parsed = CliTableStyles.Parse(name);
            Assert.True(parsed == CliTableStylePreset.Lucca || parsed == CliTableStylePreset.Details);
        }

        // ---- Rendering smoke test ----

        [Fact]
        public void EveryCityStyle_RendersRectangularInItsSupportedOrientation()
        {
            foreach (var name in CliTableStyles.CityNames)
            {
                var style = CliTableStyles.Create(name, Blue);
                var table = new CliTable().ApplyStyle(style);

                if (style.Orientation == CliTableOrientation.Horizontal)
                {
                    table.AddHeader("Name", "Server", "Auth");
                    table.AddRecord("prod", "localhost", "SQL");
                }
                else
                {
                    table.AddHeader("Name", "Server", "Auth");
                    table.AddRecord("prod", "localhost", "SQL");
                    table.AddRecord("stg", "sql-stg", "Windows");
                }

                var lines = TigerConsole.RenderToLines(table);
                Assert.NotEmpty(lines);
                var width = lines[0].Length;
                Assert.All(lines, line => Assert.Equal(width, line.Length));
            }
        }
    }
}
