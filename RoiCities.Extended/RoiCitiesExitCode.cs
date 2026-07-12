using ItTiger.Core;

namespace RoiCities.Extended;

/// <summary>
/// The application-wide exit-code contract. Commands return these values, framework outcomes are
/// mapped to them on the app builder, and --help-errors documents them from the [TigerText]
/// labels and descriptions. See docs/guides/exit-codes.md.
/// </summary>
[TigerText("roi-cities exit codes")]
public enum RoiCitiesExitCode
{
    [TigerText("OK", Description = "Operation completed successfully.")]
    Ok = 0,

    [TigerText("City not found", Description = "The requested city is not in the city store.")]
    CityNotFound = 1,

    [TigerText("Invalid arguments", Description = "Invalid command-line arguments.")]
    InvalidArguments = 2,

    [TigerText("Missing required argument", Description = "A required argument was not provided.")]
    MissingRequiredArgument = 3,

    [TigerText("Validation error", Description = "A supplied value failed validation.")]
    ValidationError = 4,

    [TigerText("Interactive input not allowed", Description = "Interaction was required but not allowed, e.g. under --non-interactive.")]
    InteractiveNotAllowed = 5,

    [TigerText("Cancelled", Description = "An interactive prompt was cancelled.")]
    Cancelled = 6,

    [TigerText("Internal error", Description = "Unexpected internal failure.")]
    InternalError = 70,
}
