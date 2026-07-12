using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tui.Abstractions;

/// <summary>
/// A reusable interactive building block used inside composite inline controls. A widget renders
/// itself into its own subgrid and handles keys, but — unlike <see cref="InlineControlBase"/> — it
/// is not necessarily dialog-hostable on its own. It is a part, not a whole.
/// </summary>
/// <remarks>
/// Cells remain render/layout only: widgets handle keys, cells do not. This is the minimal surface
/// needed for button widgets and future composition; richer responsibilities (cursor mode, scroll
/// mode, per-widget overlays) are added when a consumer needs them.
/// </remarks>
public abstract class InlineWidget : CliRenderableComponent
{
    /// <summary>Creates a widget hosted by the supplied shell.</summary>
    /// <param name="shell">The shell that supplies terminal, theme, culture, and viewport services.</param>
    protected InlineWidget(ICliAppShell shell)
    {
        Shell = shell;
    }

    /// <summary>The shell hosting this widget.</summary>
    public ICliAppShell Shell { get; }

    /// <summary>Whether the widget can receive focus. Non-focusable widgets are skipped by focus traversal.</summary>
    public virtual bool Focusable => true;

    /// <summary>Whether the widget currently has focus. Drives focus-dependent rendering (e.g. button markers).</summary>
    public bool HasFocus { get; set; }

    /// <summary>Optional focus-aware hint a composite control may surface to the dialog.</summary>
    public virtual string? Hint => null;

    /// <summary>Handles a key. Returns whether it was consumed and any requested dialog result.</summary>
    public abstract InlineKeyResult HandleKey(KeyEvent key);
}
