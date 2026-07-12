using System;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tui.Themes;

/// <summary>
/// Default dark framework theme: neutral dark background, gray text, cyan accent, and green active
/// selection.
/// </summary>
/// <remarks>
/// This is the initial <c>TigerConsole.CurrentTheme</c>. It derives from <see cref="ThemeBase"/>,
/// overriding only the roles that differ from the base fallback model.
/// </remarks>
public sealed class DarkTheme : ThemeBase
{
    /// <inheritdoc/>
    public override string Name => "dark";

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
        new CliCellStyle(
            new CliCharStyle(CliColor.Black, CliColor.Green));

    /// <inheritdoc/>
    protected override CliCellStyle Background =>
        new CliCellStyle(new CliCharStyle(null, CliColor.Black));

    // Optional semantic overrides
    /// <inheritdoc/>
    protected override CliCellStyle? DialogTitle =>
        new CliCellStyle(new CliCharStyle(CliColor.White));

    // No explicit PanelSurface/DialogSurface: both fall back to Background (Black) for this theme.

    /// <inheritdoc/>
    protected override CliCellStyle? ScrollIndicator =>
        new CliCellStyle(new CliCharStyle(CliColor.White));

    /// <inheritdoc/>
    protected override CliCellStyle? Status =>
        new CliCellStyle(new CliCharStyle(CliColor.Gray, CliColor.DarkGray));

    // Inactive (unfocused) selected list item: keep the selection visible, but on a muted dark-gray
    // highlight instead of the active green.
    /// <inheritdoc/>
    protected override CliCellStyle? InactiveSelectedListItem =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkGray));

    /// <inheritdoc/>
    protected override CliCellStyle? TextInput =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkGray));

    // Unfocused input: muted foreground on the dialog/background colour (both defined).
    /// <inheritdoc/>
    protected override CliCellStyle? InactiveTextInput =>
        new CliCellStyle(new CliCharStyle(CliColor.Gray, CliColor.DarkGray));

    // Selected button while its group is unfocused: muted dark-gray surface (not the active green).
    /// <inheritdoc/>
    protected override CliCellStyle? ButtonInactiveSelected =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkGray));

    /// <inheritdoc/>
    protected override CliCellStyle? ButtonMarker =>
        new CliCellStyle(new CliCharStyle(CliColor.Blue));

    // Table ink (foregrounds) for this theme. Structure comes from the recipe; only colours change
    // here, so LightTheme/TigerBlueTheme recolor the same recipes differently.
    /// <inheritdoc/>
    protected override CliCellStyle? TableHeader =>
        new CliCellStyle(new CliCharStyle(CliColor.Cyan));

    /// <inheritdoc/>
    protected override CliCellStyle? TableCell =>
        new CliCellStyle(new CliCharStyle(CliColor.Gray));

    /// <inheritdoc/>
    protected override CliCellStyle? TableFrame =>
        new CliCellStyle(new CliCharStyle(CliColor.DarkGray));

    // Default-surface zebra.
    /// <inheritdoc/>
    protected override CliCellStyle? DefaultSurfaceAlt =>
        new CliCellStyle(new CliCharStyle(CliColor.Gray, CliColor.DarkGray));

    // Multi-colour progress bar: keep done (Accent→Cyan) and remaining (MutedText→DarkGray) via the base
    // fallbacks; give the 100%-complete state a clearly distinct green.
    /// <inheritdoc/>
    protected override CliCellStyle? ProgressBarComplete =>
        new CliCellStyle(new CliCharStyle(CliColor.Green));

    
    /// <inheritdoc/>
    protected override CliCellStyle? Success =>
        new CliCellStyle(new CliCharStyle(CliColor.Green));

    /// <inheritdoc/>
    protected override CliCellStyle? Warning =>
        new CliCellStyle(new CliCharStyle(CliColor.Yellow));

    // Markup ink: error foreground in red; alert as white-on-dark-red. (This theme defines no
    // AlertSurface, so Alert markup gets an explicit ink rather than the surface-derived fallback.)
    /// <inheritdoc/>
    protected override CliCellStyle? Error =>
        new CliCellStyle(new CliCharStyle(CliColor.Red));

    /// <inheritdoc/>
    protected override CliCellStyle? Key => new CliCellStyle(new CliCharStyle(CliColor.Green));

    /// <inheritdoc/>
    protected override CliCellStyle? Path => new CliCellStyle(new CliCharStyle(CliColor.DarkGray));

    /// <inheritdoc/>
    protected override CliCellStyle? Link => new CliCellStyle(new CliCharStyle(CliColor.Blue, decorations: CliTextDecoration.Underline));

    /// <inheritdoc/>
    protected override CliCellStyle? Alert =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkRed));

    // Semantic message-box surfaces: a dark-yellow warning surface and a dark-red error surface, each
    // with a readable foreground. (This theme defines no Warning ink, so the explicit override avoids
    // the composed cyan-derived fallback.)
    /// <inheritdoc/>
    protected override CliCellStyle? WarningSurface =>
        new CliCellStyle(new CliCharStyle(CliColor.Black, CliColor.DarkYellow));

    /// <inheritdoc/>
    protected override CliCellStyle? ErrorSurface =>
        new CliCellStyle(new CliCharStyle(CliColor.White, CliColor.DarkRed));
}
