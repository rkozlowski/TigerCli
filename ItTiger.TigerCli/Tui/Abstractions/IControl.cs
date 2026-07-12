
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tui.Abstractions;

/// <summary>
/// Basic modal-control contract used by dialog hosts that dispatch keyboard input to renderable UI.
/// </summary>
public interface IControl
{
    /// <summary>
    /// Handles a key event. Returns <c>true</c> when the control consumed the key and the host should
    /// not apply fallback handling.
    /// </summary>
    bool HandleKey(KeyEvent key);     // return true if handled
}
