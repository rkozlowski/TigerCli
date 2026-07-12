using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// App-facing convenience API for <see cref="CliTable"/>: theme-driven defaults plus terse
/// header and record construction. These methods sit on top of the existing <see cref="Header"/>,
/// <see cref="Records"/>, <see cref="FrameConfig"/>, and <see cref="Orientation"/> members and do
/// not introduce a separate rendering or theming path — table looks come from <see cref="ITheme"/>.
/// </summary>
public partial class CliTable
{
    /// <summary>
    /// Applies a built-in table style preset to this table. Resolves the preset's recipe through
    /// <paramref name="theme"/> (the <see cref="TigerConsole.CurrentTheme"/> when <c>null</c>) and
    /// applies it as defaults via <see cref="ApplyStyle"/> — the table can still be customized
    /// afterwards. When <paramref name="orientation"/> is omitted the preset's default orientation
    /// is used (orientation-locked presets clamp to their own).
    /// </summary>
    /// <param name="preset">The built-in preset (city or alias) to apply.</param>
    /// <param name="theme">The theme that resolves the preset; defaults to the current theme.</param>
    /// <param name="orientation">Desired orientation as part of preset application; defaults to vertical.</param>
    /// <returns>This table, for fluent chaining.</returns>
    public CliTable ApplyPreset(CliTableStylePreset preset, ITheme? theme = null,
        CliTableOrientation? orientation = null)
        => ApplyStyleCore(
            orientation.HasValue
                ? CliTableStyles.Create(preset, theme, orientation.Value)
                : CliTableStyles.Create(preset, theme),
            explicitStyle: true);

    /// <summary>
    /// Applies resolved table defaults to this table. Copies the style so reusing a
    /// <see cref="CliTableStyle"/> across tables is safe. Sets defaults only; the table can be
    /// modified afterwards. Titles are app-provided, so <see cref="CliTableStyle.TitleStyle"/>
    /// is exposed for callers but not forced onto an existing <see cref="Title"/>.
    /// </summary>
    /// <param name="style">The resolved table style to apply.</param>
    /// <returns>This table, for fluent chaining.</returns>
    public CliTable ApplyStyle(CliTableStyle style)
        => ApplyStyleCore(style, explicitStyle: true);

    private CliTable ApplyStyleCore(CliTableStyle style, bool explicitStyle = false)
    {
        ArgumentNullException.ThrowIfNull(style);

        if (explicitStyle)
            _styleApplied = true;

        _orientationSupport = style.OrientationSupport;
        Orientation = style.Orientation;
        FrameConfig = CloneFrameConfig(style.FrameConfig);
        DefaultCellStyle = CliCellStyle.Clone(style.DefaultCellStyle);
        DataStyle = CliCellStyle.Clone(style.DataStyle);
        DataAltStyle = CliCellStyle.Clone(style.DataAltStyle);
        AlternateRecordsEnabled = style.AlternateRecordsEnabled;
        Header.HeaderStyle = CliCellStyle.Clone(style.HeaderStyle);
        _verticalHeaderHorizontalAlignment = style.Orientation == CliTableOrientation.Vertical
            ? style.HeaderStyle?.HorizontalAlignment
            : null;
        TitleStyle = CliCellStyle.Clone(style.TitleStyle);

        return this;
    }

    /// <summary>
    /// Sets the table orientation. Orientation-locked styles clamp to their supported orientation.
    /// </summary>
    /// <param name="orientation">The desired orientation.</param>
    /// <returns>This table, for fluent chaining.</returns>
    public CliTable SetOrientation(CliTableOrientation orientation)
    {
        Orientation = orientation;
        ApplyHeaderAlignmentForOrientation();
        return this;
    }

    /// <summary>
    /// Enables or disables alternate-record rendering using the existing <see cref="DataAltStyle"/>.
    /// </summary>
    /// <param name="enabled">Whether alternate records should be rendered with <see cref="DataAltStyle"/>.</param>
    /// <returns>This table, for fluent chaining.</returns>
    public CliTable UseAlternateRecords(bool enabled = true)
    {
        AlternateRecordsEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Sets the table <see cref="Title"/> from preformatted, markup-aware content, using the table's
    /// current <see cref="TitleStyle"/> (set by the last <see cref="ApplyStyle"/>/<see cref="ApplyPreset"/>).
    /// Titles are app-provided and sit outside the table surface — built-in presets pin the title
    /// background to the theme's base surface while varying only the title foreground. This does not
    /// alter the header/body/frame styles, the frame config, or the applied style selection.
    /// </summary>
    /// <param name="title">The title text; TigerCli markup is honoured. Must not be <c>null</c>.</param>
    /// <returns>This table, for fluent chaining.</returns>
    public CliTable AddTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        return AddTitle(title, CliFormattingMode.Preformatted);
    }

    /// <summary>
    /// Sets the table <see cref="Title"/> from arbitrary content with an explicit
    /// <see cref="CliFormattingMode"/> (and optional <see cref="CliFormatter"/>), using the table's
    /// current <see cref="TitleStyle"/>. The current title style is cloned so the table owns its copy;
    /// the requested formatting mode and formatter override the clone. Does not alter the
    /// header/body/frame styles, the frame config, or the applied style selection.
    /// </summary>
    /// <param name="title">The title content. Must not be <c>null</c>.</param>
    /// <param name="formattingMode">How the content is formatted.</param>
    /// <param name="formatter">An optional formatter; when <c>null</c> the current style's formatter is kept.</param>
    /// <returns>This table, for fluent chaining.</returns>
    public CliTable AddTitle(object title, CliFormattingMode formattingMode, CliFormatter? formatter = null)
    {
        ArgumentNullException.ThrowIfNull(title);

        var style = CliCellStyle.Clone(TitleStyle) ?? new CliCellStyle();
        style.FormattingMode = formattingMode;
        if (formatter is not null)
            style.Formatter = formatter;

        Title = new CliTableTitle(title, style);
        return this;
    }

    /// <summary>
    /// Adds one header element per caption, in order. Captions are plain strings — pass
    /// localized text such as <c>settings.T("Name")</c> or string literals. Markup in a caption
    /// is rendered as markup, matching the single-string element constructor.
    /// </summary>
    /// <param name="cells">Header captions, one per field.</param>
    /// <returns>This table, for fluent chaining.</returns>
    public CliTable AddHeader(params string[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        foreach (var caption in cells)
            Header.Elements.Add(new CliTableElement(caption, dataStyle: null));
        return this;
    }

    /// <summary>
    /// Adds a single data record from simple values such as <see cref="string"/>, enum,
    /// <see cref="bool"/>, <see cref="int"/>, or <c>null</c>. Each value is rendered through
    /// normal string conversion; <c>null</c> renders safely (empty, or the field's null display
    /// value). Records are orientation-neutral — vertical rendering shows them as rows, horizontal
    /// rendering shows them as value columns. The value count must match
    /// <see cref="CliTableHeader.Elements"/> when the table is rendered.
    /// </summary>
    /// <param name="values">The record's values, one per field.</param>
    /// <returns>This table, for fluent chaining.</returns>
    public CliTable AddRecord(params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Records.Add([.. values]);
        return this;
    }

    static CliTableFrameConfig CloneFrameConfig(CliTableFrameConfig src) => new()
    {
        JoinStyle = src.JoinStyle,
        OuterFrame = src.OuterFrame,
        AfterHeader = src.AfterHeader,
        BeforeFooter = src.BeforeFooter,
        BetweenRecords = src.BetweenRecords,
        BetweenElements = src.BetweenElements,
        CharStyle = src.CharStyle
    };

    private void ApplyHeaderAlignmentForOrientation()
    {
        if (Orientation == CliTableOrientation.Horizontal)
        {
            Header.HeaderStyle ??= new CliCellStyle();
            Header.HeaderStyle.HorizontalAlignment = CliTextAlignment.Left;
            return;
        }

        if (_verticalHeaderHorizontalAlignment.HasValue && Header.HeaderStyle is not null)
            Header.HeaderStyle.HorizontalAlignment = _verticalHeaderHorizontalAlignment;
    }
}
