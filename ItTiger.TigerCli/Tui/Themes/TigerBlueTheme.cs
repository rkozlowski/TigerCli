using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Tui.Themes;

/// <summary>
/// Blue-accented framework theme: cyan accent, dark-blue dialog background, white table headers.
/// (This is the original TigerCli dark palette, preserved as its own named theme.)
/// </summary>
public sealed class TigerBlueTheme : ThemeBase
{
    /// <inheritdoc/>
    public override string Name => "tiger-blue";

    /// <inheritdoc/>
    protected override CliCellStyle Text =>
        new CliCellStyle(new CliCharStyle(CliColor.Gray));

    /// <inheritdoc/>
    protected override CliCellStyle MutedText =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGray));

    /// <inheritdoc/>
    protected override CliCellStyle Accent =>
        new CliCellStyle(new CliCharStyle(CliColor.Cyan));

    /// <inheritdoc/>
    protected override CliCellStyle Frame =>
        new CliCellStyle(new CliCharStyle(CliColor.Gray));

    /// <inheritdoc/>
    protected override CliCellStyle Selected =>
        new CliCellStyle(new CliCharStyle(CliColor.Black, CliColor.Green));

    /// <inheritdoc/>
    protected override CliCellStyle Background =>
        new CliCellStyle(new CliCharStyle(null, CliColor.Black));

    /// <inheritdoc/>
    protected override CliCellStyle? DialogTitle =>
        new CliCellStyle(new CliCharStyle(CliColor.White));

    // PanelSurface is the elevated surface. DialogSurface is not overridden, so it falls back to
    // PanelSurface — dialogs/controls and table styles that ask for Panel share the navy surface.
    /// <inheritdoc/>
    protected override CliCellStyle? PanelSurface =>
        new CliCellStyle(new CliCharStyle(null, CliColor.Navy));

    /// <inheritdoc/>
    protected override CliCellStyle? ScrollIndicator =>
        new CliCellStyle(new CliCharStyle(CliColor.White));

    /// <inheritdoc/>
    protected override CliCellStyle? Status =>
        new CliCellStyle(new CliCharStyle(CliColor.Gray, CliColor.Gray31));

    // Inactive (unfocused) selected list item: muted dark-gray highlight instead of the active green.
    /// <inheritdoc/>
    protected override CliCellStyle? InactiveSelectedListItem =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkGray));

    /// <inheritdoc/>
    protected override CliCellStyle? TextInput =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.Gray31));

    // Unfocused input: muted foreground on the dark-blue dialog surface (both defined).
    /// <inheritdoc/>
    protected override CliCellStyle? InactiveTextInput =>
        new CliCellStyle(new CliCharStyle(CliColor.Gray, CliColor.Gray31));

    // Selected button while its group is unfocused: muted dark-gray surface (not the active green).
    /// <inheritdoc/>
    protected override CliCellStyle? ButtonInactiveSelected =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkGray));

    /// <inheritdoc/>
    protected override CliCellStyle? ButtonMarker =>
        new CliCellStyle(new CliCharStyle(CliColor.RoyalBlue));

    // Table ink (foregrounds).
    /// <inheritdoc/>
    protected override CliCellStyle? TableHeader =>
        new CliCellStyle(new CliCharStyle(CliColor.White));

    /// <inheritdoc/>
    protected override CliCellStyle? TableCell =>
        new CliCellStyle(new CliCharStyle(CliColor.Gray));

    /// <inheritdoc/>
    protected override CliCellStyle? TableFrame =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGray));

    // Semantic accents consumed by the table style recipes (mirror the TableTasting recipes).
    /// <inheritdoc/>
    protected override CliCellStyle? Success =>
        new CliCellStyle(new CliCharStyle(CliColor.Green));

    /// <inheritdoc/>
    protected override CliCellStyle? Warning =>
        new CliCellStyle(new CliCharStyle(CliColor.Yellow));

    // Multi-colour progress bar: keep done (Accent→Cyan) and remaining (MutedText→DarkGray) via the base
    // fallbacks; the 100%-complete state is green (explicit, independent of the Success accent).
    /// <inheritdoc/>
    protected override CliCellStyle? ProgressBarComplete =>
        new CliCellStyle(new CliCharStyle(CliColor.Green));

    // Markup error ink (visually error-like). Alert is intentionally not overridden: it derives from
    // AlertSurface (DarkRed background) + AlertSurfaceAlt (White foreground) via ThemeBase.BuildAlertInk.
    /// <inheritdoc/>
    protected override CliCellStyle? Error =>
        new CliCellStyle(new CliCharStyle(CliColor.Red));

    /// <inheritdoc/>
    protected override CliCellStyle? Key => new CliCellStyle(new CliCharStyle(CliColor.LawnGreen2));

    /// <inheritdoc/>
    protected override CliCellStyle? Path => new CliCellStyle(new CliCharStyle(CliColor.SlateGray));

    /// <inheritdoc/>
    protected override CliCellStyle? Link => new CliCellStyle(new CliCharStyle(CliColor.Blue, decorations: CliTextDecoration.Underline));

    // Alert/attention surface (a background, like Panel).
    /// <inheritdoc/>
    protected override CliCellStyle? AlertSurface =>
        new CliCellStyle(new CliCharStyle(null, CliColor.DarkRed));

    // Surface zebra (alternate-record) styles per surface family.
    // Default surface: DarkGray background, body-readable foreground.
    /// <inheritdoc/>
    protected override CliCellStyle? DefaultSurfaceAlt =>
        new CliCellStyle(new CliCharStyle(CliColor.Gray, CliColor.DarkGray));

    // Panel surface: DarkGreen background, body-readable foreground.
    /// <inheritdoc/>
    protected override CliCellStyle? PanelSurfaceAlt =>
        new CliCellStyle(new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));

    // Alert surface: keep the DarkRed surface with a high-contrast white foreground.
    /// <inheritdoc/>
    protected override CliCellStyle? AlertSurfaceAlt =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkRed));

    // Semantic message-box surfaces: an orange warning surface and a dark-red error surface, each
    // with a readable foreground.
    /// <inheritdoc/>
    protected override CliCellStyle? WarningSurface =>
        new CliCellStyle(new CliCharStyle(CliColor.Black, CliColor.Orange));

    /// <inheritdoc/>
    protected override CliCellStyle? ErrorSurface =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkRed));
}
