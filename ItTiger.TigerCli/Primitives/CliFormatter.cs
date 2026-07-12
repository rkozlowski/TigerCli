using System.Diagnostics;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Formats arbitrary cell values before they are escaped or interpreted as markup by the rendering pipeline.
/// </summary>
public sealed class CliFormatter
{
    private readonly string? formatString;
    private readonly Func<object?, string>? customFormatter;

    /// <summary>Formatter that returns <c>obj?.ToString() ?? string.Empty</c>.</summary>
    public static readonly CliFormatter NoOpFormatter =
    CliFormatter.FromDelegate(obj => obj?.ToString() ?? string.Empty);

    private CliFormatter(string? formatString, Func<object?, string>? customFormatter)
    {
        this.formatString = formatString;
        this.customFormatter = customFormatter;
    }

    /// <summary>
    /// Creates a formatter that applies <paramref name="formatString"/> to <see cref="IFormattable"/>
    /// values and falls back to <c>ToString()</c> for other values.
    /// </summary>
    public static CliFormatter FromFormatString(string formatString)
    {
        ArgumentNullException.ThrowIfNull(formatString);
        return new CliFormatter(formatString, null);
    }

    /// <summary>
    /// Creates a formatter backed by an application-supplied delegate.
    /// </summary>
    public static CliFormatter FromDelegate(Func<object?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        return new CliFormatter(null, formatter);
    }

    /// <summary>
    /// Formats a value; <c>null</c> formats as an empty string.
    /// </summary>
    public string Format(object? value)
    {
        if (customFormatter != null)
            return customFormatter(value);

        if (value == null)
            return string.Empty;

        if (formatString != null && value is IFormattable formattable)
            return formattable.ToString(formatString, null);

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Creates a copy of a formatter, or <c>null</c> when <paramref name="formatter"/> is <c>null</c>.
    /// </summary>
    public static CliFormatter? Clone(CliFormatter? formatter)
    {
        if (formatter == null) 
            return null;
        return new CliFormatter(formatter.formatString, formatter.customFormatter);        
    }
}
