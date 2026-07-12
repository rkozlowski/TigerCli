namespace ItTiger.TigerCli.Tui.Activity;

/// <summary>
/// Immutable row definition. A row is either <em>static</em> (unnamed, no values, fixed labels/text) or
/// <em>dynamic</em> (named, with a fixed-length value array updated at runtime). The cells reference the
/// row's values by index; the value count is fixed at build time.
/// </summary>
public sealed class ActivityRowSpec
{
    internal ActivityRowSpec(string? name, IReadOnlyList<ActivityCellSpec> cells, int valueCount, IReadOnlyList<object?> initialValues)
    {
        Name = name;
        Cells = cells;
        ValueCount = valueCount;
        InitialValues = initialValues;
    }

    /// <summary>The row's unique name, or <c>null</c> for a static (unnamed) row.</summary>
    public string? Name { get; }

    /// <summary>True when the row is dynamic (named, with runtime-updatable values).</summary>
    public bool IsDynamic => Name is not null;

    /// <summary>The cells placed in this row.</summary>
    public IReadOnlyList<ActivityCellSpec> Cells { get; }

    /// <summary>The fixed number of values this row exposes.</summary>
    public int ValueCount { get; }

    /// <summary>The initial values (length == <see cref="ValueCount"/>).</summary>
    public IReadOnlyList<object?> InitialValues { get; }
}
