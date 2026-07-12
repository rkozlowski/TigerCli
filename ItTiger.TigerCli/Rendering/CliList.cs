using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// App-facing convenience builder for list command output: a column-per-field, record-per-item table.
/// It removes the manual, error-prone pattern of rendering list headings, spacing, indentation, loops,
/// and per-column formatting by hand with <see cref="TigerConsole.MarkupLine(string)"/>.
///
/// <para><see cref="CliList{T}"/> is a builder, not a new rendering engine: <see cref="Render"/>
/// projects the items into a <see cref="CliTable"/> (one header element per column, one record per
/// item) and rendering goes through the existing <see cref="CliTable"/> → <see cref="CliGrid"/>
/// pipeline. Columns may carry a semantic <see cref="ThemeStyle"/> applied to the column's
/// <b>values</b> — use <see cref="AddKeyColumn"/> for identity/anchor values
/// (<see cref="ThemeStyle.Key"/>) and <see cref="AddPathColumn"/> for path values
/// (<see cref="ThemeStyle.Path"/>).</para>
///
/// <para>Use <see cref="CliList{T}"/> for list output, <see cref="CliDetails"/> for single-record
/// show/details output, and <see cref="CliTable"/> directly only when lower-level table control is
/// needed.</para>
/// </summary>
/// <typeparam name="T">The item/record type the list renders.</typeparam>
public sealed class CliList<T>
{
    private sealed record Column(string Label, Func<T, object?> Selector, ThemeStyle? Style)
    {
        // Per-column wrapping/truncation and width overrides. Null means "inherit the list default"
        // (for wrapping) or "unconstrained" (for width). Applied to the column's data cells at
        // Render() time, layered onto the column's semantic value style.
        public CliWrapping? Wrapping { get; set; }
        public int? Width { get; set; }
        public int? MinWidth { get; set; }
        public int? MaxWidth { get; set; }
    }

    private readonly List<Column> _columns = [];

    // Default wrapping/truncation applied to every column that does not set its own via SetWrapping.
    private CliWrapping? _defaultWrapping;

    private CliTableStylePreset _preset = CliTableStylePreset.List;
    private ITheme? _theme;
    private bool _presetApplied;

    // Title is captured and deferred so Render() can apply the preset's TitleStyle first, matching
    // CliTable.AddTitle semantics (which reads the table's current TitleStyle).
    private bool _hasTitle;
    private object? _titleContent;
    private CliFormattingMode _titleFormattingMode = CliFormattingMode.Preformatted;
    private CliFormatter? _titleFormatter;

    // Optional title horizontal-alignment override. Null means "inherit the preset's title alignment"
    // (unchanged default). When set, it is applied to the title cell's HorizontalAlignment at Render()
    // time — layout only, never touching the title's semantic style.
    private CliTextAlignment? _titleAlignment;

    /// <summary>
    /// Selects the table style preset used when the list is rendered. Defaults to
    /// <see cref="CliTableStylePreset.Default"/>. A list is always vertical (columns are fields,
    /// records are items); orientation-locked detail presets are unsuitable here.
    /// </summary>
    /// <param name="preset">The built-in preset (city or alias) to apply.</param>
    /// <param name="theme">The theme that resolves the preset; defaults to the current theme.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliList<T> ApplyPreset(CliTableStylePreset preset, ITheme? theme = null)
    {
        _preset = preset;
        _theme = theme;
        _presetApplied = true;
        return this;
    }

    /// <summary>
    /// Sets the default wrapping/truncation applied to every column's <b>values</b> that does not
    /// override it via <see cref="SetWrapping"/>. This mirrors <see cref="CliTable"/> wrapping (it is
    /// the same <see cref="CliCellStyle.Wrapping"/> behaviour on the column's data cells), so semantic
    /// theme styles are preserved — wrapping affects layout only. Wrapping needs a width bound to bind:
    /// set one per column via <see cref="SetWidth"/>, or rely on the list being width-constrained
    /// (the rendered table's soft/hard max width) so over-wide columns wrap/truncate.
    /// </summary>
    /// <param name="wrapping">The default wrapping/truncation. Must not be <c>null</c>.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliList<T> DefaultWrapping(CliWrapping wrapping)
    {
        ArgumentNullException.ThrowIfNull(wrapping);
        _defaultWrapping = wrapping;
        return this;
    }

    /// <summary>
    /// Overrides the wrapping/truncation for the most-recently added column (from the preceding
    /// <see cref="AddColumn"/> / <see cref="AddKeyColumn"/> / <see cref="AddPathColumn"/> /
    /// <see cref="AddLinkColumn"/>), taking precedence over <see cref="DefaultWrapping"/>. Wrapping
    /// needs a width bound to bind — pair it with <see cref="SetWidth"/> or a width-constrained list.
    /// </summary>
    /// <param name="wrapping">The wrapping/truncation for the last column. Must not be <c>null</c>.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">No column has been added yet.</exception>
    public CliList<T> SetWrapping(CliWrapping wrapping)
    {
        ArgumentNullException.ThrowIfNull(wrapping);
        LastColumn().Wrapping = wrapping;
        return this;
    }

    /// <summary>
    /// Sets width bounds (fixed <paramref name="width"/> and/or <paramref name="minWidth"/> /
    /// <paramref name="maxWidth"/>) for the most-recently added column. A <paramref name="maxWidth"/>
    /// is what makes a column wrap or truncate its values; without a width bound, wrapping only takes
    /// effect when the whole list is width-constrained. Only non-<c>null</c> arguments are applied.
    /// </summary>
    /// <returns>This instance, for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">No column has been added yet.</exception>
    public CliList<T> SetWidth(int? width = null, int? minWidth = null, int? maxWidth = null)
    {
        var column = LastColumn();
        column.MinWidth = minWidth;
        column.MaxWidth = maxWidth;
        column.Width = width;
        return this;
    }

    private Column LastColumn() => _columns.Count > 0
        ? _columns[^1]
        : throw new InvalidOperationException("Add a column before configuring its wrapping or width.");

    /// <summary>
    /// Sets the list title from preformatted, markup-aware content (matching
    /// <see cref="CliTable.AddTitle(string)"/>). The title uses the preset's title style. The optional
    /// <paramref name="alignment"/> overrides the title's horizontal alignment (layout only, semantic
    /// title style is preserved); when <c>null</c> the preset's title alignment is kept. Alignment can
    /// also be set separately via <see cref="SetTitleAlignment"/>.
    /// </summary>
    /// <param name="title">The title text; TigerCli markup is honoured. Must not be <c>null</c>.</param>
    /// <param name="alignment">Optional title horizontal alignment; <c>null</c> keeps the preset default.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliList<T> AddTitle(string title, CliTextAlignment? alignment = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (alignment is not null)
            _titleAlignment = alignment;
        return AddTitle(title, CliFormattingMode.Preformatted);
    }

    /// <summary>
    /// Sets the list title from arbitrary content with an explicit <see cref="CliFormattingMode"/>
    /// (and optional <see cref="CliFormatter"/>), matching
    /// <see cref="CliTable.AddTitle(object, CliFormattingMode, CliFormatter?)"/>.
    /// </summary>
    public CliList<T> AddTitle(object title, CliFormattingMode formattingMode, CliFormatter? formatter = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        _hasTitle = true;
        _titleContent = title;
        _titleFormattingMode = formattingMode;
        _titleFormatter = formatter;
        return this;
    }

    /// <summary>
    /// Sets the horizontal alignment of the list title (left, center, or right), overriding the
    /// preset's default title alignment. Affects layout only — the title's semantic style and
    /// <see cref="ThemeStyle"/> behaviour are preserved. Applies whether the title is set via
    /// <see cref="AddTitle(string, CliTextAlignment?)"/> or the formatting-mode overload.
    /// </summary>
    /// <param name="alignment">The title horizontal alignment.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliList<T> SetTitleAlignment(CliTextAlignment alignment)
    {
        _titleAlignment = alignment;
        return this;
    }

    /// <summary>
    /// Adds a column with header <paramref name="label"/> and a <paramref name="selector"/> that
    /// projects each item to a cell value. The optional <paramref name="style"/> applies a semantic
    /// theme style to the column's <b>values</b> (not the header); when <c>null</c> the preset's body
    /// styling is used.
    /// </summary>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliList<T> AddColumn(string label, Func<T, object?> selector, ThemeStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(selector);
        _columns.Add(new Column(label, selector, style));
        return this;
    }

    /// <summary>
    /// Adds a column whose values are identity/anchors (IDs, names, codes, slugs, group IDs, …), styled
    /// with <see cref="ThemeStyle.Key"/>. Convenience for
    /// <c>AddColumn(label, selector, style: ThemeStyle.Key)</c>.
    /// </summary>
    public CliList<T> AddKeyColumn(string label, Func<T, object?> selector)
        => AddColumn(label, selector, ThemeStyle.Key);

    /// <summary>
    /// Adds a column whose values are filesystem/local paths, styled with <see cref="ThemeStyle.Path"/>.
    /// Convenience for <c>AddColumn(label, selector, style: ThemeStyle.Path)</c>.
    /// </summary>
    public CliList<T> AddPathColumn(string label, Func<T, object?> selector)
        => AddColumn(label, selector, ThemeStyle.Path);

    /// <summary>
    /// Adds a column whose values are navigable/link values, styled with <see cref="ThemeStyle.Link"/>.
    /// This is semantic styling only (no clickable hyperlink). Convenience for
    /// <c>AddColumn(label, selector, style: ThemeStyle.Link)</c>.
    /// </summary>
    public CliList<T> AddLinkColumn(string label, Func<T, object?> selector)
        => AddColumn(label, selector, ThemeStyle.Link);

    /// <summary>
    /// Projects <paramref name="items"/> into a renderable <see cref="CliTable"/>: the chosen preset,
    /// the optional title, one header element per column (carrying its semantic value style), and one
    /// record per item. An empty <paramref name="items"/> sequence yields a header-only table — a
    /// consistent default empty state that still shows the columns; commands that want a custom empty
    /// message can branch before calling <see cref="Render"/>. Pass the result to
    /// <see cref="TigerConsole.Render(CliRenderableComponent)"/>.
    /// </summary>
    /// <param name="items">The items to render, one record per item. Must not be <c>null</c>.</param>
    /// <returns>A <see cref="CliTable"/> ready to render.</returns>
    /// <exception cref="InvalidOperationException">No columns were added.</exception>
    public CliTable Render(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (_columns.Count == 0)
            throw new InvalidOperationException("CliList must define at least one column before rendering.");

        var theme = _theme ?? TigerConsole.CurrentTheme;

        // A list is always vertical (records as rows). Resolve the preset's visual styling, then force
        // vertical and unlock the orientation so even an orientation-locked detail preset (e.g. Lucca)
        // contributes only its look — mirroring how CliDetails forces horizontal regardless of preset.
        var preset = _presetApplied
            ? _preset
            : CliOutputPresetContext.Current?.List ?? _preset;
        var style = CliTableStyles.Create(preset, theme, CliTableOrientation.Vertical);
        style.Orientation = CliTableOrientation.Vertical;
        style.OrientationSupport = CliTableStyleOrientationSupport.Both;

        var table = new CliTable().ApplyStyle(style);

        if (_hasTitle)
        {
            table.AddTitle(_titleContent!, _titleFormattingMode, _titleFormatter);

            // Alignment override is applied onto the already-built title cell style (the existing
            // CliCellStyle.HorizontalAlignment capability) so no parallel title rendering is added and
            // the preset's title ink/surface is preserved — only the horizontal alignment changes.
            if (_titleAlignment is not null && table.Title is not null)
                table.Title.Style.HorizontalAlignment = _titleAlignment;
        }

        foreach (var column in _columns)
        {
            // The element (column) axis carries the value data style. A semantic style resolves to a
            // foreground/decoration-only style (background null) so the preset's surface is preserved;
            // when no style is given the element data style stays null so the preset's body styling wins.
            var dataStyle = CliSemanticValueStyle.Resolve(theme, column.Style);

            // Layer wrapping/truncation and width bounds onto the value style (never onto the header).
            // A list is vertical, so the element axis IS the grid column: its width bounds drive the
            // wrap/truncate width (CliGrid reads the column style's max width), and the wrapping mode
            // reaches the data cells through the same cascade CliTable uses — no rendering logic is
            // duplicated. The style cascade keeps any semantic foreground intact (layout only).
            var wrapping = column.Wrapping ?? _defaultWrapping;
            if (wrapping is not null || column.Width is not null
                || column.MinWidth is not null || column.MaxWidth is not null)
            {
                dataStyle ??= new CliCellStyle();
                if (wrapping is not null) dataStyle.Wrapping = wrapping;
                if (column.MinWidth is not null) dataStyle.MinWidth = column.MinWidth;
                if (column.MaxWidth is not null) dataStyle.MaxWidth = column.MaxWidth;
                if (column.Width is not null) dataStyle.Width = column.Width;
            }

            var element = new CliTableElement(column.Label, dataStyle);

            // Link columns mark their data cells as hyperlinks; the render pipeline derives a per-row
            // target from each cell's own visible value (empty cells produce no link).
            if (column.Style == ThemeStyle.Link)
                element.DataIsHyperlink = true;

            table.Header.Elements.Add(element);
        }

        foreach (var item in items)
        {
            var record = new object?[_columns.Count];
            for (int i = 0; i < _columns.Count; i++)
                record[i] = _columns[i].Selector(item);
            table.AddRecord(record);
        }

        return table;
    }
}
