namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Contributes reusable, app-wide TigerCli configuration to a host application. Host apps opt in
/// by passing an implementation to <see cref="TigerCliAppBuilder.AddContribution"/>; TigerCli calls
/// <see cref="Configure"/> while the app is built.
/// </summary>
public interface ITigerCliAppContribution
{
    /// <summary>
    /// Adds this contribution's configuration to the host app through
    /// <see cref="TigerCliAppContributionBuilder.GlobalOptions"/>.
    /// </summary>
    /// <param name="builder">The contribution-scoped app configuration builder.</param>
    void Configure(TigerCliAppContributionBuilder builder);
}
