namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Opt-in policy for whether a node (app, command group, or command) participates in the
/// discoverable command menu. The effective decision for a command is resolved by walking the
/// chain app → group → command: a command is eligible when the chain contains at least one
/// <see cref="Enabled"/> and no <see cref="Disabled"/>. <see cref="Inherit"/> contributes no
/// local opinion.
/// </summary>
/// <remarks>
/// The command menu only chooses a command. The selected command still runs through the normal
/// parse, bind, prompt, validate, and execute pipeline.
/// </remarks>
public enum CommandMenuMode
{
    /// <summary>No local opinion; defer to the rest of the chain.</summary>
    Inherit,

    /// <summary>Hard-off for this node and its subtree, overriding any <see cref="Enabled"/> above.</summary>
    Disabled,

    /// <summary>Opt this node (and its subtree, unless overridden by <see cref="Disabled"/>) into the menu.</summary>
    Enabled
}
