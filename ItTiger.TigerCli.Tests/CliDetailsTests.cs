using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests
{
    /// <summary>
    /// Covers <see cref="CliDetails"/>: the key/value detail builder. Verifies field inclusion rules
    /// (Add / AddOptional / AddWhen), missing-value semantics and display, preset/title application,
    /// and that it renders through the existing <see cref="CliTable"/> pipeline.
    /// </summary>
    public class CliDetailsTests : TestBase
    {
        private static readonly ITheme Blue = new TigerBlueTheme();

        private static CliDetails NewDetails() =>
            new CliDetails().ApplyPreset(CliTableStylePreset.Details, Blue);

        private static string Render(CliDetails details) =>
            string.Join("\n", TigerConsole.RenderToLines(details));

        // ---- Inclusion rules ----

        [Fact]
        public void Add_AlwaysIncludesField_EvenWhenNull()
        {
            var table = NewDetails().Add("Name:", null).ToTable();

            Assert.Single(table.Header.Elements);
            Assert.Equal("Name:", table.Header.Elements[0].HeaderContent);
            Assert.Single(table.Records);
        }

        [Fact]
        public void AddOptional_SkipsNull()
        {
            var table = NewDetails()
                .Add("Name:", "prod")
                .AddOptional("Username:", null)
                .ToTable();

            Assert.Single(table.Header.Elements);
            Assert.Equal("Name:", table.Header.Elements[0].HeaderContent);
        }

        [Fact]
        public void AddOptional_SkipsEmptyAndWhitespaceString()
        {
            var table = NewDetails()
                .AddOptional("Empty:", "")
                .AddOptional("Blank:", "   ")
                .Add("Name:", "prod")
                .ToTable();

            Assert.Single(table.Header.Elements);
            Assert.Equal("Name:", table.Header.Elements[0].HeaderContent);
        }

        [Fact]
        public void AddOptional_DoesNotSkipFalse()
        {
            var table = NewDetails().AddOptional("Trusted:", false).ToTable();

            Assert.Single(table.Header.Elements);
            Assert.Equal(false, table.Records[0][0]);
        }

        [Fact]
        public void AddOptional_DoesNotSkipZero()
        {
            var table = NewDetails().AddOptional("Timeout:", 0).ToTable();

            Assert.Single(table.Header.Elements);
            Assert.Equal(0, table.Records[0][0]);
        }

        [Fact]
        public void AddWhen_False_SkipsField()
        {
            var table = NewDetails()
                .Add("Name:", "prod")
                .AddWhen(false, "Username:", "admin")
                .ToTable();

            Assert.Single(table.Header.Elements);
        }

        [Fact]
        public void AddWhen_True_IncludesField()
        {
            var table = NewDetails()
                .AddWhen(true, "Username:", "admin")
                .ToTable();

            Assert.Single(table.Header.Elements);
            Assert.Equal("admin", table.Records[0][0]);
        }

        [Fact]
        public void AddWhen_True_IncludesField_EvenWhenMissing()
        {
            var table = NewDetails().AddWhen(true, "Username:", null).ToTable();

            Assert.Single(table.Header.Elements);
            Assert.Null(table.Records[0][0]);
        }

        // ---- Missing-value semantics (static rule) ----

        [Fact]
        public void IsMissing_ClassifiesNullAndBlankStringsAsMissing()
        {
            Assert.True(CliDetails.IsMissing(null));
            Assert.True(CliDetails.IsMissing(""));
            Assert.True(CliDetails.IsMissing("   "));
        }

        [Fact]
        public void IsMissing_TreatsFalseZeroAndTextAsPresent()
        {
            Assert.False(CliDetails.IsMissing(false));
            Assert.False(CliDetails.IsMissing(0));
            Assert.False(CliDetails.IsMissing("prod"));
        }

        // ---- Missing display ----

        [Fact]
        public void Add_Null_UsesDefaultMissingDisplay()
        {
            var text = Render(NewDetails().Add("Database:", null));

            Assert.Contains("(not set)", text);
        }

        [Fact]
        public void Add_Null_PerFieldMissingDisplay_OverridesDefault()
        {
            var text = Render(NewDetails().Add("Database:", null, missingDisplay: "(not selected)"));

            Assert.Contains("(not selected)", text);
            Assert.DoesNotContain("(not set)", text);
        }

        [Fact]
        public void SetMissingDisplay_ChangesDefaultForFollowingFields()
        {
            var text = Render(NewDetails()
                .SetMissingDisplay("(n/a)")
                .Add("Database:", null));

            Assert.Contains("(n/a)", text);
            Assert.DoesNotContain("(not set)", text);
        }

        [Fact]
        public void MissingDisplay_SupportsMarkup_StrippedInRenderedLines()
        {
            var text = Render(NewDetails().Add("Database:", null, missingDisplay: "[Muted](not selected)[/]"));

            Assert.Contains("(not selected)", text);
            Assert.DoesNotContain("[Muted]", text); // markup consumed, not printed literally
        }

        [Fact]
        public void BlankStringValue_WithAdd_RendersMissingDisplay_NotEmpty()
        {
            var text = Render(NewDetails().Add("Username:", "   "));

            Assert.Contains("(not set)", text);
        }

        // ---- Title / preset ----

        [Fact]
        public void AddTitle_RendersTitle()
        {
            var text = Render(NewDetails()
                .AddTitle("SQL Server connection")
                .Add("Name:", "prod"));

            Assert.Contains("SQL Server connection", text);
        }

        [Fact]
        public void AddTitle_IsMarkupAware()
        {
            var text = Render(NewDetails()
                .AddTitle("Hello [Cyan]World[/]")
                .Add("Name:", "prod"));

            Assert.Contains("Hello", text);
            Assert.Contains("World", text);
            Assert.DoesNotContain("[Cyan]", text);
        }

        [Fact]
        public void AddTitle_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CliDetails().AddTitle(null!));
        }

        [Fact]
        public void AddTitle_DefaultAlignment_IsCenter_Unchanged()
        {
            var table = NewDetails().AddTitle("Connection").Add("Name:", "prod").ToTable();

            Assert.NotNull(table.Title);
            Assert.Equal(CliTextAlignment.Center, table.Title!.Style.HorizontalAlignment);
        }

        [Theory]
        [InlineData(CliTextAlignment.Left)]
        [InlineData(CliTextAlignment.Center)]
        [InlineData(CliTextAlignment.Right)]
        public void AddTitle_WithAlignmentArgument_SetsTitleAlignment(CliTextAlignment alignment)
        {
            var table = NewDetails().AddTitle("Connection", alignment).Add("Name:", "prod").ToTable();

            Assert.Equal(alignment, table.Title!.Style.HorizontalAlignment);
        }

        [Fact]
        public void SetTitleAlignment_OverridesPresetDefault()
        {
            var table = NewDetails()
                .AddTitle("Connection")
                .SetTitleAlignment(CliTextAlignment.Left)
                .Add("Name:", "prod")
                .ToTable();

            Assert.Equal(CliTextAlignment.Left, table.Title!.Style.HorizontalAlignment);
        }

        [Fact]
        public void SetTitleAlignment_PreservesTitleStyle()
        {
            var baseline = NewDetails().AddTitle("Connection").Add("Name:", "prod").ToTable();
            var baselineCharStyle = baseline.Title!.Style.CharStyle;

            var table = NewDetails()
                .AddTitle("Connection", CliTextAlignment.Left)
                .Add("Name:", "prod")
                .ToTable();

            Assert.Equal(CliTextAlignment.Left, table.Title!.Style.HorizontalAlignment);
            Assert.Equal(baselineCharStyle?.Foreground, table.Title.Style.CharStyle?.Foreground);
            Assert.Equal(baselineCharStyle?.Background, table.Title.Style.CharStyle?.Background);
        }

        [Fact]
        public void DefaultPreset_RendersHorizontal()
        {
            // The default preset (Details) is a horizontal detail view.
            var table = new CliDetails().Add("Name:", "prod").ToTable();

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
        }

        [Fact]
        public void ApplyPreset_HonorsDetailOrientation_Horizontal()
        {
            // Details → Lucca is horizontal-only: labels are row headers, the record is a value column.
            var table = NewDetails().Add("Name:", "prod").ToTable();

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
        }

        [Fact]
        public void ApplyPreset_NonDetailsPreset_StillRendersHorizontal()
        {
            // Roma is a vertical-by-default preset, but CliDetails is always a key/value detail view,
            // so orientation must stay horizontal regardless of the supplied preset.
            var table = new CliDetails()
                .ApplyPreset(CliTableStylePreset.Roma, Blue)
                .Add("Name:", "prod")
                .ToTable();

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
        }

        [Fact]
        public void ApplyPreset_Details_StillRendersHorizontal()
        {
            var table = new CliDetails()
                .ApplyPreset(CliTableStylePreset.Details, Blue)
                .Add("Name:", "prod")
                .ToTable();

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
        }

        // ---- Orientation-specific header alignment (the leak fix) ----

        // In a horizontal detail view, header cells are row labels and must use the preset's
        // horizontal header alignment (left), not its vertical/list-table header alignment (center).
        [Theory]
        [InlineData(CliTableStylePreset.Details)]        // horizontal-only (Lucca)
        [InlineData(CliTableStylePreset.DetailsCondensed)] // horizontal-only (Verona)
        [InlineData(CliTableStylePreset.Roma)]           // universal, vertical-by-default
        [InlineData(CliTableStylePreset.Milano)]         // universal, vertical-by-default
        [InlineData(CliTableStylePreset.Napoli)]         // universal, vertical-by-default
        [InlineData(CliTableStylePreset.Torino)]         // universal, vertical-by-default
        public void ApplyPreset_HeaderLabelsAreLeftAligned_NotListTableAlignment(CliTableStylePreset preset)
        {
            var table = new CliDetails()
                .ApplyPreset(preset, Blue)
                .Add("Name:", "prod")
                .ToTable();

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
            Assert.Equal(CliTextAlignment.Left, table.Header.HeaderStyle?.HorizontalAlignment);
        }

        [Fact]
        public void DefaultPreset_HeaderLabelsAreLeftAligned()
        {
            var table = new CliDetails().Add("Name:", "prod").ToTable();

            Assert.Equal(CliTextAlignment.Left, table.Header.HeaderStyle?.HorizontalAlignment);
        }

        [Fact]
        public void RomaDetails_RendersLabelsLeftAligned_NotCentered()
        {
            // Different-length labels make alignment visible: a centered label would be indented
            // within the label column; a left-aligned label sits flush against the left padding.
            var lines = TigerConsole.RenderToLines(new CliDetails()
                .ApplyPreset(CliTableStylePreset.Roma, Blue)
                .Add("Name:", "prod")
                .Add("Datacenter region:", "eu-west-1"));

            var shortLabelLine = lines.Single(l => l.Contains("Name:"));
            var longLabelLine = lines.Single(l => l.Contains("Datacenter region:"));

            // The short label starts at the same column as the long label (left-aligned), rather
            // than being pushed right to center it within the wider label column.
            Assert.Equal(longLabelLine.IndexOf("Datacenter region:"), shortLabelLine.IndexOf("Name:"));
        }

        [Fact]
        public void VerticalListTable_KeepsCenteredHeader_Unchanged()
        {
            // Regression guard: forcing horizontal details left-alignment must not change the
            // appearance of an ordinary vertical/list table using the same preset.
            var table = new CliTable()
                .ApplyPreset(CliTableStylePreset.Roma, Blue)
                .AddHeader("Name", "Region")
                .AddRecord("prod", "eu");

            Assert.Equal(CliTableOrientation.Vertical, table.Orientation);
            Assert.Equal(CliTextAlignment.Center, table.Header.HeaderStyle?.HorizontalAlignment);
        }

        [Fact]
        public void HorizontalListTable_HeaderLabelsAreLeftAligned()
        {
            // The same orientation-specific default applies to a plain CliTable resolved horizontally.
            var table = new CliTable()
                .ApplyPreset(CliTableStylePreset.Roma, Blue, CliTableOrientation.Horizontal)
                .AddHeader("Name", "Region")
                .AddRecord("prod", "eu");

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
            Assert.Equal(CliTextAlignment.Left, table.Header.HeaderStyle?.HorizontalAlignment);
        }

        // ---- Rendering / conversion integration ----

        [Fact]
        public void Render_ShowsLabelsAndValues()
        {
            var text = Render(NewDetails()
                .AddTitle("SQL Server connection")
                .Add("Name:", "prod-core")
                .Add("Server:", "localhost"));

            Assert.Contains("Name:", text);
            Assert.Contains("prod-core", text);
            Assert.Contains("Server:", text);
            Assert.Contains("localhost", text);
        }

        [Fact]
        public void ToTable_ProducesValidCliTable_WithAlignedHeaderAndRecord()
        {
            var table = NewDetails()
                .Add("Name:", "prod")
                .Add("Server:", "localhost")
                .ToTable();

            Assert.IsType<CliTable>(table);
            Assert.Equal(2, table.Header.Elements.Count);
            Assert.Single(table.Records);
            Assert.Equal(table.Header.Elements.Count, table.Records[0].Count);
        }

        [Fact]
        public void Render_RectangularOutput()
        {
            var lines = TigerConsole.RenderToLines(NewDetails()
                .AddTitle("Connection")
                .Add("Name:", "prod")
                .Add("Server:", "localhost"));

            var width = lines[0].Length;
            Assert.All(lines, l => Assert.Equal(width, l.Length));
        }

        [Fact]
        public void DatabaseNotSelected_Scenario_RendersExplicitMissingValue()
        {
            // The motivating dogfood case: show "(not selected)" rather than hiding the field.
            var text = Render(NewDetails()
                .AddTitle("SQL Server connection")
                .Add("Name:", "prod")
                .AddOptional("Username:", null)                       // hidden
                .Add("Database:", null, missingDisplay: "(not selected)")); // shown as missing

            Assert.Contains("Database:", text);
            Assert.Contains("(not selected)", text);
            Assert.DoesNotContain("Username:", text);
        }

        // ---- Empty builder ----

        [Fact]
        public void ToGrid_NoFields_ThrowsLikeEmptyTable()
        {
            // Mirrors CliTable: a table with no fields cannot be rendered.
            Assert.ThrowsAny<Exception>(() => new CliDetails().ToGrid());
        }
    }
}
