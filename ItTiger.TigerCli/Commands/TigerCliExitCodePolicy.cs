namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Resolves a <see cref="TigerCliExitKind"/> to a process exit code using the layered exit model.
/// Configuration always starts from a mandatory outcome baseline (a success code and an error code)
/// and may add category, range, and kind overrides. Resolution uses the most specific configured
/// layer: kind → range → category → outcome baseline.
/// </summary>
/// <remarks>
/// The parameterless defaults (<c>Success = 0</c>, <c>Error = -1</c>) reproduce the framework's
/// built-in behavior. The direct-kind baseline is used only by
/// <c>UseTigerCliExitKindExitCodes</c>; kind, range, and category overrides retain their normal
/// precedence above it.
/// </remarks>
internal sealed class TigerCliExitCodePolicy(
    int successCode = 0,
    int errorCode = -1,
    Type? documentedExitCodeType = null,
    bool useKindValuesAsBaseline = false)
{
    private readonly int _successCode = successCode;
    private readonly int _errorCode = errorCode;
    private readonly bool _useKindValuesAsBaseline = useKindValuesAsBaseline;
    private readonly Dictionary<TigerCliExitKind, int> _kindOverrides = [];
    private readonly Dictionary<TigerCliExitKind, int> _rangeOverrides = [];
    private readonly Dictionary<TigerCliExitCategory, int> _categoryOverrides = [];

    public Type? DocumentedExitCodeType { get; } = documentedExitCodeType;

    public int Resolve(TigerCliExitKind kind)
    {
        if (_kindOverrides.TryGetValue(kind, out var kindCode))
            return kindCode;

        if (_rangeOverrides.TryGetValue(kind, out var rangeCode))
            return rangeCode;

        var category = TigerCliExitClassification.CategoryOf(kind);
        if (_categoryOverrides.TryGetValue(category, out var categoryCode))
            return categoryCode;

        if (_useKindValuesAsBaseline)
            return (int)kind;

        return TigerCliExitClassification.OutcomeOf(category) == TigerCliExitOutcome.Success
            ? _successCode
            : _errorCode;
    }

    public void SetKind(TigerCliExitKind kind, int exitCode) => _kindOverrides[kind] = exitCode;

    public void SetCategory(TigerCliExitCategory category, int exitCode) => _categoryOverrides[category] = exitCode;

    /// <summary>
    /// Maps the inclusive band of framework kinds whose declared value is in
    /// <c>[start, end]</c> to consecutive app exit codes starting at <paramref name="firstExitCode"/>.
    /// The band is bounded strictly by the explicit start and end, so kinds added to
    /// <see cref="TigerCliExitKind"/> later cannot silently enter an existing range.
    /// </summary>
    public void SetRange(TigerCliExitKind start, TigerCliExitKind end, int firstExitCode)
    {
        if (!Enum.IsDefined(start))
            throw new ArgumentOutOfRangeException(nameof(start), start, "Range start is not a defined TigerCliExitKind.");
        if (!Enum.IsDefined(end))
            throw new ArgumentOutOfRangeException(nameof(end), end, "Range end is not a defined TigerCliExitKind.");
        if ((int)start > (int)end)
            throw new ArgumentException($"Range start '{start}' ({(int)start}) must not come after end '{end}' ({(int)end}).", nameof(start));

        var startValue = (int)start;
        var endValue = (int)end;

        var kinds = Enum.GetValues<TigerCliExitKind>()
            .Where(k => (int)k >= startValue && (int)k <= endValue)
            .OrderBy(k => (int)k)
            .ToArray();

        for (var i = 0; i < kinds.Length; i++)
            _rangeOverrides[kinds[i]] = firstExitCode + i;
    }
}
