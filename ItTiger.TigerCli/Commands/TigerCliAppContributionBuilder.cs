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
        TigerCliEnvironmentVariableRegistrations.Add(
            environmentVariables, name, description, "app-contribution scope");
        return this;
    }

    internal TigerCliAppContributionBuilder()
    {
    }

    internal IReadOnlyList<TigerCliEnvironmentVariableRegistration> BuildEnvironmentVariables() =>
        environmentVariables.ToArray();
}
