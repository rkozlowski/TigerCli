using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.PngSink;

/// <summary>
/// Convenience methods for rendering TigerCli grids and renderable components to PNG output.
/// </summary>
public static class PngRenderer
{
    /// <summary>Renders <paramref name="grid"/> with <paramref name="options"/> and writes the PNG to <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public static void RenderGridToFile(CliGrid grid, string path, PngSinkOptions options)
    {
        ArgumentNullException.ThrowIfNull(path);

        using var stream = File.Create(path);
        RenderGridToStream(grid, stream, options);
    }

    /// <summary>Renders <paramref name="grid"/> with <paramref name="options"/> and writes the PNG to <paramref name="stream"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> or <paramref name="stream"/> is null.</exception>
    public static void RenderGridToStream(CliGrid grid, Stream stream, PngSinkOptions options)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(stream);

        var sink = new PngSink(options);
        TigerConsole.RenderGrid(grid, sink);
        sink.Save(stream);
    }

    /// <summary>Renders <paramref name="grid"/> with <paramref name="options"/> and returns PNG bytes.</summary>
    public static byte[] RenderGridToBytes(CliGrid grid, PngSinkOptions options)
    {
        using var stream = new MemoryStream();
        RenderGridToStream(grid, stream, options);
        return stream.ToArray();
    }

    /// <summary>Converts <paramref name="component"/> to a grid, renders it with <paramref name="options"/>, and returns PNG bytes.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> is null.</exception>
    public static byte[] RenderToBytes(CliRenderableComponent component, PngSinkOptions options)
    {
        ArgumentNullException.ThrowIfNull(component);
        return RenderGridToBytes(component.ToGrid(), options);
    }
}
