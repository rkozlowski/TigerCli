namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Controls which missing values TigerCli may prompt for when the effective interaction mode allows
/// semi-interactive prompting.
/// </summary>
/// <remarks>
/// This mode is resolved from app, group, and command configuration, then combined with each
/// argument/option's promptability metadata. It has no effect when interaction is non-interactive.
/// Prompted values are bound before required-value and framework validation run.
/// </remarks>
public enum TigerCliPromptMode
{
    /// <summary>Do not prompt for values unless a property explicitly opts in through promptability metadata.</summary>
    No,

    /// <summary>Prompt for missing required values, subject to each property opting out or changing prompt order.</summary>
    RequiredOnly,

    /// <summary>Prompt for missing promptable values, including optional values, unless a property opts out.</summary>
    Yes
}
