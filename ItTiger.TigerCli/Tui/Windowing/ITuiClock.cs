namespace ItTiger.TigerCli.Tui.Windowing;

/// <summary>
/// Clock seam used only by the modal loop's inactivity-timeout math
/// (<see cref="InlineShell.RunModalAsync(Abstractions.ICliDialog, System.TimeSpan?, System.Threading.CancellationToken)"/>).
/// Production uses <see cref="SystemTuiClock"/> (the wall clock); tests can inject a
/// controllable clock so timeout behavior is deterministic and not dependent on
/// real-time scheduling.
/// </summary>
/// <remarks>
/// Only the deadline comparison reads this clock. Input polling deliberately stays on
/// the real timer, so test synchronization helpers such as
/// <c>TestTerminal.WaitForInputDrainedAsync</c> keep working unchanged.
/// </remarks>
internal interface ITuiClock
{
    DateTime UtcNow { get; }
}

/// <summary>Wall-clock <see cref="ITuiClock"/> used in production.</summary>
internal sealed class SystemTuiClock : ITuiClock
{
    public static readonly SystemTuiClock Instance = new();

    public DateTime UtcNow => DateTime.UtcNow;
}
