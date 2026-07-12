using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Selection;
using ItTiger.TigerCli.Tui.Windowing;

namespace ItTiger.TigerCli.Tui;

public static partial class TigerTui
{
    /// <summary>
    /// Presents a single-selection picker whose rows are structured, aligned multi-column data (see
    /// <see cref="SelectColumn"/>/<see cref="SelectRow"/>/<see cref="SelectCell"/>) and returns the chosen
    /// row index, or <c>null</c> on cancel/timeout. Runs on the default semi-interactive shell.
    /// </summary>
    public static Task<int?> MultiColumnSelectIndexAsync(
        string title,
        IReadOnlyList<SelectColumn> columns,
        IReadOnlyList<SelectRow> rows,
        int? preselectIndex = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default)
    {
        return MultiColumnSelectIndexAsync(
            InlineShell.Instance, title, columns, rows, preselectIndex, timeout, ct);
    }

    /// <summary>Shell-injected variant of <see cref="MultiColumnSelectIndexAsync(string, IReadOnlyList{SelectColumn}, IReadOnlyList{SelectRow}, int?, TimeSpan?, CancellationToken)"/>.</summary>
    public static async Task<int?> MultiColumnSelectIndexAsync(
        ICliAppShell shell,
        string title,
        IReadOnlyList<SelectColumn> columns,
        IReadOnlyList<SelectRow> rows,
        int? preselectIndex = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default)
    {
        var result = await MultiColumnSelectIndexResultAsync(
            shell, title, columns, rows, preselectIndex, timeout, ct).ConfigureAwait(false);
        ThrowIfSystemCancel(result.ResultKind);
        return result.IsOk ? result.Value : null;
    }

    /// <summary>
    /// Rich variant of <see cref="MultiColumnSelectIndexAsync(string, IReadOnlyList{SelectColumn}, IReadOnlyList{SelectRow}, int?, TimeSpan?, CancellationToken)"/>
    /// that preserves the exact <see cref="DialogResultKind"/> instead of collapsing cancel/timeout to
    /// <c>null</c>. Framework internals use this so they can branch on the kind (e.g. Escape vs. timeout).
    /// </summary>
    public static Task<TigerTuiResult<int>> MultiColumnSelectIndexResultAsync(
        string title,
        IReadOnlyList<SelectColumn> columns,
        IReadOnlyList<SelectRow> rows,
        int? preselectIndex = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default)
    {
        return MultiColumnSelectIndexResultAsync(
            InlineShell.Instance, title, columns, rows, preselectIndex, timeout, ct);
    }

    /// <summary>Shell-injected variant of <see cref="MultiColumnSelectIndexResultAsync(string, IReadOnlyList{SelectColumn}, IReadOnlyList{SelectRow}, int?, TimeSpan?, CancellationToken)"/>.</summary>
    public static async Task<TigerTuiResult<int>> MultiColumnSelectIndexResultAsync(
        ICliAppShell shell,
        string title,
        IReadOnlyList<SelectColumn> columns,
        IReadOnlyList<SelectRow> rows,
        int? preselectIndex = null,
        TimeSpan? timeout = default,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        var select = new InlineMultiColumnSelect(shell, columns, rows, preselectIndex);
        var dialog = new InlineDialog(shell, title, select);
        var dr = await shell.RunModalAsync(dialog, timeout, ct).ConfigureAwait(false);
        return dr.Kind == DialogResultKind.Ok && dr.Payload is int ix
            ? TigerTuiResult<int>.Ok(ix)
            : TigerTuiResult<int>.FromKind(dr.Kind);
    }
}
