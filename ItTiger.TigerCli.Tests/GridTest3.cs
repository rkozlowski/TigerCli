using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tests;
internal class GridTest3 : CliRenderableComponent
{
    const string Test = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation nullamco laboris nisi ut aliquip ex ea commodo consequat. ";
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
        var frameStyle = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
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
        var colDef = new CliGridColumnDefinition(new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right });
        grid.SetColumn(1, colDef);
        grid.SetColumn(32, colDef);
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
