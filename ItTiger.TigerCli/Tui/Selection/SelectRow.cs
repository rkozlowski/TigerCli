namespace ItTiger.TigerCli.Tui.Selection;

/// <summary>
/// Immutable row for a multi-column select: an ordered set of <see cref="Cells"/> (one per column, though
/// a row may supply fewer — missing trailing cells render blank) and an optional <see cref="IsDisabled"/>
/// flag. A disabled row renders muted, is skipped by keyboard navigation, and can never be the confirmed
/// selection. The row carries no key/value: callers map the confirmed row index back to their own data,
/// matching the index-based select APIs.
/// </summary>
public sealed class SelectRow
{
    /// <summary>Creates a row from its cells.</summary>
    public SelectRow(IReadOnlyList<SelectCell> cells, bool isDisabled = false)
    {
        ArgumentNullException.ThrowIfNull(cells);
        Cells = cells;
        IsDisabled = isDisabled;
    }

    /// <summary>Convenience overload building a row from cell params.</summary>
    public SelectRow(params SelectCell[] cells)
        : this((IReadOnlyList<SelectCell>)cells)
    {
    }

    /// <summary>The row's cells, left to right.</summary>
    public IReadOnlyList<SelectCell> Cells { get; }

    /// <summary>When true the row renders muted, cannot be landed on, and is never confirmable.</summary>
    public bool IsDisabled { get; }
}
