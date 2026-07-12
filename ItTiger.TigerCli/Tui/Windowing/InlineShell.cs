using System.Globalization;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;

namespace ItTiger.TigerCli.Tui.Windowing;

internal sealed class InlineShell : ICliAppShell
{
    public static InlineShell Instance { get; } = new InlineShell();

    private readonly int viewportHeightPercent;
    private readonly ITuiClock _clock;
    private readonly CancellationToken _systemCancellation;

    // The singleton reports its interaction mode from the per-run ambient scope (falling back to the
    // stored value); explicitly constructed shells own their mode and never consult the scope. Mirrors
    // the EffectiveSystemCancellation deferral so a no-shell TigerTui call sees the run's real mode.
    private readonly bool _deferModeToAmbient;
    private readonly TigerCliInteractionMode _interactionMode;

    private InlineShell()
    {
        Terminal = new ConsoleTerminal();
        _interactionMode = TigerCliInteractionMode.SemiInteractive;
        _deferModeToAmbient = true;
        viewportHeightPercent = 50;
        _clock = SystemTuiClock.Instance;
        _systemCancellation = default;
        Viewport = new Size(Terminal.WindowWidth, Terminal.WindowHeight * viewportHeightPercent / 100);
    }

    public InlineShell(
        ICliTerminal terminal,
        int viewportHeightPercent = 100,
        TigerCliInteractionMode interactionMode = TigerCliInteractionMode.SemiInteractive,
        ITuiClock? clock = null,
        CancellationToken systemCancellation = default)
    {
        Terminal = terminal;
        _interactionMode = interactionMode;
        _deferModeToAmbient = false;
        this.viewportHeightPercent = viewportHeightPercent;
        _clock = clock ?? SystemTuiClock.Instance;
        _systemCancellation = systemCancellation;
        Viewport = new Size(Terminal.WindowWidth, Terminal.WindowHeight * viewportHeightPercent / 100);
    }

    // The effective process/system cancellation token: an instance-injected token (if any) wins,
    // otherwise the per-run ambient token set by TigerCliApp via SystemCancellationScope. A default
    // token can never be cancelled.
    private CancellationToken EffectiveSystemCancellation =>
        _systemCancellation.CanBeCanceled ? _systemCancellation : SystemCancellationScope.Current;

    public ICliTerminal Terminal { get; private set; }

    public ITheme Theme => TigerConsole.CurrentTheme;
    public bool IsFullWindow => false;
    public Size Viewport { get; private set; }

    // For the singleton, defer to the run's ambient interaction mode so a no-shell TigerTui call inside
    // a command handler observes --non-interactive; an explicit shell always reports its own mode.
    public TigerCliInteractionMode InteractionMode =>
        _deferModeToAmbient ? (InteractionModeScope.Current ?? _interactionMode) : _interactionMode;

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    internal void SetCulture(CultureInfo culture) => Culture = culture;

    public async Task<DialogResult> RunModalAsync(ICliDialog dialog, CancellationToken ct = default)
    {
        return await RunModalAsync(dialog, timeout: null, ct).ConfigureAwait(false);
    }

    public async Task<DialogResult> RunModalAsync(ICliDialog dialog, TimeSpan? timeout, CancellationToken ct = default)
    {
        if (InteractionMode == TigerCliInteractionMode.NonInteractive)
            return new DialogResult(DialogResultKind.InteractionNotAllowed, null);

        var initialState = Terminal.State;
        var inactivityDeadline = timeout.HasValue ? _clock.UtcNow + timeout.Value : (DateTime?)null;

        int startRow = Terminal.CursorTop;
        int renderedLines = 0;

        // Stable anchor captured once at modal entry; used as the top of the
        // resize-time blanket clear so reflow cannot leave artifacts above the
        // tracked last-rendered region.
        int modalTop = startRow;

        // Tracked footprint of the last completed render. Used for the precise
        // trim on key-driven re-renders; intentionally NOT relied on as the sole
        // anchor for resize cleanup (terminal reflow can move artifacts off it).
        int lastRenderedStartRow = startRow;
        int lastRenderedHeight = 0;

        var bgColor = Theme.Resolve(ThemeStyle.Background).CharStyle?.Background ?? Terminal.BackgroundColor;

        // Process/system cancellation (Ctrl-C / SIGINT / SIGTERM / SIGQUIT). Distinct from the caller
        // token and the timeout; when tripped the modal completes with SystemCancel, which takes
        // precedence over both. A default token can never be cancelled, so non-app callers are unaffected.
        var systemToken = EffectiveSystemCancellation;

        // Modal-scoped cancellation: cancelled when the modal exits for any reason, so a dialog's
        // background work (e.g. an async folder load) is cancelled/ignored once the modal closes.
        // Linked to the caller's token and the system token so external/system cancellation also flows through.
        using var modalCts = CancellationTokenSource.CreateLinkedTokenSource(ct, systemToken);
        (dialog as IModalLifecycle)?.OnModalOpened(modalCts.Token);

        try
        {
            Terminal.CursorVisible = false;

            var sink = Terminal.Sink;

            void RemeasureAndRender()
            {
                int prevStart = lastRenderedStartRow;
                int prevHeight = lastRenderedHeight;

                // Hide the cursor before drawing so it never appears to move across the screen while
                // the grid is being written. It is shown again (if required) only after the render.
                Terminal.CursorVisible = false;

                var g = dialog.ToGrid();
                g.SoftMaxWidth = Viewport.Width;
                g.SoftMaxHeight = Viewport.Height;
                g.Measure(sink);
                (startRow, renderedLines) = RenderAndAdjust(g, startRow);

                // Clear any portion of the previous footprint not overdrawn by the new
                // render. Computed against the tracked previous region (not the new
                // startRow + height), so a scroll-induced startRow shift can never
                // leave a stale top band or a stale bottom band.
                if (prevHeight > 0)
                {
                    int prevEnd = prevStart + prevHeight;
                    int newEnd = startRow + renderedLines;

                    if (prevStart < startRow)
                    {
                        int topEnd = Math.Min(prevEnd, startRow);
                        if (topEnd > prevStart)
                            Terminal.ClearLines(prevStart, topEnd - prevStart, bgColor);
                    }
                    if (newEnd < prevEnd)
                    {
                        int botStart = Math.Max(newEnd, prevStart);
                        if (prevEnd > botStart)
                            Terminal.ClearLines(botStart, prevEnd - botStart, bgColor);
                    }
                }

                lastRenderedStartRow = startRow;
                lastRenderedHeight = renderedLines;
                ApplyCursorMode(g, startRow);
            }

            RemeasureAndRender();
            UpdateTitleSpinnerPrefix();

            int prevWidth = Terminal.WindowWidth;
            int prevHeight = Terminal.WindowHeight;

            while (true)
            {
                // SystemCancel has the highest precedence: whenever the system token was tripped the
                // modal completes with SystemCancel, even if the caller token or timeout also fired.
                if (systemToken.IsCancellationRequested)
                    return new DialogResult(DialogResultKind.SystemCancel, null);

                if (ct.IsCancellationRequested)
                    return new DialogResult(DialogResultKind.TokenCancel, null);

                if (inactivityDeadline.HasValue && _clock.UtcNow >= inactivityDeadline.Value)
                    return new DialogResult(DialogResultKind.Timeout, null);

                int curWidth = Terminal.WindowWidth;
                int curHeight = Terminal.WindowHeight;
                if (curWidth != prevWidth || curHeight != prevHeight)
                {
                    prevWidth = curWidth;
                    prevHeight = curHeight;
                    Viewport = new Size(curWidth, curHeight * viewportHeightPercent / 100);

                    // Blank the entire visible window from row 0 down to the bottom.
                    // Windows Terminal reflow can leave artifacts above any tracked
                    // anchor (modalTop or lastRenderedStartRow), so the only reliable
                    // recovery is to wipe the whole viewport. Command-line context above
                    // the dialog is sacrificed on resize, which is acceptable here.
                    // ClearLines clamps to buffer bounds and silently no-ops if the
                    // window is too small.
                    if (curHeight > 0)
                        Terminal.ClearLines(0, curHeight, bgColor);

                    // Tracked footprint is no longer meaningful after a blanket clear.
                    renderedLines = 0;
                    lastRenderedHeight = 0;

                    // Re-anchor: prefer modalTop if it still sits on-screen, else row 0.
                    if (curHeight > 0)
                        startRow = (modalTop >= 0 && modalTop < curHeight) ? modalTop : 0;
                    lastRenderedStartRow = startRow;

                    RemeasureAndRender();
                    UpdateTitleSpinnerPrefix();
                    continue;
                }

                // Periodic overlays (spinner / clock / progress) advance on the shell's clock. The loop
                // owns the timing: it asks the dialog to advance its tickers and re-renders only when a
                // visible frame changed. This runs on the render thread, never blocks input (the key
                // check is immediately below), and stays deterministic under a manual clock.
                if (dialog is IModalRefreshSource refreshSource && refreshSource.AdvanceAnimations(_clock.UtcNow))
                {
                    RemeasureAndRender();
                    UpdateTitleSpinnerPrefix();
                }

                // A control may complete the modal from AdvanceState (no keypress) — e.g. an async
                // activity operation finished. AdvanceAnimations pumps AdvanceState above, so check the
                // dialog result here and break before waiting for a key.
                if (dialog.Result != DialogResultKind.NoResult)
                    break;

                if (!Terminal.KeyAvailable)
                {
                    var delay = TimeSpan.FromMilliseconds(10);
                    if (inactivityDeadline.HasValue)
                    {
                        var remaining = inactivityDeadline.Value - _clock.UtcNow;
                        if (remaining <= TimeSpan.Zero)
                            return new DialogResult(DialogResultKind.Timeout, null);

                        if (remaining < delay)
                            delay = remaining;
                    }

                    try
                    {
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return new DialogResult(DialogResultKind.TokenCancel, null);
                    }
                    continue;
                }

                var k = Terminal.ReadKey(intercept: true);
                if (timeout.HasValue)
                    inactivityDeadline = _clock.UtcNow + timeout.Value;

                dialog.HandleKey(new KeyEvent(k.Key, k.Modifiers, k.KeyChar));

                if (dialog.Result != DialogResultKind.NoResult)
                    break;

                RemeasureAndRender();
                UpdateTitleSpinnerPrefix();
            }

            return new DialogResult(dialog.Result, dialog.Payload);
        }
        finally
        {
            // Cancel modal-scoped work and notify the dialog before restoring the terminal, so any
            // pending background result is abandoned rather than applied to a closed control.
            modalCts.Cancel();
            (dialog as IModalLifecycle)?.OnModalClosed();
            TerminalTitleScope.Current?.SetSpinnerPrefix(null);
            Terminal.RestoreState(initialState, lastRenderedStartRow, lastRenderedHeight);
        }

        void UpdateTitleSpinnerPrefix()
        {
            if (dialog is InlineDialog inlineDialog)
                TerminalTitleScope.Current?.SetSpinnerPrefix(inlineDialog.GetActiveSpinnerFrame());
            else
                TerminalTitleScope.Current?.SetSpinnerPrefix(null);
        }
    }

    private (int startRow, int renderedLines) RenderAndAdjust(CliGrid grid, int startRow)
    {
        Terminal.RenderGrid(0, startRow, grid);

        // If render overflowed the buffer the console scrolled: cursor advanced fewer
        // rows than the grid's measured height. Slide startRow back by the deficit and
        // clamp at 0 so it never points above the buffer top.
        int expectedHeight = grid.MeasuredHeight ?? 0;
        int actualAdvance = Terminal.CursorTop - startRow;
        if (actualAdvance < expectedHeight)
            startRow = Math.Max(0, startRow - (expectedHeight - actualAdvance));

        int renderedLines = Terminal.CursorTop - startRow;
        return (startRow, renderedLines);
    }

    // Sole authority for terminal cursor visibility after a render: show the cursor only when the
    // focused grid asks for it (CursorMode != Hidden) and the measured grid produced an active point,
    // and only after positioning it at the measured location. Otherwise the cursor stays hidden, so a
    // focus change away from a text input makes it disappear.
    private void ApplyCursorMode(CliGrid grid, int startRow)
    {
        if (grid.CursorMode != CursorMode.Hidden && grid.MeasuredActivePoint is { } ap)
        {
            var origin = grid.GetMeasuredCellOrigin(ap.Column, ap.Row) ?? new CliPoint(0, 0);
            Terminal.SetCursorPosition(
                origin.Column + ap.OffsetInLine,
                startRow + origin.Row + ap.LineIndex);
            Terminal.CursorVisible = true;
        }
        else
        {
            Terminal.CursorVisible = false;
        }
    }
}
