using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tui.Controls;

/// <summary>
/// Dialog-hostable single-line text input control backed by <see cref="InlineTextInputWidget"/>.
/// </summary>
/// <remarks>
/// The widget owns editing state, cursor placement, masking, key handling, and rendering. The
/// control adds dialog-level width, style, validation, hint text, payload, and horizontal scrolling
/// metadata. The payload is always the real text, including in secret mode.
/// </remarks>
public sealed class InlineTextInput : InlineControlBase
{
    private readonly InlineTextInputWidget _widget;
    private readonly int? _width;
    private readonly Func<string, string?>? _validator;
    private string? _validationMessage;

    /// <summary>Creates a text input control.</summary>
    public InlineTextInput(
        ICliAppShell shell,
        string? initialValue = null,
        bool isSecret = false,
        int? width = null,
        Func<string, string?>? validator = null)
        : base(shell)
    {
        if (width == null)
        {
            width = Math.Max(1, shell.Viewport.Width / 2);
        }
        _width = width;
        _widget = new InlineTextInputWidget(shell, initialValue, isSecret, _width)
        {
            HasFocus = true
        };        
        _validator = validator;
        Validate();
    }

    /// <summary>Style applied to the hosted text input content area, including the resolved width.</summary>
    public override CliCellStyle? ContentStyle
    {
        get
        {
            var style = Shell.Theme.Resolve(ThemeStyle.TextInput);
            if (_width is int width)
            {
                style.MinWidth = width;
                style.MaxWidth = width;
                style.Width = width;
            }

            return style;
        }
    }

    /// <summary>The scrollbar/indicator thumb follows the input cursor active point.</summary>
    public override CliScrollThumbMode ThumbMode => CliScrollThumbMode.ActivePoint;

    /// <summary>Text input controls request horizontal scroll indicators.</summary>
    public override CliControlDecoration ControlDecoration => CliControlDecoration.HorizontalIndicators;

    /// <summary>Text input controls scroll horizontally.</summary>
    public override CliScrollMode ScrollMode => CliScrollMode.Horizontal;

    /// <summary>Text input controls are placed inside the frame with indicator columns reserved.</summary>
    public override InlineDialogArea DialogArea => InlineDialogArea.InFrameWithIndicators;

    /// <summary>The current real text value, including in secret mode.</summary>
    public override object? Payload => _widget.Text;

    /// <summary>False when the optional validator currently returns a non-empty message.</summary>
    public override bool CanConfirm => _validationMessage == null;

    /// <summary>Validation message when invalid; otherwise the localized text-input hint.</summary>
    public override string? Hint => _validationMessage ?? TigerCliResources.Get("Tui_TextInput_Hint", Shell.Culture);

    /// <summary>Delegates editing keys to the owned widget and refreshes validation when text changes.</summary>
    public override InlineKeyResult HandleKey(KeyEvent key)
    {
        var result = _widget.HandleKey(key);

        if (result.IsHandled)
            Validate();

        return result;
    }

    /// <summary>Exposes one focused, horizontally scrollable input widget to the hosting dialog.</summary>
    public override IReadOnlyList<InlineDialogWidget> GetWidgets()
    {
        return new[]
        {
            new InlineDialogWidget
            {
                Area = InlineDialogArea.InFrameWithIndicators,
                Grid = ToGrid(),
                IsFocused = true,
                Decoration = ControlDecoration,
                ScrollMode = ScrollMode,
                ThumbMode = ThumbMode,
                ContentStyle = ContentStyle,
            }
        };
    }

    /// <summary>Returns the owned widget grid after synchronizing inherited layout settings.</summary>
    public override CliGrid ToGrid()
    {
        SyncWidgetLayout();
        _widget.HasFocus = true;
        return _widget.ToGrid();
    }

    private void Validate()
    {
        if (_validator == null)
        {
            _validationMessage = null;
            return;
        }

        var message = _validator(_widget.Text);
        _validationMessage = string.IsNullOrWhiteSpace(message) ? null : message;
    }

    private void SyncWidgetLayout()
    {
        _widget.IsInteractive = IsInteractive;
        _widget.Width = Width;
        _widget.MinWidth = MinWidth;
        _widget.SoftMaxWidth = SoftMaxWidth;
        _widget.MaxWidth = MaxWidth;
        _widget.Height = Height;
        _widget.MinHeight = MinHeight;
        _widget.SoftMaxHeight = SoftMaxHeight;
        _widget.MaxHeight = MaxHeight;
        _widget.DefaultCellStyle = DefaultCellStyle;
    }
}
