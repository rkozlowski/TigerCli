using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

public class DbNullHandlingTests : TestBase, IDisposable
{
    private readonly bool _originalGlobalSetting;

    public DbNullHandlingTests()
    {
        _originalGlobalSetting = TigerConsole.TreatDbNullAsNull;
        TigerConsole.TreatDbNullAsNull = true;
    }

    public void Dispose()
    {
        TigerConsole.TreatDbNullAsNull = _originalGlobalSetting;
    }

    [Fact]
    public void Grid_DefaultGlobal_DbNullConvertedToNull_ShowsNullDisplayValue()
    {
        var grid = new CliGrid(2, 1);
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = 3 }));
        grid.SetColumn(1, new CliGridColumnDefinition(new CliCellStyle { Width = 5 }));
        grid.DefaultCellStyle = new CliCellStyle { NullDisplayValue = "N/A" };

        grid.Set(0, 0, DBNull.Value);
        grid.Set(1, 0, "Hello");

        AssertSnapshot(grid, "N/AHello");
    }

    [Fact]
    public void Grid_DefaultGlobal_ActualNull_StillShowsNullDisplayValue()
    {
        var grid = new CliGrid(1, 1);
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = 3 }));
        grid.DefaultCellStyle = new CliCellStyle { NullDisplayValue = "N/A" };

        grid.Set(0, 0, null);

        AssertSnapshot(grid, "N/A");
    }

    [Fact]
    public void Grid_Set_WithCliGridContent_RequiresSetSubgrid()
    {
        var grid = new CliGrid(1, 1);
        var inner = new CliGrid(1, 1);

        var ex = Assert.Throws<ArgumentException>(() => grid.Set(0, 0, inner));

        Assert.Equal("content", ex.ParamName);
    }

    [Fact]
    public void Grid_PerGridOverrideDisabled_DbNullRenderedAsString()
    {
        var grid = new CliGrid(1, 1);
        grid.TreatDbNullAsNull = false;
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = 1 }));
        grid.DefaultCellStyle = new CliCellStyle { NullDisplayValue = "N/A" };

        grid.Set(0, 0, DBNull.Value);

        // DBNull.Value.ToString() returns empty string
        AssertSnapshot(grid, " ");
    }

    [Fact]
    public void Grid_GlobalDisabled_PerGridOverrideEnabled_DbNullConvertedToNull()
    {
        TigerConsole.TreatDbNullAsNull = false;

        var grid = new CliGrid(1, 1);
        grid.TreatDbNullAsNull = true;
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = 3 }));
        grid.DefaultCellStyle = new CliCellStyle { NullDisplayValue = "N/A" };

        grid.Set(0, 0, DBNull.Value);

        AssertSnapshot(grid, "N/A");
    }

    [Fact]
    public void Table_DefaultGlobal_DbNullConvertedToNull_ShowsNullDisplayValue()
    {
        var table = new CliTable();
        table.DefaultCellStyle = new CliCellStyle { NullDisplayValue = "-" };
        table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
        table.FrameConfig.AfterHeader = new CliFrameSegment(CliFrameSegmentStyle.None);
        table.FrameConfig.BetweenElements = new CliFrameSegment(CliFrameSegmentStyle.None);
        table.Header.IsVisible = false;
        table.Header.Elements.Add(new CliTableElement("Col1", new CliCellStyle { Width = 5 }));
        table.Header.Elements.Add(new CliTableElement("Col2", new CliCellStyle { Width = 1 }));

        table.Records.Add(new List<object?> { "Hello", DBNull.Value });

        AssertSnapshot(table, "Hello-");
    }

    [Fact]
    public void Table_PerTableOverrideDisabled_DbNullRenderedAsString()
    {
        var table = new CliTable();
        table.TreatDbNullAsNull = false;
        table.DefaultCellStyle = new CliCellStyle { NullDisplayValue = "-" };
        table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
        table.FrameConfig.AfterHeader = new CliFrameSegment(CliFrameSegmentStyle.None);
        table.FrameConfig.BetweenElements = new CliFrameSegment(CliFrameSegmentStyle.None);
        table.Header.IsVisible = false;
        table.Header.Elements.Add(new CliTableElement("Col1", new CliCellStyle { Width = 1 }));

        table.Records.Add(new List<object?> { DBNull.Value });

        // DBNull.Value.ToString() is empty, so with width 1 it's a space
        AssertSnapshot(table, " ");
    }

    [Fact]
    public void Table_GlobalDisabled_PerTableOverrideEnabled_DbNullConvertedToNull()
    {
        TigerConsole.TreatDbNullAsNull = false;

        var table = new CliTable();
        table.TreatDbNullAsNull = true;
        table.DefaultCellStyle = new CliCellStyle { NullDisplayValue = "-" };
        table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
        table.FrameConfig.AfterHeader = new CliFrameSegment(CliFrameSegmentStyle.None);
        table.FrameConfig.BetweenElements = new CliFrameSegment(CliFrameSegmentStyle.None);
        table.Header.IsVisible = false;
        table.Header.Elements.Add(new CliTableElement("Col1", new CliCellStyle { Width = 1 }));

        table.Records.Add(new List<object?> { DBNull.Value });

        AssertSnapshot(table, "-");
    }
}
