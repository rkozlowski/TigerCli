namespace ItTiger.TigerCli.Tui.Abstractions;

/// <summary>
/// Implemented by a dialog that has time-driven content (periodic overlays). The semi-interactive modal
/// loop calls <see cref="AdvanceAnimations"/> once per iteration on its own render thread and re-renders
/// only when it returns <c>true</c>. The shell owns the loop and timing; the dialog owns which tickers
/// exist and whether any of them changed.
/// </summary>
public interface IModalRefreshSource
{
    /// <summary>
    /// Advances every active periodic overlay to <paramref name="nowUtc"/> and returns <c>true</c> when
    /// at least one changed its visible output, so the modal loop should re-render. Returns <c>false</c>
    /// when there is nothing animated or nothing changed, so an idle dialog never re-renders merely
    /// because time passed.
    /// </summary>
    bool AdvanceAnimations(DateTime nowUtc);
}
