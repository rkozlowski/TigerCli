using System.Globalization;
using System.Resources;

namespace CommandParserTest.Resources;

internal static class CommandParserTestStrings
{
    private static readonly ResourceManager _resourceManager = new(
        "CommandParserTest.Resources.CommandParserTestStrings",
        typeof(CommandParserTestStrings).Assembly);

    public static ResourceManager ResourceManager => _resourceManager;

    public static string Get(string key, CultureInfo culture)
    {
        return _resourceManager.GetString(key, culture) ?? key;
    }
}
