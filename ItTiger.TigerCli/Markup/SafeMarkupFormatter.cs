using System.Globalization;
using System.Text;

namespace ItTiger.TigerCli.Markup;

/// <summary>
/// Composite-format text where the <em>template</em> is trusted TigerCli markup but the substituted
/// <em>values</em> are untrusted: each value is formatted (with culture, alignment and format string),
/// then markup-escaped, then inserted into the template. The template's own <c>[...]</c> markup is left
/// intact, so a placeholder value can never inject or break out of markup.
/// </summary>
/// <remarks>
/// Placeholders use standard .NET composite syntax: <c>{index}</c>, <c>{index:format}</c>,
/// <c>{index,alignment}</c>, <c>{index,alignment:format}</c>. Literal braces are written as
/// <c>{{</c> / <c>}}</c>. This deliberately never calls <see cref="string.Format(string, object?[])"/>
/// over the whole template, which would treat the template (and any value) as a single trusted format
/// string and could mis-handle data containing braces.
/// </remarks>
internal static class SafeMarkupFormatter
{
    /// <summary>
    /// Formats <paramref name="template"/> by substituting <paramref name="values"/> for its placeholders.
    /// Each formatted value is escaped via <see cref="CliMarkupParser.Escape"/> before insertion.
    /// </summary>
    public static string Format(string template, IReadOnlyList<object?> values, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(template))
            return template ?? string.Empty;

        var sb = new StringBuilder(template.Length + 16);
        int i = 0;
        while (i < template.Length)
        {
            char c = template[i];

            if (c == '{')
            {
                if (i + 1 < template.Length && template[i + 1] == '{')
                {
                    sb.Append('{');
                    i += 2;
                    continue;
                }

                int end = template.IndexOf('}', i + 1);
                if (end < 0)
                    throw new FormatException("Unterminated placeholder ('{' without matching '}') in activity text template.");

                AppendFormattedValue(sb, template.AsSpan(i + 1, end - i - 1), values, culture);
                i = end + 1;
                continue;
            }

            if (c == '}')
            {
                if (i + 1 < template.Length && template[i + 1] == '}')
                {
                    sb.Append('}');
                    i += 2;
                    continue;
                }

                throw new FormatException("Unescaped '}' in activity text template (use '}}' for a literal brace).");
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// The highest placeholder index referenced by <paramref name="template"/>, or <c>-1</c> when the
    /// template contains no placeholders. Used by the spec builder to validate templates against the
    /// row's declared value count without running a format pass.
    /// </summary>
    public static int MaxPlaceholderIndex(string template)
    {
        if (string.IsNullOrEmpty(template))
            return -1;

        int max = -1;
        int i = 0;
        while (i < template.Length)
        {
            char c = template[i];
            if (c == '{')
            {
                if (i + 1 < template.Length && template[i + 1] == '{')
                {
                    i += 2;
                    continue;
                }

                int end = template.IndexOf('}', i + 1);
                if (end < 0)
                    throw new FormatException("Unterminated placeholder ('{' without matching '}') in activity text template.");

                int idx = ParseIndex(template.AsSpan(i + 1, end - i - 1));
                if (idx > max)
                    max = idx;
                i = end + 1;
                continue;
            }

            if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
            {
                i += 2;
                continue;
            }

            i++;
        }

        return max;
    }

    private static void AppendFormattedValue(
        StringBuilder sb, ReadOnlySpan<char> token, IReadOnlyList<object?> values, CultureInfo culture)
    {
        int index = ParseIndex(token, out int consumed);

        // Whatever follows the index (",alignment" and/or ":format") is reconstructed verbatim into a
        // single-argument composite so .NET applies alignment/format exactly as it would normally.
        string suffix = token[consumed..].ToString();
        string composite = "{0" + suffix + "}";

        object? value = index >= 0 && index < values.Count ? values[index] : null;

        // A null value formats as empty (alignment still applies); pass the raw value otherwise so
        // numeric format/alignment specifiers work.
        string formatted = string.Format(culture, composite, value ?? string.Empty);

        sb.Append(CliMarkupParser.Escape(formatted));
    }

    private static int ParseIndex(ReadOnlySpan<char> token) => ParseIndex(token, out _);

    private static int ParseIndex(ReadOnlySpan<char> token, out int consumed)
    {
        int k = 0;
        while (k < token.Length && char.IsDigit(token[k]))
            k++;

        if (k == 0)
            throw new FormatException($"Activity text placeholder '{{{token.ToString()}}}' must start with a numeric index.");

        consumed = k;
        return int.Parse(token[..k], NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
