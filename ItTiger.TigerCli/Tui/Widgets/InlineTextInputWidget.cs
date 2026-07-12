using System.Text;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Widgets;

/// <summary>
/// A reusable editable single-line text input widget.
/// </summary>
public sealed class InlineTextInputWidget : InlineWidget
{
    private readonly StringBuilder _text;
    private readonly bool _isSecret;
    private CliGrid? _cachedGrid;
    private int _cursor;

    private int? _width;

    /// <summary>Creates an editable, single-line text input widget.</summary>
    /// <param name="shell">The shell that supplies the theme.</param>
    /// <param name="initialValue">The initial text.</param>
    /// <param name="isSecret">Whether to mask the displayed text.</param>
    /// <param name="width">The optional input width in cells.</param>
    public InlineTextInputWidget(ICliAppShell shell, string? initialValue = null, bool isSecret = false, int? width = null)
        : base(shell)
    {
        _text = new StringBuilder(initialValue ?? string.Empty);
        _isSecret = isSecret;
        _cursor = _text.Length;
        _width = width;
    }

    /// <summary>The current unmasked input text.</summary>
    public string Text => _text.ToString();

    /// <summary>The zero-based insertion point within <see cref="Text"/>.</summary>
    public int CursorIndex => _cursor;

    /// <summary>Whether the rendered text is masked.</summary>
    public bool IsSecret => _isSecret;

    /// <summary>Replaces the input text and optionally moves the cursor to its end.</summary>
    /// <param name="text">The replacement text; <c>null</c> clears the input.</param>
    /// <param name="moveCursorToEnd">Whether to move the cursor to the end of the replacement text.</param>
    public void SetText(string? text, bool moveCursorToEnd = true)
    {
        _text.Clear();
        _text.Append(text ?? string.Empty);
        _cursor = moveCursorToEnd ? _text.Length : Math.Clamp(_cursor, 0, _text.Length);
        _cachedGrid?.InvalidateLayout();
    }

    /// <inheritdoc/>
    public override InlineKeyResult HandleKey(KeyEvent key)
    {
        if ((key.Mods.HasFlag(ConsoleModifiers.Alt) || key.Mods.HasFlag(ConsoleModifiers.Control))
            && !IsPrintable(key.KeyChar))
            return InlineKeyResult.NotHandled;

        bool handled = true;

        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                _cursor = Math.Max(0, _cursor - 1);
                break;
            case ConsoleKey.RightArrow:
                _cursor = Math.Min(_text.Length, _cursor + 1);
                break;
            case ConsoleKey.Home:
                _cursor = 0;
                break;
            case ConsoleKey.End:
                _cursor = _text.Length;
                break;
            case ConsoleKey.Backspace:
                if (_cursor > 0)
                {
                    _text.Remove(_cursor - 1, 1);
                    _cursor--;
                }
                break;
            case ConsoleKey.Delete:
                if (_cursor < _text.Length)
                    _text.Remove(_cursor, 1);
                break;
            default:
                handled = TryInsertPrintable(key.KeyChar);
                break;
        }

        if (handled)
            _cachedGrid?.InvalidateLayout();

        return handled ? InlineKeyResult.Handled : InlineKeyResult.NotHandled;
    }

    /// <inheritdoc/>
    public override CliGrid ToGrid()
    {
        var g = _cachedGrid ??= ToGrid(1, 1);

        // Focus-aware input ink: the active input style when focused, the explicit inactive input
        // style (muted foreground + background) when not.
        var style = Shell.Theme.Resolve(HasFocus ? ThemeStyle.TextInput : ThemeStyle.InactiveTextInput);
        style.Wrapping = CliWrapping.SingleLine;
        style.FormattingMode = CliFormattingMode.Raw;
        if (_width != null)
            style.Width = _width;
        string display = _isSecret
            ? new string(ConsoleSymbol.Bullet, _text.Length)
            : _text.ToString();

        g.DefaultCellStyle = Shell.Theme.Resolve(ThemeStyle.DialogSurface);
        g.Set(0, 0, display, style);
        g.ActivePoint = new ActivePoint(0, 0, _cursor);
        g.CursorMode = HasFocus ? CursorMode.Normal : CursorMode.Hidden;
        g.InvalidateLayout();

        return g;
    }

    private bool TryInsertPrintable(char value)
    {
        if (!IsPrintable(value))
            return false;

        _text.Insert(_cursor, value);
        _cursor++;
        return true;
    }

    private static bool IsPrintable(char value)
    {
        return value != '\0' && !char.IsControl(value);
    }
}
