using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Tui.Activity;

/// <summary>
/// Fluent builder for a single <see cref="ActivityRowSpec"/>. Add cells with <see cref="Cell"/> and,
/// for a dynamic (named) row, declare the fixed value array with <see cref="Values"/>.
/// </summary>
public sealed class ActivityRowBuilder
{
    private readonly int _columnCount;
    private readonly List<ActivityCellSpec> _cells = new();
    private readonly bool[] _occupied;
    private object?[]? _values;
    private bool _valuesSet;

    internal ActivityRowBuilder(string? name, int columnCount)
    {
        Name = name;
        _columnCount = columnCount;
        _occupied = new bool[columnCount];
    }

    internal string? Name { get; }

    /// <summary>
    /// Begins a cell at <paramref name="column"/> spanning <paramref name="span"/> columns. Terminate the
    /// returned cell builder with <c>.Text(...)</c> or <c>.ProgressBar(...)</c>, which returns this row
    /// builder for further chaining.
    /// </summary>
    public ActivityCellBuilder Cell(int column, int span = 1)
    {
        if (column < 0 || column >= _columnCount)
            throw new ArgumentOutOfRangeException(nameof(column), $"Column must be in [0, {_columnCount - 1}].");
        if (span < 1)
            throw new ArgumentOutOfRangeException(nameof(span), "Span must be at least 1.");
        if (column + span > _columnCount)
            throw new ArgumentOutOfRangeException(nameof(span), "Cell exceeds the column count.");

        for (int c = column; c < column + span; c++)
            if (_occupied[c])
                throw new ArgumentException($"Column {c} is already occupied by another cell in this row.");

        return new ActivityCellBuilder(this, column, span);
    }

    /// <summary>
    /// Declares the fixed-length value array for a dynamic row and seeds the initial values. May be
    /// called once. Only valid on a named row.
    /// </summary>
    public ActivityRowBuilder Values(params object?[] values)
    {
        if (_valuesSet)
            throw new InvalidOperationException("Values(...) has already been defined for this row.");

        _values = (object?[])(values ?? Array.Empty<object?>()).Clone();
        _valuesSet = true;
        return this;
    }

    internal ActivityRowBuilder AddCell(ActivityCellSpec cell)
    {
        for (int c = cell.Column; c < cell.Column + cell.Span; c++)
            _occupied[c] = true;
        _cells.Add(cell);
        return this;
    }

    internal ActivityRowSpec Build()
    {
        int valueCount = _values?.Length ?? 0;

        if (Name is not null && !_valuesSet)
            throw new ArgumentException($"Dynamic row '{Name}' must declare Values(...).");
        if (Name is null && _valuesSet)
            throw new ArgumentException("A static (unnamed) row cannot declare Values(...). Name the row to make it dynamic.");

        foreach (var cell in _cells)
            cell.Element.Validate(valueCount, Name);

        return new ActivityRowSpec(Name, _cells.ToList(), valueCount, _values ?? Array.Empty<object?>());
    }
}

/// <summary>
/// Fluent builder for a single cell. Terminating with <see cref="Text"/> returns an
/// <see cref="ActivityTextCellBuilder"/> for optional style/alignment refinement; <see cref="ProgressBar(int, double, ProgressBarStyle, ProgressBarCaps, ProgressBarColorMode)"/>
/// adds the cell to the owning row and returns the row builder.
/// </summary>
public sealed class ActivityCellBuilder
{
    private readonly ActivityRowBuilder _row;
    private readonly int _column;
    private readonly int _span;

    internal ActivityCellBuilder(ActivityRowBuilder row, int column, int span)
    {
        _row = row;
        _column = column;
        _span = span;
    }

    /// <summary>
    /// A single-line markup text element (see <see cref="ActivityTextElement"/>). Returns a text-cell
    /// builder so the element's style/alignment can be refined fluently (<c>.Style(...)</c>/<c>.Align(...)</c>)
    /// before the next <c>.Cell(...)</c> or <c>.Values(...)</c>. Style/alignment are deliberately not
    /// parameters of <c>Text(...)</c> because a template may contain composite placeholders.
    /// </summary>
    public ActivityTextCellBuilder Text(string template)
    {
        var element = new ActivityTextElement(template);
        _row.AddCell(new ActivityCellSpec(_column, _span, element));
        return new ActivityTextCellBuilder(_row, element);
    }

    /// <summary>
    /// A progress bar with a fixed maximum (default 100), bound to <paramref name="valueIndex"/>. The
    /// optional <paramref name="style"/> selects a predefined glyph appearance (default <c>█</c>/<c>░</c>);
    /// the optional <paramref name="caps"/> adds end-cap decoration (default none), composing with any style;
    /// the optional <paramref name="colorMode"/> selects single- (default), two- or three-colour painting.
    /// </summary>
    public ActivityRowBuilder ProgressBar(
        int valueIndex,
        double maxValue = 100,
        ProgressBarStyle style = ProgressBarStyle.Default,
        ProgressBarCaps caps = ProgressBarCaps.None,
        ProgressBarColorMode colorMode = ProgressBarColorMode.Single) =>
        _row.AddCell(new ActivityCellSpec(_column, _span, new ActivityProgressBarElement(valueIndex, maxValue, null, style, caps, colorMode)));

    /// <summary>
    /// A progress bar whose maximum is read from <paramref name="maxValueIndex"/>. The optional
    /// <paramref name="style"/> selects a predefined glyph appearance (default <c>█</c>/<c>░</c>); the
    /// optional <paramref name="caps"/> adds end-cap decoration (default none), composing with any style;
    /// the optional <paramref name="colorMode"/> selects single- (default), two- or three-colour painting.
    /// </summary>
    public ActivityRowBuilder ProgressBar(
        int valueIndex,
        int maxValueIndex,
        ProgressBarStyle style = ProgressBarStyle.Default,
        ProgressBarCaps caps = ProgressBarCaps.None,
        ProgressBarColorMode colorMode = ProgressBarColorMode.Single) =>
        _row.AddCell(new ActivityCellSpec(_column, _span, new ActivityProgressBarElement(valueIndex, 100, maxValueIndex, style, caps, colorMode)));
}

/// <summary>
/// Continues the fluent chain after <see cref="ActivityCellBuilder.Text"/>. Optionally refines the
/// just-added text element's <see cref="Style"/> and <see cref="Align"/>ment, then starts the next
/// <see cref="Cell"/> or declares the row's <see cref="Values"/>. Style/alignment live here, not on
/// <c>Text(...)</c>, because a text template may contain composite placeholders, so trailing parameters
/// there would be ambiguous.
/// </summary>
public sealed class ActivityTextCellBuilder
{
    private readonly ActivityRowBuilder _row;
    private readonly ActivityTextElement _element;

    internal ActivityTextCellBuilder(ActivityRowBuilder row, ActivityTextElement element)
    {
        _row = row;
        _element = element;
    }

    /// <summary>Sets the text element's explicit theme style (highest precedence). Returns this builder.</summary>
    public ActivityTextCellBuilder Style(ThemeStyle style)
    {
        _element.Style = style;
        return this;
    }

    /// <summary>Sets the text element's explicit horizontal alignment (highest precedence). Returns this builder.</summary>
    public ActivityTextCellBuilder Align(CliTextAlignment alignment)
    {
        _element.Alignment = alignment;
        return this;
    }

    /// <summary>Sets the text element's cell padding (overrides the column padding). Returns this builder.</summary>
    public ActivityTextCellBuilder Padding(CliCellPadding padding)
    {
        _element.Padding = padding;
        return this;
    }

    /// <summary>Begins the next cell in the row (see <see cref="ActivityRowBuilder.Cell"/>).</summary>
    public ActivityCellBuilder Cell(int column, int span = 1) => _row.Cell(column, span);

    /// <summary>Declares the row's fixed-length value array (see <see cref="ActivityRowBuilder.Values"/>).</summary>
    public ActivityRowBuilder Values(params object?[] values) => _row.Values(values);
}
