namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Controls whether a <see cref="TigerCliFileSaveAttribute"/> option may resolve to an existing file.
/// </summary>
public enum TigerCliFileOverwrite
{
    /// <summary>Rejects an existing file unless the configured overwrite option is true.</summary>
    Deny,

    /// <summary>Asks in semi-interactive mode and rejects in non-interactive mode unless the configured overwrite option is true.</summary>
    Prompt,

    /// <summary>Accepts an existing file without asking.</summary>
    Allow
}
