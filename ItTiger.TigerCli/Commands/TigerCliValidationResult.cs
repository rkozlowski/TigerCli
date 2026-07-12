namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Result of <see cref="TigerCliSettings.Validate"/>: either success or a failure carrying a
/// user-facing error message. Created only through <see cref="Success"/> and
/// <see cref="Error(string)"/>.
/// </summary>
public sealed class TigerCliValidationResult
{
    /// <summary>Whether validation passed. When <c>false</c>, <see cref="ErrorMessage"/> is set.</summary>
    public bool IsValid { get; }

    /// <summary>
    /// The user-facing failure message; <c>null</c> on success. The framework renders it as
    /// literal text (markup-escaped) inside its error line.
    /// </summary>
    public string? ErrorMessage { get; }

    private TigerCliValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>Creates a passing result.</summary>
    public static TigerCliValidationResult Success() => new(true, null);

    /// <summary>
    /// Creates a failing result with the given user-facing message. The run ends with the
    /// <see cref="TigerCliExitKind.ValidationError"/> exit mapping.
    /// </summary>
    public static TigerCliValidationResult Error(string message) => new(false, message);
}
