using System.Globalization;
using System.Resources;

namespace ItTiger.TigerCli.Resources;

/// <summary>
/// Access to framework-owned localized strings. All lookups take an explicit
/// <see cref="CultureInfo"/> so the framework never depends on
/// <see cref="CultureInfo.CurrentUICulture"/>.
/// </summary>
internal static class TigerCliResources
{
    private static readonly ResourceManager _rm = new(
        "ItTiger.TigerCli.Resources.TigerCliStrings",
        typeof(TigerCliResources).Assembly);

    public static string Get(string key, CultureInfo culture)
    {
        return _rm.GetString(key, culture) ?? key;
    }

    public static string Format(string key, CultureInfo culture, params object[] args)
    {
        var template = Get(key, culture);
        return string.Format(CultureInfo.InvariantCulture, template, args);
    }
}
