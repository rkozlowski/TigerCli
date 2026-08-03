namespace ItTiger.TigerCli.Commands;

internal sealed record TigerCliEnvironmentVariableRegistration(
    string Name,
    string Description,
    string? DescriptionResourceKey = null);

internal static class TigerCliEnvironmentVariableRegistrations
{
    internal static readonly string[] FrameworkNames =
    [
        "TIGERCLI_THEME",
        "FORCE_COLOR",
        "CLICOLOR_FORCE",
        "NO_COLOR",
        "CLICOLOR",
        "TERM"
    ];

    internal static TigerCliEnvironmentVariableRegistration Create(
        string name,
        string description,
        string? descriptionResourceKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (name.Any(char.IsWhiteSpace))
            throw new ArgumentException(
                $"Environment variable name '{name}' must not contain whitespace.",
                nameof(name));

        return new TigerCliEnvironmentVariableRegistration(name, description, descriptionResourceKey);
    }

    internal static void Add(
        List<TigerCliEnvironmentVariableRegistration> registrations,
        string name,
        string description,
        string scope,
        string? descriptionResourceKey = null)
    {
        var registration = Create(name, description, descriptionResourceKey);
        if (registrations.Any(existing =>
            string.Equals(existing.Name, registration.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Environment variable '{registration.Name}' is already registered in {scope}.");
        }

        registrations.Add(registration);
    }

    internal static IReadOnlyList<TigerCliEnvironmentVariableRegistration> Merge(
        IReadOnlyList<TigerCliEnvironmentVariableRegistration> inherited,
        IReadOnlyList<TigerCliEnvironmentVariableRegistration> own,
        string scope)
    {
        var result = new List<TigerCliEnvironmentVariableRegistration>(inherited.Count + own.Count);
        result.AddRange(inherited);

        foreach (var registration in own)
        {
            if (result.Any(existing =>
                string.Equals(existing.Name, registration.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{registration.Name}' is registered more than once in {scope}.");
            }

            result.Add(registration);
        }

        return result;
    }
}
