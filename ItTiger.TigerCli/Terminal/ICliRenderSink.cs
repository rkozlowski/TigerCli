
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// Sink abstraction used by the render pipeline to write styled text segments.
/// </summary>
public interface ICliRenderSink
{
    /// <summary>Writes a styled text segment.</summary>
    void Write(CliTextSegment segment);
    /// <summary>Writes a line break.</summary>
    void NewLine();
    /// <summary>Flushes buffered output.</summary>
    void Flush();

    /// <summary>Resets sink state.</summary>
    void Reset();

    /// <summary>
    /// Sets the terminal/window title when the sink supports terminal controls; default is no-op.
    /// </summary>
    void SetWindowTitle(string title)
    {
    }

    /// <summary>Soft width constraint reported to grid measurement, or <c>null</c> when unbounded.</summary>
    int? SoftMaxWidth {  get; }
    /// <summary>Soft height constraint reported to grid measurement, or <c>null</c> when unbounded.</summary>
    int? SoftMaxHeight { get; }

    /// <summary>Hard width constraint reported to grid measurement, or <c>null</c> when unbounded.</summary>
    int? MaxWidth { get; }
    /// <summary>Hard height constraint reported to grid measurement, or <c>null</c> when unbounded.</summary>
    int? MaxHeight { get; }
}

