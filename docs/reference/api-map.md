# TigerCli API Map

<!-- Generated from DocFX metadata by internal/DocSamples (`api-map` mode). Do not edit by hand. -->

Generated from DocFX metadata. Do not edit by hand.

This is a compact index of public TigerCli types for humans and AI tools. Behavioral contracts live in XML documentation comments, the generated API reference, and the guides.

Regenerate with:

```
dotnet docfx docs/api-docfx/docfx.json
dotnet run --project internal/DocSamples -- api-map
```

## ItTiger.Core

- `TigerTextAttribute` *(class)* — Provides display text and optional localized resource keys for TigerCli-facing enum values, settings properties, and command metadata.
  Source: `ItTiger.Core/TigerTextAttribute.cs`
  API: `docs/api-docfx/_site/api/ItTiger.Core.TigerTextAttribute.html`

## ItTiger.Core.Resources

- `ChainedResourceManager` *(class)* — Resolves strings from a prioritized sequence of resource managers.
  Source: `ItTiger.Core/Resources/ChainedResourceManager.cs`
  API: `docs/api-docfx/_site/api/ItTiger.Core.Resources.ChainedResourceManager.html`

## ItTiger.TigerCli.Commands

- `TigerCliApp` *(class)* — A configured TigerCli command application: the immutable result of Build.
  Source: `ItTiger.TigerCli/Commands/TigerCliApp.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliApp.html`
- `TigerCliAppBuilder` *(class)* — Fluent builder for a TigerCliApp, obtained from CreateBuilder.
  Source: `ItTiger.TigerCli/Commands/TigerCliAppBuilder.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliAppBuilder.html`
- `TigerCliApplicationLink` *(class)* — A help-footer link configured through the TigerCliAppBuilder link methods (AddLink, AddWebsite, AddRepository, AddDocumentation) or derived from assembly metadata.
  Source: `ItTiger.TigerCli/Commands/TigerCliApplicationLink.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliApplicationLink.html`
- `TigerCliApplicationMetadata` *(class)* — The app's resolved display metadata — display name, versions, copyright, and help-footer links — assembled by Build from explicit builder calls and assembly-metadata defaults.
  Source: `ItTiger.TigerCli/Commands/TigerCliApplicationMetadata.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliApplicationMetadata.html`
- `TigerCliArgumentAttribute` *(class)* — Binds a settings property to a positional command-line argument.
  Source: `ItTiger.TigerCli/Commands/TigerCliArgumentAttribute.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliArgumentAttribute.html`
- `TigerCliAsyncCommandHandler<TSettings, TExitCode>` *(class)* — Base class for async command handlers returning a typed exit code.
  Source: `ItTiger.TigerCli/Commands/TigerCliAsyncCommandHandler.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliAsyncCommandHandler-2.html`
- `TigerCliAsyncCommandHandler<TSettings>` *(class)* — Base class for async command handlers returning a raw integer exit code.
  Source: `ItTiger.TigerCli/Commands/TigerCliAsyncCommandHandler.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliAsyncCommandHandler-1.html`
- `TigerCliCommandAliasBuilder` *(class)* — Configures a command alias registered via AddCommandAlias.
  Source: `ItTiger.TigerCli/Commands/TigerCliCommandAliasBuilder.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliCommandAliasBuilder.html`
- `TigerCliCommandBuilder` *(class)* — Configures metadata for a single command registration.
  Source: `ItTiger.TigerCli/Commands/TigerCliCommandBuilder.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliCommandBuilder.html`
- `TigerCliCommandGroupBuilder` *(class)* — Configures a command group: a command-path prefix that owns a set of child commands.
  Source: `ItTiger.TigerCli/Commands/TigerCliCommandGroupBuilder.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliCommandGroupBuilder.html`
- `TigerCliEditLoad<TSettings>` *(class)* — Result of an edit-command loader.
  Source: `ItTiger.TigerCli/Commands/TigerCliEditLoad.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliEditLoad-1.html`
- `TigerCliExactlyOneOfAttribute` *(class)* — Declares that exactly one of the listed option properties must be provided or resolved.
  Source: `ItTiger.TigerCli/Commands/TigerCliExactlyOneOfAttribute.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliExactlyOneOfAttribute.html`
- `TigerCliExitCategory` *(enum)* — The middle layer of the layered exit model: a small, stable grouping of TigerCliExitKind values.
  Source: `ItTiger.TigerCli/Commands/TigerCliExitCategory.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliExitCategory.html`
- `TigerCliExitKind` *(enum)* — The specific framework reason a run ended.
  Source: `ItTiger.TigerCli/Commands/TigerCliExitKind.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliExitKind.html`
- `TigerCliExitOutcome` *(enum)* — The coarsest layer of the layered exit model: whether a run ultimately succeeded or failed.
  Source: `ItTiger.TigerCli/Commands/TigerCliExitOutcome.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliExitOutcome.html`
- `TigerCliFolderSelectAttribute` *(class)* — Marks a string / string? command option so that, when TigerCli needs to prompt for its missing value in semi-interactive mode, it uses the inline folder picker (InlineFolderSelect) instead of a plain text prompt.
  Source: `ItTiger.TigerCli/Commands/TigerCliFolderSelectAttribute.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliFolderSelectAttribute.html`
- `TigerCliMultiSelectAttribute` *(class)* — Marks a collection command option as a multi-select.
  Source: `ItTiger.TigerCli/Commands/TigerCliMultiSelectAttribute.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliMultiSelectAttribute.html`
- `TigerCliOptionAttribute` *(class)* — Binds a settings property to a named command-line option (e.g.
  Source: `ItTiger.TigerCli/Commands/TigerCliOptionAttribute.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliOptionAttribute.html`
- `TigerCliPromptConfiguration<TSettings>` *(class)* — App-level compatibility surface for configuring property-scoped prompt providers for a settings type.
  Source: `ItTiger.TigerCli/Commands/TigerCliPromptConfiguration.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliPromptConfiguration-1.html`
- `TigerCliPromptContext` *(class)* — Context passed to prompt and provider callbacks while TigerCli is resolving provider-backed values.
  Source: `ItTiger.TigerCli/Commands/TigerCliPromptContext.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliPromptContext.html`
- `TigerCliPromptPropertyBuilder<TSettings, TValue>` *(class)* — Registers a provider-backed prompt for one property selected from TigerCliPromptConfiguration%601.
  Source: `ItTiger.TigerCli/Commands/TigerCliPromptConfiguration.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliPromptPropertyBuilder-2.html`
- `TigerCliPromptable` *(enum)* — Per-option/per-argument prompting opinion, assigned through Promptable / Promptable.
  Source: `ItTiger.TigerCli/Commands/TigerCliPromptable.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliPromptable.html`
- `TigerCliProviderConfiguration` *(class)* — Registers app-level named dynamic value providers, configured through TigerCliAppBuilder.ConfigureProviders(...).
  Source: `ItTiger.TigerCli/Commands/TigerCliProviderConfiguration.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliProviderConfiguration.html`
- `TigerCliProviderContext` *(class)* — Run-time context passed to dynamic value-provider callbacks.
  Source: `ItTiger.TigerCli/Commands/TigerCliProviderContext.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliProviderContext.html`
- `TigerCliProviderOptions` *(class)* — Optional per-registration provider options, supplied through the trailing configure callback on the provider registration APIs (AddProvider, AddAsyncProvider, Add, AddAsync).
  Source: `ItTiger.TigerCli/Commands/TigerCliProviderOptions.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliProviderOptions.html`
- `TigerCliSettings` *(class)* — Base class for command settings.
  Source: `ItTiger.TigerCli/Commands/TigerCliSettings.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliSettings.html`
- `TigerCliValidationResult` *(class)* — Result of Validate: either success or a failure carrying a user-facing error message.
  Source: `ItTiger.TigerCli/Commands/TigerCliValidationResult.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliValidationResult.html`
- `TigerCliValueMatchPreset` *(enum)* — Controls how a supplied command-line value is matched against a provider-backed option's / argument's choices during non-interactive validation and multi-select resolution.
  Source: `ItTiger.TigerCli/Commands/TigerCliValueMatchPreset.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Commands.TigerCliValueMatchPreset.html`

## ItTiger.TigerCli.Enums

- `ActivityOutcome` *(enum)* — The terminal outcome of a rich activity dialog run.
  Source: `ItTiger.TigerCli/Enums/ActivityOutcome.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.ActivityOutcome.html`
- `ActivityStopMode` *(enum)* — The single stop action a rich activity dialog exposes.
  Source: `ItTiger.TigerCli/Enums/ActivityStopMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.ActivityStopMode.html`
- `CliAnsiSupport` *(enum)* — The level of ANSI support detected for a console stream by TerminalCapabilities.
  Source: `ItTiger.TigerCli/Enums/CliAnsiSupport.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliAnsiSupport.html`
- `CliCellPadding` *(enum)* — Horizontal padding applied inside a rendered cell.
  Source: `ItTiger.TigerCli/Enums/CliCellPadding.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliCellPadding.html`
- `CliColor` *(enum)* — The full ANSI 256-color palette as a single enum.
  Source: `ItTiger.TigerCli/Enums/CliColor.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliColor.html`
- `CliColorMode` *(enum)* — Controls how TigerCli's default console output paths emit colour.
  Source: `ItTiger.TigerCli/Enums/CliColorMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliColorMode.html`
- `CliColumnSizing` *(enum)* — Controls how a column's width is determined after content-driven sizing.
  Source: `ItTiger.TigerCli/Enums/CliColumnSizing.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliColumnSizing.html`
- `CliControlDecoration` *(enum)* — Decoration flags requested by inline controls, such as scroll indicators and scroll bars.
  Source: `ItTiger.TigerCli/Enums/CliControlDecoration.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliControlDecoration.html`
- `CliFormattingMode` *(enum)* — Controls whether cell content is treated as raw data or preformatted markup-aware text.
  Source: `ItTiger.TigerCli/Enums/CliFormattingMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliFormattingMode.html`
- `CliFrameJoinStyle` *(enum)* — Controls how crossing frame segments choose junction glyphs.
  Source: `ItTiger.TigerCli/Enums/CliFrameJoinStyle.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliFrameJoinStyle.html`
- `CliFrameLineType` *(enum)* — Identifies the logical side or direction of a frame line.
  Source: `ItTiger.TigerCli/Enums/CliFrameLineType.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliFrameLineType.html`
- `CliFrameSegmentStyle` *(enum)* — Visual style for a table or grid frame segment.
  Source: `ItTiger.TigerCli/Enums/CliFrameSegmentStyle.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliFrameSegmentStyle.html`
- `CliGridAxis` *(enum)* — Selects whether a grid axis operation targets a row or a column.
  Source: `ItTiger.TigerCli/Enums/CliGridAxis.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliGridAxis.html`
- `CliHyperlinkMode` *(enum)* — Controls whether TigerCli's ANSI output emits OSC 8 clickable hyperlinks for text that carries a resolved hyperlink target (e.g.
  Source: `ItTiger.TigerCli/Enums/CliHyperlinkMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliHyperlinkMode.html`
- `CliOrientation` *(enum)* — One-dimensional orientation used by overlays and layout strips.
  Source: `ItTiger.TigerCli/Enums/CliOrientation.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliOrientation.html`
- `CliScrollAxis` *(enum)* — Indicates whether an axis is pinned or scrollable in internal scroll layout.
  Source: `ItTiger.TigerCli/Enums/CliScrollAxis.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliScrollAxis.html`
- `CliScrollMode` *(enum)* — Selects which axes of a hosted subgrid cell are scrollable.
  Source: `ItTiger.TigerCli/Enums/CliScrollMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliScrollMode.html`
- `CliScrollThumbMode` *(enum)* — Determines whether the scrollbar thumb represents the physical scroll offset or the logical active point (cursor) position.
  Source: `ItTiger.TigerCli/Enums/CliScrollThumbMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliScrollThumbMode.html`
- `CliStylePrecedence` *(enum)* — Determines whether a row style or a column style wins when both contribute the same cell style property.
  Source: `ItTiger.TigerCli/Enums/CliStylePrecedence.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliStylePrecedence.html`
- `CliTableOrientation` *(enum)* — Specifies the orientation of a CLI table.
  Source: `ItTiger.TigerCli/Enums/CliTableOrientation.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliTableOrientation.html`
- `CliTableStyleOrientationSupport` *(enum)* — Which orientations a predefined city table style (see CliTableStyles) is intended for.
  Source: `ItTiger.TigerCli/Enums/CliTableStyleOrientationSupport.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliTableStyleOrientationSupport.html`
- `CliTableStylePreset` *(enum)* — The single source of truth for built-in table style presets (see CliTableStyles).
  Source: `ItTiger.TigerCli/Enums/CliTableStylePreset.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliTableStylePreset.html`
- `CliTextAlignment` *(enum)* — Horizontal text alignment inside a measured cell.
  Source: `ItTiger.TigerCli/Enums/CliTextAlignment.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliTextAlignment.html`
- `CliTextDecoration` *(enum)* — Text decoration attributes that can be combined on a styled span.
  Source: `ItTiger.TigerCli/Enums/CliTextDecoration.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliTextDecoration.html`
- `CliVerticalAlignment` *(enum)* — Vertical content alignment inside a measured cell.
  Source: `ItTiger.TigerCli/Enums/CliVerticalAlignment.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliVerticalAlignment.html`
- `CliWrapMode` *(enum)* — Line-breaking strategy used by CliWrapping.
  Source: `ItTiger.TigerCli/Enums/CliWrapMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CliWrapMode.html`
- `CommandMenuMode` *(enum)* — Opt-in policy for whether a node (app, command group, or command) participates in the discoverable command menu.
  Source: `ItTiger.TigerCli/Enums/CommandMenuMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CommandMenuMode.html`
- `CursorMode` *(enum)* — Cursor visibility requested by a grid or inline widget.
  Source: `ItTiger.TigerCli/Enums/CursorMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.CursorMode.html`
- `DialogConfirmationKinds` *(enum)* — The confirmable dialog result kinds an InlineDialog can gate behind an "are you sure?" confirmation.
  Source: `ItTiger.TigerCli/Enums/DialogConfirmationKinds.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.DialogConfirmationKinds.html`
- `DialogResultKind` *(enum)* — Result kind returned by modal and inline dialog interactions.
  Source: `ItTiger.TigerCli/Enums/DialogResultKind.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.DialogResultKind.html`
- `HtmlHyperlinkMode` *(enum)* — Controls how HtmlSink renders a text run that carries a HyperlinkTarget.
  Source: `ItTiger.TigerCli/Enums/HtmlHyperlinkMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.HtmlHyperlinkMode.html`
- `InlineDialogArea` *(enum)* — Fixed layout areas where an inline dialog can place title, frame, status, widgets, and activity overlays.
  Source: `ItTiger.TigerCli/Enums/InlineDialogArea.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.InlineDialogArea.html`
- `MessageBoxButtons` *(enum)* — The button set shown by a message-box style inline dialog.
  Source: `ItTiger.TigerCli/Enums/MessageBoxButtons.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.MessageBoxButtons.html`
- `MessageBoxKind` *(enum)* — The semantic severity of a message-box style inline dialog.
  Source: `ItTiger.TigerCli/Enums/MessageBoxKind.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.MessageBoxKind.html`
- `ProgressBarCaps` *(enum)* — Optional end-cap decoration for an activity progress bar.
  Source: `ItTiger.TigerCli/Enums/ProgressBarCaps.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.ProgressBarCaps.html`
- `ProgressBarColorMode` *(enum)* — How an activity progress bar is coloured.
  Source: `ItTiger.TigerCli/Enums/ProgressBarColorMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.ProgressBarColorMode.html`
- `ProgressBarStyle` *(enum)* — A predefined progress-bar glyph appearance for an activity dialog's progress-bar element.
  Source: `ItTiger.TigerCli/Enums/ProgressBarStyle.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.ProgressBarStyle.html`
- `SelectOrder` *(enum)* — Ordering mode for selectable option collections.
  Source: `ItTiger.TigerCli/Enums/SelectOrder.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.SelectOrder.html`
- `SpinnerFrameSet` *(enum)* — A predefined spinner frame sequence owned by SpinnerTicker.
  Source: `ItTiger.TigerCli/Enums/SpinnerFrameSet.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.SpinnerFrameSet.html`
- `SurfaceRole` *(enum)* — A reusable UI surface family resolved by an ITheme into concrete colours (see SurfaceColors).
  Source: `ItTiger.TigerCli/Enums/SurfaceRole.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.SurfaceRole.html`
- `TableAccent` *(enum)* — A semantic accent role a table style recipe (see CliTableStyleRecipe) references for foreground-only elements such as the title or frame.
  Source: `ItTiger.TigerCli/Enums/TableAccent.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.TableAccent.html`
- `ThemeStyle` *(enum)* — Semantic style roles resolved by an ITheme into concrete cell styles.
  Source: `ItTiger.TigerCli/Enums/ThemeStyle.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.ThemeStyle.html`
- `TigerCliInteractionMode` *(enum)* — Controls whether a command run may use interactive TigerCli UI while resolving command input.
  Source: `ItTiger.TigerCli/Enums/TigerCliInteractionMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.TigerCliInteractionMode.html`
- `TigerCliPromptMode` *(enum)* — Controls which missing values TigerCli may prompt for when the effective interaction mode allows semi-interactive prompting.
  Source: `ItTiger.TigerCli/Enums/TigerCliPromptMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.TigerCliPromptMode.html`
- `TigerCliRenderStage` *(enum)* — Rendering pipeline stage associated with a TigerCliException.
  Source: `ItTiger.TigerCli/Enums/TigerCliRenderStage.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.TigerCliRenderStage.html`
- `TigerCliTheme` *(enum)* — Selects a TigerCli UI theme.
  Source: `ItTiger.TigerCli/Enums/TigerCliTheme.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.TigerCliTheme.html`
- `TigerThemeFamily` *(enum)* — The contrast family a theme belongs to.
  Source: `ItTiger.TigerCli/Enums/TigerThemeFamily.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Enums.TigerThemeFamily.html`

## ItTiger.TigerCli.Exceptions

- `TigerCliCommandException` *(class)* — Classified command failure for handlers — especially reusable command libraries — that want to express what kind of failure occurred using TigerCli concepts without owning the application's numeric exit-code scheme.
  Source: `ItTiger.TigerCli/Exceptions/TigerCliCommandException.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Exceptions.TigerCliCommandException.html`
- `TigerCliException` *(class)* — Exception raised for TigerCli framework failures that should identify the render or execution stage where the failure occurred.
  Source: `ItTiger.TigerCli/Exceptions/TigerCliException.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Exceptions.TigerCliException.html`
- `TigerCliProviderException` *(class)* — Deliberate failure signal for dynamic value providers.
  Source: `ItTiger.TigerCli/Exceptions/TigerCliProviderException.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Exceptions.TigerCliProviderException.html`
- `TigerCliSystemCancellationException` *(class)* — Thrown by the simple (collapsing) TigerTui prompt helpers when the underlying modal completed with SystemCancel — a process/system cancellation request such as Ctrl-C / SIGINT / SIGTERM / SIGQUIT.
  Source: `ItTiger.TigerCli/Exceptions/TigerCliSystemCancellationException.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Exceptions.TigerCliSystemCancellationException.html`

## ItTiger.TigerCli.Markup

- `CliMarkupParser` *(class)* — Parses TigerCli bracket markup into styled text segments.
  Source: `ItTiger.TigerCli/Markup/CliMarkupParser.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Markup.CliMarkupParser.html`
- `IColorAliasResolver` *(interface)* — Resolves an application-defined raw colour alias (e.g.
  Source: `ItTiger.TigerCli/Markup/IColorAliasResolver.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Markup.IColorAliasResolver.html`
- `IMarkupStyleResolver` *(interface)* — Resolves a curated semantic markup token (e.g.
  Source: `ItTiger.TigerCli/Markup/IMarkupStyleResolver.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Markup.IMarkupStyleResolver.html`
- `ThemeMarkupStyleResolver` *(class)* — Theme-backed IMarkupStyleResolver exposing the curated framework semantic markup tokens plus, optionally, an app's registered custom semantic styles.
  Source: `ItTiger.TigerCli/Markup/ThemeMarkupStyleResolver.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Markup.ThemeMarkupStyleResolver.html`

## ItTiger.TigerCli.PngSink

- `PngFontSource` *(class)* — Describes the font files used by the PNG renderer for terminal text or title text.
  Source: `ItTiger.TigerCli.PngSink/PngFontSource.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.PngSink.PngFontSource.html`
- `PngOverflowMode` *(enum)* — Controls how PngSink handles text written beyond the configured grid dimensions.
  Source: `ItTiger.TigerCli.PngSink/PngOverflowMode.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.PngSink.PngOverflowMode.html`
- `PngRenderer` *(class)* — Convenience methods for rendering TigerCli grids and renderable components to PNG output.
  Source: `ItTiger.TigerCli.PngSink/PngRenderer.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.PngSink.PngRenderer.html`
- `PngSink` *(class)* — An ICliRenderSink that captures TigerCli text segments into a fixed-size terminal grid and materializes the result as a PNG image.
  Source: `ItTiger.TigerCli.PngSink/PngSink.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.PngSink.PngSink.html`
- `PngSinkOptions` *(class)* — Immutable options used by PngSink and PngRenderer to size and style a rendered PNG image.
  Source: `ItTiger.TigerCli.PngSink/PngSinkOptions.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.PngSink.PngSinkOptions.html`
- `PngTitleBarIcon` *(class)* — Describes the icon rendered in the PNG title bar when window chrome is enabled.
  Source: `ItTiger.TigerCli.PngSink/PngTitleBarIcon.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.PngSink.PngTitleBarIcon.html`
- `PngTitleBarSymbols` *(class)* — Describes the window-control symbol strip rendered in the PNG title bar when window chrome is enabled.
  Source: `ItTiger.TigerCli.PngSink/PngTitleBarSymbols.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.PngSink.PngTitleBarSymbols.html`
- `PngWindowChrome` *(enum)* — Controls whether PNG output is just the terminal content area or includes TigerCli's terminal frame and title bar.
  Source: `ItTiger.TigerCli.PngSink/PngWindowChrome.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.PngSink.PngWindowChrome.html`

## ItTiger.TigerCli.Primitives

- `ActivePoint` *(class)* — 'A logical position within a grid: column, row, and character offset inside the cell content.'
  Source: `ItTiger.TigerCli/Primitives/ActivePoint.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.ActivePoint.html`
- `CliCellStyle` *(class)* — Style information applied to grid cells, rows, columns, and table bands.
  Source: `ItTiger.TigerCli/Primitives/CliCellStyle.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliCellStyle.html`
- `CliCharStyle` *(struct)* — Character-level style for foreground/background colours, text decorations, and hyperlink target.
  Source: `ItTiger.TigerCli/Primitives/CliCharStyle.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliCharStyle.html`
- `CliColorMapper` *(class)* — Converts between TigerCli's CliColor palette and the legacy ConsoleColor API, and parses raw color names used by markup.
  Source: `ItTiger.TigerCli/Primitives/CliColorMapper.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliColorMapper.html`
- `CliColorPalette` *(class)* — RGB source of truth for CliColor across the full ANSI 0–255 palette, plus the down-conversion used to degrade a 256-color value to the nearest standard 0–15 color until a 256-color (ANSI) sink exists.
  Source: `ItTiger.TigerCli/Primitives/CliColorPalette.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliColorPalette.html`
- `CliFormatter` *(class)* — Formats arbitrary cell values before they are escaped or interpreted as markup by the rendering pipeline.
  Source: `ItTiger.TigerCli/Primitives/CliFormatter.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliFormatter.html`
- `CliFrameLine` *(class)* — A frame line scheduled inside a frame area.
  Source: `ItTiger.TigerCli/Primitives/CliFrameLine.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliFrameLine.html`
- `CliFrameSegment` *(class)* — Describes the visual style of one frame segment, with optional custom content.
  Source: `ItTiger.TigerCli/Primitives/CliFrameSegment.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliFrameSegment.html`
- `CliGridAxisDefinition` *(class)* — Base definition for a grid row or column axis.
  Source: `ItTiger.TigerCli/Primitives/CliGridAxisDefinition.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliGridAxisDefinition.html`
- `CliGridCell` *(class)* — A single grid cell definition, holding either scalar content or a hosted subgrid plus optional style and span information.
  Source: `ItTiger.TigerCli/Primitives/CliGridCell.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliGridCell.html`
- `CliGridColumnDefinition` *(class)* — Column definition for a CliGrid, including style and sizing mode.
  Source: `ItTiger.TigerCli/Primitives/CliGridColumnDefinition.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliGridColumnDefinition.html`
- `CliGridRowDefinition` *(class)* — Row definition for a CliGrid, including row style.
  Source: `ItTiger.TigerCli/Primitives/CliGridRowDefinition.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliGridRowDefinition.html`
- `CliLayoutComponent` *(class)* — Base layout constraints shared by renderable components and grids.
  Source: `ItTiger.TigerCli/Primitives/CliLayoutComponent.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliLayoutComponent.html`
- `CliOverlay` *(class)* — A post-layout, one-dimensional strip that can overwrite measured cells in a CliGrid after the standard measurement pass.
  Source: `ItTiger.TigerCli/Primitives/CliOverlay.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliOverlay.html`
- `CliOverlayGlyph` *(struct)* — A single character emitted by a CliStyledOverlayRenderer, optionally carrying its own per-character style.
  Source: `ItTiger.TigerCli/Primitives/CliOverlay.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliOverlayGlyph.html`
- `CliOverlayRenderer` *(delegate)* — A delegate that produces the renderable content for a CliOverlay.
  Source: `ItTiger.TigerCli/Primitives/CliOverlay.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliOverlayRenderer.html`
- `CliPoint` *(struct)* — A zero-based (column, row) position within a CliGrid.
  Source: `ItTiger.TigerCli/Primitives/CliPoint.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliPoint.html`
- `CliRenderCell` *(struct)* — Character and console colours stored in a render buffer cell.
  Source: `ItTiger.TigerCli/Primitives/CliRenderCell.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliRenderCell.html`
- `CliScrollableCell` *(class)* — Scroll state for a grid cell that hosts a scrollable subgrid.
  Source: `ItTiger.TigerCli/Primitives/CliScrollableCell.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliScrollableCell.html`
- `CliStyledOverlayRenderer` *(delegate)* — A delegate that produces styled per-character content for a CliOverlay.
  Source: `ItTiger.TigerCli/Primitives/CliOverlay.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliStyledOverlayRenderer.html`
- `CliTableFrameConfig` *(class)* — Frame and separator configuration used by CliTable when building its grid.
  Source: `ItTiger.TigerCli/Primitives/CliTableFrameConfig.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliTableFrameConfig.html`
- `CliTableStyle` *(class)* — Resolved, renderable table defaults — the output of resolving a CliTableStyleRecipe against an ITheme.
  Source: `ItTiger.TigerCli/Primitives/CliTableStyle.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliTableStyle.html`
- `CliTextSegment` *(class)* — A contiguous run of text with a single character style.
  Source: `ItTiger.TigerCli/Primitives/CliTextSegment.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliTextSegment.html`
- `CliWrapping` *(class)* — Text wrapping and truncation policy for rendered cell content.
  Source: `ItTiger.TigerCli/Primitives/CliWrapping.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.CliWrapping.html`
- `ConsoleSymbol` *(class)* — Shared Unicode symbols used by frames, overlays, indicators, and inline controls.
  Source: `ItTiger.TigerCli/Primitives/ConsoleSymbol.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.ConsoleSymbol.html`
- `KeyEvent` *(struct)* — Keyboard event captured by a terminal or test terminal.
  Source: `ItTiger.TigerCli/Primitives/KeyEvent.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.KeyEvent.html`
- `MeasuredActivePoint` *(class)* — The result of mapping an ActivePoint through the measurement pipeline.
  Source: `ItTiger.TigerCli/Primitives/MeasuredActivePoint.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.MeasuredActivePoint.html`
- `MeasuredCell` *(class)* — Measured representation of a grid cell after formatting, markup parsing, and layout.
  Source: `ItTiger.TigerCli/Primitives/MeasuredCell.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.MeasuredCell.html`
- `OptionItem<TKey>` *(struct)* — A provider choice with a canonical key and a user-facing label.
  Source: `ItTiger.TigerCli/Primitives/OptionItem.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.OptionItem-1.html`
- `Size` *(struct)* — Width and height pair.
  Source: `ItTiger.TigerCli/Primitives/Size.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.Size.html`
- `SurfaceColors` *(struct)* — The concrete colours a theme resolves for a SurfaceRole: the surface Background plus the optional alternate-record (zebra) colours used for data cells.
  Source: `ItTiger.TigerCli/Primitives/SurfaceColors.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Primitives.SurfaceColors.html`

## ItTiger.TigerCli.Rendering

- `CliDetails` *(class)* — App-facing convenience builder for one-record key/value detail views ("Name: value" panels).
  Source: `ItTiger.TigerCli/Rendering/CliDetails.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliDetails.html`
- `CliFrameArea` *(class)* — Describes a rectangular frame region owned by a CliGrid.
  Source: `ItTiger.TigerCli/Rendering/CliFrameArea.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliFrameArea.html`
- `CliGrid` *(class)* — Lower-level grid layout and rendering building block used by tables, detail/list builders, and TUI widgets.
  Source: `ItTiger.TigerCli/Rendering/CliGrid.Wrapping.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliGrid.html`
- `CliList<T>` *(class)* — App-facing convenience builder for list command output: a column-per-field, record-per-item table.
  Source: `ItTiger.TigerCli/Rendering/CliList.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliList-1.html`
- `CliOverlayEdge` *(enum)* — Which edge a horizontal scroll indicator sits on.
  Source: `ItTiger.TigerCli/Rendering/CliOverlayRenderers.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliOverlayEdge.html`
- `CliOverlayRenderers` *(class)* — Reusable CliOverlayRenderer factories for the common one-dimensional overlays (vertical scrollbar, horizontal scroll indicators, time-/state-driven text such as a spinner or clock).
  Source: `ItTiger.TigerCli/Rendering/CliOverlayRenderers.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliOverlayRenderers.html`
- `CliRenderBuffer` *(class)* — Low-level dirty-cell console render buffer used by interactive rendering paths.
  Source: `ItTiger.TigerCli/Rendering/CliRenderBuffer.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliRenderBuffer.html`
- `CliRenderableComponent` *(class)* — Base class for renderable components that share layout constraints and convert to a CliGrid for output.
  Source: `ItTiger.TigerCli/Rendering/CliRenderableComponent.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliRenderableComponent.html`
- `CliTable` *(class)* — App-facing convenience API for CliTable: theme-driven defaults plus terse header and record construction.
  Source: `ItTiger.TigerCli/Rendering/CliTable.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliTable.html`
- `CliTableElement` *(class)* — Describes a single table element (column in Vertical, row in Horizontal).
  Source: `ItTiger.TigerCli/Rendering/CliTableElement.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliTableElement.html`
- `CliTableHeader` *(class)* — Represents the header of a CLI table, including visibility and structural elements.
  Source: `ItTiger.TigerCli/Rendering/CliTableHeader.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliTableHeader.html`
- `CliTableStyleRecipe` *(class)* — A colour-free table style "recipe": the structural and role-based definition of a table preset (frame configuration, padding, surface role, and title/frame accent roles).
  Source: `ItTiger.TigerCli/Rendering/CliTableStyleRecipe.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliTableStyleRecipe.html`
- `CliTableStyles` *(class)* — Convenience facade over the predefined table style presets.
  Source: `ItTiger.TigerCli/Rendering/CliTableStyles.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliTableStyles.html`
- `CliTableTitle` *(class)* — Title content and style used by CliTable when materializing a table.
  Source: `ItTiger.TigerCli/Primitives/CliTableTitle.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.CliTableTitle.html`
- `ICliRenderable` *(interface)* — Represents a component that can materialize itself as a CliGrid for measurement and rendering.
  Source: `ItTiger.TigerCli/Rendering/ICliRenderable.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Rendering.ICliRenderable.html`

## ItTiger.TigerCli.Terminal

- `AnsiSink` *(class)* — An ICliRenderSink that writes styled output as ANSI SGR escape sequences to a TextWriter.
  Source: `ItTiger.TigerCli/Terminal/AnsiSink.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Terminal.AnsiSink.html`
- `ConsoleTerminal` *(class)* — Real console-backed terminal implementation.
  Source: `ItTiger.TigerCli/Terminal/ConsoleTerminal.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Terminal.ConsoleTerminal.html`
- `ConsoleTerminalState` *(class)* — Snapshot of real console state captured by ConsoleTerminal so the terminal can restore colors after an interactive render.
  Source: `ItTiger.TigerCli/Terminal/ConsoleTerminalState.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Terminal.ConsoleTerminalState.html`
- `HtmlSink` *(class)* — An ICliRenderSink that renders TigerCli text segments to deterministic HTML — for snapshot tests (internal and external) and for generating documentation examples from real rendering.
  Source: `ItTiger.TigerCli/Terminal/HtmlSink.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Terminal.HtmlSink.html`
- `HtmlSinkOptions` *(class)* — Options for HtmlSink.
  Source: `ItTiger.TigerCli/Terminal/HtmlSinkOptions.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Terminal.HtmlSinkOptions.html`
- `ICliRenderSink` *(interface)* — Sink abstraction used by the render pipeline to write styled text segments.
  Source: `ItTiger.TigerCli/Terminal/ICliRenderSink.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Terminal.ICliRenderSink.html`
- `ICliTerminal` *(interface)* — Terminal abstraction used by interactive rendering and test hosts.
  Source: `ItTiger.TigerCli/Terminal/ICliTerminal.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Terminal.ICliTerminal.html`
- `ITerminalState` *(interface)* — Snapshot marker interface for terminal state captured before an interactive render.
  Source: `ItTiger.TigerCli/Terminal/ITerminalState.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Terminal.ITerminalState.html`
- `TerminalCapabilities` *(class)* — Detects the ANSI capability of a console stream for Auto.
  Source: `ItTiger.TigerCli/Terminal/TerminalCapabilities.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Terminal.TerminalCapabilities.html`
- `TigerConsole` *(class)* — Static entry point for TigerCli console output, markup rendering, structured rendering, themes, colour mode, and test/documentation capture helpers.
  Source: `ItTiger.TigerCli/Terminal/TigerConsole.Themes.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Terminal.TigerConsole.html`

## ItTiger.TigerCli.Testing

- `TigerCliAppRunResult` *(class)* — Result of a TigerCliAppTestHost run: the process exit code the app would have returned, plus the text captured from Out and Error during the run.
  Source: `ItTiger.TigerCli/Testing/TigerCliAppRunResult.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Testing.TigerCliAppRunResult.html`
- `TigerCliAppTestHost` *(class)* — App-level test host for running a TigerCliApp without the real console input path.
  Source: `ItTiger.TigerCli/Testing/TigerCliAppTestHost.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Testing.TigerCliAppTestHost.html`

## ItTiger.TigerCli.Tui

- `ActivityResult<T>` *(struct)* — The rich outcome of a TigerTui activity run: the ActivityOutcome, the exact modal DialogResultKind it was derived from, the produced Value (meaningful only when Completed), and the Exception (set only when Failed).
  Source: `ItTiger.TigerCli/Tui/ActivityResult.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.ActivityResult-1.html`
- `TigerTui` *(class)* — Facade for TigerCli's semi-interactive prompts, message boxes, folder picker, custom dialog hosting, and activity/progress dialogs.
  Source: `ItTiger.TigerCli/Tui/TigerTui.Shell.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.TigerTui.html`
- `TigerTuiResult<T>` *(struct)* — The rich outcome of a TigerCli modal prompt: the exact DialogResultKind the modal loop reported, plus the produced Value (meaningful only when the prompt completed with Ok).
  Source: `ItTiger.TigerCli/Tui/TigerTuiResult.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.TigerTuiResult-1.html`

## ItTiger.TigerCli.Tui.Abstractions

- `DialogBase` *(class)* — Base class for renderable modal dialogs that expose a result and optional payload.
  Source: `ItTiger.TigerCli/Tui/Abstractions/DialogBase.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.DialogBase.html`
- `DialogResult` *(struct)* — Result returned by a modal dialog, preserving the exact result kind and any optional payload.
  Source: `ItTiger.TigerCli/Tui/Abstractions/DialogResult.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.DialogResult.html`
- `FolderEntry` *(struct)* — A single selectable folder row in IFolderBrowser: either a child directory or, at the Windows top level, a drive root.
  Source: `ItTiger.TigerCli/Tui/Abstractions/IFolderBrowser.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.FolderEntry.html`
- `ICliAppShell` *(interface)* — Host abstraction for running TigerCli modal dialogs against a terminal, theme, viewport, and interaction policy.
  Source: `ItTiger.TigerCli/Tui/Abstractions/ICliAppShell.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.ICliAppShell.html`
- `ICliDialog` *(interface)* — Renderable modal dialog contract for controls that can be hosted by an ICliAppShell.
  Source: `ItTiger.TigerCli/Tui/Abstractions/ICliDialog.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.ICliDialog.html`
- `IControl` *(interface)* — Basic modal-control contract used by dialog hosts that dispatch keyboard input to renderable UI.
  Source: `ItTiger.TigerCli/Tui/Abstractions/IControl.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.IControl.html`
- `IFolderBrowser` *(interface)* — Filesystem navigation policy used by InlineFolderSelect.
  Source: `ItTiger.TigerCli/Tui/Abstractions/IFolderBrowser.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.IFolderBrowser.html`
- `IModalLifecycle` *(interface)* — Implemented by a dialog that needs to know the bounds of its modal session — when the semi-interactive modal loop begins and ends hosting it.
  Source: `ItTiger.TigerCli/Tui/Abstractions/IModalLifecycle.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.IModalLifecycle.html`
- `IModalRefreshSource` *(interface)* — Implemented by a dialog that has time-driven content (periodic overlays).
  Source: `ItTiger.TigerCli/Tui/Abstractions/IModalRefreshSource.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.IModalRefreshSource.html`
- `ITheme` *(interface)* — Resolves semantic TigerCli style and surface roles to concrete render styles for TUI controls, markup, and structured rendering.
  Source: `ItTiger.TigerCli/Tui/Abstractions/ITheme.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.ITheme.html`
- `InlineActivityOverlay` *(class)* — Describes a time-varying overlay an inline control exposes to its hosting InlineDialog.
  Source: `ItTiger.TigerCli/Tui/Abstractions/InlineActivityOverlay.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.InlineActivityOverlay.html`
- `InlineControlBase` *(class)* — Base class for dialog-hostable inline controls.
  Source: `ItTiger.TigerCli/Tui/Abstractions/InlineControlBase.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.InlineControlBase.html`
- `InlineDialogWidget` *(class)* — Describes one top-level widget/area an inline control exposes to its hosting InlineDialog.
  Source: `ItTiger.TigerCli/Tui/Abstractions/InlineDialogWidget.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.InlineDialogWidget.html`
- `InlineKeyResult` *(struct)* — The outcome of an inline control/widget handling a key.
  Source: `ItTiger.TigerCli/Tui/Abstractions/InlineKeyResult.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.InlineKeyResult.html`
- `InlineWidget` *(class)* — A reusable interactive building block used inside composite inline controls.
  Source: `ItTiger.TigerCli/Tui/Abstractions/InlineWidget.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.InlineWidget.html`
- `TuiTicker` *(class)* — A time-driven source of overlay content for semi-interactive dialogs.
  Source: `ItTiger.TigerCli/Tui/Abstractions/TuiTicker.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Abstractions.TuiTicker.html`

## ItTiger.TigerCli.Tui.Activity

- `ActivityCellBuilder` *(class)* — Fluent builder for a single cell.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityBuilders.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivityCellBuilder.html`
- `ActivityCellSpec` *(class)* — Immutable placement of one ActivityElement within a row: the anchor Column and how many columns it Spans.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityCellSpec.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivityCellSpec.html`
- `ActivityColumnSpec` *(class)* — Immutable column definition for an ActivityDialogSpec: an optional fixed Width (mutually exclusive with Star), default cell Alignment, and an optional default Style.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityColumnSpec.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivityColumnSpec.html`
- `ActivityContext` *(class)* — The safe, thread-safe surface a background activity operation uses to report progress.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityContext.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivityContext.html`
- `ActivityDialogSpec` *(class)* — Immutable description of a rich activity dialog's live layout: variable columns and rows of cells, with named dynamic rows carrying fixed-length value arrays.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityDialogSpec.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivityDialogSpec.html`
- `ActivityElement` *(class)* — Base type for the renderable element hosted by an ActivityCellSpec.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityElement.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivityElement.html`
- `ActivityProgressBarElement` *(class)* — A progress-bar element bound to row-local values.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityElement.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivityProgressBarElement.html`
- `ActivityRowBuilder` *(class)* — Fluent builder for a single ActivityRowSpec.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityBuilders.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivityRowBuilder.html`
- `ActivityRowSpec` *(class)* — Immutable row definition.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityRowSpec.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivityRowSpec.html`
- `ActivitySpecBuilder` *(class)* — Fluent builder for an ActivityDialogSpec.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityDialogSpec.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivitySpecBuilder.html`
- `ActivitySpinnerSpec` *(class)* — Configures the spinner an activity dialog shows on its top frame.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivitySpinnerSpec.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivitySpinnerSpec.html`
- `ActivityTextCellBuilder` *(class)* — Continues the fluent chain after Text.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityBuilders.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivityTextCellBuilder.html`
- `ActivityTextElement` *(class)* — A single-line text element.
  Source: `ItTiger.TigerCli/Tui/Activity/ActivityElement.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Activity.ActivityTextElement.html`

## ItTiger.TigerCli.Tui.Controls

- `FileSystemFolderBrowser` *(class)* — Default IFolderBrowser backed by IO.
  Source: `ItTiger.TigerCli/Tui/Controls/FileSystemFolderBrowser.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.FileSystemFolderBrowser.html`
- `InlineActivityControl<T>` *(class)* — Rich activity dialog control: a live CliGrid-backed layout (columns, rows, text and progress-bar elements) driven by named dynamic row values, plus a background operation and a single stop button (Cancel or Abort — never both).
  Source: `ItTiger.TigerCli/Tui/Controls/InlineActivityControl.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.InlineActivityControl-1.html`
- `InlineDialog` *(class)* — Modal inline dialog that hosts one InlineControlBase and renders it through the shared CliGrid layout pipeline.
  Source: `ItTiger.TigerCli/Tui/Controls/InlineDialog.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.InlineDialog.html`
- `InlineDialogConfirmationPolicy` *(class)* — Optional, reusable confirmation policy for an InlineDialog.
  Source: `ItTiger.TigerCli/Tui/Controls/InlineDialogConfirmationPolicy.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.InlineDialogConfirmationPolicy.html`
- `InlineFolderSelect` *(class)* — 'Composite folder picker: editable path input, folder list, and OK/Cancel buttons.'
  Source: `ItTiger.TigerCli/Tui/Controls/InlineFolderSelect.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.InlineFolderSelect.html`
- `InlineMessageBoxControl` *(class)* — A message-box style inline control: a block of message text plus a row of buttons.
  Source: `ItTiger.TigerCli/Tui/Controls/InlineMessageBoxControl.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.InlineMessageBoxControl.html`
- `InlineMultiColumnSelect` *(class)* — Dialog-hostable single-selection control over structured, multi-column rows.
  Source: `ItTiger.TigerCli/Tui/Controls/InlineMultiColumnSelect.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.InlineMultiColumnSelect.html`
- `InlineMultiControl` *(class)* — Base class for dialog-hostable inline controls composed from several top-level widgets.
  Source: `ItTiger.TigerCli/Tui/Controls/InlineMultiControl.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.InlineMultiControl.html`
- `InlineMultiControl.InlineMultiControlWidget` *(class)* — Metadata for one widget slot owned by an InlineMultiControl.
  Source: `ItTiger.TigerCli/Tui/Controls/InlineMultiControl.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.InlineMultiControl.InlineMultiControlWidget.html`
- `InlineMultiSelect` *(class)* — Dialog-hostable multi-selection checklist control.
  Source: `ItTiger.TigerCli/Tui/Controls/InlineMultiSelect.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.InlineMultiSelect.html`
- `InlineSelect` *(class)* — Dialog-hostable single-selection list control backed by InlineSelectWidget.
  Source: `ItTiger.TigerCli/Tui/Controls/InlineSelect.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.InlineSelect.html`
- `InlineTextInput` *(class)* — Dialog-hostable single-line text input control backed by InlineTextInputWidget.
  Source: `ItTiger.TigerCli/Tui/Controls/InlineTextInput.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.InlineTextInput.html`
- `SpinnerTicker` *(class)* — A TuiTicker that cycles through a fixed sequence of string frames, wrapping back to the first frame after the last.
  Source: `ItTiger.TigerCli/Tui/Controls/SpinnerTicker.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Controls.SpinnerTicker.html`

## ItTiger.TigerCli.Tui.Selection

- `SelectCell` *(class)* — Immutable content of one cell in a SelectRow: its Text plus optional per-cell overrides for semantic Style, Alignment, and FormattingMode.
  Source: `ItTiger.TigerCli/Tui/Selection/SelectCell.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Selection.SelectCell.html`
- `SelectColumn` *(class)* — Immutable column definition for a multi-column select (see InlineMultiColumnSelect and TigerTui.MultiColumnSelectIndexAsync).
  Source: `ItTiger.TigerCli/Tui/Selection/SelectColumn.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Selection.SelectColumn.html`
- `SelectRow` *(class)* — Immutable row for a multi-column select: an ordered set of Cells (one per column, though a row may supply fewer — missing trailing cells render blank) and an optional IsDisabled flag.
  Source: `ItTiger.TigerCli/Tui/Selection/SelectRow.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Selection.SelectRow.html`

## ItTiger.TigerCli.Tui.Testing

- `TestShell` *(class)* — Test shell that runs the real semi-interactive modal loop against a TestTerminal.
  Source: `ItTiger.TigerCli/Tui/Testing/TestShell.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Testing.TestShell.html`
- `TestTerminal` *(class)* — In-memory terminal implementation intended for tests of semi-interactive TUI flows.
  Source: `ItTiger.TigerCli/Tui/Testing/TestTerminal.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Testing.TestTerminal.html`

## ItTiger.TigerCli.Tui.Themes

- `CliCustomStyle` *(class)* — An app-defined custom semantic style: a named markup token (e.g.
  Source: `ItTiger.TigerCli/Tui/Themes/CliCustomStyle.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Themes.CliCustomStyle.html`
- `DarkTheme` *(class)* — Default dark framework theme: neutral dark background, gray text, cyan accent, and green active selection.
  Source: `ItTiger.TigerCli/Tui/Themes/DarkTheme.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Themes.DarkTheme.html`
- `LightTheme` *(class)* — Light framework theme: dark text on a light background, dark-blue accent and table headers.
  Source: `ItTiger.TigerCli/Tui/Themes/LightTheme.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Themes.LightTheme.html`
- `ThemeBase` *(class)* — Base class for TigerCli themes.
  Source: `ItTiger.TigerCli/Tui/Themes/ThemeBase.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Themes.ThemeBase.html`
- `TigerBlueTheme` *(class)* — Blue-accented framework theme: cyan accent, dark-blue dialog background, white table headers.
  Source: `ItTiger.TigerCli/Tui/Themes/TigerBlueTheme.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Themes.TigerBlueTheme.html`
- `TigerColorAliasRegistry` *(class)* — App-scoped registry of raw colour aliases: application-defined names for concrete CliColor values (e.g.
  Source: `ItTiger.TigerCli/Tui/Themes/TigerColorAliasRegistry.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Themes.TigerColorAliasRegistry.html`
- `TigerCustomStyleRegistry` *(class)* — App-scoped registry of custom semantic styles (see CliCustomStyle).
  Source: `ItTiger.TigerCli/Tui/Themes/TigerCustomStyleRegistry.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Themes.TigerCustomStyleRegistry.html`
- `TigerThemeConfiguration` *(class)* — App-scoped theme, style, and colour-alias policy configured through the app builder's ConfigureThemes block.
  Source: `ItTiger.TigerCli/Tui/Themes/TigerThemeConfiguration.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Themes.TigerThemeConfiguration.html`

## ItTiger.TigerCli.Tui.Widgets

- `InlineButtonGroupWidget` *(class)* — A reusable horizontal row of buttons — the main reusable unit for message-box style button rows ([OK], [Yes] [No], [OK] [Cancel], [Abort] [Retry] [Ignore]).
  Source: `ItTiger.TigerCli/Tui/Widgets/InlineButtonGroupWidget.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Widgets.InlineButtonGroupWidget.html`
- `InlineButtonWidget` *(class)* — A reusable button widget.
  Source: `ItTiger.TigerCli/Tui/Widgets/InlineButtonWidget.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Widgets.InlineButtonWidget.html`
- `InlineMultiColumnSelectWidget` *(class)* — A reusable single-selection list widget whose rows are structured multi-column data rather than one preformatted string.
  Source: `ItTiger.TigerCli/Tui/Widgets/InlineMultiColumnSelectWidget.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Widgets.InlineMultiColumnSelectWidget.html`
- `InlineSelectWidget` *(class)* — A reusable single-selection list widget.
  Source: `ItTiger.TigerCli/Tui/Widgets/InlineSelectWidget.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Widgets.InlineSelectWidget.html`
- `InlineTextInputWidget` *(class)* — A reusable editable single-line text input widget.
  Source: `ItTiger.TigerCli/Tui/Widgets/InlineTextInputWidget.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Widgets.InlineTextInputWidget.html`
- `InlineTextWidget` *(class)* — A reusable read-only text widget.
  Source: `ItTiger.TigerCli/Tui/Widgets/InlineTextWidget.cs`
  API: `docs/api-docfx/_site/api/ItTiger.TigerCli.Tui.Widgets.InlineTextWidget.html`
