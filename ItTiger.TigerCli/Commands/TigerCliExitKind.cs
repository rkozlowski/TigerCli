using System.ComponentModel;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// A portable semantic command outcome or specific framework reason a run ended. Small apps may
/// use these declared values directly as process exit codes; reusable command libraries may return
/// them for the consuming app to map through its exit-code policy. This is the most granular layer
/// of the layered exit model (<see cref="TigerCliExitOutcome"/> →
/// <see cref="TigerCliExitCategory"/> → <see cref="TigerCliExitKind"/>).
/// </summary>
/// <remarks>
/// The integer values are part of the contract: <c>Range(...)</c> mapping walks these kinds in
/// ascending value order, so the declared order is locked by tests
/// (<c>TigerCliExitCodeTests.ExitKind_DeclaredOrderIsLocked</c>). Add new kinds at the end with the
/// next free value; never renumber or reorder existing members, or existing range mappings shift.
/// </remarks>
[Description("TigerCli command outcomes")]
public enum TigerCliExitKind
{
    /// <summary>The command completed successfully.</summary>
    [Description("The command completed successfully.")]
    Success = 0,

    /// <summary>Help was rendered instead of executing a command; rolls up to <see cref="TigerCliExitCategory.Success"/>.</summary>
    [Description("Help was shown instead of executing a command.")]
    HelpShown = 1,

    /// <summary>The command ran and reported a generic failure; rolls up to <see cref="TigerCliExitCategory.Execution"/>.</summary>
    [Description("The command reported a generic failure.")]
    GenericFail = 2,

    /// <summary>The command-line input could not be parsed or contained invalid values; rolls up to <see cref="TigerCliExitCategory.Usage"/>.</summary>
    [Description("The command-line input was invalid.")]
    InvalidArguments = 3,

    /// <summary>A required argument or option was missing and could not be prompted for; rolls up to <see cref="TigerCliExitCategory.Usage"/>.</summary>
    [Description("A required argument or option was missing.")]
    MissingRequiredArgument = 4,

    /// <summary>Framework validation or <see cref="TigerCliSettings.Validate"/> rejected the well-formed input; rolls up to <see cref="TigerCliExitCategory.Validation"/>.</summary>
    [Description("The input failed validation.")]
    ValidationError = 5,

    /// <summary>Interactive prompting was needed but the effective interaction mode does not allow it; rolls up to <see cref="TigerCliExitCategory.Usage"/>.</summary>
    [Description("The command required interaction that was not allowed.")]
    InteractiveNotAllowed = 6,

    /// <summary>No command was specified or matched, and no interactive command selection was available; rolls up to <see cref="TigerCliExitCategory.Usage"/>.</summary>
    [Description("No command was specified or matched.")]
    NoCommand = 7,

    /// <summary>An unhandled exception escaped the pipeline or the handler; rolls up to <see cref="TigerCliExitCategory.Unexpected"/>.</summary>
    [Description("An unhandled exception escaped the command pipeline.")]
    UnhandledException = 8,

    /// <summary>
    /// The user cancelled an interactive prompt (Escape / timeout / token / system cancel) after a
    /// command was selected. This is a normal flow, not a validation or usage error; it rolls up to
    /// <see cref="TigerCliExitCategory.Cancelled"/>. Appended at the end to preserve the locked
    /// declared order of the earlier kinds.
    /// </summary>
    [Description("The operation was cancelled.")]
    Cancelled = 9,

    /// <summary>
    /// A dynamic value provider reported a deliberate failure
    /// (<see cref="ItTiger.TigerCli.Exceptions.TigerCliProviderException"/>): its backing source was
    /// reachable but could not produce choices, so the run could neither prompt nor validate. This
    /// is not the user's fault (not <see cref="TigerCliExitCategory.Usage"/> or
    /// <see cref="TigerCliExitCategory.Validation"/>) and not an unexpected bug; it rolls up to
    /// <see cref="TigerCliExitCategory.Execution"/>. Appended at the end to preserve the locked
    /// declared order of the earlier kinds.
    /// </summary>
    [Description("A dynamic value provider could not produce choices.")]
    ProviderError = 10,

    /// <summary>
    /// The requested application resource or entity was not found; rolls up to
    /// <see cref="TigerCliExitCategory.Execution"/>.
    /// </summary>
    [Description("The requested resource or entity was not found.")]
    NotFound = 11,

    /// <summary>
    /// The application resource or entity to be created already exists; rolls up to
    /// <see cref="TigerCliExitCategory.Execution"/>.
    /// </summary>
    [Description("The resource or entity to be created already exists.")]
    AlreadyExists = 12,

    /// <summary>
    /// The requested operation conflicts with the current application state; rolls up to
    /// <see cref="TigerCliExitCategory.Execution"/>.
    /// </summary>
    [Description("The operation conflicts with the current application state.")]
    Conflict = 13,

    /// <summary>
    /// The requested operation is not supported by the command or current application context;
    /// rolls up to <see cref="TigerCliExitCategory.Execution"/>.
    /// </summary>
    [Description("The requested operation is not supported.")]
    NotSupported = 14
}
