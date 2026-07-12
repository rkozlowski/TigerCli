using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tui.Abstractions;

/// <summary>
/// Base class for dialog-hostable inline controls. Controls render themselves as grids, handle keys
/// through <see cref="InlineKeyResult"/>, and can expose dialog metadata such as hints, labels, widgets,
/// overlays, and completion state.
/// </summary>
public abstract class InlineControlBase : CliRenderableComponent
{
    /// <summary>The shell hosting this control.</summary>
    public ICliAppShell Shell { get; private set; }

    /// <summary>The scroll modes requested for the control's default widget.</summary>
    public virtual CliScrollMode ScrollMode { get; protected set; }

    /// <summary>The scroll indicators or scrollbars requested for the control's default widget.</summary>
    public virtual CliControlDecoration ControlDecoration { get; protected set; }

    /// <summary>Optional status/hint text shown by the hosting dialog.</summary>
    public virtual string? Hint => null;

    /// <summary>Formatting mode for <see cref="Hint"/>.</summary>
    public virtual CliFormattingMode HintMode => CliFormattingMode.Raw;

    /// <summary>
    /// A stable width (in cells) the hosting <c>InlineDialog</c> reserves for the status/hint bar, so
    /// that changing the (focus-aware) <see cref="Hint"/> text does not change the dialog width. The
    /// default is the current hint's width — correct for controls whose hint never changes. Composite
    /// controls whose hint varies with focus override this to return the widest hint they can surface,
    /// keeping the dialog width focus-stable.
    /// </summary>
    public virtual int HintReservedWidth => Hint?.Length ?? 0;

    /// <summary>
    /// Optional control-driven label shown above the content, refreshed on each render. Used when the
    /// hosting dialog has no constructor-supplied label.
    /// </summary>
    public virtual string? ContentLabel => null;

    /// <summary>Formatting mode for <see cref="ContentLabel"/>.</summary>
    public virtual CliFormattingMode ContentLabelMode => CliFormattingMode.Raw;

    /// <summary>True when the hosting dialog may complete this control with an Enter/OK action.</summary>
    public virtual bool CanConfirm => true;
    //public virtual int? PreferredContentWidth => null;

    /// <summary>Optional content style applied to the control's default widget host cell.</summary>
    public virtual CliCellStyle? ContentStyle => null;

    /// <summary>
    /// The theme surface token the hosting <c>InlineDialog</c> resolves for this control's dialog
    /// background (frame body + content). The default is <see cref="ThemeStyle.DialogSurface"/>;
    /// controls with a semantic severity (e.g. a warning/error message box) override this to return a
    /// semantic surface token, keeping the dialog background theme-driven rather than hard-coded.
    /// </summary>
    public virtual ThemeStyle DialogSurfaceStyle => ThemeStyle.DialogSurface;

    /// <summary>Controls whether scroll thumbs track offsets or the logical active point.</summary>
    public virtual CliScrollThumbMode ThumbMode => CliScrollThumbMode.Offset;

    /// <summary>
    /// The dialog area this control's single top-level widget is placed into. Controls that expose
    /// multiple widgets override <see cref="GetWidgets"/> instead and may ignore this.
    /// </summary>
    public virtual InlineDialogArea DialogArea => InlineDialogArea.InFrame;

    /// <summary>
    /// Exposes the top-level widgets the hosting <c>InlineDialog</c> should place. The default
    /// wraps the control's own <see cref="CliRenderableComponent.ToGrid()"/> output into one focused
    /// widget, preserving the legacy single-content behavior. Composite controls override this to
    /// expose several widgets across different <see cref="InlineDialogArea"/> areas.
    /// </summary>
    public virtual IReadOnlyList<InlineDialogWidget> GetWidgets()
    {
        return new[]
        {
            new InlineDialogWidget
            {
                Area = DialogArea,
                Grid = ToGrid(),
                IsFocused = true,
                Decoration = ControlDecoration,
                ScrollMode = ScrollMode,
                ThumbMode = ThumbMode,
                ContentStyle = ContentStyle,
            }
        };
    }

    /// <summary>
    /// Time-varying overlays this control exposes to its hosting <c>InlineDialog</c> (e.g. a loading
    /// spinner or a clock). The dialog adds them once through the normal overlay system and advances
    /// their tickers each modal-loop iteration. The default is none; the returned set must be
    /// structurally stable for the control's lifetime (only the tickers' content/active state changes).
    /// </summary>
    public virtual IReadOnlyList<InlineActivityOverlay> GetActivityOverlays() => Array.Empty<InlineActivityOverlay>();

    /// <summary>
    /// Called once per modal-loop iteration on the render thread (immediately before periodic overlays
    /// are advanced). Lets a control apply state that changed off the loop — for example the result of
    /// an asynchronous operation — and report whether the UI must re-render. The default does nothing.
    /// Implementations must be cheap and must not block the loop.
    /// </summary>
    public virtual bool AdvanceState(DateTime nowUtc) => false;

    /// <summary>
    /// A result the control wants the modal to complete with <em>without a keypress</em> (e.g. an async
    /// operation finished). The hosting <c>InlineDialog</c> surfaces this through its own
    /// <c>Result</c> with precedence over key-/confirmation-driven results, and the shell loop breaks on
    /// it after pumping <see cref="AdvanceState"/>. The default is <see cref="DialogResultKind.NoResult"/>
    /// (the control never self-completes).
    /// </summary>
    public virtual DialogResultKind CompletionResult => DialogResultKind.NoResult;

    /// <summary>
    /// Offered the chance to take over completion when the hosting dialog is about to commit a confirmed
    /// <paramref name="kind"/> (Cancel/Abort) after its confirmation gate. Returning <c>true</c> means the
    /// control has begun a deferred completion (e.g. requested operation cancellation and switched to a
    /// "Cancelling…" view) and the dialog must stay open until the control reports a
    /// <see cref="CompletionResult"/>; returning <c>false</c> (the default) lets the dialog complete
    /// immediately with <paramref name="kind"/>, preserving existing behavior.
    /// </summary>
    public virtual bool TryBeginDeferredCompletion(DialogResultKind kind) => false;

    /// <summary>
    /// Called by the modal loop as it starts hosting this control, supplying a token that is cancelled
    /// when the modal closes for any reason. Controls that start background work should observe it. The
    /// default does nothing.
    /// </summary>
    public virtual void OnModalOpened(CancellationToken modalToken) { }

    /// <summary>
    /// Called by the modal loop as it stops hosting this control (any exit path). Controls should stop
    /// tickers and abandon/ignore pending background results so a closed control is never mutated. The
    /// default does nothing.
    /// </summary>
    public virtual void OnModalClosed() { }

    /// <summary>
    /// Handles a key and optionally requests a dialog result. Returning
    /// <see cref="InlineKeyResult.NotHandled"/> lets the hosting dialog apply fallback keys.
    /// </summary>
    public abstract InlineKeyResult HandleKey(KeyEvent key);

    /// <summary>Optional value produced by this control when its hosting dialog completes.</summary>
    public abstract object? Payload { get; }

    //public abstract void SetFocus(bool focused);

    /// <summary>Creates a control hosted by <paramref name="shell"/>.</summary>
    protected InlineControlBase(ICliAppShell shell)
    {
        Shell = shell;
    }
}
