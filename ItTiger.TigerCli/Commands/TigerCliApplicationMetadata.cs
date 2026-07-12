namespace ItTiger.TigerCli.Commands;

/// <summary>
/// The app's resolved display metadata — display name, versions, copyright, and help-footer
/// links — assembled by <see cref="TigerCliAppBuilder.Build"/> from explicit builder calls and
/// assembly-metadata defaults. Exposed on <see cref="TigerCliApp.ApplicationMetadata"/>.
/// </summary>
public sealed class TigerCliApplicationMetadata
{
    internal TigerCliApplicationMetadata(
        string displayName,
        string? version,
        string? productVersion,
        bool versionEnabled,
        string? copyright,
        IReadOnlyList<TigerCliApplicationLink> links)
    {
        DisplayName = displayName;
        Version = version;
        ProductVersion = productVersion;
        VersionEnabled = versionEnabled;
        Copyright = copyright;
        Links = links;
    }

    /// <summary>
    /// The human/product name. Falls back to the application name when
    /// <see cref="TigerCliAppBuilder.SetDisplayName"/> is not called.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// The short user-facing version from <see cref="TigerCliAppBuilder.SetVersion"/> or assembly
    /// metadata. May be set even when <see cref="VersionEnabled"/> is <c>false</c>.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// The full product/informational version used by <c>--version-full</c>. Falls back to
    /// <see cref="Version"/>.
    /// </summary>
    public string? ProductVersion { get; }

    /// <summary>
    /// <c>true</c> when the built-in global <c>--version</c> and <c>--version-full</c> options are
    /// enabled.
    /// </summary>
    public bool VersionEnabled { get; }

    /// <summary>
    /// Optional help-footer copyright text.
    /// </summary>
    public string? Copyright { get; }

    /// <summary>
    /// The configured help-footer links, in display order.
    /// </summary>
    public IReadOnlyList<TigerCliApplicationLink> Links { get; }
}
