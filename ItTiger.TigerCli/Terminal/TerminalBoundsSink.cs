using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// Wraps a sink that writes to a real console stream but carries no layout bounds of its own
/// (<see cref="AnsiSink"/>, <see cref="TextWriterSink"/>) and reports the terminal's dimensions to
/// the measure pass, exactly as <see cref="ConsoleSink"/> does.
/// </summary>
/// <remarks>
/// Without this, the sink chosen for a stream decides whether structured output wraps at all: the
/// 16-colour path reported a width and wrapped, while the ANSI and plain paths reported "unbounded"
/// and emitted long lines that only the terminal's own auto-wrap broke — mid-word, and without any
/// of the layout's indentation. Bounds belong to the destination, not to the colour encoding, so
/// every console-bound sink reports the same ones.
/// </remarks>
internal sealed class TerminalBoundsSink(ICliRenderSink inner, bool isError) : ICliRenderSink
{
    /// <summary>The wrapped sink, which decides the colour encoding written to the stream.</summary>
    public ICliRenderSink Inner => inner;

    /// <summary>Returns the sink that encodes output, unwrapping the terminal-bounds decorator.</summary>
    public static ICliRenderSink Unwrap(ICliRenderSink sink) =>
        sink is TerminalBoundsSink bounded ? bounded.Inner : sink;

    public int? SoftMaxWidth => TerminalCapabilities.GetSafeOutputWidth(isError);

    public int? SoftMaxHeight => TerminalCapabilities.GetSafeOutputHeight(isError);

    public int? MaxWidth => inner.MaxWidth;

    public int? MaxHeight => inner.MaxHeight;

    public void Write(CliTextSegment segment) => inner.Write(segment);

    public void NewLine() => inner.NewLine();

    public void Flush() => inner.Flush();

    public void Reset() => inner.Reset();

    public void SetWindowTitle(string title) => inner.SetWindowTitle(title);
}
