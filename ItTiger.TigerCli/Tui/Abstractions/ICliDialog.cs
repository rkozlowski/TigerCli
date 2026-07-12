

using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tui.Abstractions;

/// <summary>
/// Renderable modal dialog contract for controls that can be hosted by an <see cref="ICliAppShell"/>.
/// </summary>
public interface ICliDialog : IControl, ICliRenderable
{
    /// <summary>The current dialog result, or <see cref="DialogResultKind.NoResult"/> while still active.</summary>
    DialogResultKind Result { get; }

    /// <summary>Optional value produced by the dialog when it completes.</summary>
    object? Payload { get; }
}

