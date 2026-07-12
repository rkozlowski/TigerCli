namespace ItTiger.TigerCli.Tui.Activity;

/// <summary>
/// Mutable runtime values for an activity's dynamic rows. Updates arrive from the background operation
/// thread and are applied under a lock; the modal-loop thread drains a coalesced snapshot in
/// <c>AdvanceState</c>. Each named row keeps a fixed-length value array (length == the row's declared
/// value count); validation rejects unknown rows, bad indices, and wrong value counts on the caller's
/// thread before any mutation.
/// </summary>
/// <remarks>
/// This mirrors the folder-picker async-load discipline (<c>InlineFolderSelect</c>): the operation
/// thread only mutates guarded fields and never touches widgets or renders. Updates coalesce by row —
/// the latest value per slot wins — so a fast operation cannot flood the loop.
/// </remarks>
internal sealed class ActivityState
{
    private readonly object _sync = new();
    private readonly Dictionary<string, object?[]> _values = new(StringComparer.Ordinal);
    private bool _dirty;
    private bool _closed;

    public ActivityState(ActivityDialogSpec spec)
    {
        foreach (var row in spec.Rows)
        {
            if (row.Name is null)
                continue;

            var array = new object?[row.ValueCount];
            for (int i = 0; i < row.ValueCount && i < row.InitialValues.Count; i++)
                array[i] = row.InitialValues[i];
            _values[row.Name] = array;
        }
    }

    /// <summary>Sets a single value in a dynamic row. Validates row/index on the calling thread.</summary>
    public void SetValue(string row, int index, object? value)
    {
        ArgumentNullException.ThrowIfNull(row);
        var array = Resolve(row);
        if (index < 0 || index >= array.Length)
            throw new ArgumentOutOfRangeException(
                nameof(index), $"Value index {index} is out of range for row '{row}' ({array.Length} value(s)).");

        lock (_sync)
        {
            if (_closed)
                return;
            array[index] = value;
            _dirty = true;
        }
    }

    /// <summary>Replaces all values of a dynamic row. The count must match the row's declared length.</summary>
    public void SetValues(string row, params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(row);
        values ??= Array.Empty<object?>();
        var array = Resolve(row);
        if (values.Length != array.Length)
            throw new ArgumentException(
                $"Row '{row}' expects {array.Length} value(s) but {values.Length} were supplied.", nameof(values));

        lock (_sync)
        {
            if (_closed)
                return;
            Array.Copy(values, array, array.Length);
            _dirty = true;
        }
    }

    /// <summary>
    /// If values changed since the last drain, copies a fresh per-row snapshot into
    /// <paramref name="snapshot"/> and returns <c>true</c>. Called on the modal-loop thread.
    /// </summary>
    public bool TryDrainSnapshot(out IReadOnlyDictionary<string, object?[]> snapshot)
    {
        lock (_sync)
        {
            if (!_dirty)
            {
                snapshot = EmptySnapshot;
                return false;
            }

            var copy = new Dictionary<string, object?[]>(_values.Count, StringComparer.Ordinal);
            foreach (var kvp in _values)
                copy[kvp.Key] = (object?[])kvp.Value.Clone();

            _dirty = false;
            snapshot = copy;
            return true;
        }
    }

    /// <summary>A one-shot snapshot of all rows, regardless of the dirty flag (used to seed the first render).</summary>
    public IReadOnlyDictionary<string, object?[]> Snapshot()
    {
        lock (_sync)
        {
            var copy = new Dictionary<string, object?[]>(_values.Count, StringComparer.Ordinal);
            foreach (var kvp in _values)
                copy[kvp.Key] = (object?[])kvp.Value.Clone();
            return copy;
        }
    }

    /// <summary>Stops accepting updates; later <c>SetValue</c>/<c>SetValues</c> calls are ignored.</summary>
    public void Close()
    {
        lock (_sync)
            _closed = true;
    }

    private object?[] Resolve(string row)
    {
        if (!_values.TryGetValue(row, out var array))
            throw new ArgumentException($"No dynamic row named '{row}'.", nameof(row));
        return array;
    }

    private static readonly IReadOnlyDictionary<string, object?[]> EmptySnapshot =
        new Dictionary<string, object?[]>(0, StringComparer.Ordinal);
}
