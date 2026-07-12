using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests
{
    /// <summary>
    /// Covers <see cref="CliTable.AddTitle(string)"/> / <see cref="CliTable.AddTitle(object, CliFormattingMode, CliFormatter?)"/>:
    /// the title uses the table's currently applied title style, defaults to preformatted/markup-aware
    /// content, supports an explicit formatter, and never touches the header/body/frame styling.
    /// </summary>
    public class CliTableAddTitleTests : TestBase
    {
        private static readonly ITheme Blue = new TigerBlueTheme();

        // ---- Title style source ----

        [Fact]
        public void AddTitle_AfterApplyPreset_UsesPresetTitleStyle()
        {
            var table = new CliTable()
                .ApplyPreset(CliTableStylePreset.Roma, Blue)
                .AddTitle("Connections");

            Assert.NotNull(table.Title);
            Assert.Equal(CliColor.Cyan, table.Title!.Style.CharStyle?.Foreground);  // accent title
            Assert.Equal(CliColor.Black, table.Title!.Style.CharStyle?.Background);  // base surface
            Assert.Equal(CliFormattingMode.Preformatted, table.Title!.Style.FormattingMode);
        }

        [Fact]
        public void AddTitle_AfterApplyStyle_UsesCustomTitleStyle()
        {
            var style = new CliTableStyle
            {
                TitleStyle = new CliCellStyle(new CliCharStyle(CliColor.Magenta, CliColor.Black))
                {
                    HorizontalAlignment = CliTextAlignment.Center,
                }
            };

            var table = new CliTable().ApplyStyle(style).AddTitle("Custom");

            Assert.NotNull(table.Title);
            Assert.Equal(CliColor.Magenta, table.Title!.Style.CharStyle?.Foreground);
            Assert.Equal(CliColor.Black, table.Title!.Style.CharStyle?.Background);
        }

        [Fact]
        public void AddTitle_ClonesCurrentTitleStyle_NotSharedAfterwards()
        {
            var table = new CliTable().ApplyPreset(CliTableStylePreset.Roma, Blue).AddTitle("T");

            // Mutating the table's current TitleStyle must not change the already-created title.
            table.TitleStyle!.CharStyle = new CliCharStyle(CliColor.Red, CliColor.Red);

            Assert.Equal(CliColor.Cyan, table.Title!.Style.CharStyle?.Foreground);
        }

        // ---- Formatting ----

        [Fact]
        public void AddTitle_String_IsPreformatted_AndMarkupAware()
        {
            var table = new CliTable()
                .ApplyPreset(CliTableStylePreset.Torino, Blue)
                .AddTitle("Hello [Cyan]World[/]")
                .AddHeader("A", "B");
            table.AddRecord("1", "2");

            Assert.Equal(CliFormattingMode.Preformatted, table.Title!.Style.FormattingMode);

            var text = string.Join("\n", TigerConsole.RenderToLines(table));
            Assert.Contains("Hello", text);
            Assert.Contains("World", text);
            Assert.DoesNotContain("[Cyan]", text); // markup consumed, not printed literally
        }

        [Fact]
        public void AddTitle_Object_WithFormattingModeAndFormatter_FormatsContent()
        {
            var formatter = CliFormatter.FromDelegate(o => $"VAL_{o}");

            var table = new CliTable()
                .ApplyPreset(CliTableStylePreset.Torino, Blue)
                .AddTitle(42, CliFormattingMode.Raw, formatter)
                .AddHeader("Name", "Server");
            table.AddRecord("prod-core", "localhost"); // table wide enough that the title fits one line

            Assert.Equal(CliFormattingMode.Raw, table.Title!.Style.FormattingMode);
            Assert.NotNull(table.Title!.Style.Formatter);

            var text = string.Join("\n", TigerConsole.RenderToLines(table));
            Assert.Contains("VAL_42", text);
        }

        // ---- Null rejection ----

        [Fact]
        public void AddTitle_String_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CliTable().AddTitle(null!));
        }

        [Fact]
        public void AddTitle_Object_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CliTable().AddTitle(null!, CliFormattingMode.Raw));
        }

        // ---- Absence of a title ----

        [Fact]
        public void NotCallingAddTitle_LeavesTitleUnset()
        {
            var table = new CliTable().ApplyPreset(CliTableStylePreset.Roma, Blue);
            Assert.Null(table.Title);
        }

        // ---- Isolation: AddTitle does not alter other styling ----

        [Fact]
        public void AddTitle_DoesNotAffectHeaderBodyFrameOrPreset()
        {
            var table = new CliTable().ApplyPreset(CliTableStylePreset.Roma, Blue);

            var header = table.Header.HeaderStyle;
            var body = table.DefaultCellStyle;
            var frame = table.FrameConfig;
            var dataAlt = table.DataAltStyle;
            var orientation = table.Orientation;

            table.AddTitle("T");

            Assert.Same(header, table.Header.HeaderStyle);
            Assert.Same(body, table.DefaultCellStyle);
            Assert.Same(frame, table.FrameConfig);
            Assert.Same(dataAlt, table.DataAltStyle);
            Assert.Equal(orientation, table.Orientation);
        }

        [Fact]
        public void AddTitle_TitleBackgroundStaysBase_ForAlertPreset()
        {
            // Palermo's body sits on the alert (DarkRed) surface, but the title stays on the base surface.
            var table = new CliTable().ApplyPreset(CliTableStylePreset.Palermo, Blue).AddTitle("Heads up");

            Assert.Equal(CliColor.Black, table.Title!.Style.CharStyle?.Background);
            Assert.NotEqual(CliColor.DarkRed, table.Title!.Style.CharStyle?.Background);
        }

        // ---- Rendering smoke: title row renders and stays rectangular ----

        [Fact]
        public void AddTitle_RendersTitleRow_RectangularOutput()
        {
            var table = new CliTable()
                .ApplyPreset(CliTableStylePreset.Roma, Blue)
                .AddTitle("Connections")
                .AddHeader("Name", "Server");
            table.AddRecord("prod", "localhost");

            var lines = TigerConsole.RenderToLines(table);

            Assert.Contains(lines, l => l.Contains("Connections"));
            var width = lines[0].Length;
            Assert.All(lines, l => Assert.Equal(width, l.Length));
        }
    }
}
