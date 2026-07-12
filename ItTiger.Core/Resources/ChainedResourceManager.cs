using System.Globalization;
using System.Resources;

namespace ItTiger.Core.Resources;

/// <summary>
/// Resolves strings from a prioritized sequence of resource managers.
/// </summary>
/// <remarks>
/// Use this resource manager to compose application resources with reusable-library resources.
/// Place application resources first so that they can override library defaults.
/// A missing or empty value continues lookup with the next manager. A manager that throws
/// <see cref="MissingManifestResourceException"/> is skipped.
/// </remarks>
public sealed class ChainedResourceManager : ResourceManager
{
    private readonly IReadOnlyList<ResourceManager> managers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChainedResourceManager"/> class.
    /// </summary>
    /// <param name="managers">
    /// The resource managers to query in priority order. Null entries are ignored.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="managers"/> is <see langword="null"/>.
    /// </exception>
    public ChainedResourceManager(params ResourceManager[] managers)
    {
        ArgumentNullException.ThrowIfNull(managers);
        this.managers = managers.Where(manager => manager is not null).ToArray();
    }

    /// <summary>
    /// Gets the first non-empty string found for the specified resource name, using the current
    /// UI culture and its fallback chain.
    /// </summary>
    /// <param name="name">The name of the resource to retrieve.</param>
    /// <returns>The first non-empty matching resource string, or <see langword="null"/> if none is found.</returns>
    public override string? GetString(string name) => GetString(name, culture: null);

    /// <summary>
    /// Gets the first non-empty string found for the specified resource name and culture.
    /// </summary>
    /// <param name="name">The name of the resource to retrieve.</param>
    /// <param name="culture">
    /// The culture to use for lookup, or <see langword="null"/> to use the current UI culture.
    /// The value is passed unchanged to each resource manager.
    /// </param>
    /// <returns>The first non-empty matching resource string, or <see langword="null"/> if none is found.</returns>
    public override string? GetString(string name, CultureInfo? culture)
    {
        foreach (var manager in managers)
        {
            try
            {
                var value = manager.GetString(name, culture);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            catch (MissingManifestResourceException)
            {
                // A manager without resources for this culture chain is skipped.
            }
        }

        return null;
    }
}
