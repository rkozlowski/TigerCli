using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests
{
    public abstract class TestBase
    {
        protected static void AssertSnapshot(CliRenderableComponent c, params string[] expected)
        {
            var lines = TigerConsole.RenderToLines(c);
            Assert.Equal(expected.Length, lines.Count);
            Assert.Equal(expected, lines);
        }

        protected static void AssertSnapshot(CliGrid g, params string[] expected)
        {
            var lines = TigerConsole.RenderGridToLines(g);
            Assert.Equal(expected.Length, lines.Count);
            Assert.Equal(expected, lines);
        }

        // in TestBase.cs (or new helper file)
        protected static CliGrid Grid1x1(
            string text,
            int width,
            CliWrapMode mode,
            bool allowTruncation = false,
            string indicator = "…")
        {
            var grid = new CliGrid(1, 1);
            var style = new CliCellStyle
            {
                Wrapping = new CliWrapping(mode, allowTruncation, indicator)
            };

            // 1 column with fixed width
            grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = width, MinWidth = width, MaxWidth = width }));
            // 1 row (height content-driven)
            grid.SetRow(0, new CliGridRowDefinition(new CliCellStyle { }));

            grid.Set(0, 0, text, style);
            
            return grid;
        }

    }
}
