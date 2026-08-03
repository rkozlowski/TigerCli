namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Builds the app-wide configuration supplied by registered <see cref="ITigerCliAppContribution"/>
/// instances. Contributions may add optional string global options and help-only metadata for
/// environment variables recognized by the contributing library.
/// </summary>
public sealed class TigerCliAppContributionBuilder
{
    private readonly List<TigerCliEnvironmentVariableRegistration> environmentVariables = new();

    /// <summary>
    /// Gets the builder for CLI-only, app-wide global options contributed by reusable libraries.
    /// </summary>
    public TigerCliGlobalOptionBuilder GlobalOptions { get; } = new();

    /// <summary>
    /// Adds app-wide help metadata for an environment variable recognized by the contribution.
    /// TigerCli displays the name and description in <c>--help-env</c>; it does not read, parse, or
    /// apply the variable.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="description">The help description. TigerCli markup is supported.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or <paramref name="description"/> is null, empty, or whitespace, or
    /// <paramref name="name"/> contains whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="name"/> duplicates another contributed environment variable.
    /// </exception>
    public TigerCliAppContributionBuilder AddEnvironmentVariable(string name, string description)
    {
        return AddEnvironmentVariable(name, description, descriptionResourceKey: null);
    }

    /// <summary>
    /// Adds app-wide help metadata with a localizable description for an environment variable
    /// recognized by the contribution. TigerCli displays the name and description in
    /// <c>--help-env</c>; it does not read, parse, or apply the variable.
    /// </summary>
    /// <param name="name">The literal environment variable name.</param>
    /// <param name="description">Fallback help description used when the resource key is absent or
    /// does not resolve. TigerCli markup is supported.</param>
    /// <param name="descriptionResourceKey">Optional resource key resolved through the app
    /// <see cref="System.Resources.ResourceManager"/> registered via
    /// <c>TigerCliAppBuilder.UseAppResources(...)</c> against the active run culture. Missing or
    /// empty resource values silently fall back to <paramref name="description"/>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or <paramref name="description"/> is null, empty, or whitespace, or
    /// <paramref name="name"/> contains whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="name"/> duplicates another contributed environment variable.
    /// </exception>
    public TigerCliAppContributionBuilder AddEnvironmentVariable(
        string name,
        string description,
        string? descriptionResourceKey = null)
    {
        TigerCliEnvironmentVariableRegistrations.Add(
            environmentVariables,
            name,
            description,
            "app-contribution scope",
            descriptionResourceKey);
        return this;
    }

    internal TigerCliAppContributionBuilder()
    {
    }

    internal IReadOnlyList<TigerCliEnvironmentVariableRegistration> BuildEnvironmentVariables() =>
        environmentVariables.ToArray();
}
