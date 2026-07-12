using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Tui.Windowing;

/// <summary>
/// Per-run holder for the effective <see cref="TigerCliInteractionMode"/> published by the running
/// <c>TigerCliApp</c>. Kept on a dedicated type (not on <see cref="InlineShell"/>) so publishing the
/// value never touches an <see cref="InlineShell"/> static member, which would eagerly initialize the
/// console-backed singleton. Backed by an <see cref="AsyncLocal{T}"/> so the value flows only into the
/// current run's calls and stays isolated across parallel runs/tests.
/// </summary>
/// <remarks>
/// The <see cref="InlineShell"/> singleton defers its reported <see cref="Abstractions.ICliAppShell"/> interaction
/// mode to this ambient value (falling back to semi-interactive when unset), so a no-shell
/// <c>TigerTui</c> call inside a command handler observes the run's real mode. Explicitly constructed
/// shells own their mode and never consult this scope. This mirrors <see cref="SystemCancellationScope"/>.
/// </remarks>
internal static class InteractionModeScope
{
    private static readonly AsyncLocal<TigerCliInteractionMode?> _current = new();

    /// <summary>The ambient interaction mode for the current async flow, or <c>null</c> when unset.</summary>
    internal static TigerCliInteractionMode? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
