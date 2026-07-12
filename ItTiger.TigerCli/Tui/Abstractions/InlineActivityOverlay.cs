using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Tui.Abstractions;

/// <summary>
/// Describes a time-varying overlay an inline control exposes to its hosting <c>InlineDialog</c>. The
/// dialog adds it to the grid once (through the normal <c>CliGrid.AddOverlay</c> system) and never
/// rebuilds the grid when the content changes — the overlay's renderer reads the live
/// <see cref="Ticker"/> on every render, so animation rides on the cached grid. Placement is given
/// relative to a dialog <see cref="Area"/>, matching how widgets are placed, so controls never depend
/// on absolute grid coordinates. The overlay is a horizontal strip on the area's row, running from
/// <see cref="ColumnOffset"/> to the end of the area.
/// </summary>
/// <remarks>
/// This is placement/animation metadata only. The dialog derives the actual <c>CliOverlay</c> renderer
/// from the ticker: while the ticker is active it writes <see cref="TuiTicker.CurrentContent"/>,
/// optionally passed through <see cref="ContentFormatter"/> for overlay-specific presentation; while
/// inactive it renders nothing, leaving the underlying cells intact — the same visibility model as the
/// scroll overlays. <see cref="MaxLength"/> is the control's content contract: formatted content may be
/// shorter, but content wider than <see cref="MaxLength"/> is a usage error the dialog fails loudly on
/// (it never truncates or silently hides declared-oversize content). Content within the contract that
/// still does not fit the measured strip (a very narrow dialog) renders nothing for that frame.
/// </remarks>
public sealed class InlineActivityOverlay
{
    // Shared content cap for the framework's top-frame spinner overlays ("[frame]"): comfortably
    // holds every predefined frame set (widest bracketed frame today is Snake's 4 cells) while still
    // catching runaway content loudly instead of letting it crawl across the frame.
    internal const int SpinnerMaxLength = 10;

    /// <summary>The dialog area whose row the overlay is anchored to.</summary>
    public required InlineDialogArea Area { get; init; }

    /// <summary>Column offset from the area's start column at which the overlay begins.</summary>
    public int ColumnOffset { get; init; }

    /// <summary>
    /// Maximum number of screen cells the formatted content may occupy. Rendered content may be
    /// shorter; producing wider content is a usage error (the hosting dialog throws at render time).
    /// </summary>
    public required int MaxLength { get; init; }

    /// <summary>The time-driven content source the overlay renders.</summary>
    public required TuiTicker Ticker { get; init; }

    /// <summary>Optional overlay-only presentation applied to the ticker's raw content.</summary>
    public Func<string, string>? ContentFormatter { get; init; }

    /// <summary>Uniform character style applied to the overlay's content.</summary>
    public CliCharStyle Style { get; init; }
}
