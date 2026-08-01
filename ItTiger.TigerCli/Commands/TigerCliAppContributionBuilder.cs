namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Builds the app-wide configuration supplied by registered <see cref="ITigerCliAppContribution"/>
/// instances. The 0.9.1 surface intentionally exposes only optional string global options.
/// </summary>
public sealed class TigerCliAppContributionBuilder
{
    /// <summary>
    /// Gets the builder for CLI-only, app-wide global options contributed by reusable libraries.
    /// </summary>
    public TigerCliGlobalOptionBuilder GlobalOptions { get; } = new();

    internal TigerCliAppContributionBuilder()
    {
    }
}
