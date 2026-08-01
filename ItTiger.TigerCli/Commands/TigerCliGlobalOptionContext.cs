using System.Globalization;
using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Describes the active app run when a contributed global option is applied. A contribution can
/// use this context and its parsed value to update contribution-owned options or services before
/// command settings are bound.
/// </summary>
public sealed class TigerCliGlobalOptionContext
{
    /// <summary>Gets the resolved interaction mode for the command run.</summary>
    public TigerCliInteractionMode InteractionMode { get; }

    /// <summary>Gets the resolved UI culture for the command run.</summary>
    public CultureInfo Culture { get; }

    internal TigerCliGlobalOptionContext(
        TigerCliInteractionMode interactionMode,
        CultureInfo culture)
    {
        InteractionMode = interactionMode;
        Culture = culture;
    }
}
