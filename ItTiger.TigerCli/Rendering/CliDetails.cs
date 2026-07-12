using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// App-facing convenience builder for one-record key/value detail views ("Name: value" panels).
/// It removes the manual, error-prone pattern of keeping a parallel header (labels) and record
/// (values) in sync, of hiding optional fields, and of formatting missing values by hand.
///
/// <para><see cref="CliDetails"/> is a builder, not a new rendering engine: it converts to a
/// <see cref="CliTable"/> (via <see cref="ToTable"/>) and renders through the existing
/// <see cref="CliTable"/> → <see cref="CliGrid"/> pipeline. A detail view is always horizontal —
/// header captions become row labels and the single record becomes the value column — so
/// <see cref="ToTable"/> forces <see cref="CliTableOrientation.Horizontal"/> regardless of the
/// applied preset. The default preset is <see cref="CliTableStylePreset.Details"/>.</para>
///
/// <para>Use <see cref="CliDetails"/> for a single record shown as labelled fields. Use
/// <see cref="CliTable"/> when you have many records, columnar/tabular data, or need full control
/// over header/record construction.</para>
/// </summary>
public sealed class CliDetails : CliRenderableComponent
{
    /// <summary>
    /// Default markup-aware display for a missing value when a field is still rendered
    /// (e.g. <c>Add(label, null)</c>). Muted so missing values read as absent, not as data.
    /// </summary>
    public const string DefaultMissingDisplay = "[Muted](not set)[/]";

    private sealed record Field(string Label, object? Value, string? MissingDisplay, ThemeStyle? Style)
    {
        // Per-field wrapping/truncation override. Null means "inherit the detail-view default"
        // (SetMissingDisplay-style). Applied to the field's value cell at ToTable() time. Width is
        // NOT per-field: a detail view is horizontal, so every value shares one value column whose
        // width is set once via SetValueWidth.
        public CliWrapping? Wrapping { get; set; }
    }

    private readonly List<Field> _fields = [];

    private CliTableStylePreset _preset = CliTableStylePreset.Details;
    private ITheme? _theme;
    private bool _presetApplied;
    private string _missingDisplay = DefaultMissingDisplay;

    // Default wrapping/truncation applied to every field's value that does not set its own via
    // SetWrapping.
    private CliWrapping? _defaultWrapping;

    // Shared width bounds for the single value column (all field values live in one column).
    private int? _valueWidth;
    private int? _valueMinWidth;
    private int? _valueMaxWidth;

    // Title is captured and deferred so ToTable() can apply the preset's TitleStyle first,
    // matching CliTable.AddTitle semantics (which reads the table's current TitleStyle).
    private bool _hasTitle;
    private object? _titleContent;
    private CliFormattingMode _titleFormattingMode = CliFormattingMode.Preformatted;
    private CliFormatter? _titleFormatter;

    // Optional title horizontal-alignment override. Null means "inherit the preset's title alignment"
    // (unchanged default). When set, it is applied to the title cell's HorizontalAlignment at ToTable()
    // time — layout only, never touching the title's semantic style.
    private CliTextAlignment? _titleAlignment;

    /// <summary>
    /// Selects the table style preset used when this detail view is converted to a
    /// <see cref="CliTable"/>. Defaults to <see cref="CliTableStylePreset.Details"/>. The preset
    /// controls visual styling only; orientation is always forced to
    /// <see cref="CliTableOrientation.Horizontal"/> (labels are row headers), even for a non-details
    /// preset such as Roma or Milano.
    /// </summary>
    /// <param name="preset">The built-in preset (city or alias) to apply.</param>
    /// <param name="theme">The theme that resolves the preset; defaults to the current theme.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliDetails ApplyPreset(CliTableStylePreset preset, ITheme? theme = null)
    {
        _preset = preset;
        _theme = theme;
        _presetApplied = true;
        return this;
    }

    /// <summary>
    /// Sets the detail view title from preformatted, markup-aware content (matching
    /// <see cref="CliTable.AddTitle(string)"/>). The title uses the preset's title style. The optional
    /// <paramref name="alignment"/> overrides the title's horizontal alignment (layout only, semantic
    /// title style is preserved); when <c>null</c> the preset's title alignment is kept. Alignment can
    /// also be set separately via <see cref="SetTitleAlignment"/>.
    /// </summary>
    /// <param name="title">The title text; TigerCli markup is honoured. Must not be <c>null</c>.</param>
    /// <param name="alignment">Optional title horizontal alignment; <c>null</c> keeps the preset default.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliDetails AddTitle(string title, CliTextAlignment? alignment = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (alignment is not null)
            _titleAlignment = alignment;
        return AddTitle(title, CliFormattingMode.Preformatted);
    }

    /// <summary>
    /// Sets the detail view title from arbitrary content with an explicit
    /// <see cref="CliFormattingMode"/> (and optional <see cref="CliFormatter"/>), matching
    /// <see cref="CliTable.AddTitle(object, CliFormattingMode, CliFormatter?)"/>.
    /// </summary>
    /// <param name="title">The title content. Must not be <c>null</c>.</param>
    /// <param name="formattingMode">How the content is formatted.</param>
    /// <param name="formatter">An optional formatter; when <c>null</c> the preset's title formatter is kept.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliDetails AddTitle(object title, CliFormattingMode formattingMode, CliFormatter? formatter = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        _hasTitle = true;
        _titleContent = title;
        _titleFormattingMode = formattingMode;
        _titleFormatter = formatter;
        return this;
    }

    /// <summary>
    /// Sets the horizontal alignment of the detail view title (left, center, or right), overriding the
    /// preset's default title alignment. Affects layout only — the title's semantic style and
    /// <see cref="ThemeStyle"/> behaviour are preserved. Applies whether the title is set via
    /// <see cref="AddTitle(string, CliTextAlignment?)"/> or the formatting-mode overload.
    /// </summary>
    /// <param name="alignment">The title horizontal alignment.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliDetails SetTitleAlignment(CliTextAlignment alignment)
    {
        _titleAlignment = alignment;
        return this;
    }

    /// <summary>
    /// Sets the default display used for missing values across fields. Per-field overrides
    /// (the <c>missingDisplay</c> argument on <see cref="Add(string, object, string, ThemeStyle?)"/> and friends)
    /// take precedence. The value is markup-aware (e.g. <c>[Muted](n/a)[/]</c>).
    /// </summary>
    /// <param name="missingDisplay">The default missing display; must not be <c>null</c>.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliDetails SetMissingDisplay(string missingDisplay)
    {
        ArgumentNullException.ThrowIfNull(missingDisplay);
        _missingDisplay = missingDisplay;
        return this;
    }

    /// <summary>
    /// Sets the default wrapping/truncation applied to every field's <b>value</b> that does not
    /// override it via <see cref="SetWrapping"/>. This is the same <see cref="CliCellStyle.Wrapping"/>
    /// behaviour <see cref="CliTable"/> uses on data cells, so semantic theme styles are preserved —
    /// wrapping affects layout only. Wrapping needs a width bound to bind: set the shared value-column
    /// width via <see cref="SetValueWidth"/>, or rely on the detail view being width-constrained.
    /// </summary>
    /// <param name="wrapping">The default wrapping/truncation. Must not be <c>null</c>.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliDetails DefaultWrapping(CliWrapping wrapping)
    {
        ArgumentNullException.ThrowIfNull(wrapping);
        _defaultWrapping = wrapping;
        return this;
    }

    /// <summary>
    /// Overrides the wrapping/truncation for the value of the most-recently added field, taking
    /// precedence over <see cref="DefaultWrapping"/>. When the preceding add was skipped (a missing
    /// <see cref="AddOptional(string, object?, string?, ThemeStyle?)"/> or a false
    /// <see cref="AddWhen(bool, string, object?, string?, ThemeStyle?)"/>), this is a no-op — there is
    /// no field to configure. Wrapping needs a width bound to bind — pair it with
    /// <see cref="SetValueWidth"/> or a width-constrained detail view.
    /// </summary>
    /// <param name="wrapping">The wrapping/truncation for the last field's value. Must not be <c>null</c>.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliDetails SetWrapping(CliWrapping wrapping)
    {
        ArgumentNullException.ThrowIfNull(wrapping);
        if (_lastField is not null)
            _lastField.Wrapping = wrapping;
        return this;
    }

    /// <summary>
    /// Sets the width bounds for the single value column shared by all fields (a detail view is
    /// horizontal, so there is exactly one value column). A <paramref name="maxWidth"/> is what makes
    /// values wrap or truncate; without one, wrapping only takes effect when the whole detail view is
    /// width-constrained. Only non-<c>null</c> arguments are applied. This is a view-level setting,
    /// not per-field, because every value lives in the same column.
    /// </summary>
    /// <returns>This instance, for fluent chaining.</returns>
    public CliDetails SetValueWidth(int? width = null, int? minWidth = null, int? maxWidth = null)
    {
        _valueMinWidth = minWidth;
        _valueMaxWidth = maxWidth;
        _valueWidth = width;
        return this;
    }

    // The field configured by the last Add*/AddOptional*/AddWhen* call, or null when that call added
    // no field (a missing AddOptional / false AddWhen). SetWrapping targets it so a skipped add does
    // not accidentally reconfigure an earlier field.
    private Field? _lastField;

    private void AddField(Field field)
    {
        _fields.Add(field);
        _lastField = field;
    }

    /// <summary>
    /// Adds a field that is always rendered. When the value is missing it renders
    /// <paramref name="missingDisplay"/> (or the default missing display when <c>null</c>).
    /// Use this — not <see cref="AddOptional(string, object?, string?, ThemeStyle?)"/> — when an absent
    /// value should be shown explicitly, e.g. <c>Add("Database:", db, "(not selected)")</c>. The optional
    /// <paramref name="style"/> applies a semantic theme style to the <b>value</b> (not the label);
    /// e.g. <see cref="ThemeStyle.Key"/> for an identity/anchor value or <see cref="ThemeStyle.Path"/>
    /// for a filesystem path.
    /// </summary>
    public CliDetails Add(string label, object? value, string? missingDisplay = null, ThemeStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        AddField(new Field(label, value, missingDisplay, style));
        return this;
    }

    /// <summary>
    /// Adds a field only when the value is present; missing values omit the field entirely
    /// (no row is rendered). Contrast with <see cref="Add(string, object?, string?, ThemeStyle?)"/>,
    /// which always renders. The <paramref name="missingDisplay"/> is accepted for signature symmetry
    /// but never used (a missing value omits the field). The optional <paramref name="style"/> styles
    /// the value when the field is shown.
    /// </summary>
    public CliDetails AddOptional(string label, object? value, string? missingDisplay = null, ThemeStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (IsMissing(value))
        {
            _lastField = null;
            return this;
        }
        AddField(new Field(label, value, missingDisplay, style));
        return this;
    }

    /// <summary>
    /// Adds a field only when <paramref name="condition"/> is <c>true</c>. When added, the field is
    /// always rendered (like <see cref="Add(string, object?, string?, ThemeStyle?)"/>), showing the
    /// missing display for a missing value. The optional <paramref name="style"/> styles the value.
    /// </summary>
    public CliDetails AddWhen(bool condition, string label, object? value, string? missingDisplay = null, ThemeStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (!condition)
        {
            _lastField = null;
            return this;
        }
        AddField(new Field(label, value, missingDisplay, style));
        return this;
    }

    /// <summary>
    /// Adds a field whose value is an identity/anchor (ID, name, code, slug, group ID, …), styled with
    /// <see cref="ThemeStyle.Key"/>. Convenience for <c>Add(label, value, style: ThemeStyle.Key)</c>.
    /// Key values are normally present, so there is intentionally no <c>AddKeyWhen</c>/<c>AddOptionalKey</c>;
    /// for the rare conditional/optional key use the generic styled overload with
    /// <see cref="ThemeStyle.Key"/>.
    /// </summary>
    public CliDetails AddKey(string label, object? value)
        => Add(label, value, style: ThemeStyle.Key);

    /// <summary>
    /// Adds a field whose value is a filesystem/local path, styled with <see cref="ThemeStyle.Path"/>.
    /// Convenience for <c>Add(label, value, style: ThemeStyle.Path)</c>.
    /// </summary>
    public CliDetails AddPath(string label, object? value)
        => Add(label, value, style: ThemeStyle.Path);

    /// <summary>
    /// Adds a path field only when the value is present, styled with <see cref="ThemeStyle.Path"/>.
    /// Convenience for <c>AddOptional(label, value, style: ThemeStyle.Path)</c>.
    /// </summary>
    public CliDetails AddOptionalPath(string label, object? value)
        => AddOptional(label, value, style: ThemeStyle.Path);

    /// <summary>
    /// Adds a field whose value is a navigable/link value, styled with <see cref="ThemeStyle.Link"/>.
    /// This is semantic styling only (no clickable hyperlink). Convenience for
    /// <c>Add(label, value, style: ThemeStyle.Link)</c>.
    /// </summary>
    public CliDetails AddLink(string label, object? value)
        => Add(label, value, style: ThemeStyle.Link);

    /// <summary>
    /// Adds a link field only when the value is present, styled with <see cref="ThemeStyle.Link"/>.
    /// Convenience for <c>AddOptional(label, value, style: ThemeStyle.Link)</c>.
    /// </summary>
    public CliDetails AddOptionalLink(string label, object? value)
        => AddOptional(label, value, style: ThemeStyle.Link);

    /// <summary>
    /// The "missing" rule for detail values: <c>null</c> is missing, and a <see cref="string"/> that
    /// is empty or all-whitespace is missing. Other values — including <c>false</c> and <c>0</c> —
    /// are present. This keeps detail UX clean (blank strings read as "no value") while never hiding
    /// meaningful falsy data.
    /// </summary>
    public static bool IsMissing(object? value) => value switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s),
        _ => false
    };

    /// <summary>
    /// Builds the equivalent <see cref="CliTable"/>: the chosen preset (forced to horizontal detail
    /// orientation), the optional title, one header element per field (the label), and a single record
    /// carrying the field values. Missing values
    /// are rendered through the field's effective missing display. This is the integration seam —
    /// rendering goes through the normal <see cref="CliTable"/> → <see cref="CliGrid"/> pipeline.
    /// </summary>
    public CliTable ToTable()
    {
        // CliDetails is a key/value detail view: labels are row headers and the single record is the
        // value column. Resolve the preset for HORIZONTAL orientation so the preset's horizontal
        // header defaults (left-aligned row labels) are used — not its vertical/list-table header
        // alignment. A non-details preset (e.g. Roma, Milano) then contributes only visual styling
        // without leaking list-table header alignment into the detail view.
        var preset = _presetApplied
            ? _preset
            : CliOutputPresetContext.Current?.Details ?? _preset;
        var table = new CliTable().ApplyPreset(preset, _theme, CliTableOrientation.Horizontal);

        // A detail view is always horizontal regardless of the supplied preset. For universal and
        // horizontal-only presets the line above already resolves to horizontal; this keeps the
        // guarantee explicit.
        table.Orientation = CliTableOrientation.Horizontal;

        CopyLayoutTo(table);

        if (_hasTitle)
        {
            table.AddTitle(_titleContent!, _titleFormattingMode, _titleFormatter);

            // Alignment override is applied onto the already-built title cell style (the existing
            // CliCellStyle.HorizontalAlignment capability) so no parallel title rendering is added and
            // the preset's title ink/surface is preserved — only the horizontal alignment changes.
            if (_titleAlignment is not null && table.Title is not null)
                table.Title.Style.HorizontalAlignment = _titleAlignment;
        }

        // No fields → leave the table without a header/record; CliTable.ToGrid() reports the
        // "at least one field" error consistently with a hand-built empty table.
        if (_fields.Count == 0)
            return table;

        var theme = _theme ?? TigerConsole.CurrentTheme;

        var record = new object?[_fields.Count];
        for (int i = 0; i < _fields.Count; i++)
        {
            var field = _fields[i];

            // The element (row) axis carries the value's data style. A semantic style resolves to a
            // foreground/decoration-only style (background null) so the preset's surface is preserved;
            // when no style is given only the missing display is set so the preset's body styling wins
            // via the style cascade. The semantic foreground sits on the element/row axis — the value
            // (record) column axis has no foreground, so a horizontal detail view applies the row ink.
            var dataStyle = CliSemanticValueStyle.Resolve(theme, field.Style) ?? new CliCellStyle();
            dataStyle.NullDisplayValue = field.MissingDisplay ?? _missingDisplay;

            // Wrapping/truncation is a per-cell property, so it lives on the field's (row/element) data
            // style and reaches the value cell through the same style cascade CliTable uses — the shared
            // value-column width (set below on the record axis) provides the bound it wraps within. The
            // cascade keeps any semantic foreground intact (layout only). Width is never per-field here:
            // horizontal detail views have a single value column, sized once via SetValueWidth.
            var wrapping = field.Wrapping ?? _defaultWrapping;
            if (wrapping is not null)
                dataStyle.Wrapping = wrapping;

            var element = new CliTableElement(field.Label, dataStyle);

            // A present Link value is a clickable hyperlink; the render pipeline derives the target from
            // the value text. A missing value (rendered as the missing display) is never a hyperlink.
            if (field.Style == ThemeStyle.Link && !IsMissing(field.Value))
                element.DataIsHyperlink = true;

            table.Header.Elements.Add(element);

            // Normalize missing values to null so the field's NullDisplayValue is used (a blank
            // string would otherwise render as empty rather than as the missing display).
            record[i] = IsMissing(field.Value) ? null : field.Value;
        }

        table.AddRecord(record);

        // The value column is the record axis in a horizontal table, whose axis style is the table's
        // DataStyle. Its width bounds are what CliGrid reads to wrap/truncate the values (per-cell
        // wrapping mode + column width bound). Augment the preset's DataStyle so surface/zebra styling
        // is preserved; setting min/max before the fixed width keeps CliCellStyle's own validation happy.
        if (_valueWidth is not null || _valueMinWidth is not null || _valueMaxWidth is not null)
        {
            table.DataStyle ??= new CliCellStyle();
            if (_valueMinWidth is not null) table.DataStyle.MinWidth = _valueMinWidth;
            if (_valueMaxWidth is not null) table.DataStyle.MaxWidth = _valueMaxWidth;
            if (_valueWidth is not null) table.DataStyle.Width = _valueWidth;
        }

        return table;
    }

    /// <inheritdoc />
    public override CliGrid ToGrid() => ToTable().ToGrid();

    private void CopyLayoutTo(CliTable table)
    {
        table.IsInteractive = IsInteractive;

        table.Width = Width;
        table.MinWidth = MinWidth;
        table.SoftMaxWidth = SoftMaxWidth;
        table.MaxWidth = MaxWidth;

        table.Height = Height;
        table.MinHeight = MinHeight;
        table.SoftMaxHeight = SoftMaxHeight;
        table.MaxHeight = MaxHeight;
    }
}
