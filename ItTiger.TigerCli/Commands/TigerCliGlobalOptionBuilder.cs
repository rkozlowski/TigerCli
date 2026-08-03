namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Registers simple app-wide global options supplied by app contributions. TigerCli 0.9.1 supports
/// one constrained shape: an optional string value with one canonical long option name.
/// </summary>
public sealed class TigerCliGlobalOptionBuilder
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "--color",
        "--culture",
        "--help",
        "--help-env",
        "--help-errors",
        "--no-color",
        "--non-interactive",
        "--theme",
        "--version",
        "--version-full"
    };

    private readonly List<TigerCliGlobalOptionRegistration> registrations = new();
    private readonly HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

    internal TigerCliGlobalOptionBuilder()
    {
    }

    /// <summary>
    /// Adds an optional, CLI-only string global option. The option is app-wide in meaning but follows
    /// TigerCli's command-line grammar, appearing after the command path and positional arguments with
    /// other options. It appears in root and command help, is never prompted, and invokes
    /// <paramref name="apply"/> once per command run with <c>null</c> when absent. Return a validation
    /// error to stop the run before command binding and execution.
    /// </summary>
    /// <param name="name">The canonical long name, beginning with <c>--</c>.</param>
    /// <param name="valueName">The value placeholder displayed in help, such as <c>path</c>.</param>
    /// <param name="description">The help description. TigerCli markup is supported.</param>
    /// <param name="apply">
    /// Applies the parsed value to contribution-owned state and optionally validates it. The value is
    /// <c>null</c> when the option was not supplied.
    /// </param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="valueName"/> or <paramref name="description"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="apply"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="name"/> is not a canonical long name, contains whitespace, is reserved by
    /// TigerCli, or duplicates another contributed global option.
    /// </exception>
    public TigerCliGlobalOptionBuilder AddOptionalString(
        string name,
        string valueName,
        string description,
        Func<TigerCliGlobalOptionContext, string?, TigerCliValidationResult> apply)
    {
        return AddOptionalString(name, valueName, description, apply, descriptionResourceKey: null);
    }

    /// <summary>
    /// Adds an optional, CLI-only string global option with a localizable help description. The
    /// option follows TigerCli's command-line grammar, appears in root and command help, is never
    /// prompted, and invokes <paramref name="apply"/> once per command run with <c>null</c> when
    /// absent. Return a validation error to stop the run before command binding and execution.
    /// </summary>
    /// <param name="name">The canonical long name, beginning with <c>--</c>.</param>
    /// <param name="valueName">The literal value placeholder displayed in help, such as
    /// <c>path</c>.</param>
    /// <param name="description">Fallback help description used when the resource key is absent or
    /// does not resolve. TigerCli markup is supported.</param>
    /// <param name="apply">
    /// Applies the parsed value to contribution-owned state and optionally validates it. The value is
    /// <c>null</c> when the option was not supplied.
    /// </param>
    /// <param name="descriptionResourceKey">Optional resource key resolved through the app
    /// <see cref="System.Resources.ResourceManager"/> registered via
    /// <c>TigerCliAppBuilder.UseAppResources(...)</c> against the active run culture. Missing or
    /// empty resource values silently fall back to <paramref name="description"/>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="valueName"/> or <paramref name="description"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="apply"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="name"/> is not a canonical long name, contains whitespace, is reserved by
    /// TigerCli, or duplicates another contributed global option.
    /// </exception>
    public TigerCliGlobalOptionBuilder AddOptionalString(
        string name,
        string valueName,
        string description,
        Func<TigerCliGlobalOptionContext, string?, TigerCliValidationResult> apply,
        string? descriptionResourceKey = null)
    {
        ValidateName(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(apply);

        if (!names.Add(name))
            throw new InvalidOperationException(
                $"A contributed global option named '{name}' is already registered.");

        registrations.Add(new TigerCliGlobalOptionRegistration(
            name,
            valueName,
            description,
            apply,
            descriptionResourceKey));
        return this;
    }

    internal IReadOnlyList<TigerCliGlobalOptionRegistration> Build() => registrations.ToArray();

    private static void ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException(
                "A contributed global option name must not be empty and must begin with '--'.");
        if (!name.StartsWith("--", StringComparison.Ordinal) || name.Length == 2)
            throw new InvalidOperationException(
                $"Contributed global option name '{name}' must be one canonical long name beginning with '--'.");
        if (name[2] == '-' || name.Contains('=') || name.Contains('|'))
            throw new InvalidOperationException(
                $"Contributed global option name '{name}' must be one canonical long name beginning with '--'.");
        if (name.Any(char.IsWhiteSpace))
            throw new InvalidOperationException(
                $"Contributed global option name '{name}' must not contain whitespace.");
        if (ReservedNames.Contains(name))
            throw new InvalidOperationException(
                $"Contributed global option name '{name}' is reserved by TigerCli.");
    }
}

internal sealed record TigerCliGlobalOptionRegistration(
    string Name,
    string ValueName,
    string Description,
    Func<TigerCliGlobalOptionContext, string?, TigerCliValidationResult> Apply,
    string? DescriptionResourceKey);
