using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Activity;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Rich activity dialog control: a live <c>CliGrid</c>-backed layout (columns, rows, text and
/// progress-bar elements) driven by named dynamic row values, plus a background operation and a single
/// stop button (Cancel or Abort — never both). The operation runs on a background task and reports
/// values through an <see cref="ActivityContext"/>; updates are applied on the modal-loop thread in
/// <see cref="AdvanceState"/>. Completion, failure and a confirmed stop all close the dialog without a
/// keypress through <see cref="CompletionResult"/>.
/// </summary>
/// <remarks>
/// Layout/measurement is entirely owned by <c>CliGrid</c>: text cells are single-line and clipped by the
/// grid; progress bars are post-layout overlays (<see cref="CliOverlayRenderers.ProgressBar(Func{double}, char, char, char?, char?)"/>) over a
/// star column, so the grid resolves the bar width. The stop action flows through the hosting dialog's
/// generic confirmation policy; on a confirmed stop the control begins a deferred completion
/// (<see cref="TryBeginDeferredCompletion"/>): it requests operation cancellation, switches to a
/// "Cancelling…"/"Aborting…" view <em>with no action button</em> (the request has been accepted; the
/// dialog only waits), and closes once the operation observes cancellation.
/// </remarks>
public sealed class InlineActivityControl<T> : InlineControlBase
{
    private enum Phase { Running, Stopping, Done }

    private readonly ActivityDialogSpec _spec;
    private readonly ActivityState _state;
    private readonly Func<ActivityContext, CancellationToken, Task<T>> _operation;
    private readonly ActivityStopMode _stopMode;
    private readonly DialogResultKind _stopKind;
    private readonly bool _hasStarColumn;

    // The overlay's time source (raw frames). _spinner is the same instance when it is a SpinnerTicker the
    // dialog drives via Start()/Stop(); for an external non-SpinnerTicker source it stays null and the
    // caller owns the ticker's active state.
    private readonly TuiTicker _ticker;
    private readonly SpinnerTicker? _spinner;
    private readonly InlineActivityOverlay[] _activityOverlays;
    private readonly InlineButtonGroupWidget _buttons;

    // Snapshot of the row values used by the current render; replaced (on the loop thread) when the
    // operation pushes new values. Overlay/text builders read this, never the live locked state.
    private IReadOnlyDictionary<string, object?[]> _render;
    private CliGrid? _contentGrid;
    private bool _contentDirty = true;

    // Operation lifecycle. The op task only writes the guarded result fields and the volatile done flag;
    // it never touches widgets or renders.
    private CancellationTokenSource? _opCts;
    private Task? _opTask;
    private readonly object _opSync = new();
    private T? _opValue;
    private Exception? _opException;
    private volatile bool _opDone;

    private Phase _phase = Phase.Running;
    private DialogResultKind _completionResult = DialogResultKind.NoResult;

    /// <summary>Creates a control that runs and presents an asynchronous activity.</summary>
    /// <param name="shell">The shell that hosts the control.</param>
    /// <param name="spec">The activity layout and non-interactive presentation specification.</param>
    /// <param name="operation">The asynchronous activity to run.</param>
    /// <param name="stopMode">Whether the stop action is presented as cancellation or abort.</param>
    /// <param name="spinner">The spinner configuration, or <c>null</c> for the default spinner.</param>
    public InlineActivityControl(
        ICliAppShell shell,
        ActivityDialogSpec spec,
        Func<ActivityContext, CancellationToken, Task<T>> operation,
        ActivityStopMode stopMode = ActivityStopMode.Cancel,
        ActivitySpinnerSpec? spinner = null)
        : base(shell)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        _stopMode = stopMode;
        _stopKind = stopMode == ActivityStopMode.Abort ? DialogResultKind.Abort : DialogResultKind.Cancel;
        _state = new ActivityState(_spec);
        _render = _state.Snapshot();
        _hasStarColumn = _spec.Columns.Any(c => c.Sizing == CliColumnSizing.Star);

        (_ticker, _spinner) = (spinner ?? ActivitySpinnerSpec.Default).Build();
        _activityOverlays =
        [
            new InlineActivityOverlay
            {
                Area = InlineDialogArea.TopFrame,
                ColumnOffset = 1,
                // Content cap, not placement: bracketed frames up to 8 cells fit (Snake's two-column
                // frames use 4); a wider frame is a usage error the dialog throws on.
                MaxLength = InlineActivityOverlay.SpinnerMaxLength,
                Ticker = _ticker,
                ContentFormatter = static frame => $"[{frame}]",
                Style = Shell.Theme.Resolve(ThemeStyle.Frame).CharStyle ?? default,
            }
        ];

        // Exactly one stop action — Cancel or Abort, never both — so the button, the confirmation
        // prompt, and the in-progress state all describe the same intent.
        string buttonLabel = TigerCliResources.Get(
            stopMode == ActivityStopMode.Abort ? "Tui_Button_Abort" : "Tui_Button_Cancel", Shell.Culture);
        _buttons = new InlineButtonGroupWidget(shell,
            new[] { new InlineButtonWidget(shell, buttonLabel, _stopKind) });
        _buttons.HasFocus = true;
    }

    /// <summary>The value produced by the operation when it completed successfully.</summary>
    public T? OperationValue
    {
        get { lock (_opSync) return _opValue; }
    }

    /// <summary>The exception thrown by the operation, or <c>null</c> when it did not fault.</summary>
    public Exception? OperationException
    {
        get { lock (_opSync) return _opException; }
    }

    /// <summary>The operation task, exposed so callers can observe its completion if needed.</summary>
    public Task? OperationTask => _opTask;

    // No selectable payload; the produced value is read via OperationValue by the run API.
    /// <inheritdoc/>
    public override object? Payload => null;

    // Enter must never confirm via the dialog fallback; a focused button drives any result.
    /// <inheritdoc/>
    public override bool CanConfirm => false;

    // The hint names the single stop action so Esc and the button agree with what it says.
    /// <inheritdoc/>
    public override string? Hint => TigerCliResources.Get(
        _stopMode == ActivityStopMode.Abort ? "Tui_Activity_Hint_Abort" : "Tui_Activity_Hint", Shell.Culture);
    /// <inheritdoc/>
    public override CliFormattingMode HintMode => CliFormattingMode.Raw;

    /// <inheritdoc/>
    public override DialogResultKind CompletionResult => _completionResult;

    // Structurally stable single spinner overlay; its ticker's active state controls visibility.
    /// <inheritdoc/>
    public override IReadOnlyList<InlineActivityOverlay> GetActivityOverlays() => _activityOverlays;

    /// <inheritdoc/>
    public override void OnModalOpened(CancellationToken modalToken)
    {
        // Operation-scoped token, linked to the modal token so any modal exit also cancels the op.
        _opCts = CancellationTokenSource.CreateLinkedTokenSource(modalToken);
        var token = _opCts.Token;
        var context = new ActivityContext(_state);

        _spinner?.Start();

        // Not started with the token: the delegate must always run so it can observe cancellation
        // cooperatively and record its result/exception (mirrors the folder-picker load discipline).
        _opTask = Task.Run(async () =>
        {
            try
            {
                var value = await _operation(context, token).ConfigureAwait(false);
                lock (_opSync)
                    _opValue = value;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Cooperative cancellation observed: no value, no error.
            }
            catch (Exception ex)
            {
                lock (_opSync)
                    _opException = ex;
            }
            finally
            {
                _opDone = true;
            }
        });
    }

    /// <inheritdoc/>
    public override void OnModalClosed()
    {
        _state.Close();
        _spinner?.Stop();
        _opCts?.Cancel();
    }

    /// <inheritdoc/>
    public override bool AdvanceState(DateTime nowUtc)
    {
        bool changed = false;

        // Apply any coalesced value updates from the operation thread.
        if (_state.TryDrainSnapshot(out var snapshot))
        {
            _render = snapshot;
            _contentDirty = true;
            changed = true;
        }

        // Complete the modal once the operation finishes (no keypress needed).
        if (_completionResult == DialogResultKind.NoResult && _opDone)
        {
            // While stopping, the user's confirmed stop kind wins regardless of how the op ended;
            // otherwise the op's natural completion closes the dialog (Ok). The run API inspects the
            // captured exception to distinguish Completed from Failed.
            _completionResult = _phase == Phase.Stopping ? _stopKind : DialogResultKind.Ok;
            _phase = Phase.Done;
            _spinner?.Stop();
            changed = true;
        }

        return changed;
    }

    /// <inheritdoc/>
    public override bool TryBeginDeferredCompletion(DialogResultKind kind)
    {
        // Only the dialog's single stop action is deferred here; any other kind is left to the dialog.
        if (kind != _stopKind)
            return false;
        if (_phase != Phase.Running)
            return false;

        // Request operation cancellation and switch to the "Cancelling…"/"Aborting…" view; the dialog
        // stays open (spinner keeps animating) and completes via CompletionResult once the op stops.
        _phase = Phase.Stopping;
        _opCts?.Cancel();
        _contentDirty = true;
        return true;
    }

    /// <inheritdoc/>
    public override InlineKeyResult HandleKey(KeyEvent key)
    {
        // Once stopping/done, swallow keys so a second Esc/stop cannot re-enter confirmation.
        if (_phase != Phase.Running)
            return InlineKeyResult.Handled;

        // Escape is the keyboard shortcut for this dialog's single stop action, so it must request the
        // configured stop kind (Cancel or Abort) rather than the dialog's generic Cancel fallback —
        // otherwise an Abort-mode dialog would confirm/complete as Cancel.
        if (key.Mods == ConsoleModifiers.None && key.Key == ConsoleKey.Escape)
            return InlineKeyResult.WithResult(_stopKind);

        return _buttons.HandleKey(key);
    }

    /// <inheritdoc/>
    public override IReadOnlyList<InlineDialogWidget> GetWidgets()
    {
        if (_contentGrid is null || _contentDirty)
        {
            _contentGrid = BuildContentGrid();
            _contentDirty = false;
        }

        var content = new InlineDialogWidget
        {
            Area = InlineDialogArea.InFrame,
            Grid = _contentGrid,
            IsFocused = false,
            ScrollMode = CliScrollMode.None,
        };

        // Once the stop has been confirmed (or the op has otherwise finished), the request is already
        // accepted and the dialog is only waiting for the operation to stop — show no action button.
        if (_phase != Phase.Running)
            return new[] { content };

        return new[]
        {
            content,
            new InlineDialogWidget
            {
                Area = InlineDialogArea.BelowFrame,
                Grid = _buttons.ToGrid(),
                IsFocused = true,
            },
        };
    }

    /// <inheritdoc/>
    public override CliGrid ToGrid() => _contentGrid ??= BuildContentGrid();

    private CliGrid BuildContentGrid()
    {
        if (_phase == Phase.Stopping)
            return BuildStoppingGrid();

        var theme = Shell.Theme;
        var grid = new CliGrid(_spec.Columns.Count, _spec.Rows.Count)
        {
            DefaultCellStyle = theme.Resolve(ThemeStyle.DialogSurface),
            StylePrecedence = CliStylePrecedence.RowOverColumn,
        };

        // Sizing the activity content to a viewport-derived width is the dialog's own choice; the star
        // column then absorbs the remainder so a progress bar has space. CliGrid still owns the actual
        // width resolution and the bar overlay reads that resolved width.
        if (_hasStarColumn)
            grid.SoftMaxWidth = TargetWidth();

        for (int c = 0; c < _spec.Columns.Count; c++)
        {
            var col = _spec.Columns[c];
            var colStyle = new CliCellStyle(theme.Resolve(col.Style ?? ThemeStyle.Text).CharStyle)
            {
                HorizontalAlignment = col.Align,
            };
            if (col.Width.HasValue)
                colStyle.Width = col.Width.Value;
            if (col.Padding is CliCellPadding colPadding)
                colStyle.Padding = colPadding; // a cell's own padding overrides this via the grid cascade

            grid.SetColumn(c, new CliGridColumnDefinition(colStyle) { Sizing = col.Sizing });
        }

        for (int r = 0; r < _spec.Rows.Count; r++)
        {
            var row = _spec.Rows[r];
            foreach (var cell in row.Cells)
                PlaceCell(grid, theme, row, cell, r);
        }

        return grid;
    }

    private void PlaceCell(CliGrid grid, ITheme theme, ActivityRowSpec row, ActivityCellSpec cell, int rowIndex)
    {
        var col = _spec.Columns[cell.Column];

        switch (cell.Element)
        {
            case ActivityTextElement text:
            {
                var values = ValuesFor(row);
                string content = SafeMarkupFormatter.Format(text.Template, values, Shell.Culture);

                // Precedence: text element explicit > column default > spec default > built-in fallback.
                ThemeStyle styleKey = text.Style ?? col.Style ?? _spec.DefaultCellStyle ?? ThemeStyle.Text;
                CliTextAlignment alignment =
                    text.Alignment ?? col.Align ?? _spec.DefaultCellAlignment ?? CliTextAlignment.Left;

                var style = new CliCellStyle(theme.Resolve(styleKey).CharStyle)
                {
                    HorizontalAlignment = alignment,
                    FormattingMode = CliFormattingMode.Preformatted, // value already safe-formatted/escaped
                    Wrapping = CliWrapping.SingleLineTruncate,        // CliGrid owns truncation/clipping
                };
                // Cell padding overrides the column padding through the grid's cell-style cascade; only set
                // it when explicit so an unset cell inherits the column padding rather than clearing it.
                if (text.Padding is CliCellPadding textPadding)
                    style.Padding = textPadding;
                grid.Set(cell.Column, rowIndex, content, style, colSpan: cell.Span);
                break;
            }

            case ActivityProgressBarElement bar:
            {
                // Empty underlying cell so the grid produces a full-width space line for the overlay to
                // overwrite; the bar itself is a post-layout overlay that fills the resolved width.
                var underlay = new CliCellStyle
                {
                    FormattingMode = CliFormattingMode.Raw,
                    Wrapping = CliWrapping.SingleLine,
                };
                grid.Set(cell.Column, rowIndex, string.Empty, underlay, colSpan: cell.Span);

                string rowName = row.Name!; // progress bars require a dynamic row (validated)
                Func<double> fraction = () => bar.Fraction(CurrentValues(rowName));
                grid.AddOverlay(BuildBarOverlay(
                    theme, col, bar, new CliPoint(cell.Column, rowIndex), cell.Span, fraction));
                break;
            }
        }
    }

    // Builds the bar's post-layout overlay. Single (default) keeps the original uniform char path unchanged
    // (the char overlay constructor adapts it onto the styled pipeline internally). The multi-colour modes
    // use the styled overload with one glyph per cell, distinguished only by colour; theme styles are
    // resolved here (the overlay stays theme-agnostic) — done/remaining for two colours plus a complete
    // colour for three, which the styled renderer applies only at exactly 100%.
    private static CliOverlay BuildBarOverlay(
        ITheme theme, ActivityColumnSpec col, ActivityProgressBarElement bar, CliPoint start, int span, Func<double> fraction)
    {
        var (leftCap, rightCap) = ResolveBarCaps(bar.Caps);

        if (bar.ColorMode == ProgressBarColorMode.Single)
        {
            var barStyle = theme.Resolve(col.Style ?? ThemeStyle.Accent).CharStyle ?? default;
            var (filled, track) = ResolveBarGlyphs(bar.Style);
            return new CliOverlay(start, CliOrientation.Horizontal, span, barStyle,
                CliOverlayRenderers.ProgressBar(fraction, filled, track, leftCap, rightCap));
        }

        char glyph = ResolveBarSolidGlyph(bar.Style);
        var doneStyle = theme.Resolve(ThemeStyle.ProgressBarDone).CharStyle ?? default;
        var done = new CliOverlayGlyph(glyph, doneStyle);
        var remaining = new CliOverlayGlyph(glyph, theme.Resolve(ThemeStyle.ProgressBarRemaining).CharStyle);
        CliOverlayGlyph? complete = bar.ColorMode == ProgressBarColorMode.ThreeColor
            ? new CliOverlayGlyph(glyph, theme.Resolve(ThemeStyle.ProgressBarComplete).CharStyle)
            : null;

        // The glyphs carry their own styles, so the overlay base style only backs the caps; use the done ink
        // so brackets read in the bar's primary colour.
        return new CliOverlay(start, CliOrientation.Horizontal, span, doneStyle,
            CliOverlayRenderers.ProgressBar(fraction, done, remaining, complete, leftCap, rightCap));
    }

    // Maps a predefined bar style to its single-colour (filled, track) glyph pair. The colour is uniform
    // (owned by the overlay's CharStyle), so a style only changes glyphs — never the colour.
    private static (char filled, char track) ResolveBarGlyphs(ProgressBarStyle style) => style switch
    {
        ProgressBarStyle.Line        => (ConsoleSymbol.HeavyHorizontal, ConsoleSymbol.SingleH),
        ProgressBarStyle.Square      => (ConsoleSymbol.Square, ConsoleSymbol.WhiteSquare),
        ProgressBarStyle.VerticalBar => (ConsoleSymbol.BlackVerticalRectangle, ConsoleSymbol.WhiteVerticalRectangle),
        ProgressBarStyle.Dash        => (ConsoleSymbol.BlackParallelogram, ConsoleSymbol.WhiteParallelogram),
        _                            => (ConsoleSymbol.FullBlock, ConsoleSymbol.ShadeLight),
    };

    // The single solid glyph a multi-colour bar repeats across every cell (done/remaining/complete are
    // distinguished by colour, not glyph). Uses the "filled" glyph of the chosen family.
    private static char ResolveBarSolidGlyph(ProgressBarStyle style) => style switch
    {
        ProgressBarStyle.Line        => ConsoleSymbol.HeavyHorizontal,
        ProgressBarStyle.Square      => ConsoleSymbol.Square,
        ProgressBarStyle.VerticalBar => ConsoleSymbol.BlackVerticalRectangle,
        ProgressBarStyle.Dash        => ConsoleSymbol.BlackParallelogram,
        _                            => ConsoleSymbol.FullBlock,
    };

    // Maps the optional cap decoration to its end-cap glyphs; caps compose with any bar style and share the
    // bar's (base) colour via the overlay's CharStyle. Cap colouring is deferred to a later slice.
    private static (char? leftCap, char? rightCap) ResolveBarCaps(ProgressBarCaps caps) => caps switch
    {
        ProgressBarCaps.Brackets => ('[', ']'),
        _                        => (null, null),
    };

    private CliGrid BuildStoppingGrid()
    {
        var theme = Shell.Theme;
        var message = TigerCliResources.Get(
            _stopMode == ActivityStopMode.Abort ? "Tui_Activity_Aborting" : "Tui_Activity_Cancelling",
            Shell.Culture);

        var grid = new CliGrid(1, 1) { DefaultCellStyle = theme.Resolve(ThemeStyle.DialogSurface) };
        if (_hasStarColumn)
            grid.SoftMaxWidth = TargetWidth();

        grid.Set(0, 0, message, new CliCellStyle(theme.Resolve(ThemeStyle.Text).CharStyle)
        {
            HorizontalAlignment = CliTextAlignment.Left,
            FormattingMode = CliFormattingMode.Preformatted,
            Wrapping = CliWrapping.SingleLineTruncate,
        });
        return grid;
    }

    private IReadOnlyList<object?> ValuesFor(ActivityRowSpec row) =>
        row.IsDynamic ? CurrentValues(row.Name!) : Array.Empty<object?>();

    private IReadOnlyList<object?> CurrentValues(string rowName) =>
        _render.TryGetValue(rowName, out var values) ? values : Array.Empty<object?>();

    // Overhead the dialog frame (and a small safety margin) adds around the activity content; the content
    // must stay within the viewport minus this so it can never overflow the frame on a narrow terminal.
    private const int FrameChrome = 10;

    // A comfortable width a star column claims when nothing external constrains it (a content-sized dialog
    // gives star columns no "remaining space" to absorb), so the progress bar is usable without the dialog
    // ballooning to the full terminal width on a wide screen. Matches a typical fixed progress column.
    private const int DefaultStarContent = 40;

    // The width the activity content (with a star column) asks CliGrid to lay out into. This decides only
    // how wide the dialog is — a legitimate dialog concern, since a content-sized frame offers a star
    // column no remainder to fill; CliGrid still owns distributing this width to the star column(s).
    private int TargetWidth()
    {
        int fixedFootprint = 0;
        int starCount = 0;
        foreach (var c in _spec.Columns)
        {
            if (c.Width is int w)
                fixedFootprint += w;
            else if (c.Sizing == CliColumnSizing.Star)
                starCount++;
            fixedFootprint += PaddingWidth(c.Padding);
        }

        // Size to content (fixed columns + a sensible amount per star column) so the dialog does not
        // balloon on a wide terminal...
        int desired = fixedFootprint + DefaultStarContent * Math.Max(1, starCount);

        // ...but never exceed the width the dialog actually has, so the content cannot overflow the frame
        // (or the terminal) on a narrow one. The star column simply gets less room in that case.
        int available = Shell.Viewport.Width - FrameChrome;

        return Math.Max(1, Math.Min(desired, available));
    }

    private static int PaddingWidth(CliCellPadding? padding) => padding switch
    {
        CliCellPadding.Left or CliCellPadding.Right => 1,
        CliCellPadding.Both => 2,
        _ => 0,
    };
}
