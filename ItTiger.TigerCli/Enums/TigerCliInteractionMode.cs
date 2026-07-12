namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Controls whether a command run may use interactive TigerCli UI while resolving command input.
/// </summary>
public enum TigerCliInteractionMode
{
    /// <summary>
    /// Disables interactive prompts and modal input. The command still parses, validates, and executes
    /// when all required input is available without interaction.
    /// </summary>
    NonInteractive,

    /// <summary>
    /// Enables guided single-command prompting for missing promptable values before validation and
    /// handler execution.
    /// </summary>
    SemiInteractive,

    /// <summary>
    /// Reserved for persistent full-application interactive sessions. The built-in
    /// <c>--non-interactive</c> override cannot be combined with this mode.
    /// </summary>
    FullInteractive
}
