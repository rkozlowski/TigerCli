namespace ItTiger.TigerCli.Commands;

/// <summary>
/// The coarsest layer of the layered exit model: whether a run ultimately succeeded or failed.
/// Every <see cref="TigerCliExitCategory"/> rolls up into exactly one outcome, and every
/// <see cref="TigerCliExitKind"/> rolls up into exactly one category.
/// </summary>
public enum TigerCliExitOutcome
{
    /// <summary>The run succeeded (the <see cref="TigerCliExitCategory.Success"/> category).</summary>
    Success,

    /// <summary>The run failed; every non-success category rolls up here by default, including cancellation.</summary>
    Error
}
