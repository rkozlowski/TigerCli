using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Tui.Widgets;

/// <summary>
/// A reusable read-only text widget. It renders its text as a single-cell <see cref="CliGrid"/> and
/// delegates all text handling — end-of-line splitting, wrapping, formatting, measuring, and
/// truncation — to <see cref="CliGrid"/>. It never splits the text into multiple rows or implements
/// wrapping/truncation itself.
/// </summary>
/// <remarks>
/// Truncation is expressed through <see cref="Wrapping"/> (TigerCli carries the truncation flag and
/// indicator on <see cref="CliWrapping"/>, e.g. <see cref="CliWrapping.WordWrapTruncate"/>). The
/// widget is read-only: it is not focusable and consumes no keys.
/// </remarks>
public sealed class InlineTextWidget : InlineWidget
{
    private CliGrid? _cachedGrid;

    /// <summary>Creates a read-only text widget.</summary>
    /// <param name="shell">The shell that supplies the theme.</param>
    /// <param name="text">The text to display.</param>
    public InlineTextWidget(ICliAppShell shell, string? text = null)
        : base(shell)
    {
        Text = text ?? string.Empty;
    }

    /// <summary>The full text. Passed unchanged into a single grid cell; embedded newlines are handled by <see cref="CliGrid"/>.</summary>
    public string Text { get; set; }

    /// <summary>Theme style resolved for the text cell. Defaults to <see cref="ThemeStyle.Text"/>.</summary>
    public ThemeStyle Style { get; set; } = ThemeStyle.Text;

    /// <summary>Whether the text is treated as raw or markup. Defaults to <see cref="CliFormattingMode.Raw"/>.</summary>
    public CliFormattingMode FormattingMode { get; set; } = CliFormattingMode.Raw;

    /// <summary>Wrapping (and, via its truncation flag/indicator, truncation) applied to the cell. Defaults to <see cref="CliWrapping.WordWrap"/>.</summary>
    public CliWrapping Wrapping { get; set; } = CliWrapping.WordWrap;

    // A read-only text widget never takes focus and never consumes keys.
    /// <inheritdoc/>
    public override bool Focusable => false;
    /// <inheritdoc/>
    public override InlineKeyResult HandleKey(KeyEvent key) => InlineKeyResult.NotHandled;

    /// <inheritdoc/>
    public override CliGrid ToGrid()
    {
        var g = _cachedGrid ??= ToGrid(1, 1);

        var style = Shell.Theme.Resolve(Style).MergeWith(new CliCellStyle
        {
            FormattingMode = FormattingMode,
            Wrapping = Wrapping,
            Padding = CliCellPadding.None,
        });

        // Full text in a single cell: CliGrid handles end-of-line splitting, wrapping, measuring,
        // formatting, and truncation. No manual line splitting or row-per-line construction.
        g.Set(0, 0, Text, style: style);
        g.InvalidateLayout();
        return g;
    }
}
