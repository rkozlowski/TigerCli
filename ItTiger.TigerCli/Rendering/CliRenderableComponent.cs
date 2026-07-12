using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Rendering;


/// <summary>
/// Base class for renderable components that share layout constraints and convert to a
/// <see cref="CliGrid"/> for output.
/// </summary>
public abstract class CliRenderableComponent : CliLayoutComponent, ICliRenderable
{
    /// <summary>
    /// Converts the component into a grid for measurement and rendering.
    /// </summary>
    public abstract CliGrid ToGrid();

    /// <summary>
    /// Creates a grid with this component's shared layout settings already applied.
    /// Concrete components should call this helper from <see cref="ToGrid()"/>,
    /// then populate the returned grid with their component-specific cells.
    /// </summary>
    protected CliGrid ToGrid(int columnCount, int rowCount)
    {
        var grid = new CliGrid(columnCount, rowCount)
        {
            IsInteractive = this.IsInteractive,

            Width = this.Width,
            MinWidth = this.MinWidth,
            SoftMaxWidth = this.SoftMaxWidth,
            MaxWidth = this.MaxWidth,

            Height = this.Height,
            MinHeight = this.MinHeight,
            SoftMaxHeight = this.SoftMaxHeight,
            MaxHeight = this.MaxHeight,

            DefaultCellStyle = this.DefaultCellStyle
        };

        return grid;
    }
    
    
}
