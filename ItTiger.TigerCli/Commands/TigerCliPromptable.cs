namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Per-option/per-argument prompting opinion, assigned through
/// <see cref="TigerCliOptionAttribute.Promptable"/> / <see cref="TigerCliArgumentAttribute.Promptable"/>.
/// Values other than <see cref="No"/> also influence prompt ordering. When the attribute property
/// is not assigned, the effective command/app prompt mode decides, in normal prompt order.
/// </summary>
public enum TigerCliPromptable
{
    /// <summary>Never prompt for this value.</summary>
    No = 0,

    /// <summary>Prompt early, before <see cref="Normal"/> values, when prompting applies.</summary>
    First,

    /// <summary>Prompt in normal prompt order when prompting applies.</summary>
    Normal,

    /// <summary>Prompt late, after <see cref="Normal"/> values, when prompting applies.</summary>
    Last
}
