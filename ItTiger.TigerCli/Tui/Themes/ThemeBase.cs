using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;      // CliCellStyle
using ItTiger.TigerCli.Tui.Abstractions; // ITheme, ThemeStyle
using System;

namespace ItTiger.TigerCli.Tui.Themes;

/// <summary>
/// Base class for TigerCli themes. Derived themes provide the required base palette and can override
/// semantic role hooks for controls, markup, tables, surfaces, buttons, and progress bars.
/// </summary>
/// <remarks>
/// <see cref="Resolve(ThemeStyle)"/> returns cloned styles and applies fallback rules so a theme can
/// override only the roles that differ. Required roles are text, muted text, accent, frame, selected,
/// and background; optional roles fall back to related base roles or composed defaults.
/// </remarks>
public abstract class ThemeBase : ITheme
{
    /// <summary>Unique theme name (see <see cref="ITheme.Name"/>).</summary>
    public abstract string Name { get; }

    /// <summary>
    /// The theme's contrast family (see <see cref="ITheme.Family"/>). Defaults to
    /// <see cref="TigerThemeFamily.Dark"/>; light-background themes override this to return
    /// <see cref="TigerThemeFamily.Light"/>.
    /// </summary>
    public virtual TigerThemeFamily Family => TigerThemeFamily.Dark;

    /// <summary>Default text ink.</summary>
    protected abstract CliCellStyle Text { get; }

    /// <summary>Muted or secondary text ink.</summary>
    protected abstract CliCellStyle MutedText { get; }

    /// <summary>Primary accent ink.</summary>
    protected abstract CliCellStyle Accent { get; }

    /// <summary>Frame/border ink.</summary>
    protected abstract CliCellStyle Frame { get; }

    /// <summary>Active selection style.</summary>
    protected abstract CliCellStyle Selected { get; }

    /// <summary>Base output or application background style.</summary>
    protected abstract CliCellStyle Background { get; }

    /// <summary>Selected list item style; defaults to <see cref="Selected"/>.</summary>
    protected virtual CliCellStyle? SelectedListItem => null;

    /// <summary>Selected list item style when the list is unfocused; defaults to <see cref="MutedText"/>.</summary>
    protected virtual CliCellStyle? InactiveSelectedListItem => null;

    /// <summary>Selected command-menu item style; defaults to <see cref="Selected"/>.</summary>
    protected virtual CliCellStyle? SelectedMenuItem => null;

    /// <summary>Dialog title ink; defaults to <see cref="Accent"/>.</summary>
    protected virtual CliCellStyle? DialogTitle => null;

    /// <summary>Scrollbar ink; defaults to <see cref="Frame"/>.</summary>
    protected virtual CliCellStyle? ScrollBar => null;

    /// <summary>Scroll indicator ink; defaults to <see cref="Accent"/>.</summary>
    protected virtual CliCellStyle? ScrollIndicator => null;

    /// <summary>Status or hint row style; defaults to <see cref="MutedText"/>.</summary>
    protected virtual CliCellStyle? Status => null;

    /// <summary>Focused text-input style; defaults to <see cref="Text"/>.</summary>
    protected virtual CliCellStyle? TextInput => null;

    /// <summary>Unfocused text-input style; defaults to <see cref="MutedText"/>.</summary>
    protected virtual CliCellStyle? InactiveTextInput => null;

    /// <summary>Normal button style; defaults to <see cref="Text"/>.</summary>
    protected virtual CliCellStyle? Button => null;

    /// <summary>Focused button style; defaults to <see cref="Selected"/>.</summary>
    protected virtual CliCellStyle? ButtonFocused => null;

    /// <summary>Button marker ink; defaults to <see cref="Accent"/>.</summary>
    protected virtual CliCellStyle? ButtonMarker => null;

    /// <summary>Disabled button style; defaults to <see cref="MutedText"/>.</summary>
    protected virtual CliCellStyle? ButtonDisabled => null;

    /// <summary>Selected button style when its group is unfocused; defaults to inactive selection.</summary>
    protected virtual CliCellStyle? ButtonInactiveSelected => null;

    /// <summary>Elevated or panel surface; defaults to <see cref="Background"/>.</summary>
    protected virtual CliCellStyle? PanelSurface => null;

    /// <summary>Dialog/control surface; defaults to <see cref="PanelSurface"/>, then <see cref="Background"/>.</summary>
    protected virtual CliCellStyle? DialogSurface => null;

    /// <summary>Attention surface; defaults to <see cref="Background"/>.</summary>
    protected virtual CliCellStyle? AlertSurface => null;

    /// <summary>Warning message-box surface; composed from warning/accent ink when not overridden.</summary>
    protected virtual CliCellStyle? WarningSurface => null;

    /// <summary>Error message-box surface; composed from error/warning/accent ink when not overridden.</summary>
    protected virtual CliCellStyle? ErrorSurface => null;

    /// <summary>Alternate row style for the default surface; <c>null</c> means no zebra style.</summary>
    protected virtual CliCellStyle? DefaultSurfaceAlt => null;

    /// <summary>Alternate row style for the panel surface; <c>null</c> means no zebra style.</summary>
    protected virtual CliCellStyle? PanelSurfaceAlt => null;

    /// <summary>Alternate row style for the alert surface; <c>null</c> means no zebra style.</summary>
    protected virtual CliCellStyle? AlertSurfaceAlt => null;

    /// <summary>Table title foreground; defaults to dialog title/accent.</summary>
    protected virtual CliCellStyle? TableTitle => null;

    /// <summary>Table header foreground; defaults to accent.</summary>
    protected virtual CliCellStyle? TableHeader => null;

    /// <summary>Table body foreground; defaults to text.</summary>
    protected virtual CliCellStyle? TableCell => null;

    /// <summary>Table frame foreground; defaults to frame.</summary>
    protected virtual CliCellStyle? TableFrame => null;

    /// <summary>Success foreground accent; defaults to accent.</summary>
    protected virtual CliCellStyle? Success => null;

    /// <summary>Warning foreground accent; defaults to accent.</summary>
    protected virtual CliCellStyle? Warning => null;

    /// <summary>Structured-output key or identity value foreground; defaults to accent.</summary>
    protected virtual CliCellStyle? Key => null;

    /// <summary>Structured-output normal value foreground; defaults to text.</summary>
    protected virtual CliCellStyle? Value => null;

    /// <summary>Structured-output path foreground; defaults to muted text.</summary>
    protected virtual CliCellStyle? Path => null;

    /// <summary>Structured-output link foreground; defaults to underlined accent.</summary>
    protected virtual CliCellStyle? Link => null;

    /// <summary>Section, list, or details heading foreground; defaults to bold accent.</summary>
    protected virtual CliCellStyle? Heading => null;

    /// <summary>Done portion of a multi-colour progress bar; defaults to accent.</summary>
    protected virtual CliCellStyle? ProgressBarDone => null;

    /// <summary>Remaining portion of a multi-colour progress bar; defaults to muted text.</summary>
    protected virtual CliCellStyle? ProgressBarRemaining => null;

    /// <summary>Complete-state progress-bar ink; defaults through success to accent.</summary>
    protected virtual CliCellStyle? ProgressBarComplete => null;

    /// <summary>Markup error foreground; defaults through warning to accent.</summary>
    protected virtual CliCellStyle? Error => null;

    /// <summary>Markup alert foreground/background; composed from the alert surface when not overridden.</summary>
    protected virtual CliCellStyle? Alert => null;

    /// <summary>
    /// Resolves a semantic style role to a concrete cell style, applying ThemeBase fallback rules and
    /// returning a clone so callers can safely mutate the result for a single render operation.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="style"/> is not a known role.</exception>
    public CliCellStyle Resolve(ThemeStyle style)
    {
      var cellStyle = style switch
           {
               // Base
               ThemeStyle.Text => Text,
               ThemeStyle.MutedText => MutedText,
               ThemeStyle.Accent => Accent,
               ThemeStyle.Frame => Frame,
               ThemeStyle.Selected => Selected,
               ThemeStyle.Background => Background,

               // Semantic with one-level fallback
               ThemeStyle.SelectedListItem => SelectedListItem ?? Selected,
               // Inactive selection falls back to the muted text token (never to the generic Selected
               // highlight), so an unfocused list looks distinctly inactive even without an override.
               ThemeStyle.InactiveSelectedListItem => InactiveSelectedListItem ?? MutedText,
               ThemeStyle.SelectedMenuItem => SelectedMenuItem ?? Selected,
               ThemeStyle.DialogTitle => DialogTitle ?? Accent,

               // Surfaces: PanelSurface falls back to Background; DialogSurface falls back to
               // PanelSurface (then Background).
               ThemeStyle.PanelSurface => PanelSurface ?? Background,
               ThemeStyle.DialogSurface => DialogSurface ?? PanelSurface ?? Background,

               // Semantic message-box surfaces: explicit override, otherwise a surface composed from
               // the Warning / Error accent inks so warning/error dialogs are styled on every theme.
               ThemeStyle.WarningSurface => WarningSurface ?? BuildWarningSurfaceInk(),
               ThemeStyle.ErrorSurface => ErrorSurface ?? BuildErrorSurfaceInk(),

               ThemeStyle.ScrollBar => ScrollBar ?? Frame,
               ThemeStyle.ScrollIndicator => ScrollIndicator ?? Accent,
               ThemeStyle.Status => Status ?? MutedText,
               ThemeStyle.TextInput => TextInput ?? Text,
               // Unfocused input falls back to the muted text token when not overridden.
               ThemeStyle.InactiveTextInput => InactiveTextInput ?? MutedText,

               // Button tokens. Unfocused buttons use the normal text ink (the brackets give the
               // affordance); focused buttons reuse the Selected highlight (foreground + background)
               // present in every theme; markers default to the Accent ink; disabled to MutedText.
               ThemeStyle.Button => Button ?? Text,
               ThemeStyle.ButtonFocused => ButtonFocused ?? Selected,
               ThemeStyle.ButtonMarker => ButtonMarker ?? Accent,
               ThemeStyle.ButtonDisabled => ButtonDisabled ?? MutedText,
               // A selected-but-unfocused button reuses the inactive-selection family (muted), never
               // the active ButtonFocused/Selected highlight, so its background reads as inactive.
               ThemeStyle.ButtonInactiveSelected => ButtonInactiveSelected ?? InactiveSelectedListItem ?? MutedText,

               // Semantic accents / table ink with one-level fallback to existing tokens, so
               // themes that do not override them still resolve to something sensible.
               ThemeStyle.Success => Success ?? Accent,
               ThemeStyle.Warning => Warning ?? Accent,
               ThemeStyle.Error => Error ?? Warning ?? Accent,
               ThemeStyle.Alert => Alert ?? BuildAlertInk(),
               ThemeStyle.TableTitleForeground => TableTitle ?? DialogTitle ?? Accent,
               ThemeStyle.TableHeaderForeground => TableHeader ?? Accent,
               ThemeStyle.TableBodyForeground => TableCell ?? Text,
               ThemeStyle.TableFrameForeground => TableFrame ?? Frame,

               // Semantic value roles (CRUD list/details). One-level fallback to base tokens so every
               // theme styles values; Link composes an underlined accent when not overridden.
               ThemeStyle.Key => Key ?? Accent,
               ThemeStyle.Value => Value ?? Text,
               ThemeStyle.Path => Path ?? MutedText,
               ThemeStyle.Link => Link ?? BuildLinkInk(),
               ThemeStyle.Heading => Heading ?? BuildHeadingInk(),

               // Progress-bar inks: one-level fallback to base tokens so every theme colours a
               // multi-colour bar; Complete falls back through Success so a theme that already defines a
               // success accent reuses it for the 100% state.
               ThemeStyle.ProgressBarDone => ProgressBarDone ?? Accent,
               ThemeStyle.ProgressBarRemaining => ProgressBarRemaining ?? MutedText,
               ThemeStyle.ProgressBarComplete => ProgressBarComplete ?? Success ?? Accent,

               _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown theme style token.")
           };
        var clonedStyle = CliCellStyle.Clone(cellStyle);
        return clonedStyle!;
    }

    /// <summary>
    /// Composes the default Alert ink when a theme does not override <see cref="Alert"/>: the alert
    /// surface background (falling back to the base background) plus a readable foreground (the alert
    /// surface's alternate-record foreground if present, otherwise the Error/Warning/Accent ink).
    /// This keeps Alert markup usable on every theme without depending on table zebra styles.
    /// </summary>
    private CliCellStyle BuildAlertInk()
    {
        var background = (AlertSurface ?? Background).CharStyle?.Background;
        var foreground = AlertSurfaceAlt?.CharStyle?.Foreground
            ?? (Error ?? Warning ?? Accent).CharStyle?.Foreground;
        return new CliCellStyle(new CliCharStyle(foreground, background));
    }

    /// <summary>
    /// Composes the default warning surface when a theme does not override <see cref="WarningSurface"/>:
    /// the <see cref="Warning"/> accent colour (falling back to <see cref="Accent"/>) used as the surface
    /// background, with a readable dark foreground. Keeps warning message boxes styled on every theme
    /// without a per-theme override.
    /// </summary>
    private CliCellStyle BuildWarningSurfaceInk()
    {
        var background = (Warning ?? Accent).CharStyle?.Foreground;
        return new CliCellStyle(new CliCharStyle(CliColor.Black, background));
    }

    /// <summary>
    /// Composes the default error surface when a theme does not override <see cref="ErrorSurface"/>:
    /// the <see cref="Error"/> accent colour (falling back to <see cref="Warning"/> → <see cref="Accent"/>)
    /// used as the surface background, with a readable light foreground. Keeps error message boxes styled
    /// on every theme without a per-theme override.
    /// </summary>
    private CliCellStyle BuildErrorSurfaceInk()
    {
        var background = (Error ?? Warning ?? Accent).CharStyle?.Foreground;
        return new CliCellStyle(new CliCharStyle(CliColor.White, background));
    }

    /// <summary>
    /// Composes the default Link ink when a theme does not override <see cref="Link"/>: the Accent
    /// foreground with an added underline decoration. Keeps Link navigable-looking on every theme
    /// without depending on a per-theme override, and stays foreground-only (no background).
    /// </summary>
    private CliCellStyle BuildLinkInk()
    {
        var foreground = Accent.CharStyle?.Foreground;
        return new CliCellStyle(new CliCharStyle(foreground, null, CliTextDecoration.Underline));
    }

    /// <summary>
    /// Composes the default Heading ink when a theme does not override <see cref="Heading"/>: the
    /// Accent foreground with an added bold decoration. Keeps headings emphasised on every theme
    /// without a per-theme override, and stays foreground-only (no background).
    /// </summary>
    private CliCellStyle BuildHeadingInk()
    {
        var foreground = Accent.CharStyle?.Foreground;
        return new CliCellStyle(new CliCharStyle(foreground, null, CliTextDecoration.Bold));
    }

    /// <summary>
    /// Resolves a reusable surface family into concrete colours. The surface background and its
    /// optional alternate-record (zebra) colours come from this theme's surface hooks (which fall
    /// back to the base background / no-zebra). Override to fully customize per theme.
    /// </summary>
    public virtual SurfaceColors ResolveSurface(SurfaceRole role)
    {
        var (background, alt) = role switch
        {
            SurfaceRole.Default => (Background, DefaultSurfaceAlt),
            SurfaceRole.Panel => (PanelSurface ?? Background, PanelSurfaceAlt),
            SurfaceRole.Alert => (AlertSurface ?? Background, AlertSurfaceAlt),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown surface role.")
        };

        return new SurfaceColors(
            background.CharStyle?.Background,
            alt?.CharStyle?.Background,
            alt?.CharStyle?.Foreground);
    }
}
