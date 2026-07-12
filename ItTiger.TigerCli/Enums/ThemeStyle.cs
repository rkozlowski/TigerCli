using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItTiger.TigerCli.Enums
{
    /// <summary>
    /// Semantic style roles resolved by an <see cref="Tui.Abstractions.ITheme"/> into concrete cell styles.
    /// </summary>
    public enum ThemeStyle
    {
        /// <summary>Normal foreground text.</summary>
        Text,
        /// <summary>Muted secondary text.</summary>
        MutedText,
        /// <summary>Emphasized accent text.</summary>
        Accent,
        /// <summary>Frames and borders.</summary>
        Frame,
        /// <summary>Selected content.</summary>
        Selected,

        /// <summary>The default application surface.</summary>
        Background,

        /// <summary>The selected item in a focused list.</summary>
        SelectedListItem,
        // The selected list item when the list widget is NOT focused: the row stays clearly selected
        // but with a muted (inactive) highlight rather than the active selected-list style.
        /// <summary>The selected item in an unfocused list.</summary>
        InactiveSelectedListItem,
        /// <summary>The selected menu item.</summary>
        SelectedMenuItem,
        /// <summary>A dialog title.</summary>
        DialogTitle,

        // Reusable elevated/panel surface. DialogSurface is a dialog/control-specific surface that
        // defaults to PanelSurface but is independently overrideable; PanelSurface itself falls back
        // to Background.
        /// <summary>A reusable elevated panel surface.</summary>
        PanelSurface,
        /// <summary>A dialog or control surface.</summary>
        DialogSurface,

        // Semantic message-box dialog surfaces (background + readable foreground). WarningSurface is a
        // yellow/orange attention surface; ErrorSurface is a red one. Both fall back to a surface
        // composed from the Warning/Error accent inks when a theme does not override them, so every
        // theme styles a warning/error message box without an explicit override.
        /// <summary>A warning message surface.</summary>
        WarningSurface,
        /// <summary>An error message surface.</summary>
        ErrorSurface,

        /// <summary>A scroll bar track.</summary>
        ScrollBar,
        /// <summary>A scroll position indicator.</summary>
        ScrollIndicator,
        /// <summary>Status text.</summary>
        Status,
        /// <summary>An active text input.</summary>
        TextInput,
        // The text input surface+text when the input is NOT focused. Defines both foreground and
        // background so an unfocused field reads as a muted input rather than plain body text.
        /// <summary>An inactive text input.</summary>
        InactiveTextInput,

        // Button styles for message-box style dialogs and composite controls. Button is the
        // normal/unfocused button surface+text; ButtonFocused is the focused button surface+text;
        // ButtonMarker styles the focus markers (e.g. ▸ ◂); ButtonDisabled styles a disabled button.
        // All fall back to existing tokens so every theme styles buttons without an override.
        /// <summary>An unfocused button.</summary>
        Button,
        /// <summary>A focused button.</summary>
        ButtonFocused,
        /// <summary>Button focus markers.</summary>
        ButtonMarker,
        /// <summary>A disabled button.</summary>
        ButtonDisabled,
        // The selected button's surface when its button group is NOT focused. The button stays
        // selected (it keeps its focus markers); only the background is muted relative to ButtonFocused.
        /// <summary>A selected button in an unfocused button group.</summary>
        ButtonInactiveSelected,

        // Semantic foreground accents used by the predefined table style recipes
        // (see Rendering.CliTableStyleRecipe / CliTableStyles).
        /// <summary>Success text.</summary>
        Success,
        /// <summary>Warning text.</summary>
        Warning,

        // Semantic ink tokens consumed by markup. Error is a foreground accent (falls back to
        // Warning/Accent). Alert is a combined foreground/background attention ink derived from the
        // alert surface; it is independent of the table-only AlertSurface zebra concept.
        /// <summary>Error text.</summary>
        Error,
        /// <summary>High-attention alert content.</summary>
        Alert,

        // Table ink (foreground-only) roles consumed by table style recipes. They fall back to the
        // base text/accent/frame tokens, so a theme that does not override them still styles tables.
        /// <summary>Table title text.</summary>
        TableTitleForeground,
        /// <summary>Table header text.</summary>
        TableHeaderForeground,
        /// <summary>Table body text.</summary>
        TableBodyForeground,
        /// <summary>Table frames and borders.</summary>
        TableFrameForeground,

        // Semantic value roles for CRUD-style list/details output (see Rendering.CliList /
        // Rendering.CliDetails). They style field/column VALUES (not labels) and fall back to base
        // tokens so any theme styles them without an override: Key→Accent, Value→Text, Path→MutedText,
        // Link→Accent + underline. These are foreground-only roles; backgrounds stay null so the
        // table/detail surface shows through.
        /// <summary>Key values in structured output.</summary>
        Key,
        /// <summary>General values in structured output.</summary>
        Value,
        /// <summary>Path values in structured output.</summary>
        Path,
        /// <summary>Link values in structured output.</summary>
        Link,

        // Section/list/details heading role for structured output titles. Foreground-only; falls back
        // to Accent + bold so any theme styles headings without an override.
        /// <summary>Section and structured-output headings.</summary>
        Heading,

        // Semantic progress-bar ink roles for multi-colour activity bars. Foreground accents that colour
        // the bar glyphs; they fall back to base tokens (Done→Accent, Remaining→MutedText,
        // Complete→Success→Accent) so any theme colours a multi-colour bar without an override. Single
        // (uniform) bars do not use these — they keep painting the whole strip with one style.
        /// <summary>The completed portion of an in-progress activity bar.</summary>
        ProgressBarDone,
        /// <summary>The remaining portion of an activity bar.</summary>
        ProgressBarRemaining,
        /// <summary>A fully completed activity bar.</summary>
        ProgressBarComplete
    }
}
