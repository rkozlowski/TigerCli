using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests
{
    /// <summary>
    /// Covers the table-style authoring surface after the city-recipe cleanup: the
    /// <see cref="CliTable.AddHeader"/>/<see cref="CliTable.AddRecord"/> convenience API,
    /// <see cref="CliTable.ApplyStyle"/>, <see cref="CliTableStyleRecipe"/> customization, and that
    /// styles follow the resolving theme.
    /// </summary>
    public class CliTableThemeAndBuilderTests : TestBase
    {
        // ---- Test themes ----

        private abstract class TestThemeBase : ThemeBase
        {
            protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Gray));
            protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
            protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.Cyan));
            protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
            protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
            protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
        }

        // Recolors the table header ink only; structure comes from the recipe.
        private sealed class RedHeaderTheme : TestThemeBase
        {
            public override string Name => "test-red-header";
            protected override CliCellStyle? TableHeader => new(new CliCharStyle(CliColor.Red));
        }

        // A frameless, no-padding recipe for builder tests that need predictable plain output.
        private static readonly CliTableStyleRecipe Frameless = new()
        {
            Outer = CliFrameSegmentStyle.None,
            AfterHeader = CliFrameSegmentStyle.None,
            BetweenElements = CliFrameSegmentStyle.None,
            BetweenRecords = CliFrameSegmentStyle.None,
            Padding = CliCellPadding.None,
        };

        private static CliTable FramelessTable() => new CliTable().ApplyStyle(Frameless.Resolve(new DarkTheme()));

        // ---- AddHeader / AddRecord convenience API ----

        [Fact]
        public void AddHeader_CreatesExpectedHeaderCells()
        {
            var table = new CliTable();

            var returned = table.AddHeader("Name", "Server", "Authentication", "Database");

            Assert.Same(table, returned);
            Assert.Equal(4, table.Header.Elements.Count);
            Assert.Equal("Name", table.Header.Elements[0].HeaderContent);
            Assert.Equal("Server", table.Header.Elements[1].HeaderContent);
            Assert.Equal("Authentication", table.Header.Elements[2].HeaderContent);
            Assert.Equal("Database", table.Header.Elements[3].HeaderContent);
        }

        [Fact]
        public void AddRecord_CreatesExpectedRecordCells()
        {
            var table = new CliTable().AddHeader("A", "B", "C");

            var returned = table.AddRecord("x", 1, null);

            Assert.Same(table, returned);
            var record = Assert.Single(table.Records);
            Assert.Equal(3, record.Count);
            Assert.Equal("x", record[0]);
            Assert.Equal(1, record[1]);
            Assert.Null(record[2]);
        }

        [Fact]
        public void AddRecord_NullValues_RenderSafely()
        {
            var table = FramelessTable().AddHeader("Value");

            table.AddRecord((object?)null);

            // Should not throw, and should still produce the header plus a (blank) record.
            var lines = TigerConsole.RenderToLines(table);
            Assert.Equal(["Value", "     "], lines);
        }

        [Fact]
        public void AddRecord_EnumBoolInt_RenderThroughNormalStringConversion()
        {
            var table = FramelessTable().AddHeader("Align", "Flag", "Count");

            table.AddRecord(CliTextAlignment.Right, true, 42);

            var text = string.Join("\n", TigerConsole.RenderToLines(table));
            Assert.Contains("Right", text);
            Assert.Contains("True", text);
            Assert.Contains("42", text);
        }

        // ---- ApplyStyle follows the resolving theme ----

        [Fact]
        public void ApplyStyle_FromRecipe_UsesThemeInk()
        {
            var table = new CliTable().ApplyStyle(CliTableStyleRecipe.Roma.Resolve(new RedHeaderTheme()));

            Assert.Equal(CliColor.Red, table.Header.HeaderStyle?.CharStyle?.Foreground);
        }

        [Fact]
        public void Resolve_Parameterless_UsesCurrentTheme()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                TigerConsole.CurrentTheme = new LightTheme();
                var table = new CliTable().ApplyStyle(CliTableStyleRecipe.Milano.Resolve());

                Assert.Equal(CliTableOrientation.Vertical, table.Orientation);
                Assert.Equal(CliFrameSegmentStyle.SingleFrame, table.FrameConfig.OuterFrame.Style);
                // Milano uses LightTheme's warning foreground, proving it followed CurrentTheme.
                Assert.Equal(CliColor.DarkYellow, table.Header.HeaderStyle?.CharStyle?.Foreground);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        // ---- Recipe customization (`with`) ----

        [Fact]
        public void Recipe_With_OverridesSurface_WithoutChangingStructure()
        {
            var theme = new TigerBlueTheme();
            var alertRoma = CliTableStyleRecipe.Roma with { Surface = SurfaceRole.Alert };

            var baseStyle = CliTableStyleRecipe.Roma.Resolve(theme);
            var altered = alertRoma.Resolve(theme);

            // Structure unchanged.
            Assert.Equal(baseStyle.FrameConfig.OuterFrame.Style, altered.FrameConfig.OuterFrame.Style);
            Assert.Equal(baseStyle.Orientation, altered.Orientation);

            // Only the surface changed: Roma sits on Panel (Navy); the override moves it to Alert (DarkRed).
            Assert.Equal(CliColor.Navy, baseStyle.DefaultCellStyle?.CharStyle?.Background);
            Assert.Equal(CliColor.DarkRed, altered.DefaultCellStyle?.CharStyle?.Background);
        }

        // A developer-defined style based on a city recipe, per the documented pattern.
        private static class MyTableStyles
        {
            public static readonly CliTableStyleRecipe InvoiceRecipe =
                CliTableStyleRecipe.Roma with { Surface = SurfaceRole.Panel, TitleAccent = TableAccent.Success };

            public static CliTableStyle Invoice(ITheme? theme = null) => InvoiceRecipe.Resolve(theme);
        }

        [Fact]
        public void DeveloperDefinedStyle_BasedOnCityRecipe_Resolves()
        {
            var theme = new TigerBlueTheme();
            var style = MyTableStyles.Invoice(theme);

            // Panel surface (Navy) + success title (Green) from the theme.
            Assert.Equal(CliColor.Navy, style.DefaultCellStyle?.CharStyle?.Background);
            Assert.Equal(CliColor.Green, style.TitleStyle?.CharStyle?.Foreground);

            // It applies and renders rectangularly.
            var table = new CliTable().ApplyStyle(style).AddHeader("A", "B");
            table.AddRecord("1", "2");
            var lines = TigerConsole.RenderToLines(table);
            Assert.NotEmpty(lines);
            Assert.All(lines, l => Assert.Equal(lines[0].Length, l.Length));
        }

        // ---- ApplyStyle compatibility and customization ----

        [Fact]
        public void NewTable_WithoutStyle_KeepsLegacyDefaults()
        {
            var table = new CliTable();

            Assert.Equal(CliTableOrientation.Vertical, table.Orientation);
            Assert.Equal(CliFrameSegmentStyle.DoubleFrame, table.FrameConfig.OuterFrame.Style);
            Assert.Equal(CliFrameSegmentStyle.SingleFrame, table.FrameConfig.AfterHeader.Style);
            Assert.Equal(CliFrameSegmentStyle.SingleFrame, table.FrameConfig.BetweenElements.Style);
            Assert.Equal(CliFrameSegmentStyle.None, table.FrameConfig.BetweenRecords.Style);
            Assert.Null(table.Header.HeaderStyle);
            Assert.Null(table.DefaultCellStyle);
        }

        [Fact]
        public void Table_CanBeModified_AfterApplyStyle_BeforeAddRecord()
        {
            var table = new CliTable().ApplyStyle(CliTableStyleRecipe.Milano.Resolve(new DarkTheme()));

            table.Orientation = CliTableOrientation.Horizontal;
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);

            table.AddHeader("Name", "Server");
            table.AddRecord("db1", "localhost");

            Assert.Equal(CliTableOrientation.Horizontal, table.Orientation);
            Assert.Equal(CliFrameSegmentStyle.None, table.FrameConfig.OuterFrame.Style);
            Assert.NotEmpty(TigerConsole.RenderToLines(table));
        }

        [Fact]
        public void UseAlternateRecords_EnablesAlternateRecords_AndReturnsTable()
        {
            var table = AlternateRecordTable()
                .UseAlternateRecords()
                .AddHeader("Name")
                .AddRecord("prod")
                .AddRecord("stg");

            Assert.True(table.AlternateRecordsEnabled);
            Assert.Equal(CliColor.DarkGreen, table.ToGrid().GetCellStyle(0, 2).CharStyle?.Background);
        }

        [Fact]
        public void UseAlternateRecords_False_DisablesAlternateRecords()
        {
            var table = AlternateRecordTable()
                .UseAlternateRecords()
                .UseAlternateRecords(false)
                .AddHeader("Name")
                .AddRecord("prod")
                .AddRecord("stg");

            Assert.False(table.AlternateRecordsEnabled);
            Assert.Equal(CliColor.Black, table.ToGrid().GetCellStyle(0, 2).CharStyle?.Background);
        }

        [Fact]
        public void UseAlternateRecords_DoesNotModifyStylesOrFrameConfig()
        {
            var frameConfig = new CliTableFrameConfig();
            var defaultStyle = new CliCellStyle(new CliCharStyle(CliColor.Gray, CliColor.Black));
            var dataStyle = new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.Black));
            var dataAltStyle = new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkGreen));
            var headerStyle = new CliCellStyle(new CliCharStyle(CliColor.Cyan, CliColor.Black));
            var titleStyle = new CliCellStyle(new CliCharStyle(CliColor.Green, CliColor.Black));
            var table = new CliTable
            {
                FrameConfig = frameConfig,
                DefaultCellStyle = defaultStyle,
                DataStyle = dataStyle,
                DataAltStyle = dataAltStyle,
                TitleStyle = titleStyle,
            };
            table.Header.HeaderStyle = headerStyle;

            var returned = table.UseAlternateRecords();

            Assert.Same(table, returned);
            Assert.Same(frameConfig, table.FrameConfig);
            Assert.Same(defaultStyle, table.DefaultCellStyle);
            Assert.Same(dataStyle, table.DataStyle);
            Assert.Same(dataAltStyle, table.DataAltStyle);
            Assert.Same(headerStyle, table.Header.HeaderStyle);
            Assert.Same(titleStyle, table.TitleStyle);
        }

        [Fact]
        public void SetOrientation_DoesNotModifyTitleDataOrFrameConfig()
        {
            var frameConfig = new CliTableFrameConfig
            {
                OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.DoubleFrame),
                CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.Black),
            };
            var defaultStyle = new CliCellStyle(new CliCharStyle(CliColor.Gray, CliColor.Black));
            var dataStyle = new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.Black))
            {
                HorizontalAlignment = CliTextAlignment.Right,
            };
            var dataAltStyle = new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkGreen));
            var headerStyle = new CliCellStyle(new CliCharStyle(CliColor.Cyan, CliColor.Black));
            var titleStyle = new CliCellStyle(new CliCharStyle(CliColor.Green, CliColor.Black))
            {
                HorizontalAlignment = CliTextAlignment.Center,
            };
            var table = new CliTable
            {
                FrameConfig = frameConfig,
                DefaultCellStyle = defaultStyle,
                DataStyle = dataStyle,
                DataAltStyle = dataAltStyle,
                TitleStyle = titleStyle,
            };
            table.Header.HeaderStyle = headerStyle;
            table.Title = new CliTableTitle("Details", CliTextAlignment.Center, new CliCharStyle(CliColor.Green, CliColor.Black));

            table.SetOrientation(CliTableOrientation.Horizontal);

            Assert.Same(frameConfig, table.FrameConfig);
            Assert.Equal(CliFrameSegmentStyle.DoubleFrame, table.FrameConfig.OuterFrame.Style);
            Assert.Equal(CliColor.Yellow, table.FrameConfig.CharStyle?.Foreground);
            Assert.Equal(CliColor.Black, table.FrameConfig.CharStyle?.Background);
            Assert.Same(defaultStyle, table.DefaultCellStyle);
            Assert.Same(dataStyle, table.DataStyle);
            Assert.Same(dataAltStyle, table.DataAltStyle);
            Assert.Same(headerStyle, table.Header.HeaderStyle);
            Assert.Same(titleStyle, table.TitleStyle);
            Assert.Equal(CliTextAlignment.Center, table.TitleStyle.HorizontalAlignment);
            Assert.Equal(CliTextAlignment.Center, table.Title.Style.HorizontalAlignment);
            Assert.Equal(CliColor.Green, table.Title.Style.CharStyle?.Foreground);
            Assert.Equal(CliColor.Black, table.Title.Style.CharStyle?.Background);
            Assert.Equal(CliTextAlignment.Right, table.DataStyle.HorizontalAlignment);
            Assert.Equal(CliColor.Cyan, table.Header.HeaderStyle.CharStyle?.Foreground);
            Assert.Equal(CliColor.Black, table.Header.HeaderStyle.CharStyle?.Background);
            Assert.Equal(CliTextAlignment.Left, table.Header.HeaderStyle.HorizontalAlignment);
        }

        [Fact]
        public void ApplyStyle_CopiesAlternateRecordsDefaultToTable()
        {
            var style = AlternateRecordStyle();
            style.AlternateRecordsEnabled = true;

            var table = new CliTable().ApplyStyle(style);

            Assert.True(table.AlternateRecordsEnabled);
        }

        [Fact]
        public void ApplyStyle_CopiesFrameConfig_SoReuseIsSafe()
        {
            var style = CliTableStyleRecipe.Milano.Resolve(new DarkTheme());

            var a = new CliTable().ApplyStyle(style);
            var b = new CliTable().ApplyStyle(style);

            // Mutating one table's frame must not leak into the other or the shared style.
            a.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);

            Assert.Equal(CliFrameSegmentStyle.SingleFrame, b.FrameConfig.OuterFrame.Style);
            Assert.Equal(CliFrameSegmentStyle.SingleFrame, style.FrameConfig.OuterFrame.Style);
        }

        private static CliTable AlternateRecordTable()
            => new CliTable().ApplyStyle(AlternateRecordStyle());

        private static CliTableStyle AlternateRecordStyle() => new()
        {
            FrameConfig = new CliTableFrameConfig
            {
                OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None),
                AfterHeader = new CliFrameSegment(CliFrameSegmentStyle.None),
                BetweenElements = new CliFrameSegment(CliFrameSegmentStyle.None),
                BetweenRecords = new CliFrameSegment(CliFrameSegmentStyle.None),
            },
            DefaultCellStyle = new CliCellStyle(new CliCharStyle(CliColor.Gray, CliColor.Black))
            {
                Padding = CliCellPadding.None,
            },
            DataAltStyle = new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkGreen))
            {
                Padding = CliCellPadding.None,
            },
        };

        // ---- Stable rendered output (colors are stripped; layout must stay stable) ----

        [Fact]
        public void Torino_Vertical_RendersStableList()
        {
            var table = new CliTable()
                .ApplyPreset(CliTableStylePreset.Torino, new DarkTheme())
                .AddHeader("Name", "Server");

            table.AddRecord("db1", "localhost");

            AssertSnapshot(table,
                " Name │  Server   ",
                "──────┼───────────",
                " db1  │ localhost ");
        }

        [Fact]
        public void EveryCityStyle_RendersWithoutError_AndIsRectangular()
        {
            var theme = new TigerBlueTheme();
            foreach (var name in CliTableStyles.CityNames)
            {
                var style = CliTableStyles.Create(name, theme);
                var table = new CliTable().ApplyStyle(style);
                table.AddHeader("One", "Two", "Three");
                table.AddRecord(1, 2, 3);
                table.AddRecord("a", "bb", "ccc");

                var lines = TigerConsole.RenderToLines(table);
                Assert.NotEmpty(lines);
                var width = lines[0].Length;
                Assert.All(lines, line => Assert.Equal(width, line.Length));
            }
        }
    }
}
