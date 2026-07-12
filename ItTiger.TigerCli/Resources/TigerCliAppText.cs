using System.Globalization;
using System.Resources;

namespace ItTiger.TigerCli.Resources;

/// <summary>
/// Resolves app-owned metadata text (application/command/option/argument
/// descriptions) using the optional resource-key + fallback shape.
/// </summary>
/// <remarks>
/// Rule:
///   1. If <c>resourceKey</c> is non-empty, <c>appResources</c>
///      is configured, and the key resolves to a non-empty string for the active
///      culture, return that resource string.
///   2. Otherwise return <c>fallback</c> as-is (null or empty stays as-is).
/// Raw resource keys are never surfaced — a missing/empty lookup silently falls
/// back to <c>fallback</c>.
/// </remarks>
internal static class TigerCliAppText
{
    public static string? Resolve(
        string? fallback,
        string? resourceKey,
        CultureInfo culture,
        ResourceManager? appResources)
    {
        if (!string.IsNullOrEmpty(resourceKey) && appResources != null)
        {
            try
            {
                var localized = appResources.GetString(resourceKey, culture);
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
            catch
            {
                // Resource lookup failed — fall through to fallback.
            }
        }
        return fallback;
    }
}
