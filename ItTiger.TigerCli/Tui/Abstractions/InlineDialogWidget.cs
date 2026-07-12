using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tui.Abstractions;

/// <summary>
/// Describes one top-level widget/area an inline control exposes to its hosting <c>InlineDialog</c>.
/// A control may expose a single widget (the common case, preserving the legacy single-content
/// behavior) or several, each placed into a fixed <see cref="InlineDialogArea"/>.
/// </summary>
/// <remarks>
/// This is layout/placement metadata only. Control-level concerns (content label, hint, payload,
/// confirmability) stay on the control; this descriptor carries the per-widget geometry,
/// decoration, scroll behavior, and rendered grid the dialog needs to place the widget.
/// </remarks>
public sealed class InlineDialogWidget
{
    /// <summary>The fixed dialog area this widget is placed into.</summary>
    public InlineDialogArea Area { get; init; } = InlineDialogArea.InFrame;

    /// <summary>The widget's rendered content subgrid.</summary>
    public required CliGrid Grid { get; init; }

    /// <summary>
    /// Whether this widget is the focused/active one. The dialog drives the active scrollable cell,
    /// cursor, and scroll overlays from the focused widget so only it shows scroll indicators.
    /// </summary>
    public bool IsFocused { get; init; }

    /// <summary>Decoration flags (scrollbar / horizontal indicators) the dialog renders for this widget when focused.</summary>
    public CliControlDecoration Decoration { get; init; }

    /// <summary>Scroll mode applied to the widget's host subgrid cell.</summary>
    public CliScrollMode ScrollMode { get; init; }

    /// <summary>Scrollbar/indicator thumb position source for the widget's host subgrid cell.</summary>
    public CliScrollThumbMode ThumbMode { get; init; } = CliScrollThumbMode.Offset;

    /// <summary>Optional style applied to the widget's host content cell.</summary>
    public CliCellStyle? ContentStyle { get; init; }
}
