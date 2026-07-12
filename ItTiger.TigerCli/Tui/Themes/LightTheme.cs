using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Tui.Themes;

/// <summary>
/// Light framework theme: dark text on a light background, dark-blue accent and table headers.
/// Distinct from <see cref="DarkTheme"/> and <see cref="TigerBlueTheme"/> to exercise the theme system.
/// </summary>
public sealed class LightTheme : ThemeBase
{
    /// <inheritdoc/>
    public override string Name => "light";

    /// <inheritdoc/>
    public override TigerThemeFamily Family => TigerThemeFamily.Light;

    /// <inheritdoc/>
    protected override CliCellStyle Text =>
        new CliCellStyle(new CliCharStyle(CliColor.Black));

    /// <inheritdoc/>
    protected override CliCellStyle MutedText =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGray));

    /// <inheritdoc/>
    protected override CliCellStyle Accent =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkBlue));

    /// <inheritdoc/>
    protected override CliCellStyle Frame =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGray));

    /// <inheritdoc/>
    protected override CliCellStyle Selected =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkBlue));

    /// <inheritdoc/>
    protected override CliCellStyle Background =>
        new CliCellStyle(new CliCharStyle(null, CliColor.White));

    /// <inheritdoc/>
    protected override CliCellStyle? DialogTitle =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkBlue));

    // No explicit PanelSurface/DialogSurface: both fall back to Background (White) for this theme.

    /// <inheritdoc/>
    protected override CliCellStyle? ScrollIndicator =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkBlue));

    /// <inheritdoc/>
    protected override CliCellStyle? Status =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGray, CliColor.Gray));

    /// <inheritdoc/>
    protected override CliCellStyle? InactiveSelectedListItem =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGray, CliColor.Gray));

    /// <inheritdoc/>
    protected override CliCellStyle? TextInput =>
        new CliCellStyle(new CliCharStyle(CliColor.Black, CliColor.Gray));
    
    /// <inheritdoc/>
    protected override CliCellStyle? InactiveTextInput =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGray, CliColor.Gray));

    /// <inheritdoc/>
    protected override CliCellStyle? ButtonInactiveSelected =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGray, CliColor.Gray));

    /// <inheritdoc/>
    protected override CliCellStyle? Button => 
        new CliCellStyle(new CliCharStyle(CliColor.Gray, CliColor.DarkGray));

    /// <inheritdoc/>
    protected override CliCellStyle? ButtonMarker =>
        new CliCellStyle(new CliCharStyle(CliColor.Green));

    /// <inheritdoc/>
    protected override CliCellStyle? TableHeader =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkBlue));

    /// <inheritdoc/>
    protected override CliCellStyle? TableCell =>
        new CliCellStyle(new CliCharStyle(CliColor.Black));

    /// <inheritdoc/>
    protected override CliCellStyle? TableFrame =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGray));

    // Default-surface zebra.
    /// <inheritdoc/>
    protected override CliCellStyle? DefaultSurfaceAlt =>
        new CliCellStyle(new CliCharStyle(CliColor.Black, CliColor.Gray));

    // Multi-colour progress bar: keep done (Accent→DarkBlue) and remaining (MutedText→DarkGray) via the
    // base fallbacks; give the 100%-complete state a green that stays readable on the light background.
    /// <inheritdoc/>
    protected override CliCellStyle? ProgressBarComplete =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGreen));

    /// <inheritdoc/>
    protected override CliCellStyle? Success =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGreen));

    /// <inheritdoc/>
    protected override CliCellStyle? Warning =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkYellow));

    // Markup ink: error foreground readable on the light background; alert as white-on-dark-red.
    /// <inheritdoc/>
    protected override CliCellStyle? Error =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkRed));

    /// <inheritdoc/>
    protected override CliCellStyle? Alert =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkRed));

    // Semantic message-box surfaces: a dark-yellow warning surface and a dark-red error surface, each
    // with a readable foreground that stays legible on this light theme.
    /// <inheritdoc/>
    protected override CliCellStyle? WarningSurface =>
        new CliCellStyle(new CliCharStyle(CliColor.Black, CliColor.Yellow));

    /// <inheritdoc/>
    protected override CliCellStyle? ErrorSurface =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.Red));
}
