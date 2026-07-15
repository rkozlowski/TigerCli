namespace ItTiger.TigerCli.Commands;

/// <summary>
/// The specific framework reason a run ended. This is the most granular layer of the
/// layered exit model (<see cref="TigerCliExitOutcome"/> → <see cref="TigerCliExitCategory"/> →
/// <see cref="TigerCliExitKind"/>).
/// </summary>
/// <remarks>
/// The integer values are part of the contract: <c>Range(...)</c> mapping walks these kinds in
/// ascending value order, so the declared order is locked by tests
/// (<c>TigerCliExitCodeTests.ExitKind_DeclaredOrderIsLocked</c>). Add new kinds at the end with the
/// next free value; never renumber or reorder existing members, or existing range mappings shift.
/// </remarks>
public enum TigerCliExitKind
{
    /// <summary>The command completed successfully.</summary>
    Success = 0,

    /// <summary>Help was rendered instead of executing a command; rolls up to <see cref="TigerCliExitCategory.Success"/>.</summary>
    HelpShown = 1,

    /// <summary>The command ran and reported a generic failure; rolls up to <see cref="TigerCliExitCategory.Execution"/>.</summary>
    GenericFail = 2,

    /// <summary>The command-line input could not be parsed or contained invalid values; rolls up to <see cref="TigerCliExitCategory.Usage"/>.</summary>
    InvalidArguments = 3,

    /// <summary>A required argument or option was missing and could not be prompted for; rolls up to <see cref="TigerCliExitCategory.Usage"/>.</summary>
    MissingRequiredArgument = 4,

    /// <summary>Framework validation or <see cref="TigerCliSettings.Validate"/> rejected the well-formed input; rolls up to <see cref="TigerCliExitCategory.Validation"/>.</summary>
    ValidationError = 5,

    /// <summary>Interactive prompting was needed but the effective interaction mode does not allow it; rolls up to <see cref="TigerCliExitCategory.Usage"/>.</summary>
    InteractiveNotAllowed = 6,

    /// <summary>No command was specified or matched, and no interactive command selection was available; rolls up to <see cref="TigerCliExitCategory.Usage"/>.</summary>
    NoCommand = 7,

    /// <summary>An unhandled exception escaped the pipeline or the handler; rolls up to <see cref="TigerCliExitCategory.Unexpected"/>.</summary>
    UnhandledException = 8,

    /// <summary>
    /// The user cancelled an interactive prompt (Escape / timeout / token / system cancel) after a
    /// command was selected. This is a normal flow, not a validation or usage error; it rolls up to
    /// <see cref="TigerCliExitCategory.Cancelled"/>. Appended at the end to preserve the locked
    /// declared order of the earlier kinds.
    /// </summary>
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
    ProviderError = 10
}
