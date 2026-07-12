using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tests;

public sealed class CliRenderableComponentTests
{
    [Fact]
    public void ToGrid_HelperCopiesSharedLayoutSettings()
    {
        var defaultStyle = new CliCellStyle
        {
            HorizontalAlignment = CliTextAlignment.Center,
            FormattingMode = CliFormattingMode.Raw,
            CharStyle = new CliCharStyle(CliColor.Cyan, CliColor.Black)
        };

        var component = new TestComponent
        {
            IsInteractive = true,
            Width = 20,
            MinWidth = 10,
            SoftMaxWidth = 30,
            MaxWidth = 40,
            Height = 2,
            MinHeight = 1,
            SoftMaxHeight = 3,
            MaxHeight = 4,
            DefaultCellStyle = defaultStyle
        };

        var grid = component.ToGrid();

        Assert.Equal(2, grid.ColumnCount);
        Assert.Equal(1, grid.RowCount);
        Assert.True(grid.IsInteractive);
        Assert.Equal(component.Width, grid.Width);
        Assert.Equal(component.MinWidth, grid.MinWidth);
        Assert.Equal(component.SoftMaxWidth, grid.SoftMaxWidth);
        Assert.Equal(component.MaxWidth, grid.MaxWidth);
        Assert.Equal(component.Height, grid.Height);
        Assert.Equal(component.MinHeight, grid.MinHeight);
        Assert.Equal(component.SoftMaxHeight, grid.SoftMaxHeight);
        Assert.Equal(component.MaxHeight, grid.MaxHeight);
        Assert.Equal(defaultStyle.HorizontalAlignment, grid.DefaultCellStyle?.HorizontalAlignment);
        Assert.Equal(defaultStyle.FormattingMode, grid.DefaultCellStyle?.FormattingMode);
        Assert.Equal(defaultStyle.CharStyle?.Foreground, grid.DefaultCellStyle?.CharStyle?.Foreground);
        Assert.Equal(defaultStyle.CharStyle?.Background, grid.DefaultCellStyle?.CharStyle?.Background);
    }

    private sealed class TestComponent : CliRenderableComponent
    {
        public override CliGrid ToGrid()
        {
            var grid = ToGrid(columnCount: 2, rowCount: 1);

            grid.Set(0, 0, "Title");
            grid.Set(1, 0, "Value");

            return grid;
        }
    }
}
