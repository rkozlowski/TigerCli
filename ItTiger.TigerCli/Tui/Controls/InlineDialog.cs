using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Modal inline dialog that hosts one <see cref="InlineControlBase"/> and renders it through the
/// shared <see cref="CliGrid"/> layout pipeline.
/// </summary>
/// <remarks>
/// The hosted control receives the first chance to handle each key. If it does not handle Enter or
/// Escape, the dialog applies the standard fallback: Enter confirms when the control can confirm,
/// and Escape requests <see cref="DialogResultKind.Cancel"/>. Optional
/// <see cref="InlineDialogConfirmationPolicy"/> instances can gate Cancel or Abort behind a
/// confirmation message box while keeping the original control alive. Controls may also complete the
/// modal asynchronously through <see cref="InlineControlBase.CompletionResult"/>.
/// <para>
/// Controls expose one or more <see cref="InlineDialogWidget"/> descriptors. The dialog places those
/// widgets into fixed <see cref="InlineDialogArea"/> regions, follows the focused widget for cursor,
/// active-point, scrollbar, and indicator behavior, and advances any activity overlays through
/// <see cref="IModalRefreshSource"/>.
/// </para>
/// </remarks>
public class InlineDialog(ICliAppShell shell, string? title, InlineControlBase control,
    string? label = null, CliFormattingMode labelMode = CliFormattingMode.Preformatted,
    InlineDialogConfirmationPolicy? confirmation = null) : DialogBase, IModalRefreshSource, IModalLifecycle
{
    private const int MinimumPreferredContentWidth = 4;

    /// <summary>The shell that owns modal input, rendering, theme, culture, and viewport policy.</summary>
    public ICliAppShell Shell { get; } = shell;

    /// <summary>Optional dialog title rendered above the frame.</summary>
    public string? Title { get; } = title;

    /// <summary>The hosted control whose grid, payload, hints, widgets, and key handling drive the dialog.</summary>
    public InlineControlBase Control { get; } = control;

    /// <summary>Optional constructor-supplied label rendered inside the frame above in-frame content.</summary>
    public string? Label { get; } = label;

    /// <summary>Formatting mode used for the constructor-supplied <see cref="Label"/>.</summary>
    public CliFormattingMode LabelMode { get; } = labelMode;

    // Generic optional confirmation gate (Cancel / Abort / both). Never confirms loop-produced kinds
    // (TokenCancel / Timeout / SystemCancel) because those are produced by the modal loop, not here.
    private readonly InlineDialogConfirmationPolicy _confirmation = confirmation ?? InlineDialogConfirmationPolicy.None;

    // Confirmation-mode state. When in confirmation mode the original control stays alive (its state,
    // background work, and tickers keep running); rendering and key handling temporarily switch to an
    // internal Yes/No message box. _pendingConfirmKind holds the originally requested result so a Yes
    // completes with exactly that kind.
    private DialogResultKind _pendingConfirmKind = DialogResultKind.NoResult;
    private InlineMessageBoxControl? _confirmControl;
    private bool InConfirmMode => _confirmControl != null;

    // The control that currently drives layout and key handling: the confirmation message box while
    // confirming, otherwise the hosted control.
    private InlineControlBase ActiveControl => _confirmControl ?? Control;

    DialogResultKind result = DialogResultKind.NoResult;

    // Completion precedence: a hosted control may complete the modal without a keypress (e.g. an async
    // operation finished). That wins over any key-/confirmation-driven result and even closes the dialog
    // while a confirmation message box is shown (completion precedence), so a finished operation is never
    // hidden behind a pending "are you sure?".
    /// <summary>
    /// The current dialog result. A control-supplied completion result wins over key- or
    /// confirmation-driven results so asynchronous controls can close the dialog without another key.
    /// </summary>
    public override DialogResultKind Result =>
        Control.CompletionResult != DialogResultKind.NoResult ? Control.CompletionResult : result;

    /// <summary>The payload exposed by the hosted control.</summary>
    public override object? Payload
    {
        get {  return Control.Payload; }
    }

    /// <summary>
    /// Handles a key by first routing it to the active control, then applying dialog fallback or
    /// confirmation policy when the control did not consume the key.
    /// </summary>
    public override bool HandleKey(KeyEvent key)
    {
        if (InConfirmMode)
            return HandleConfirmKey(key);

        // The hosted control gets the first chance at every key, including Enter/Escape.
        var controlResult = Control.HandleKey(key);
        if (controlResult.IsHandled)
        {
            // A control/widget (e.g. a focused button) may complete the dialog directly by
            // returning a concrete result; otherwise it just consumes the key.
            if (controlResult.Result != DialogResultKind.NoResult)
                RequestResult(controlResult.Result);
            return true;
        }

        // The control did not handle it: apply the dialog's confirm/cancel fallback.
        if (key.Mods == ConsoleModifiers.None)
        {
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    // Consume Enter either way; only commit a result when confirmation is allowed.
                    if (Control.CanConfirm)
                        result = DialogResultKind.Ok;
                    return true;
                case ConsoleKey.Escape:
                    RequestResult(DialogResultKind.Cancel);
                    return true;
            }
        }

        return false;
    }

    // Routes a key-produced result through the confirmation gate: confirmable kinds covered by the
    // policy enter confirmation mode instead of completing; everything else completes immediately.
    private void RequestResult(DialogResultKind kind)
    {
        if (_confirmation.ShouldConfirm(kind))
            EnterConfirmMode(kind);
        else
            result = kind;
    }

    private void EnterConfirmMode(DialogResultKind kind)
    {
        _pendingConfirmKind = kind;
        // Default the focus to No so an accidental Enter does not perform the cancel/abort.
        _confirmControl = new InlineMessageBoxControl(
            Shell, ConfirmMessage(kind), MessageBoxButtons.YesNo, DialogResultKind.No);
    }

    private void ExitConfirmMode()
    {
        _pendingConfirmKind = DialogResultKind.NoResult;
        _confirmControl = null;
    }

    // Key handling while the confirmation message box is shown. Yes completes with the originally
    // requested kind; No (and Escape) dismiss the confirmation and resume the original dialog. No is
    // never surfaced as the dialog result.
    private bool HandleConfirmKey(KeyEvent key)
    {
        var controlResult = _confirmControl!.HandleKey(key);
        if (controlResult.IsHandled)
        {
            if (controlResult.Result == DialogResultKind.Yes)
            {
                var kind = _pendingConfirmKind; // preserve the originally requested kind
                ExitConfirmMode();
                // Offer the hosted control a deferred completion (e.g. an activity that wants to switch
                // to "Cancelling…" and wait for its operation). If it takes over, the dialog stays open
                // and completes later through the control's CompletionResult; otherwise complete now.
                if (!Control.TryBeginDeferredCompletion(kind))
                    result = kind;
            }
            else if (controlResult.Result == DialogResultKind.No)
            {
                ExitConfirmMode();
            }
            return true;
        }

        // The message box leaves Escape unhandled; treat it as No (dismiss, resume original dialog).
        if (key.Mods == ConsoleModifiers.None && key.Key == ConsoleKey.Escape)
        {
            ExitConfirmMode();
            return true;
        }

        return false;
    }

    private string ConfirmMessage(DialogResultKind kind)
    {
        var custom = _confirmation.MessageProvider?.Invoke(kind);
        if (!string.IsNullOrEmpty(custom))
            return custom;

        var key = kind == DialogResultKind.Abort ? "Tui_Confirm_Abort_Message" : "Tui_Confirm_Cancel_Message";
        return TigerCliResources.Get(key, Shell.Culture);
    }

    private readonly record struct RowDefinition(
        InlineDialogArea Area, 
        string? Content, 
        CliFormattingMode? FormattingMode = null, 
        CliCellStyle? Style = null, 
        InlineDialogWidget? Widget = null
    );


    private sealed record InlineDialogAreaDefinition(
        InlineDialogArea Area,
        InlineDialogAreaType Type,
        string Code,
        int RowDelta,
        int Column,
        int ColumnSpan,
        int? LeftIndicatorColumn = null,
        int? RightIndicatorColumn = null,
        int? ScrollbarColumn = null,
        ThemeStyle? RowStyle = null
    ); 

    private static readonly InlineDialogAreaDefinition[] AreaDefinitions =
    [
        new(InlineDialogArea.Title, InlineDialogAreaType.Title, "T", 0, 0, 7, RowStyle: ThemeStyle.Background),
        new(InlineDialogArea.AboveFrame, InlineDialogAreaType.Widget, "WA", 0, 0, 7, RowStyle: ThemeStyle.Background),
        new(InlineDialogArea.AboveFrameWithIndicators, InlineDialogAreaType.Widget, "WAI", 0, 1, 5, 0, 6, RowStyle: ThemeStyle.Background),
        new(InlineDialogArea.TopFrame, InlineDialogAreaType.TopFrame, "FT", 0, 0, 5),
        new(InlineDialogArea.Label, InlineDialogAreaType.Label, "L", 1, 1, 3),
        new(InlineDialogArea.InFrameWithIndicators, InlineDialogAreaType.Widget, "WII", 1, 2, 1, 1, 3),
        new(InlineDialogArea.InFrameScrollable, InlineDialogAreaType.Widget, "WIS", 1, 1, 3, ScrollbarColumn: 4),
        new(InlineDialogArea.InFrame, InlineDialogAreaType.Widget, "WI", 1, 1, 3),
        new(InlineDialogArea.BottomFrame, InlineDialogAreaType.BottomFrame, "FB", 2, 0, 5),
        new(InlineDialogArea.BelowFrameWithIndicators, InlineDialogAreaType.Widget, "WBI", 2, 1, 5, 0, 6, RowStyle: ThemeStyle.Background),
        new(InlineDialogArea.BelowFrame, InlineDialogAreaType.Widget, "WB", 2, 0, 7, RowStyle: ThemeStyle.Background),
        new(InlineDialogArea.Status, InlineDialogAreaType.Status, "S", 2, 0, 7, RowStyle: ThemeStyle.Status)
    ];

    private static readonly Dictionary<InlineDialogArea, InlineDialogAreaDefinition> AreaDefinitionMap = InitAreaDefinitionMap();

    private static Dictionary<InlineDialogArea, InlineDialogAreaDefinition> InitAreaDefinitionMap()
    {
        var areaDefinitionMap = new Dictionary<InlineDialogArea, InlineDialogAreaDefinition>();
        foreach (var area in AreaDefinitions)
        {
            areaDefinitionMap[area.Area] = area;            
        }
        return areaDefinitionMap;
    }

    private CliGrid? _cachedGrid;
    private string? _cachedSignature;

    /// <summary>
    /// Builds the dialog grid from the hosted control's widget descriptors, labels, hints, frame,
    /// focus metadata, and overlays.
    /// </summary>
    public override CliGrid ToGrid()
    {
        // In confirmation mode the internal message box drives layout/keys; the hosted control stays
        // alive (state/tickers keep running) but is not rendered.
        var active = ActiveControl;
        var widgets = active.GetWidgets();

        var rowDefinitions = new Dictionary<InlineDialogArea, RowDefinition>();

        // The dialog title and constructor-supplied label belong to the hosted control's view; suppress
        // them while the confirmation message box is shown so it renders as a clean Yes/No prompt.
        if (Title != null && !InConfirmMode)
        {
            var row = new RowDefinition(InlineDialogArea.Title, Title,
                Style: new CliCellStyle() { HorizontalAlignment = CliTextAlignment.Left, FormattingMode = CliFormattingMode.Preformatted });
            rowDefinitions[row.Area] = row;
        }
        string? effectiveLabel = InConfirmMode ? active.ContentLabel : (Label ?? active.ContentLabel);
        CliFormattingMode effectiveLabelMode = InConfirmMode
            ? active.ContentLabelMode
            : (Label is not null ? LabelMode : active.ContentLabelMode);

        if (effectiveLabel != null)
        {
            var row = new RowDefinition(InlineDialogArea.Label, effectiveLabel,
                Style: LabelStyle(effectiveLabelMode));
            rowDefinitions[row.Area] = row;
        }
        var hint = active.Hint ?? string.Empty; // always render status bar
        var reservedHintWidth = Math.Max(hint.Length, active.HintReservedWidth);
        var hintMode = active.HintMode;
        {
            var row = new RowDefinition(InlineDialogArea.Status, hint,
                Style: new CliCellStyle() { HorizontalAlignment = CliTextAlignment.Left, FormattingMode = hintMode, MinWidth = reservedHintWidth });
            rowDefinitions[row.Area] = row;
        }

        // placeholders to simplify row counting
        rowDefinitions[InlineDialogArea.TopFrame] = new RowDefinition(InlineDialogArea.TopFrame, null);
        rowDefinitions[InlineDialogArea.BottomFrame] = new RowDefinition(InlineDialogArea.BottomFrame, null);

        if (widgets.Count == 0)
        {
            throw new TigerCliException("Inline control must define at least one widget", TigerCliRenderStage.ToGrid);
        }

        foreach (var w in widgets)
        {
            var def = AreaDefinitionMap[w.Area];
            if (def.Type != InlineDialogAreaType.Widget)
                throw new TigerCliException("Widget defined in wrong dialog area", TigerCliRenderStage.ToGrid);
            if (rowDefinitions.ContainsKey(w.Area))
                throw new TigerCliException("Cannot define more than one widget in the same area", TigerCliRenderStage.ToGrid);
            var widgetRow = new RowDefinition(w.Area, null, Style: w.ContentStyle, Widget: w);                
            rowDefinitions[widgetRow.Area] = widgetRow;
        }

        var totalRows = 0;
        var totalColumns = 7;
        var inFrameRows = 0;
        var topFrameRow = 0;
        var bottomFrameRow = 0;
        var rowNum = 0;
        var rowList = new List<RowDefinition>();
        var signature = new StringBuilder();
        foreach (var def in AreaDefinitions)
        {
            if (!rowDefinitions.ContainsKey(def.Area))
                continue;
            if (def.Area == InlineDialogArea.TopFrame)
                topFrameRow = rowNum;
            if (def.Area == InlineDialogArea.BottomFrame)
                bottomFrameRow = rowNum;            
            if (def.RowDelta == 1)
                inFrameRows++;
            rowNum++;
            var sigRow = rowDefinitions[def.Area];
            rowList.Add(sigRow);
            signature.Append($"{def.Code}:");
            // Overlay structure (which scrollbar/indicator overlays exist and where) depends on each
            // widget's decoration/scroll/thumb. Include them so a decoration change rebuilds the cached
            // grid; focus state is deliberately excluded so moving focus reuses the cached grid.
            if (sigRow.Widget is { } sigWidget)
                signature.Append($"d{(int)sigWidget.Decoration}s{(int)sigWidget.ScrollMode}t{(int)sigWidget.ThumbMode}:");
        }
        totalRows = rowNum;
        signature.Append($"{totalColumns}:{totalRows}");
        // Confirmation mode is a distinct structural state: include it so the cached grid rebuilds
        // cleanly when entering/leaving confirmation (and never reuses the hosted control's grid).
        signature.Append(InConfirmMode ? ":confirm" : ":normal");

        if (inFrameRows == 0)
            throw new TigerCliException("Inline control must define at least one in-frame row", TigerCliRenderStage.ToGrid);
        CliGrid g;
        bool isCachedGrid = _cachedGrid != null && signature.ToString() == _cachedSignature;
        if (!isCachedGrid)
        {
            g = ToGrid(totalColumns, totalRows);
            // The active control selects the dialog surface token (DialogSurface by default; a
            // warning/error message box returns its semantic surface). The confirmation message box is
            // a plain Yes/No, so confirmation mode resolves to the normal DialogSurface.
            g.DefaultCellStyle = Shell.Theme.Resolve(active.DialogSurfaceStyle);
            g.StylePrecedence = CliStylePrecedence.RowOverColumn;
            g.MinWidth = 10;
            g.MinHeight = 3;

            g.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle() { Width = 1 }));
            g.SetColumn(1, new CliGridColumnDefinition(new CliCellStyle() { Width = 1 }));
            g.SetColumn(3, new CliGridColumnDefinition(new CliCellStyle() { Width = 1 }));
            g.SetColumn(4, new CliGridColumnDefinition(new CliCellStyle() { Width = 1 }));
            g.SetColumn(5, new CliGridColumnDefinition(Shell.Theme.Resolve(ThemeStyle.Background)));
            g.SetColumn(6, new CliGridColumnDefinition(Shell.Theme.Resolve(ThemeStyle.Background))
            { Sizing = CliColumnSizing.Star }
                );

            var frameCharStyle = Shell.Theme.Resolve(ThemeStyle.Frame).CharStyle;
            var area = g.AddFrameArea(CliFrameJoinStyle.PreferDoubleJunctions, 0, topFrameRow, 4, bottomFrameRow, frameCharStyle);
            area.AddOuterFrame(new CliFrameSegment(CliFrameSegmentStyle.DoubleFrame), frameCharStyle);

            for (rowNum = 0; rowNum < rowList.Count; rowNum++)
            {
                var def = AreaDefinitionMap[rowList[rowNum].Area];
                if (def.RowStyle != null)
                {
                    g.SetRow(rowNum, new CliGridRowDefinition(Shell.Theme.Resolve(def.RowStyle.Value)));
                }
            }

            // Overlays are structural: created once when the grid is built, never re-added on a focus
            // change (AddOverlay rejects duplicate start cells). Their visibility stays dynamic — the
            // render callbacks read the active scrollable cell via GetVerticalScrollInfo() /
            // GetHorizontalScrollInfo(), which Stage 1's focus-driven ActivePoint selects, so only the
            // focused widget's overlays appear. CliGrid owns the measured scroll geometry.
            var scrollBarStyle = Shell.Theme.Resolve(ThemeStyle.ScrollBar).CharStyle ?? default;
            var indicatorStyle = Shell.Theme.Resolve(ThemeStyle.ScrollIndicator).CharStyle ?? default;
            for (rowNum = 0; rowNum < rowList.Count; rowNum++)
            {
                var overlayRow = rowList[rowNum];
                if (overlayRow.Widget is not { } overlayWidget)
                    continue;
                var def = AreaDefinitionMap[overlayRow.Area];
                var decoration = overlayWidget.Decoration;

                if ((decoration & CliControlDecoration.VerticalScrollBar) != 0 && def.ScrollbarColumn is int scrollbarColumn)
                {
                    g.AddOverlay(new CliOverlay(
                        new CliPoint(scrollbarColumn, topFrameRow + 1),
                        CliOrientation.Vertical,
                        bottomFrameRow - topFrameRow - 1,
                        scrollBarStyle,
                        CliOverlayRenderers.VerticalScrollBar()));
                }

                if ((decoration & CliControlDecoration.HorizontalIndicators) != 0
                    && def.LeftIndicatorColumn is int leftColumn
                    && def.RightIndicatorColumn is int rightColumn)
                {
                    g.AddOverlay(new CliOverlay(
                        new CliPoint(leftColumn, rowNum),
                        CliOrientation.Horizontal,
                        1,
                        indicatorStyle,
                        CliOverlayRenderers.HorizontalIndicator(CliOverlayEdge.Left)));
                    g.AddOverlay(new CliOverlay(
                        new CliPoint(rightColumn, rowNum),
                        CliOrientation.Horizontal,
                        1,
                        indicatorStyle,
                        CliOverlayRenderers.HorizontalIndicator(CliOverlayEdge.Right)));
                }
            }

            // Periodic-activity overlays (spinner / clock / progress). Like the scroll overlays above
            // they are added once as structural elements; visibility and content stay dynamic, driven by
            // the ticker the renderer closes over. They are deliberately excluded from the structural
            // signature, so animation never rebuilds the cached grid.
            AddActivityOverlays(g, rowList);

            _cachedGrid = g;
            _cachedSignature = signature.ToString();
        }
        else
        {
            g = _cachedGrid!;
        }

        // Focus is dynamic state: it is resolved on every refresh and never enters the structural
        // signature, so moving focus reuses the cached grid (see _cachedSignature above) rather than
        // rebuilding it. We capture the focused widget's host cell here so the parent grid's active
        // point can follow focus below.
        int focusedColumn = -1;
        int focusedRow = -1;
        // Cursor intent is propagated from the focused widget's own grid: a focused text input asks
        // for a Normal cursor, everything else (and "no focused widget") stays Hidden. The dialog only
        // forwards this intent — CliGrid still owns the measured cursor position.
        CursorMode focusedCursorMode = CursorMode.Hidden;
        for (rowNum = 0; rowNum < rowList.Count; rowNum++)
        {
            var rowDef = rowList[rowNum];
            if (rowDef.Area is (InlineDialogArea.TopFrame or InlineDialogArea.BottomFrame))
                continue;
            var def = AreaDefinitionMap[rowDef.Area];
            if (rowDef.Widget != null)
            {
                var w = rowDef.Widget;
                if (w.IsFocused)
                {
                    focusedColumn = def.Column;
                    focusedRow = rowNum;
                    focusedCursorMode = w.Grid.CursorMode;
                }
                g.Set(def.Column, rowNum,
                    content: null,
                    isFrameCell: false,
                    subgrid: w.Grid,
                    style: w.ContentStyle,
                    colSpan: def.ColumnSpan,
                    rowSpan: 1,
                    fillHorizontal: false,
                    fillVertical: false,
                    scrollMode: w.ScrollMode,
                    thumbMode: w.ThumbMode);
            }
            else
            {
                g.Set(def.Column, rowNum, rowDef.Content, rowDef.Style, def.ColumnSpan);
            }
        }

        // Drive the parent grid's active point from the focused widget's host cell so the correct
        // scrollable region is active when more than one widget is present. A single-widget dialog
        // leaves ActivePoint null so CliGrid auto-propagates the sole widget's active point (which
        // carries the in-line cursor offset the engine needs) — preserving existing behavior. This is
        // reset on every refresh because the cached grid persists across focus changes.
        if (widgets.Count > 1 && focusedColumn >= 0)
            g.ActivePoint = new ActivePoint(focusedColumn, focusedRow, 0);
        else
            g.ActivePoint = null;

        // Forward the focused widget's cursor intent. The shell makes the terminal cursor visible only
        // after rendering, and only when this is Normal and the measured grid has an active point.
        g.CursorMode = focusedCursorMode;

        return g;
    }

    // Maps each control-declared activity overlay onto its area row (area start column + offset) and
    // registers it through the normal overlay system. The strip spans from the offset to the end of the
    // area row — MaxLength caps the content, not the strip. The renderer reads the live ticker every
    // render: while active it writes the current content; while inactive it renders nothing, leaving the
    // underlying cells intact. Content wider than the overlay's declared MaxLength is a usage error and
    // throws; content within the contract that still exceeds the measured strip (a very narrow dialog)
    // renders nothing for that frame (DynamicText's soft-hide).
    private void AddActivityOverlays(CliGrid g, List<RowDefinition> rowList)
    {
        // Overlays follow the active control's layout. The confirmation message box exposes none, so no
        // activity overlays render while confirming (the hosted control's tickers still advance via
        // AdvanceAnimations — only their rendering pauses).
        foreach (var activity in ActiveControl.GetActivityOverlays())
        {
            if (!AreaDefinitionMap.TryGetValue(activity.Area, out var adef))
                continue;

            int activityRow = -1;
            for (int i = 0; i < rowList.Count; i++)
            {
                if (rowList[i].Area == activity.Area)
                {
                    activityRow = i;
                    break;
                }
            }

            if (activityRow < 0)
                continue; // area not present in this dialog's layout

            if (activity.MaxLength < 1)
                throw new TigerCliException(
                    $"Activity overlay on {activity.Area}: MaxLength must be at least 1 (was {activity.MaxLength}).",
                    TigerCliRenderStage.InvalidUsage);
            if (activity.ColumnOffset < 0 || activity.ColumnOffset >= adef.ColumnSpan)
                throw new TigerCliException(
                    $"Activity overlay on {activity.Area}: ColumnOffset {activity.ColumnOffset} is outside the area (span {adef.ColumnSpan}).",
                    TigerCliRenderStage.InvalidUsage);

            var ticker = activity.Ticker;
            int maxLength = activity.MaxLength;
            var renderer = CliOverlayRenderers.DynamicText(() =>
            {
                if (!ticker.IsActive)
                    return null;

                var content = activity.ContentFormatter?.Invoke(ticker.CurrentContent) ?? ticker.CurrentContent;
                if (content is not null && content.Length > maxLength)
                    throw new TigerCliException(
                        $"Activity overlay on {activity.Area}: content \"{content}\" is {content.Length} cells wide "
                        + $"and exceeds the overlay's declared MaxLength of {maxLength}.",
                        TigerCliRenderStage.InvalidUsage);
                return content;
            });

            // The strip covers up to MaxLength grid cells (every cell is at least one screen cell, so
            // that always reserves enough room for within-contract content), clamped to the area end.
            g.AddOverlay(new CliOverlay(
                new CliPoint(adef.Column + activity.ColumnOffset, activityRow),
                CliOrientation.Horizontal,
                Math.Min(activity.MaxLength, adef.ColumnSpan - activity.ColumnOffset),
                activity.Style,
                renderer));
        }
    }

    /// <summary>
    /// Per modal-loop iteration: lets the control apply state that changed off the loop (e.g. a
    /// completed async load) and advances every active periodic overlay to <paramref name="nowUtc"/>.
    /// Returns whether anything changed its visible output, so the loop re-renders. Tickers shared by
    /// multiple overlays are advanced once.
    /// </summary>
    public bool AdvanceAnimations(DateTime nowUtc)
    {
        // Apply off-loop state first so a freshly-applied result is reflected in this same render and
        // any now-inactive ticker (e.g. a stopped spinner) renders nothing.
        bool changed = Control.AdvanceState(nowUtc);

        var activities = Control.GetActivityOverlays();
        if (activities.Count > 0)
        {
            HashSet<TuiTicker> advanced = [];
            foreach (var activity in activities)
            {
                if (advanced.Add(activity.Ticker))
                    changed |= activity.Ticker.Advance(nowUtc);
            }
        }

        return changed;
    }

    internal string? GetActiveSpinnerFrame()
    {
        foreach (var activity in Control.GetActivityOverlays())
        {
            if (activity.Ticker is SpinnerTicker spinner && spinner.IsActive)
                return spinner.CurrentContent;
        }

        return null;
    }

    void IModalLifecycle.OnModalOpened(CancellationToken modalToken) => Control.OnModalOpened(modalToken);

    void IModalLifecycle.OnModalClosed() => Control.OnModalClosed();


    private CliCellStyle LabelStyle(CliFormattingMode labelMode)
    {
        return Shell.Theme.Resolve(ThemeStyle.Text).MergeWith(new CliCellStyle
        {
            HorizontalAlignment = CliTextAlignment.Left,
            FormattingMode = labelMode
        });
    }


}
