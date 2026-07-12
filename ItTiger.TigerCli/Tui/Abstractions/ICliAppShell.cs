using System.Globalization;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Tui.Abstractions;


/// <summary>
/// Host abstraction for running TigerCli modal dialogs against a terminal, theme, viewport, and
/// interaction policy.
/// </summary>
public interface ICliAppShell
{
    /// <summary>The theme used by controls hosted in this shell.</summary>
    ITheme Theme { get; }

    /// <summary>True when the shell owns the full terminal window rather than an inline region.</summary>
    bool IsFullWindow { get; }

    /// <summary>The current viewport available for modal rendering.</summary>
    Size Viewport { get; }

    /// <summary>The interaction mode that controls whether prompts may display UI.</summary>
    TigerCliInteractionMode InteractionMode { get; }

    /// <summary>
    /// UI culture used to resolve framework-owned localized strings (Yes/No,
    /// MultiSelect hint, empty-state labels). Defaults to en-US for shells
    /// that do not override it.
    /// </summary>
    CultureInfo Culture => CultureInfo.GetCultureInfo("en-US");

    /// <summary>Runs a dialog until it produces a result or the cancellation token is cancelled.</summary>
    Task<DialogResult> RunModalAsync(ICliDialog dialog, CancellationToken ct = default);

    /// <summary>
    /// Runs a dialog until it produces a result, the optional timeout expires, or the cancellation
    /// token is cancelled.
    /// </summary>
    Task<DialogResult> RunModalAsync(ICliDialog dialog, TimeSpan? timeout = default, CancellationToken ct = default);
    //void Invalidate();
}
