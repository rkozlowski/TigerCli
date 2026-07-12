using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Rendering;

internal sealed record CliOutputPresetDefaults(
    CliTableStylePreset Details,
    CliTableStylePreset List,
    CliTableStylePreset? Table);

internal static class CliOutputPresetContext
{
    private static readonly AsyncLocal<CliOutputPresetDefaults?> CurrentSlot = new();

    public static CliOutputPresetDefaults? Current => CurrentSlot.Value;

    public static IDisposable Push(CliOutputPresetDefaults? presets)
    {
        if (presets is null)
            return NoopDisposable.Instance;

        var previous = CurrentSlot.Value;
        CurrentSlot.Value = presets;
        return new Scope(previous);
    }

    private sealed class Scope(CliOutputPresetDefaults? previous) : IDisposable
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

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
