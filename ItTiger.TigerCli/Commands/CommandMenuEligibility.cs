using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Resolves whether a command is eligible for the command menu from its app → group → command
/// <see cref="CommandMenuMode"/> chain. The rule is intentionally a pure boolean: a command is
/// eligible when at least one level in the chain is <see cref="CommandMenuMode.Enabled"/> and no
/// level is <see cref="CommandMenuMode.Disabled"/>. <see cref="CommandMenuMode.Inherit"/> levels
/// contribute nothing, and a single <see cref="CommandMenuMode.Disabled"/> anywhere wins.
/// </summary>
internal static class CommandMenuEligibility
{
    public static bool IsEligible(params CommandMenuMode[] chain)
    {
        var anyEnabled = false;
        foreach (var mode in chain)
        {
            if (mode == CommandMenuMode.Disabled)
                return false;
            if (mode == CommandMenuMode.Enabled)
                anyEnabled = true;
        }

        return anyEnabled;
    }
}
