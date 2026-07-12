using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;

namespace ItTiger.TigerCli.Tui.Activity;

/// <summary>
/// Base type for the renderable element hosted by an <see cref="ActivityCellSpec"/>. Elements are
/// immutable and describe <em>what</em> a cell shows; the activity control maps them onto a
/// <c>CliGrid</c> cell (text) or a post-layout overlay (progress bar). Kept small and extensible.
/// </summary>
public abstract class ActivityElement
{
    /// <summary>
    /// Validates the element against the owning row's declared <paramref name="valueCount"/> and whether
    /// the row is dynamic (<paramref name="rowName"/> non-<c>null</c>). Throws on misuse so an invalid
    /// spec fails at build time, never at render time.
    /// </summary>
    internal abstract void Validate(int valueCount, string? rowName);
}

/// <summary>
/// A single-line text element. <see cref="Template"/> is trusted TigerCli markup that may contain
/// composite placeholders (<c>{0}</c>, <c>{2,5:F1}</c>, …); the substituted values are formatted and
/// markup-escaped at render time (see <see cref="SafeMarkupFormatter"/>) so data can never inject markup.
/// </summary>
public sealed class ActivityTextElement : ActivityElement
{
    /// <summary>Creates a text element from a trusted markup template.</summary>
    /// <param name="template">
    /// The markup template, optionally containing composite placeholders for the activity row values.
    /// </param>
    public ActivityTextElement(string template)
    {
        Template = template ?? throw new ArgumentNullException(nameof(template));
    }

    /// <summary>The trusted markup template with optional composite placeholders.</summary>
    public string Template { get; }

    /// <summary>
    /// Optional explicit theme style for this text. When set it takes precedence over the column default
    /// and the spec default. Set fluently via <see cref="ActivityTextCellBuilder.Style"/>.
    /// </summary>
    public ThemeStyle? Style { get; internal set; }

    /// <summary>
    /// Optional explicit horizontal alignment for this text. When set it takes precedence over the column
    /// default and the spec default. Set fluently via <see cref="ActivityTextCellBuilder.Align"/>.
    /// </summary>
    public CliTextAlignment? Alignment { get; internal set; }

    /// <summary>
    /// Optional explicit cell padding for this text. When set it overrides the column padding. Set
    /// fluently via <see cref="ActivityTextCellBuilder.Padding"/>.
    /// </summary>
    public CliCellPadding? Padding { get; internal set; }

    internal override void Validate(int valueCount, string? rowName)
    {
        int max = SafeMarkupFormatter.MaxPlaceholderIndex(Template);
        if (max < 0)
            return; // static text, no values referenced

        if (rowName is null)
            throw new ArgumentException(
                $"Static (unnamed) row text cannot use placeholders: \"{Template}\". Name the row and add Values(...).");

        if (max >= valueCount)
            throw new ArgumentException(
                $"Text template \"{Template}\" references {{{max}}} but row '{rowName}' declares {valueCount} value(s).");
    }
}

/// <summary>
/// A progress-bar element bound to row-local values. The current value is read from
/// <see cref="ValueIndex"/>; the maximum is either the fixed <see cref="FixedMax"/> (default 100) or,
/// when <see cref="MaxValueIndex"/> is set, read from that value. Rendered as a post-layout overlay so
/// <c>CliGrid</c> resolves the bar's width (place it in a star column to fill the remaining space).
/// </summary>
public sealed class ActivityProgressBarElement : ActivityElement
{
    internal ActivityProgressBarElement(
        int valueIndex,
        double fixedMax,
        int? maxValueIndex,
        ProgressBarStyle style = ProgressBarStyle.Default,
        ProgressBarCaps caps = ProgressBarCaps.None,
        ProgressBarColorMode colorMode = ProgressBarColorMode.Single)
    {
        ValueIndex = valueIndex;
        FixedMax = fixedMax;
        MaxValueIndex = maxValueIndex;
        Style = style;
        Caps = caps;
        ColorMode = colorMode;
    }

    /// <summary>Row value index holding the current progress value.</summary>
    public int ValueIndex { get; }

    /// <summary>Fixed maximum used when <see cref="MaxValueIndex"/> is <c>null</c>. Defaults to 100.</summary>
    public double FixedMax { get; }

    /// <summary>Optional row value index holding a dynamic maximum.</summary>
    public int? MaxValueIndex { get; }

    /// <summary>
    /// The predefined glyph appearance for this bar. Defaults to <see cref="ProgressBarStyle.Default"/>
    /// (<c>█</c>/<c>░</c>); the colour stays uniform regardless of style. Set via the
    /// <c>ProgressBar(..., style)</c> builder overloads.
    /// </summary>
    public ProgressBarStyle Style { get; }

    /// <summary>
    /// The optional end-cap decoration for this bar. Defaults to <see cref="ProgressBarCaps.None"/>.
    /// Composes with any <see cref="Style"/>; set via the <c>ProgressBar(..., caps)</c> builder overloads.
    /// </summary>
    public ProgressBarCaps Caps { get; }

    /// <summary>
    /// How the bar is coloured. Defaults to <see cref="ProgressBarColorMode.Single"/> (uniform). In the
    /// two-/three-colour modes the activity control resolves the done / remaining / complete colours from
    /// semantic theme styles. Set via the <c>ProgressBar(..., colorMode)</c> builder overloads.
    /// </summary>
    public ProgressBarColorMode ColorMode { get; }

    /// <summary>
    /// The clamped [0, 1] fill fraction for the given row <paramref name="values"/>. A non-positive
    /// maximum yields <c>0</c> (no divide-by-zero); values clamp into range.
    /// </summary>
    public double Fraction(IReadOnlyList<object?> values)
    {
        double current = ActivityValue.ToDouble(values[ValueIndex]);
        double max = MaxValueIndex is int mi ? ActivityValue.ToDouble(values[mi]) : FixedMax;

        if (max <= 0 || double.IsNaN(max))
            return 0;

        double fraction = current / max;
        if (double.IsNaN(fraction) || fraction < 0)
            return 0;
        return fraction > 1 ? 1 : fraction;
    }

    internal override void Validate(int valueCount, string? rowName)
    {
        if (rowName is null)
            throw new ArgumentException("A progress bar requires a named dynamic row with Values(...).");

        if (ValueIndex < 0 || ValueIndex >= valueCount)
            throw new ArgumentOutOfRangeException(
                nameof(ValueIndex),
                $"Progress bar valueIndex {ValueIndex} is out of range for row '{rowName}' ({valueCount} value(s)).");

        if (MaxValueIndex is int mi && (mi < 0 || mi >= valueCount))
            throw new ArgumentOutOfRangeException(
                nameof(MaxValueIndex),
                $"Progress bar maxValueIndex {mi} is out of range for row '{rowName}' ({valueCount} value(s)).");
    }
}
