namespace ItTiger.TigerCli.Terminal;

internal static class TerminalTitleScope
{
    private static readonly AsyncLocal<TerminalTitleSession?> CurrentSlot = new();

    public static TerminalTitleSession? Current
    {
        get => CurrentSlot.Value;
        private set => CurrentSlot.Value = value;
    }

    public static IDisposable Push(TerminalTitleSession? session)
    {
        var previous = CurrentSlot.Value;
        CurrentSlot.Value = session;
        return new Scope(previous);
    }

    private sealed class Scope(TerminalTitleSession? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            CurrentSlot.Value = previous;
            _disposed = true;
        }
    }
}
