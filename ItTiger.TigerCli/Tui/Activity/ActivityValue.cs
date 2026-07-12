using System.Globalization;

namespace ItTiger.TigerCli.Tui.Activity;

/// <summary>
/// Numeric coercion for activity row values. Progress bars accept any numeric value type stored in a
/// row's value array; this converts them to <see cref="double"/> for fraction calculation. Non-numeric
/// or <c>null</c> values coerce to <c>0</c> so a progress bar never throws on unexpected data.
/// </summary>
internal static class ActivityValue
{
    public static double ToDouble(object? value)
    {
        switch (value)
        {
            case null:
                return 0;
            case double d:
                return d;
            case float f:
                return f;
            case int i:
                return i;
            case long l:
                return l;
            case short s:
                return s;
            case byte b:
                return b;
            case decimal m:
                return (double)m;
        }

        if (value is IConvertible)
        {
            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                return 0;
            }
        }

        return 0;
    }
}
