namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Fixed roll-up of the layered exit model: each <see cref="TigerCliExitKind"/> belongs to exactly
/// one <see cref="TigerCliExitCategory"/>, and each category to exactly one
/// <see cref="TigerCliExitOutcome"/>. This classification is framework-owned and not configurable;
/// apps override the resulting exit code at the kind, range, category, or outcome-baseline layer.
/// </summary>
internal static class TigerCliExitClassification
{
    public static TigerCliExitCategory CategoryOf(TigerCliExitKind kind) => kind switch
    {
        TigerCliExitKind.Success or
        TigerCliExitKind.HelpShown => TigerCliExitCategory.Success,

        TigerCliExitKind.InvalidArguments or
        TigerCliExitKind.MissingRequiredArgument or
        TigerCliExitKind.InteractiveNotAllowed or
        TigerCliExitKind.NoCommand => TigerCliExitCategory.Usage,

        TigerCliExitKind.ValidationError => TigerCliExitCategory.Validation,

        TigerCliExitKind.Cancelled => TigerCliExitCategory.Cancelled,

        TigerCliExitKind.GenericFail or
        TigerCliExitKind.ProviderError or
        TigerCliExitKind.NotFound or
        TigerCliExitKind.AlreadyExists or
        TigerCliExitKind.Conflict or
        TigerCliExitKind.NotSupported => TigerCliExitCategory.Execution,

        TigerCliExitKind.UnhandledException => TigerCliExitCategory.Unexpected,

        // Unknown future kinds are treated as generic execution failures until classified.
        _ => TigerCliExitCategory.Execution
    };

    public static TigerCliExitOutcome OutcomeOf(TigerCliExitCategory category) =>
        category == TigerCliExitCategory.Success
            ? TigerCliExitOutcome.Success
            : TigerCliExitOutcome.Error;

    public static TigerCliExitOutcome OutcomeOf(TigerCliExitKind kind) => OutcomeOf(CategoryOf(kind));
}
