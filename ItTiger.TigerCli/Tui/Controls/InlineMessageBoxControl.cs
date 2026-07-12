using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// A message-box style inline control: a block of message text plus a row of buttons. It is the first
/// consumer of the multi-widget dialog layout — it exposes exactly two top-level widgets, the message
/// content (<see cref="InlineDialogArea.InFrameScrollable"/>) and the button group
/// (<see cref="InlineDialogArea.InFrame"/>), and routes keys to the (focused) button group.
/// </summary>
/// <remarks>
/// The button group is the top-level interactive widget; individual buttons are not separate dialog
/// widgets. Activating a button returns its <see cref="DialogResultKind"/> via
/// <see cref="InlineKeyResult.WithResult"/> so the hosting <c>InlineDialog</c> completes with that
/// result. Escape is left unhandled so the dialog's cancel fallback fires
/// (<see cref="DialogResultKind.Cancel"/>), even for button sets that have no explicit Cancel button.
/// </remarks>
public sealed class InlineMessageBoxControl : InlineControlBase
{
    // Word-wrap cap for the message body; long messages wrap, the content column never grows past it
    // (subject to the dialog clamping it to the available viewport width).
    private const int MaxMessageWidth = 60;

    private readonly string _message;
    private readonly MessageBoxKind _kind;
    private readonly InlineButtonGroupWidget _buttons;
    private readonly InlineTextWidget _messageText;

    /// <summary>Creates a message-box control with a message and button row.</summary>
    /// <param name="shell">The shell that supplies the theme, culture, and viewport.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="buttons">The buttons to include.</param>
    /// <param name="defaultButton">The button selected initially, or <c>null</c> to use the button-set default.</param>
    /// <param name="kind">The semantic kind that selects the control's dialog surface.</param>
    public InlineMessageBoxControl(
        ICliAppShell shell,
        string message,
        MessageBoxButtons buttons = MessageBoxButtons.Ok,
        DialogResultKind? defaultButton = null,
        MessageBoxKind kind = MessageBoxKind.Normal)
        : base(shell)
    {
        _message = message ?? string.Empty;
        _kind = kind;
        _messageText = new InlineTextWidget(shell, _message)
        {
            Style = ThemeStyle.Text,
            FormattingMode = CliFormattingMode.Raw,
            Wrapping = CliWrapping.WordWrap,
        };
        _buttons = BuildButtonGroup(shell, buttons, defaultButton);
        _buttons.HasFocus = true;
    }

    /// <summary>The button group is the single focused/interactive top-level widget.</summary>
    public InlineButtonGroupWidget ButtonGroup => _buttons;

    /// <summary>The semantic severity of this message box (selects the dialog surface).</summary>
    public MessageBoxKind Kind => _kind;

    // The dialog background follows the message-box severity: a warning/error box returns its semantic
    // surface token, a normal box the standard dialog surface. The hosting InlineDialog resolves this
    // against the active theme, so the colour stays theme-driven rather than hard-coded here.
    /// <inheritdoc/>
    public override ThemeStyle DialogSurfaceStyle => _kind switch
    {
        MessageBoxKind.Warning => ThemeStyle.WarningSurface,
        MessageBoxKind.Error => ThemeStyle.ErrorSurface,
        _ => ThemeStyle.DialogSurface,
    };

    // The answer is carried by the dialog result kind (set via InlineKeyResult.WithResult), so the
    // message box has no separate payload.
    /// <inheritdoc/>
    public override object? Payload => null;

    // Enter must not confirm via the dialog fallback: a focused button drives the result. When the
    // active button is disabled this still prevents an accidental Enter-confirm.
    /// <inheritdoc/>
    public override bool CanConfirm => false;

    /// <inheritdoc/>
    public override string? Hint => TigerCliResources.Get("Tui_MessageBox_Hint", Shell.Culture);

    //public override int? PreferredContentWidth => ResolvePreferredContentWidth();

    /// <inheritdoc/>
    public override InlineKeyResult HandleKey(KeyEvent key) => _buttons.HandleKey(key);

    /// <inheritdoc/>
    public override IReadOnlyList<InlineDialogWidget> GetWidgets()
    {
        var messageWidget = new InlineDialogWidget
        {
            Area = InlineDialogArea.InFrameScrollable,
            Grid = BuildMessageGrid(),
            IsFocused = false,
            ScrollMode = CliScrollMode.None,
        };

        var buttonWidget = new InlineDialogWidget
        {
            Area = InlineDialogArea.InFrame,
            Grid = _buttons.ToGrid(),
            IsFocused = true,
        };

        return new[] { messageWidget, buttonWidget };
    }

    // The message box does not use a single content subgrid; the dialog drives layout from
    // GetWidgets(). ToGrid() is still required by the base type, so expose the message body.
    /// <inheritdoc/>
    public override CliGrid ToGrid() => BuildMessageGrid();

    // Text handling (end-of-line splitting, wrapping, formatting, measuring, truncation) is delegated
    // to the reusable InlineTextWidget / CliGrid. The control only supplies the dialog-surface
    // background so the message text sits on the dialog surface as before.
    private CliGrid BuildMessageGrid()
    {
        var g = _messageText.ToGrid();
        g.DefaultCellStyle = Shell.Theme.Resolve(DialogSurfaceStyle);
        return g;
    }

    /*
    // Keep the content column at least as wide as the button row (so buttons never truncate) and at
    // most the word-wrap cap; the hosting dialog clamps the result to the available viewport width.
    private int ResolvePreferredContentWidth()
    {
        int buttonWidth = ComputeButtonRowWidth();

        int longestLine = 0;
        foreach (var line in _message.Replace("\r\n", "\n").Split('\n'))
            longestLine = Math.Max(longestLine, line.Length);

        int wrapTarget = Math.Min(MaxMessageWidth, longestLine);
        return Math.Max(buttonWidth, wrapTarget);
    }
    

    private int ComputeButtonRowWidth()
    {
        const int gap = 4;     // matches InlineButtonGroupWidget's inter-button gap
        const int frame = 6;   // "[", marker, " label ", marker, "]" overhead around each label

        int total = 0;
        var buttons = _buttons.Buttons;
        for (int i = 0; i < buttons.Count; i++)
        {
            total += buttons[i].Label.Length + frame;
            if (i < buttons.Count - 1)
                total += gap;
        }

        return Math.Max(1, total);
    }
    */

    private static InlineButtonGroupWidget BuildButtonGroup(
        ICliAppShell shell, MessageBoxButtons buttons, DialogResultKind? defaultButton)
    {
        var specs = ButtonSpecs(buttons);

        var widgets = specs
            .Select(spec => new InlineButtonWidget(shell, Label(shell, spec.label), spec.result))
            .ToArray();

        // Default active button: the explicit override when it matches a button, otherwise the first
        // button — every set's natural default (OK / Yes / Abort) is at index 0.
        int active = 0;
        if (defaultButton is DialogResultKind want)
        {
            for (int i = 0; i < specs.Length; i++)
            {
                if (specs[i].result == want)
                {
                    active = i;
                    break;
                }
            }
        }

        return new InlineButtonGroupWidget(shell, widgets, active);
    }

    private static (string label, DialogResultKind result)[] ButtonSpecs(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.Ok => new[]
        {
            ("Tui_Button_Ok", DialogResultKind.Ok),
        },
        MessageBoxButtons.OkCancel => new[]
        {
            ("Tui_Button_Ok", DialogResultKind.Ok),
            ("Tui_Button_Cancel", DialogResultKind.Cancel),
        },
        MessageBoxButtons.YesNo => new[]
        {
            ("Tui_Confirm_Yes", DialogResultKind.Yes),
            ("Tui_Confirm_No", DialogResultKind.No),
        },
        MessageBoxButtons.YesNoCancel => new[]
        {
            ("Tui_Confirm_Yes", DialogResultKind.Yes),
            ("Tui_Confirm_No", DialogResultKind.No),
            ("Tui_Button_Cancel", DialogResultKind.Cancel),
        },
        MessageBoxButtons.AbortRetryIgnore => new[]
        {
            ("Tui_Button_Abort", DialogResultKind.Abort),
            ("Tui_Button_Retry", DialogResultKind.Retry),
            ("Tui_Button_Ignore", DialogResultKind.Ignore),
        },
        _ => new[]
        {
            ("Tui_Button_Ok", DialogResultKind.Ok),
        },
    };

    private static string Label(ICliAppShell shell, string resourceKey)
        => TigerCliResources.Get(resourceKey, shell.Culture);
}
