namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// Represents a component that can materialize itself as a <see cref="CliGrid"/> for measurement and
/// rendering.
/// </summary>
public interface ICliRenderable
{
    /// <summary>
    /// Builds the grid representation used by TigerCli's render pipeline.
    /// </summary>
    public CliGrid ToGrid();
}
