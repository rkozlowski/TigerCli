using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Tui.Activity;

/// <summary>
/// Immutable description of a rich activity dialog's live layout: variable columns and rows of cells,
/// with named dynamic rows carrying fixed-length value arrays. Build one with <see cref="Create"/>; the
/// activity control maps it (plus the runtime values) onto a <c>CliGrid</c>. The spec is shape only —
/// runtime values are held separately and mutated through the operation's activity context.
/// </summary>
public sealed class ActivityDialogSpec
{
    private readonly Dictionary<string, int> _rowIndexByName;

    internal ActivityDialogSpec(
        IReadOnlyList<ActivityColumnSpec> columns,
        IReadOnlyList<ActivityRowSpec> rows,
        ThemeStyle? defaultCellStyle,
        CliTextAlignment? defaultCellAlignment,
        string? nonInteractiveMessage = null)
    {
        Columns = columns;
        Rows = rows;
        DefaultCellStyle = defaultCellStyle;
        DefaultCellAlignment = defaultCellAlignment;
        NonInteractiveMessage = nonInteractiveMessage;

        _rowIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < rows.Count; i++)
        {
            var name = rows[i].Name;
            if (name is null)
                continue;
            // Uniqueness is also enforced as rows are added to the builder; this is a defensive backstop.
            if (!_rowIndexByName.TryAdd(name, i))
                throw new ArgumentException($"Duplicate dynamic row name '{name}'.");
        }
    }

    /// <summary>The column definitions, left to right.</summary>
    public IReadOnlyList<ActivityColumnSpec> Columns { get; }

    /// <summary>The row definitions, top to bottom.</summary>
    public IReadOnlyList<ActivityRowSpec> Rows { get; }

    /// <summary>
    /// Optional spec-wide default theme style for text cells, applied when neither the text element nor
    /// its column declares a style. <c>null</c> means fall back to the built-in default.
    /// </summary>
    public ThemeStyle? DefaultCellStyle { get; }

    /// <summary>
    /// Optional spec-wide default horizontal alignment for text cells, applied when neither the text
    /// element nor its column declares an alignment. <c>null</c> means fall back to the built-in default.
    /// </summary>
    public CliTextAlignment? DefaultCellAlignment { get; }

    /// <summary>
    /// Optional one-line message printed once (to stdout, via <c>TigerConsole.MarkupLine</c>) before the
    /// operation body runs when the activity is executed in <see cref="Enums.TigerCliInteractionMode.NonInteractive"/>
    /// mode. It gives scripts a single line of progress context in place of the live dialog. It is never
    /// used in interactive mode (the dialog is shown instead), and <c>null</c>/empty prints nothing. Like
    /// activity text templates it is a trusted markup template (may contain TigerCli markup).
    /// </summary>
    public string? NonInteractiveMessage { get; }

    /// <summary>Starts a new fluent activity specification.</summary>
    public static ActivitySpecBuilder Create() => new();

    /// <summary>The dynamic row with the given <paramref name="name"/>, or <c>null</c> when none exists.</summary>
    public ActivityRowSpec? GetRow(string name) =>
        _rowIndexByName.TryGetValue(name, out int i) ? Rows[i] : null;
}

/// <summary>
/// Fluent builder for an <see cref="ActivityDialogSpec"/>. Add columns first, then rows; call
/// <see cref="Build"/> (done automatically by the run API) to validate and freeze the spec.
/// </summary>
public sealed class ActivitySpecBuilder
{
    private readonly List<ActivityColumnSpec> _columns = new();
    private readonly List<ActivityRowBuilder> _rows = new();
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private ThemeStyle? _defaultCellStyle;
    private CliTextAlignment? _defaultCellAlignment;
    private string? _nonInteractiveMessage;

    /// <summary>
    /// Sets a one-line message printed once before the operation runs when the activity is executed in
    /// non-interactive mode (in place of the live dialog). Ignored in interactive mode; <c>null</c>/empty
    /// prints nothing. It is a trusted markup template — prefer present-progress phrasing (e.g.
    /// <c>"Importing card..."</c>) and localize it at the call site. Use this explicit spec-based control
    /// when non-interactive output should differ from the visible activity message.
    /// </summary>
    public ActivitySpecBuilder SetNonInteractiveMessage(string? message)
    {
        _nonInteractiveMessage = message;
        return this;
    }

    /// <summary>
    /// Sets the spec-wide default theme style for text cells (applied when a text element and its column
    /// both leave the style unset). Text-element and column styles take precedence over this.
    /// </summary>
    public ActivitySpecBuilder SetDefaultCellStyle(ThemeStyle style)
    {
        _defaultCellStyle = style;
        return this;
    }

    /// <summary>
    /// Sets the spec-wide default horizontal alignment for text cells (applied when a text element and
    /// its column both leave alignment unset). Text-element and column alignments take precedence.
    /// </summary>
    public ActivitySpecBuilder SetDefaultCellAlignment(CliTextAlignment alignment)
    {
        _defaultCellAlignment = alignment;
        return this;
    }

    /// <summary>
    /// Adds a column. A fixed <paramref name="width"/> is mutually exclusive with star sizing. Leave
    /// <paramref name="align"/> <c>null</c> to defer to the spec default / built-in fallback.
    /// </summary>
    public ActivitySpecBuilder AddColumn(
        int? width = null,
        CliColumnSizing sizing = CliColumnSizing.Auto,
        CliTextAlignment? align = null,
        ThemeStyle? style = null)
    {
        if (width.HasValue && sizing == CliColumnSizing.Star)
            throw new ArgumentException("A column cannot be both a fixed width and star-sized.");
        if (width.HasValue && width.Value < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Column width must be at least 1.");

        _columns.Add(new ActivityColumnSpec(width, sizing, align, style));
        return this;
    }

    /// <summary>
    /// Sets the cell <paramref name="padding"/> on the most-recently-added column (chains after
    /// <see cref="AddColumn"/>). A cell's own padding overrides the column padding.
    /// </summary>
    public ActivitySpecBuilder Padding(CliCellPadding padding)
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("Padding(...) must follow AddColumn(...).");

        var c = _columns[^1];
        _columns[^1] = new ActivityColumnSpec(c.Width, c.Sizing, c.Align, c.Style, padding);
        return this;
    }

    /// <summary>
    /// Adds a row. Pass a unique <paramref name="name"/> for a dynamic row (which must declare
    /// <c>Values(...)</c>), or <c>null</c> for a static row.
    /// </summary>
    public ActivitySpecBuilder AddRow(string? name, Action<ActivityRowBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (_columns.Count == 0)
            throw new InvalidOperationException("Add at least one column before adding rows.");
        if (name is not null && !_names.Add(name))
            throw new ArgumentException($"Duplicate dynamic row name '{name}'.");

        var row = new ActivityRowBuilder(name, _columns.Count);
        configure(row);
        _rows.Add(row);
        return this;
    }

    /// <summary>Validates and produces the immutable <see cref="ActivityDialogSpec"/>.</summary>
    public ActivityDialogSpec Build()
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("An activity spec needs at least one column.");
        if (_rows.Count == 0)
            throw new InvalidOperationException("An activity spec needs at least one row.");

        var rows = new List<ActivityRowSpec>(_rows.Count);
        foreach (var rb in _rows)
            rows.Add(rb.Build());

        return new ActivityDialogSpec(
            _columns.ToList(), rows, _defaultCellStyle, _defaultCellAlignment, _nonInteractiveMessage);
    }
}
