using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tests;
internal class GridTest1 : CliRenderableComponent
{
    const string Test = "Lorem ipsum dolor sit amet,\r\nconsectetur adipiscing elit, \r\nsed do eiusmod tempor \r\nincididunt ut labore \r\net dolore magna aliqua. \r\nUt enim ad minim veniam, \r\nquis nostrud exercitation \r\nullamco laboris nisi \r\nut aliquip ex ea \r\ncommodo consequat. ";
    public override CliGrid ToGrid()
    {
        int cols = 34;
        int rows = 19;
        CliGrid grid = ToGrid(cols, rows);
        grid.DefaultCellStyle = new CliCellStyle()
        {
            CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue),
            FormattingMode = CliFormattingMode.Raw,
            HorizontalAlignment = CliTextAlignment.Left,
            VerticalAlignment = CliVerticalAlignment.Top
        };

        var area = grid.AddFrameArea(CliFrameJoinStyle.SimplifiedCompatible, 0, 0, cols - 1, rows - 1,
            new CliCharStyle(CliColor.Gray, CliColor.DarkBlue));
        var frameStyle = new CliFrameSegment(CliFrameSegmentStyle.DoubleFrame);
        area.AddOuterFrame(frameStyle);            
        char cc = 'A';
        for (int c = 2; c < 32; c++)
        {                
            grid.Set(c, 1, cc);
            grid.Set(c, 17, cc);
            if (cc == 'Z')
                cc = 'a';
            else
                cc++;
        }
        for (int r = 2; r < 17; r++)
        {
            grid.Set(1, r, r - 1);
            grid.Set(32, r, r - 1);                
        }
        var style = new CliCellStyle
        {
            HorizontalAlignment = CliTextAlignment.Center,
            VerticalAlignment = CliVerticalAlignment.Center,
            CharStyle = new CliCharStyle(CliColor.Red, CliColor.DarkCyan)
        };
        grid.Set(2, 2, Test, style, 30, 15);
        return grid;
    }
}
