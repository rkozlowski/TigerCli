using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Minimal generic loading indicator: a single localized message line with a top-frame spinner. It owns
/// no operation — it only watches a caller-supplied <see cref="Task"/> and completes the modal with
/// <see cref="DialogResultKind.Ok"/> once that task finishes, so the caller can then await the task for
/// the real result or exception. Escape requests a plain cancel through the hosting dialog. The control
/// never renders the watched task's payload; it is purely a "please wait" surface.
/// </summary>
internal sealed class InlineLoadingControl : InlineControlBase
{
    private readonly Task _watched;
    private readonly string _message;
    private readonly SpinnerTicker _spinner;
    private readonly InlineActivityOverlay[] _activityOverlays;
    private DialogResultKind _completionResult = DialogResultKind.NoResult;
    private CliGrid? _grid;

    public InlineLoadingControl(ICliAppShell shell, Task watched, string message)
        : base(shell)
    {
        _watched = watched ?? throw new ArgumentNullException(nameof(watched));
        _message = message ?? throw new ArgumentNullException(nameof(message));
        _spinner = new SpinnerTicker(active: true);
        _activityOverlays =
        [
            new InlineActivityOverlay
            {
                Area = InlineDialogArea.TopFrame,
                ColumnOffset = 1,
                MaxLength = InlineActivityOverlay.SpinnerMaxLength,
                Ticker = _spinner,
                ContentFormatter = static frame => $"[{frame}]",
                Style = Shell.Theme.Resolve(ThemeStyle.Frame).CharStyle ?? default,
            }
        ];
    }

    public override object? Payload => null;

    // No confirmable payload: Enter must never complete the loading dialog.
    public override bool CanConfirm => false;

    public override string? Hint => TigerCliResources.Get("Tui_Loading_Hint", Shell.Culture);
    public override CliFormattingMode HintMode => CliFormattingMode.Raw;

    public override DialogResultKind CompletionResult => _completionResult;

    // Structurally stable single spinner overlay; its ticker's active state controls visibility.
    public override IReadOnlyList<InlineActivityOverlay> GetActivityOverlays() => _activityOverlays;

    public override bool AdvanceState(DateTime nowUtc)
    {
        // The watched load finished off the loop → close the modal (Ok) without a keypress; the caller
        // then awaits the task for the actual choices (or the provider exception). Provider failures are
        // therefore surfaced by the caller as provider failures, never as a loading-UI error.
        if (_completionResult == DialogResultKind.NoResult && _watched.IsCompleted)
        {
            _completionResult = DialogResultKind.Ok;
            _spinner.Stop();
            return true;
        }

        return false;
    }

    // Escape is left to the hosting dialog's generic Cancel fallback; every other key is swallowed so
    // stray input cannot leak into the select prompt that follows once choices are loaded.
    public override InlineKeyResult HandleKey(KeyEvent key)
    {
        if (key.Mods == ConsoleModifiers.None && key.Key == ConsoleKey.Escape)
            return InlineKeyResult.NotHandled;

        return InlineKeyResult.Handled;
    }

    public override void OnModalClosed() => _spinner.Stop();

    public override IReadOnlyList<InlineDialogWidget> GetWidgets() =>
        new[]
        {
            new InlineDialogWidget
            {
                Area = InlineDialogArea.InFrame,
                Grid = ToGrid(),
                IsFocused = false,
                ScrollMode = CliScrollMode.None,
            }
        };

    public override CliGrid ToGrid()
    {
        if (_grid is not null)
            return _grid;

        var theme = Shell.Theme;
        var grid = new CliGrid(1, 1) { DefaultCellStyle = theme.Resolve(ThemeStyle.DialogSurface) };
        grid.Set(0, 0, _message, new CliCellStyle(theme.Resolve(ThemeStyle.Text).CharStyle)
        {
            HorizontalAlignment = CliTextAlignment.Left,
            FormattingMode = CliFormattingMode.Preformatted,
            Wrapping = CliWrapping.SingleLineTruncate,
        });

        _grid = grid;
        return grid;
    }
}
