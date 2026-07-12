namespace ItTiger.TigerCli.Terminal;

public static partial class TigerConsole
{
    // Ambient sink scopes for the console output paths. A pushed scope overrides the
    // ConsoleSinkFactory choice for the duration of a logical async flow — this is how a whole app
    // run renders through one sink (TigerCliApp.RunAsync) and how styled app-run capture works
    // (TigerCliAppTestHost.WithHtmlCapture pushes HtmlSink scopes for stdout and stderr).
    //
    // PlainBaseStyle: markup rendering normally seeds its base style from the current console
    // colours, which is correct when the sink ultimately writes to that console but is
    // machine-dependent noise when the scope captures into a non-console sink (HTML). A capture
    // scope sets PlainBaseStyle so unstyled runs stay unstyled. Console behaviour is unchanged when
    // no scope is pushed or when the scope was created by EnsureOutputSinkScope (flag stays false).
    private sealed record SinkScopeState(ICliRenderSink Sink, bool PlainBaseStyle);

    private static readonly AsyncLocal<SinkScopeState?> CurrentOutputSinkSlot = new();
    private static readonly AsyncLocal<SinkScopeState?> CurrentErrorSinkSlot = new();

    internal static ICliRenderSink? CurrentOutputSink => CurrentOutputSinkSlot.Value?.Sink;

    internal static IDisposable EnsureOutputSinkScope(out ICliRenderSink sink)
    {
        var current = CurrentOutputSinkSlot.Value;
        if (current != null)
        {
            sink = current.Sink;
            return NoopDisposable.Instance;
        }

        sink = ConsoleSinkFactory.CreateOutputSink();
        return PushOutputSink(sink);
    }

    internal static IDisposable PushOutputSink(ICliRenderSink sink, bool plainBaseStyle = false)
        => Push(CurrentOutputSinkSlot, sink, plainBaseStyle);

    internal static IDisposable PushErrorSink(ICliRenderSink sink, bool plainBaseStyle = false)
        => Push(CurrentErrorSinkSlot, sink, plainBaseStyle);

    private static IDisposable Push(AsyncLocal<SinkScopeState?> slot, ICliRenderSink sink, bool plainBaseStyle)
    {
        ArgumentNullException.ThrowIfNull(sink);
        var previous = slot.Value;
        slot.Value = new SinkScopeState(sink, plainBaseStyle);
        return new SinkScope(slot, previous);
    }

    internal static ICliRenderSink GetOutputSink() =>
        CurrentOutputSinkSlot.Value?.Sink ?? ConsoleSinkFactory.CreateOutputSink();

    // Sink plus base-style policy, for the markup entry points that seed a base style from the
    // current console colours: (sink, true) means "use a plain base style".
    private static (ICliRenderSink Sink, bool PlainBaseStyle) GetOutputSinkWithPolicy()
    {
        var scope = CurrentOutputSinkSlot.Value;
        return scope is null
            ? (ConsoleSinkFactory.CreateOutputSink(), false)
            : (scope.Sink, scope.PlainBaseStyle);
    }

    private static (ICliRenderSink Sink, bool PlainBaseStyle) GetErrorSinkWithPolicy()
    {
        var scope = CurrentErrorSinkSlot.Value;
        return scope is null
            ? (ConsoleSinkFactory.CreateErrorSink(), false)
            : (scope.Sink, scope.PlainBaseStyle);
    }

    private sealed class SinkScope(AsyncLocal<SinkScopeState?> slot, SinkScopeState? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            slot.Value = previous;
            _disposed = true;
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
