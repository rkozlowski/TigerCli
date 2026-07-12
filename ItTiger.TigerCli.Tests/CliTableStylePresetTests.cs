using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests
{
    /// <summary>
    /// Locks the enum-based table style preset model: the <see cref="CliTableStylePreset"/> values and
    /// numeric grouping, alias canonicalization, the enum/string Create equivalence, and the
    /// string-boundary parsing behaviour.
    /// </summary>
    public class CliTableStylePresetTests : TestBase
    {
        private static readonly ITheme Blue = new TigerBlueTheme();

        // ---- Enum shape / numeric grouping ----

        [Fact]
        public void Enum_HasExpectedNumericGrouping()
        {
            Assert.Equal(0, (int)CliTableStylePreset.Default);
            Assert.Equal(100, (int)CliTableStylePreset.Roma);

            // Aliases live below the city threshold; cities at or above it.
            foreach (var alias in CliTableStyles.AliasPresets)
                Assert.True((int)alias < 100, $"{alias} should be an alias (< 100).");
            foreach (var city in CliTableStyles.CityPresets)
                Assert.True((int)city >= 100, $"{city} should be a city (>= 100).");
        }

        [Fact]
        public void CityPresets_AreExactlyTheTenExpectedCities()
        {
            CliTableStylePreset[] expected =
            [
                CliTableStylePreset.Roma, CliTableStylePreset.Milano, CliTableStylePreset.Napoli,
                CliTableStylePreset.Torino, CliTableStylePreset.Genova, CliTableStylePreset.Bologna,
                CliTableStylePreset.Palermo, CliTableStylePreset.Parma, CliTableStylePreset.Verona,
                CliTableStylePreset.Lucca,
            ];

            Assert.Equal(expected, CliTableStyles.CityPresets);
        }

        [Fact]
        public void AliasPresets_AreExactlyTheEightExpectedAliases()
        {
            CliTableStylePreset[] expected =
            [
                CliTableStylePreset.Default, CliTableStylePreset.Light, CliTableStylePreset.Grid,
                CliTableStylePreset.Alert, CliTableStylePreset.Condensed, CliTableStylePreset.Details,
                CliTableStylePreset.DetailsCondensed, CliTableStylePreset.List,
            ];

            Assert.Equal(expected, CliTableStyles.AliasPresets);
        }

        // ---- Removed tasting variants are not enum values and not accepted as strings ----

        [Theory]
        [InlineData("Venezia")]
        [InlineData("Firenze")]
        [InlineData("Pisa")]
        public void RemovedTastingVariants_AreNotPresets(string removed)
        {
            Assert.False(Enum.TryParse<CliTableStylePreset>(removed, ignoreCase: true, out _));
            Assert.DoesNotContain(removed, CliTableStyles.PresetNames);
            Assert.Throws<ArgumentException>(() => CliTableStyles.Parse(removed));
            Assert.Throws<ArgumentException>(() => CliTableStyles.Create(removed, Blue));
            Assert.Throws<ArgumentException>(() => CliTableStyles.GetRecipe(removed));
            Assert.Throws<ArgumentException>(() => CliTableStyles.OrientationSupport(removed));
        }

        // ---- Alias canonicalization (enum-to-enum) ----

        [Theory]
        [InlineData(CliTableStylePreset.Default, CliTableStylePreset.Roma)]
        [InlineData(CliTableStylePreset.Light, CliTableStylePreset.Milano)]
        [InlineData(CliTableStylePreset.Grid, CliTableStylePreset.Napoli)]
        [InlineData(CliTableStylePreset.Alert, CliTableStylePreset.Palermo)]
        [InlineData(CliTableStylePreset.Condensed, CliTableStylePreset.Parma)]
        [InlineData(CliTableStylePreset.Details, CliTableStylePreset.Lucca)]
        [InlineData(CliTableStylePreset.DetailsCondensed, CliTableStylePreset.Verona)]
        [InlineData(CliTableStylePreset.List, CliTableStylePreset.Milano)]
        public void Canonicalize_MapsAliasToCity(CliTableStylePreset alias, CliTableStylePreset city)
        {
            Assert.Equal(city, CliTableStyles.Canonicalize(alias));
            // A city canonicalizes to itself.
            Assert.Equal(city, CliTableStyles.Canonicalize(city));
            // GetRecipe is identical for the alias and its city.
            Assert.Same(CliTableStyles.GetRecipe(city), CliTableStyles.GetRecipe(alias));
        }

        // ---- Enum-based and string-based Create agree ----

        [Fact]
        public void EnumCreate_And_StringCreate_AreEquivalent()
        {
            foreach (var preset in CliTableStyles.Presets)
            {
                var fromEnum = CliTableStyles.Create(preset, Blue);
                var fromString = CliTableStyles.Create(preset.ToString(), Blue);

                Assert.Equal(fromEnum.Orientation, fromString.Orientation);
                Assert.Equal(fromEnum.FrameConfig.OuterFrame.Style, fromString.FrameConfig.OuterFrame.Style);
                Assert.Equal(fromEnum.DefaultCellStyle?.CharStyle?.Background,
                    fromString.DefaultCellStyle?.CharStyle?.Background);
                Assert.Equal(fromEnum.TitleStyle?.CharStyle?.Foreground,
                    fromString.TitleStyle?.CharStyle?.Foreground);
            }
        }

        // ---- GetRecipe / OrientationSupport over both preset kinds ----

        [Fact]
        public void GetRecipe_WorksForCityAndAlias()
        {
            Assert.Same(CliTableStyleRecipe.Roma, CliTableStyles.GetRecipe(CliTableStylePreset.Roma));
            Assert.Same(CliTableStyleRecipe.Roma, CliTableStyles.GetRecipe(CliTableStylePreset.Default));
            Assert.Same(CliTableStyleRecipe.Lucca, CliTableStyles.GetRecipe(CliTableStylePreset.Details));
            Assert.Same(CliTableStyleRecipe.Verona, CliTableStyles.GetRecipe(CliTableStylePreset.DetailsCondensed));
        }

        [Fact]
        public void OrientationSupport_WorksForCityAndAlias()
        {
            Assert.Equal(CliTableStyleOrientationSupport.Both,
                CliTableStyles.OrientationSupport(CliTableStylePreset.Roma));
            Assert.Equal(CliTableStyleOrientationSupport.VerticalOnly,
                CliTableStyles.OrientationSupport(CliTableStylePreset.Condensed)); // -> Parma
            Assert.Equal(CliTableStyleOrientationSupport.HorizontalOnly,
                CliTableStyles.OrientationSupport(CliTableStylePreset.Details));   // -> Lucca
            Assert.Equal(CliTableStyleOrientationSupport.HorizontalOnly,
                CliTableStyles.OrientationSupport(CliTableStylePreset.DetailsCondensed)); // -> Verona
        }

        // ---- String boundary behaviour ----

        [Theory]
        [InlineData("roma", CliTableStylePreset.Roma)]
        [InlineData("ROMA", CliTableStylePreset.Roma)]
        [InlineData("Default", CliTableStylePreset.Default)]
        [InlineData("detailscondensed", CliTableStylePreset.DetailsCondensed)]
        public void Parse_IsCaseInsensitive(string name, CliTableStylePreset expected)
        {
            Assert.Equal(expected, CliTableStyles.Parse(name));
        }

        [Theory]
        [InlineData("Nope")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("100")]   // numeric input is rejected — names only
        [InlineData("0")]
        public void Parse_InvalidNames_Throw(string name)
        {
            Assert.Throws<ArgumentException>(() => CliTableStyles.Parse(name));
        }

        [Fact]
        public void Parse_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CliTableStyles.Parse(null!));
        }

        // ---- ApplyPreset (the preferred application path) ----

        [Fact]
        public void ApplyPreset_MatchesApplyStyleWithCreate()
        {
            var viaPreset = new CliTable().ApplyPreset(CliTableStylePreset.Default, Blue);
            var viaStyle = new CliTable().ApplyStyle(CliTableStyles.Create(CliTableStylePreset.Default, Blue));

            Assert.Equal(viaStyle.Orientation, viaPreset.Orientation);
            Assert.Equal(viaStyle.AlternateRecordsEnabled, viaPreset.AlternateRecordsEnabled);
            Assert.Equal(viaStyle.FrameConfig.OuterFrame.Style, viaPreset.FrameConfig.OuterFrame.Style);
            Assert.Equal(viaStyle.FrameConfig.BetweenElements.Style, viaPreset.FrameConfig.BetweenElements.Style);
            Assert.Equal(viaStyle.DefaultCellStyle?.CharStyle?.Background, viaPreset.DefaultCellStyle?.CharStyle?.Background);
            Assert.Equal(viaStyle.Header.HeaderStyle?.CharStyle?.Foreground, viaPreset.Header.HeaderStyle?.CharStyle?.Foreground);
        }

        [Fact]
        public void ApplyPreset_AppliesPresetDefaultOrientation_WhenOmitted()
        {
            var table = new CliTable()
                .SetOrientation(CliTableOrientation.Horizontal)
                .ApplyPreset(CliTableStylePreset.Roma, Blue);

            Assert.Equal(CliTableOrientation.Vertical, table.Orientation);
        }

        [Fact]
        public void ApplyPreset_NormalPresetDefaultsToVertical()
        {
            var table = new CliTable().ApplyPreset(CliTableStylePreset.Default, Blue);

            Assert.Equal(CliTableOrientation.Vertical, table.Orientation);
        }

        [Fact]
        public void ApplyPreset_ParmaDefaultsToVertical()
        {
            var table = new CliTable().ApplyPreset(CliTableStylePreset.Parma, Blue);

            Assert.Equal(CliTableOrientation.Vertical, table.Orientation);
        }

        [Fact]
        public void ApplyPreset_VeronaDefaultsToHorizontal()
        {
            var table = new CliTable().ApplyPreset(CliTableStylePreset.Verona, Blue);

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
        }

        [Fact]
        public void ApplyPreset_Default_SetOrientationHorizontal_LeftAlignsHeaderLabels()
        {
            var table = new CliTable()
                .ApplyPreset(CliTableStylePreset.Default, Blue)
                .SetOrientation(CliTableOrientation.Horizontal);

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
            Assert.Equal(CliTextAlignment.Left, table.Header.HeaderStyle?.HorizontalAlignment);
        }

        [Fact]
        public void ApplyPreset_Milano_SetOrientationHorizontal_LeftAlignsHeaderLabels()
        {
            var table = new CliTable()
                .ApplyPreset(CliTableStylePreset.Milano, Blue)
                .SetOrientation(CliTableOrientation.Horizontal);

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
            Assert.Equal(CliTextAlignment.Left, table.Header.HeaderStyle?.HorizontalAlignment);
        }

        [Fact]
        public void ApplyPreset_Verona_LeftAlignsHeaderLabels()
        {
            var table = new CliTable().ApplyPreset(CliTableStylePreset.Verona, Blue);

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
            Assert.Equal(CliTextAlignment.Left, table.Header.HeaderStyle?.HorizontalAlignment);
        }

        [Fact]
        public void SetOrientation_Vertical_RestoresNormalPresetHeaderAlignment()
        {
            var table = new CliTable()
                .ApplyPreset(CliTableStylePreset.Default, Blue);

            var verticalAlignment = table.Header.HeaderStyle?.HorizontalAlignment;

            table
                .SetOrientation(CliTableOrientation.Horizontal)
                .SetOrientation(CliTableOrientation.Vertical);

            Assert.Equal(CliTableOrientation.Vertical, table.Orientation);
            Assert.Equal(verticalAlignment, table.Header.HeaderStyle?.HorizontalAlignment);
            Assert.Equal(CliTextAlignment.Center, table.Header.HeaderStyle?.HorizontalAlignment);
        }

        [Fact]
        public void ApplyPreset_ExplicitOrientation_IsPartOfPresetApplication()
        {
            var table = new CliTable().ApplyPreset(CliTableStylePreset.Roma, Blue, CliTableOrientation.Horizontal);

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
        }

        [Fact]
        public void ApplyPreset_ExplicitOrientation_StillClampsLockedPresets()
        {
            var parma = new CliTable().ApplyPreset(CliTableStylePreset.Parma, Blue, CliTableOrientation.Horizontal);
            var verona = new CliTable().ApplyPreset(CliTableStylePreset.Verona, Blue, CliTableOrientation.Vertical);

            Assert.Equal(CliTableOrientation.Vertical, parma.Orientation);
            Assert.Equal(CliTableOrientation.Horizontal, verona.Orientation);
        }

        [Fact]
        public void SetOrientation_SetsOrientationAndReturnsTable()
        {
            var table = new CliTable();

            var returned = table.SetOrientation(CliTableOrientation.Horizontal);

            Assert.Same(table, returned);
            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
        }

        [Fact]
        public void SetOrientation_AfterApplyPreset_ChangesNormalPresetOrientation()
        {
            var table = new CliTable()
                .ApplyPreset(CliTableStylePreset.Default, Blue)
                .SetOrientation(CliTableOrientation.Horizontal);

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
        }

        [Fact]
        public void ApplyPreset_OrientationLockedPreset_ClampsRegardlessOfTable()
        {
            var parma = new CliTable()
                .ApplyPreset(CliTableStylePreset.Parma, Blue)
                .SetOrientation(CliTableOrientation.Horizontal);
            var verona = new CliTable()
                .ApplyPreset(CliTableStylePreset.DetailsCondensed, Blue)
                .SetOrientation(CliTableOrientation.Vertical);

            Assert.Equal(CliTableOrientation.Vertical, parma.Orientation);
            Assert.Equal(CliTableOrientation.Horizontal, verona.Orientation);
        }

        [Fact]
        public void ApplyPreset_LuccaDefaultsToHorizontal()
        {
            var table = new CliTable().ApplyPreset(CliTableStylePreset.Lucca, Blue);

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
        }

        [Fact]
        public void ApplyPreset_DetailsAliasDefaultsToHorizontal()
        {
            var table = new CliTable().ApplyPreset(CliTableStylePreset.Details, Blue);

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
        }

        [Theory]
        [InlineData("details")]
        [InlineData("Details")]
        [InlineData("DETAILS")]
        [InlineData("lucca")]
        [InlineData("Lucca")]
        [InlineData("LUCCA")]
        public void Parse_AcceptsDetailsAndLucca_CaseInsensitive(string name)
        {
            var parsed = CliTableStyles.Parse(name);
            Assert.True(parsed == CliTableStylePreset.Details || parsed == CliTableStylePreset.Lucca);
        }

        [Fact]
        public void Details_CanonicalizeToLucca_DetailsCondensed_CanonicalizeToVerona()
        {
            Assert.Equal(CliTableStylePreset.Lucca, CliTableStyles.Canonicalize(CliTableStylePreset.Details));
            Assert.Equal(CliTableStylePreset.Verona, CliTableStyles.Canonicalize(CliTableStylePreset.DetailsCondensed));
        }
    }
}
