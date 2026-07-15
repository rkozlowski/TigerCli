using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Selection;
using ItTiger.TigerCli.Tui.Themes;
using ItTiger.TigerCli.Tui.Windowing;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// A configured TigerCli command application: the immutable result of
/// <see cref="TigerCliAppBuilder.Build"/>. Owns the full run pipeline — culture resolution, theme
/// application, command resolution, parsing, prompting, validation, and handler execution — and
/// maps the outcome to a process exit code through the configured exit-code policy.
/// Create one with <see cref="CreateBuilder"/>; instances cannot be constructed directly.
/// </summary>
public sealed class TigerCliApp
{
    private const string ThemeEnvironmentVariable = "TIGERCLI_THEME";

    private readonly string _applicationName;
    private readonly TigerCliApplicationMetadata _metadata;
    private readonly string? _description;
    private readonly string? _descriptionResourceKey;
    private readonly List<TigerCliCommandRegistration> _namedCommands;
    private readonly List<TigerCliCommandGroupRegistration> _commandGroups;
    private readonly List<TigerCliCommandAliasRegistration> _aliases;
    private readonly TigerCliCommandRegistration? _defaultCommand;
    private readonly TigerCliExitCodePolicy _exitCodePolicy;
    private readonly TigerCliInteractionMode _interactionMode;
    private readonly Dictionary<Type, TigerCliInteractionMode> _commandInteractionModes;
    private readonly TigerCliPromptMode? _promptMode;
    private readonly Dictionary<Type, TigerCliPromptMode> _commandPromptModes;
    private readonly List<ITigerCliValueProvider> _providers;
    private readonly bool _cultureOptionEnabled;
    private readonly CliColorMode _defaultColorMode;
    private readonly CliOutputPresetDefaults? _defaultOutputPresets;
    private readonly ResourceManager? _appResources;
    private readonly TigerThemeConfiguration _themeConfiguration;
    private readonly IFolderBrowser _folderBrowser;
    private readonly bool _processCancellationEnabled;
    private readonly bool _terminalTitleManagementEnabled;
    private readonly bool _spinnerTitlePrefixEnabled;
    private readonly CommandMenuMode _commandMenuMode;

    // Whether TigerCli registers process/system cancellation handlers (Ctrl-C / SIGINT / SIGTERM /
    // SIGQUIT) at run start. Default-on; disabled via TigerCliAppBuilder.DisableProcessCancellation().
    internal bool ProcessCancellationEnabled => _processCancellationEnabled;

    /// <summary>
    /// The UI culture used for framework strings when no <c>--culture</c> override is supplied.
    /// </summary>
    public CultureInfo DefaultCulture { get; }

    /// <summary>
    /// The UI cultures the app accepts via <c>--culture</c>. Always contains
    /// <see cref="DefaultCulture"/>.
    /// </summary>
    public IReadOnlyList<CultureInfo> SupportedCultures { get; }

    /// <summary>
    /// Whether the framework-owned <c>--culture</c> option is exposed in help and parsing.
    /// <c>true</c> unless <see cref="TigerCliAppBuilder.DisableCultureOption"/> was called.
    /// </summary>
    public bool CultureOptionEnabled => _cultureOptionEnabled;

    /// <summary>
    /// Optional app-owned resource manager registered via
    /// <see cref="TigerCliAppBuilder.UseAppResources"/>, used to resolve localized enum text,
    /// explicit metadata resource keys, and <see cref="TigerCliSettings"/> text-helper lookups
    /// against the active run culture. <c>null</c> when not configured.
    /// </summary>
    public ResourceManager? AppResources => _appResources;

    /// <summary>
    /// The single source for app display name, version, copyright, and help-footer links,
    /// assembled from the builder's metadata calls and assembly-metadata defaults.
    /// </summary>
    public TigerCliApplicationMetadata ApplicationMetadata => _metadata;

    internal TigerCliApp(
        string applicationName,
        TigerCliApplicationMetadata metadata,
        string? description,
        string? descriptionResourceKey,
        List<TigerCliCommandRegistration> namedCommands,
        List<TigerCliCommandGroupRegistration> commandGroups,
        Type? defaultHandlerType,
        Func<object>? defaultFactory,
        TigerCliExitCodePolicy exitCodePolicy,
        TigerCliInteractionMode interactionMode,
        Dictionary<Type, TigerCliInteractionMode> commandInteractionModes,
        TigerCliPromptMode? promptMode,
        Dictionary<Type, TigerCliPromptMode> commandPromptModes,
        List<ITigerCliValueProvider> providers,
        CultureInfo defaultCulture,
        IReadOnlyList<CultureInfo> supportedCultures,
        bool cultureOptionEnabled,
        CliColorMode defaultColorMode,
        CliOutputPresetDefaults? defaultOutputPresets,
        ResourceManager? appResources,
        TigerThemeConfiguration themeConfiguration,
        IFolderBrowser? folderBrowser = null,
        bool processCancellationEnabled = true,
        bool terminalTitleManagementEnabled = true,
        bool spinnerTitlePrefixEnabled = true,
        CommandMenuMode commandMenuMode = CommandMenuMode.Disabled,
        bool commandMenuRegistered = false,
        string? commandMenuName = null,
        bool commandMenuIsDefault = false,
        string? commandMenuDescription = null,
        string? commandMenuDescriptionResourceKey = null,
        List<TigerCliCommandAliasRegistration>? aliases = null)
    {
        _processCancellationEnabled = processCancellationEnabled;
        _commandMenuMode = commandMenuMode;
        _terminalTitleManagementEnabled = terminalTitleManagementEnabled;
        _spinnerTitlePrefixEnabled = spinnerTitlePrefixEnabled;
        _description = description;
        _descriptionResourceKey = descriptionResourceKey;
        _applicationName = applicationName;
        _metadata = metadata;
        _namedCommands = namedCommands;
        _commandGroups = commandGroups;
        _exitCodePolicy = exitCodePolicy;
        _interactionMode = interactionMode;
        _commandInteractionModes = new Dictionary<Type, TigerCliInteractionMode>(commandInteractionModes);
        _promptMode = promptMode;
        _commandPromptModes = new Dictionary<Type, TigerCliPromptMode>(commandPromptModes);
        _providers = providers;
        DefaultCulture = defaultCulture;
        SupportedCultures = supportedCultures;
        _cultureOptionEnabled = cultureOptionEnabled;
        _defaultColorMode = defaultColorMode;
        _defaultOutputPresets = defaultOutputPresets;
        _appResources = appResources;
        _themeConfiguration = themeConfiguration;
        _folderBrowser = folderBrowser ?? new FileSystemFolderBrowser();

        // The command menu is registered as a normal command (default or named) using the internal
        // sentinel handler. It is intercepted before execution; it never runs the sentinel handler.
        TigerCliCommandRegistration? commandMenuDefault = null;
        if (commandMenuRegistered)
        {
            var menuRegistration = new TigerCliCommandRegistration(
                commandMenuName,
                commandMenuDescription,
                typeof(CommandMenuCommand),
                commandMenuDescriptionResourceKey,
                isCommandMenu: true);

            if (commandMenuIsDefault)
                commandMenuDefault = menuRegistration;
            else
                _namedCommands.Add(menuRegistration);
        }

        _defaultCommand = defaultHandlerType != null
            ? new TigerCliCommandRegistration(null, null, defaultHandlerType, factory: defaultFactory)
            : commandMenuDefault;

        if (defaultHandlerType != null)
        {
            foreach (var cmd in _namedCommands)
            {
                if (cmd.HandlerType == defaultHandlerType)
                {
                    cmd.IsDefault = true;
                    break;
                }
            }
        }

        // Aliases are validated and linked after the full command tree (including the command-menu
        // sentinel) is known, so conflict detection and target resolution see every command path.
        _aliases = aliases ?? new List<TigerCliCommandAliasRegistration>();
        ValidateAndLinkAliases();
    }

    /// <summary>
    /// Validates each alias against the resolved command tree and links it to its target command.
    /// Throws when an alias path collides with a command path, a group path, or another alias, or
    /// when the target does not name an existing command. Real commands always win over aliases at
    /// resolution time; these checks ensure the two namespaces never overlap.
    /// </summary>
    private void ValidateAndLinkAliases()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var alias in _aliases)
        {
            var aliasPath = string.Join(' ', alias.PathTokens);

            if (!seen.Add(aliasPath))
                throw new InvalidOperationException(
                    $"An alias with the path '{aliasPath}' is already registered.");

            if (_namedCommands.Any(c => c.PathTokens.SequenceEqual(alias.PathTokens, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    $"The alias '{aliasPath}' conflicts with an existing command of the same path.");

            if (_commandGroups.Any(g => g.PathTokens.SequenceEqual(alias.PathTokens, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    $"The alias '{aliasPath}' conflicts with an existing command group of the same path.");

            var target = _namedCommands.FirstOrDefault(c =>
                !c.IsCommandMenu
                && c.PathTokens.SequenceEqual(alias.TargetPathTokens, StringComparer.OrdinalIgnoreCase));
            if (target == null)
            {
                if (_commandGroups.Any(g => g.PathTokens.SequenceEqual(alias.TargetPathTokens, StringComparer.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(
                        $"The alias '{aliasPath}' targets command group '{alias.TargetPath}', but aliases must target a command, not a group.");
                throw new InvalidOperationException(
                    $"The alias '{aliasPath}' targets unknown command '{alias.TargetPath}'.");
            }

            alias.LinkTarget(target);
        }
    }

    /// <summary>
    /// Creates a new <see cref="TigerCliAppBuilder"/> — the starting point for configuring and
    /// building a TigerCli app.
    /// </summary>
    public static TigerCliAppBuilder CreateBuilder() => new();

    private sealed record ProviderEntry(ITigerCliValueProvider Provider, int Scope);

    private sealed class TigerCliEffectiveProviderMap
    {
        public Dictionary<string, ProviderEntry> Named { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<(Type SettingsType, string PropertyName), ProviderEntry> PropertyScoped { get; } = new();
    }

    /// <summary>
    /// Runs the app for the given command-line arguments: resolves the culture and command,
    /// parses and binds settings, prompts for missing values when the effective interaction and
    /// prompt modes allow, validates, executes the handler, and returns the process exit code
    /// resolved through the configured exit-code policy. Framework failures (usage errors,
    /// validation errors, cancellation, unhandled exceptions) are reported and mapped to exit
    /// codes instead of throwing.
    /// </summary>
    /// <param name="args">The raw command-line arguments, typically from <c>Main</c>.</param>
    /// <returns>The process exit code to return from <c>Main</c>.</returns>
    public Task<int> RunAsync(string[] args)
    {
        return RunAsync(args, promptShell: null);
    }

    /// <summary>
    /// Runs the same pipeline as <see cref="RunAsync(string[])"/> with an injected prompt shell,
    /// an optional prompt timeout, and a prompt cancellation token. Hosts and tests inject a
    /// shell (e.g. a test shell) to answer prompts without the real console; when
    /// <paramref name="promptShell"/> is <c>null</c> the default inline shell is used. When a
    /// shell is injected the app does not register process/system cancellation handlers — the
    /// host owns signal handling.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <param name="promptShell">The shell that hosts prompts, or <c>null</c> for the default console shell.</param>
    /// <param name="promptTimeout">Optional timeout applied to individual prompts; <c>null</c> means no timeout.</param>
    /// <param name="ct">Cancellation token observed by prompts and provider callbacks.</param>
    /// <returns>The process exit code to return from <c>Main</c>.</returns>
    public async Task<int> RunAsync(
        string[] args,
        ICliAppShell? promptShell,
        TimeSpan? promptTimeout = default,
        CancellationToken ct = default)
    {
        // 0. Resolve culture (before any help/error rendering).
        var cultureResolution = ResolveRunCulture(args);
        var culture = cultureResolution.Culture;

        if (!cultureResolution.Success)
        {
            var supportedList = string.Join(", ", SupportedCultures.Select(c => c.Name));
            WriteFrameworkError(culture, TigerCliResources.Format(
                "Error_UnsupportedCulture", culture,
                Esc(cultureResolution.RequestedRaw ?? string.Empty),
                Esc(supportedList)));
            return _exitCodePolicy.Resolve(TigerCliExitKind.InvalidArguments);
        }

        // 0a-theme. Apply the app's theme/style/colour-alias policy to the active console appearance
        // before any theme resolution or output rendering. This registers the app's custom themes and
        // makes its colour aliases and custom styles active for the run.
        ApplyThemeConfiguration();

        // 0b. Resolve the framework --theme option or environment default (before any help/output
        // rendering) so the selected theme applies to the run's themed output (e.g. CliTable).
        var themeResolution = ResolveRunTheme(args);
        if (!themeResolution.Success)
        {
            var supportedThemes = string.Join(", ", GetEnabledThemeNames());
            var errorResourceKey = themeResolution.Source == ThemeResolutionSource.Environment
                ? "Error_UnsupportedThemeEnvironment"
                : "Error_UnsupportedTheme";
            WriteFrameworkError(culture, TigerCliResources.Format(
                errorResourceKey, culture,
                Esc(themeResolution.RequestedRaw ?? string.Empty),
                Esc(supportedThemes)));
            return _exitCodePolicy.Resolve(TigerCliExitKind.InvalidArguments);
        }
        if (themeResolution.Theme != null)
            TigerConsole.CurrentTheme = themeResolution.Theme;

        // 0c. Resolve the framework --color / --no-color option and apply it to the process-global
        // colour mode before any output rendering. CLI wins over the app's configured default. The
        // framework only claims --color when the value is a recognized mode (auto|never|16|256), so
        // an app may still define its own --color option for other values.
        TigerConsole.ColorMode = ResolveRunColorMode(args);

        using var outputSinkScope = TigerConsole.EnsureOutputSinkScope(out var outputSink);
        using var outputPresetScope = CliOutputPresetContext.Push(_defaultOutputPresets);
        var titleSession = _terminalTitleManagementEnabled
            ? new TerminalTitleSession(
                outputSink,
                enabled: true,
                spinnerPrefixEnabled: _spinnerTitlePrefixEnabled)
            : null;
        using var titleScope = TerminalTitleScope.Push(titleSession);
        var appTitle = GetApplicationDisplayName();
        titleSession?.SetBaseTitle(appTitle);

        if (_metadata.VersionEnabled && args.Any(a => a is "--version" or "--version-full"))
        {
            PrintVersion(culture, showVersion: args.Any(a => a is "--version"), showProductVersion: args.Any(a => a is "--version-full"));
            return _exitCodePolicy.Resolve(TigerCliExitKind.Success);
        }

        var nonInteractiveRequested = args.Any(a => a is "--non-interactive");
        var frameworkArgs = StripFrameworkOptions(args);

        // 1. Resolve command from first positional token. When no leaf command
        // matches, a matched group prefix gives us the group context for help.
        var (command, matchedAlias, remainingArgs) = ResolveCommand(frameworkArgs);
        var matchedNamedCommand = matchedAlias != null || command is { Name: not null };
        var group = matchedNamedCommand ? null : ResolveGroup(frameworkArgs);
        var effectiveCommand = command ?? _defaultCommand;
        if (effectiveCommand != null)
            titleSession?.SetBaseTitle(ResolveCommandTitle(appTitle, effectiveCommand));

        // 2. Pre-scan for help anywhere in args
        var showHelp = frameworkArgs.Any(a => a is "-h" or "--help");
        var showExitCodeHelp = frameworkArgs.Any(a => a is "--help-errors");
        if (showHelp)
        {
            if (group != null)
                PrintGroupHelp(group, culture);
            else
                PrintHelp(effectiveCommand, culture, matchedAlias);
            if (showExitCodeHelp)
                PrintExitCodeHelp(effectiveCommand, leadingBlankLine: true, culture);
            return _exitCodePolicy.Resolve(TigerCliExitKind.HelpShown);
        }

        if (showExitCodeHelp)
        {
            PrintExitCodeHelp(effectiveCommand, leadingBlankLine: false, culture);
            return _exitCodePolicy.Resolve(TigerCliExitKind.HelpShown);
        }

        // 3a. A matched group prefix with no leaf subcommand shows the group's help.
        if (group != null)
        {
            PrintGroupHelp(group, culture);
            return _exitCodePolicy.Resolve(TigerCliExitKind.NoCommand);
        }

        // 3b. No command resolved and no default → show help
        if (effectiveCommand == null)
        {
            PrintHelp(null, culture);
            return _exitCodePolicy.Resolve(TigerCliExitKind.NoCommand);
        }

        if (!TryResolveInteractionMode(effectiveCommand, nonInteractiveRequested, out var effectiveInteractionMode))
        {
            WriteFrameworkError(
                culture,
                TigerCliResources.Get("Error_NonInteractiveWithFullInteractive", culture));
            return _exitCodePolicy.Resolve(TigerCliExitKind.InteractiveNotAllowed);
        }

        // Process/system cancellation handling (Ctrl-C / SIGINT / SIGTERM / SIGQUIT). Registered once at
        // run start unless opted out, and only for the real-console prompt path (no injected promptShell)
        // and an interactive mode — a host that injects its own shell owns signal handling, and a fully
        // non-interactive run never prompts. The lifetime's token is published through SystemCancellationScope
        // (an AsyncLocal on a neutral type, so publishing it never eagerly initializes the console-backed
        // InlineShell singleton) and stays isolated across parallel runs. Torn down in the finally.
        TigerCliLifetime? lifetime = null;
        TigerCliProcessCancellation? processCancellation = null;
        var previousAmbientSystemCancellation = SystemCancellationScope.Current;

        // Publish the run's effective interaction mode so no-shell TigerTui calls inside the command
        // handler (which run on the InlineShell singleton) observe --non-interactive. In non-interactive
        // mode this drives the headless activity path and makes prompts return InteractionNotAllowed.
        // Restored in the finally so nested/sequential runs never observe a stale mode.
        var previousAmbientInteractionMode = InteractionModeScope.Current;
        InteractionModeScope.Current = effectiveInteractionMode;

        var processCancellationActive =
            _processCancellationEnabled
            && promptShell == null
            && effectiveInteractionMode != TigerCliInteractionMode.NonInteractive;
        if (processCancellationActive)
        {
            lifetime = new TigerCliLifetime();
            processCancellation = TigerCliProcessCancellation.Register(lifetime);
            SystemCancellationScope.Current = lifetime.SystemCancellation;
        }

        try
        {
        // 3.5 Command menu: when the resolved command is the menu sentinel, present the picker and
        // replace the effective command with the user's choice. The chosen command then flows
        // through the normal pipeline below with empty remaining args, so prompting collects every
        // missing value. The menu itself never executes a handler.
        if (effectiveCommand.IsCommandMenu)
        {
            if (effectiveInteractionMode != TigerCliInteractionMode.SemiInteractive)
            {
                WriteFrameworkError(
                    culture, TigerCliResources.Get("Error_CommandMenuRequiresInteractive", culture));
                return _exitCodePolicy.Resolve(TigerCliExitKind.InteractiveNotAllowed);
            }

            AdoptSingletonCultureIfNoShell(promptShell, culture);
            var menuShell = promptShell ?? InlineShell.Instance;
            var menuSelection = await RunCommandMenuAsync(menuShell, promptTimeout, ct, culture)
                .ConfigureAwait(false);

            if (menuSelection.IsEmpty)
            {
                TigerConsole.MarkupLine($"  {Esc(L("CommandMenu_Empty", culture))}");
                return _exitCodePolicy.Resolve(TigerCliExitKind.NoCommand);
            }

            if (menuSelection.Command == null)
                // Escape / cancel / timeout / system-cancel — no command handler runs.
                return _exitCodePolicy.Resolve(TigerCliExitKind.Success);

            effectiveCommand = menuSelection.Command;
            remainingArgs = [];
            titleSession?.SetBaseTitle(ResolveCommandTitle(appTitle, effectiveCommand));
        }

        var effectivePromptMode = ResolvePromptMode(effectiveCommand);
        var effectiveProviders = BuildEffectiveProviderMap(effectiveCommand);

        // 4. Build argument and option metadata from settings type
        var argumentMeta = BuildArgumentMetadata(effectiveCommand.SettingsType);
        var optionMeta = BuildOptionMetadata(effectiveCommand.SettingsType);

        // 5. Parse remaining args as positionals followed by options
        var parseResult = ParseArgumentsAndOptions(remainingArgs, argumentMeta, optionMeta, effectiveCommand);

        if (parseResult.ErrorResourceKey != null)
        {
            WriteFrameworkError(culture, TigerCliResources.Format(
                parseResult.ErrorResourceKey, culture, parseResult.ErrorArgs ?? Array.Empty<object>()));
            return _exitCodePolicy.Resolve(parseResult.ErrorExitKind);
        }

        // 6. Validate and bind settings
        var conversionError = ValidateScalarConversions(argumentMeta, optionMeta, parseResult, culture);
        if (conversionError != null)
        {
            WriteFrameworkError(culture, conversionError);
            return _exitCodePolicy.Resolve(TigerCliExitKind.InvalidArguments);
        }

        var commandLinePolicyValidation = ValidateCommandLineValuePolicy(optionMeta, parseResult, culture);
        if (!commandLinePolicyValidation.IsValid)
        {
            WriteFrameworkError(culture, Esc(commandLinePolicyValidation.ErrorMessage!));
            return _exitCodePolicy.Resolve(TigerCliExitKind.ValidationError);
        }

        var settings = BindSettings(effectiveCommand.SettingsType, argumentMeta, optionMeta, parseResult);
        settings.InteractionMode = effectiveInteractionMode;
        settings.Culture = culture;
        settings.AppResources = _appResources;

        // 6b. Edit-only: load the existing object and merge its values into the bound
        // settings for properties not supplied on the command line. Runs in both
        // interaction modes so partial non-interactive edits keep existing values.
        var editMode = effectiveCommand.IsEdit;
        if (editMode)
        {
            // 6a. Resolve every missing positional selector argument before the loader runs.
            // Each missing argument is prompted (when promptable in the current interaction
            // mode) or rejected with the standard missing-argument error. The loader is never
            // called with a missing/empty required selector, and missing selectors are never
            // seeded from the existing object (it has not been loaded yet).
            var selectorResult = await ResolveMissingSelectorArgumentsAsync(
                settings,
                argumentMeta,
                optionMeta,
                parseResult,
                effectiveInteractionMode,
                effectivePromptMode,
                promptShell,
                promptTimeout,
                ct,
                effectiveProviders,
                culture,
                _appResources).ConfigureAwait(false);
            if (TryResolvePromptOutcome(selectorResult, culture, out var selectorExit))
                return selectorExit;

            var editResult = await effectiveCommand.EditLoader!(settings).ConfigureAwait(false);
            if (!editResult.IsFound)
            {
                var selector = string.Join(' ', parseResult.ArgumentValues.Values);
                if (string.IsNullOrWhiteSpace(selector))
                    selector = effectiveCommand.Name ?? string.Empty;
                WriteFrameworkError(culture, TigerCliResources.Format(
                    "Error_EditTargetNotFound", culture, Esc(selector)));
                return _exitCodePolicy.Resolve(TigerCliExitKind.InvalidArguments);
            }

            MergeExistingValues(settings, editResult.Existing!, optionMeta, parseResult);
        }

        // Snapshot which fields were supplied on the command line (and, in edit mode, seeded
        // from the existing object) before prompting. Provider validation uses this to skip
        // fields whose value was chosen from a provider during prompting — those are valid by
        // construction — while still validating command-line, existing, and default values.
        var prePromptOptions = new HashSet<TigerCliOptionMetadata>(parseResult.OptionValues.Keys);
        var prePromptArguments = new HashSet<TigerCliArgumentMetadata>(parseResult.ArgumentValues.Keys);

        // 7. Prompt for missing values when policy and interaction mode allow it.
        // Culture is threaded down to the prompt helpers; the InlineShell singleton is
        // only touched right before a no-shell TigerTui call actually happens, to keep
        // the singleton's lazy initialization (which needs a real console) out of
        // non-prompt paths.
        var promptResult = await ResolveMissingPromptableValuesAsync(
            settings,
            argumentMeta,
            optionMeta,
            parseResult,
            effectiveInteractionMode,
            effectivePromptMode,
            promptShell,
            promptTimeout,
            ct,
            effectiveProviders,
            culture,
            _appResources,
            editMode,
            _folderBrowser).ConfigureAwait(false);

        if (TryResolvePromptOutcome(promptResult, culture, out var promptExit))
            return promptExit;

        // 7b. Resolve [TigerCliMultiSelect] options: validate/bind command-line lists and, when
        // interactive, prompt missing ones with the inline checklist. Runs after the generic prompt
        // stage (which skips multi-select) and before required/framework validation.
        var multiSelectResult = await ResolveMultiSelectOptionsAsync(
            settings,
            argumentMeta,
            optionMeta,
            parseResult,
            effectiveInteractionMode,
            effectivePromptMode,
            promptShell,
            promptTimeout,
            ct,
            effectiveProviders,
            culture,
            _appResources,
            editMode).ConfigureAwait(false);
        if (TryResolvePromptOutcome(multiSelectResult, culture, out var multiSelectExit))
            return multiSelectExit;

        var missingArgument = argumentMeta.FirstOrDefault(arg => !parseResult.ArgumentValues.ContainsKey(arg));
        if (missingArgument != null)
        {
            WriteFrameworkError(culture, TigerCliResources.Format(
                "Error_MissingArgument", culture, Esc(missingArgument.DisplayName)));
            return _exitCodePolicy.Resolve(TigerCliExitKind.MissingRequiredArgument);
        }

        // 8. Framework-level validation (required options, forbidden values)
        var frameworkValidation = ValidateFrameworkRules(argumentMeta, optionMeta, parseResult, settings, culture);
        if (!frameworkValidation.IsValid)
        {
            WriteFrameworkError(culture, Esc(frameworkValidation.ErrorMessage!));
            return _exitCodePolicy.Resolve(TigerCliExitKind.ValidationError);
        }

        var integerBoundsValidation = await ValidateIntegerBoundsAsync(
            settings,
            argumentMeta,
            optionMeta,
            parseResult,
            effectiveProviders,
            promptTimeout,
            ct,
            culture).ConfigureAwait(false);
        if (TryResolvePromptOutcome(integerBoundsValidation, culture, out var integerBoundsExit))
            return integerBoundsExit;

        // 8b. Provider validation: provider-backed fields are validated against their
        // provider's current choices (editable options; arguments with an explicit
        // provider in normal commands, editable arguments in edit mode). Applies in add
        // and edit modes, interactive and non-interactive. Runs after required-field
        // validation so a missing required field reports first without calling the provider.
        var providerValidation = await ValidateProviderBackedValuesAsync(
            settings,
            argumentMeta,
            optionMeta,
            parseResult,
            prePromptOptions,
            prePromptArguments,
            effectiveProviders,
            promptTimeout,
            ct,
            culture,
            editMode).ConfigureAwait(false);
        if (TryResolvePromptOutcome(providerValidation, culture, out var providerExit))
            return providerExit;

        // 9. User-defined validation
        var validation = settings.Validate();
        if (!validation.IsValid)
        {
            WriteFrameworkError(culture, TigerCliResources.Format(
                "Error_ValidationWrapper", culture, Esc(validation.ErrorMessage!)));
            return _exitCodePolicy.Resolve(TigerCliExitKind.ValidationError);
        }

        // 10. Execute handler
        try
        {
            return await ExecuteHandler(effectiveCommand, settings);
        }
        catch (TigerCliCommandException ex)
        {
            // A classified command failure (typically from a reusable command library): report the
            // handler's own message (with its stable error id when present) and resolve the exit code
            // from the thrown kind through the app's policy instead of UnhandledException.
            var message = ex.ErrorId == null
                ? Esc(ex.Message)
                : TigerCliResources.Format("Error_CommandFailedWithId", culture, Esc(ex.Message), Esc(ex.ErrorId));
            WriteFrameworkError(culture, message);
            return _exitCodePolicy.Resolve(ex.ExitKind);
        }
        catch (Exception ex)
        {
            WriteFrameworkError(culture, Esc(GetUserFacingExceptionMessage(ex)));
            return _exitCodePolicy.Resolve(TigerCliExitKind.UnhandledException);
        }
        }
        finally
        {
            // Restore the prior ambient (so nested/sequential runs don't observe a stale token) and
            // unregister the OS signal handlers for this run.
            if (processCancellationActive)
                SystemCancellationScope.Current = previousAmbientSystemCancellation;
            InteractionModeScope.Current = previousAmbientInteractionMode;
            processCancellation?.Dispose();
            lifetime?.Dispose();
        }
    }

    private static void WriteFrameworkError(CultureInfo culture, string messageMarkup)
    {
        TigerConsole.MarkupErrorLine(
            $"[Error]{Esc(TigerCliResources.Get("Error_Prefix", culture))}[/] {messageMarkup}");
    }

    /// <summary>
    /// Writes the gentle prompt-cancellation notice: a muted, single-line message with no error prefix
    /// and no error styling. Cancellation is a normal flow, so it must not read like a validation or
    /// usage error. Goes to stderr (like other framework diagnostics) so stdout stays script-clean.
    /// </summary>
    private static void WriteCancellationNotice(CultureInfo culture)
    {
        TigerConsole.MarkupErrorLine($"[Muted]{Esc(TigerCliResources.Get("Prompt_Cancelled", culture))}[/]");
    }

    /// <summary>
    /// Renders and maps a terminal <see cref="PromptResolutionResult"/> (from any prompt/resolve/validate
    /// stage). A user cancellation gets the gentle notice and the <see cref="TigerCliExitKind.Cancelled"/>
    /// mapping; a genuine failure keeps the error prefix and its own kind. Returns <c>true</c> (with the
    /// resolved exit code) when the result is terminal, <c>false</c> when the pipeline should continue.
    /// </summary>
    private bool TryResolvePromptOutcome(PromptResolutionResult result, CultureInfo culture, out int exitCode)
    {
        if (result.IsUserCancellation)
        {
            WriteCancellationNotice(culture);
            exitCode = _exitCodePolicy.Resolve(TigerCliExitKind.Cancelled);
            return true;
        }

        if (result.ErrorResourceKey != null)
        {
            var message = result.ErrorIsLiteral
                ? result.ErrorResourceKey
                : TigerCliResources.Format(
                    result.ErrorResourceKey, culture, result.ErrorArgs ?? Array.Empty<object>());
            WriteFrameworkError(culture, message);
            exitCode = _exitCodePolicy.Resolve(result.ExitKind);
            return true;
        }

        exitCode = 0;
        return false;
    }

    // ── Shell culture plumbing ──────────────────────────────────────

    /// <summary>
    /// When <paramref name="shell"/> is null, sets the InlineShell singleton's culture
    /// so that its built-in localized labels (Yes/No, MultiSelect hint, empty state)
    /// match the run's resolved culture. Touching <see cref="InlineShell.Instance"/>
    /// requires a real console, so callers must invoke this only on the prompt path.
    /// </summary>
    private static void AdoptSingletonCultureIfNoShell(ICliAppShell? shell, CultureInfo culture)
    {
        if (shell != null)
            return;
        InlineShell.Instance.SetCulture(culture);
    }

    // ── Command resolution ──────────────────────────────────────────

    private (TigerCliCommandRegistration? Command, TigerCliCommandAliasRegistration? Alias, string[] RemainingArgs) ResolveCommand(string[] args)
    {
        var match = _namedCommands
            .Where(c => c.PathTokens.Length > 0 && IsPathMatch(args, c.PathTokens))
            .OrderByDescending(c => c.PathTokens.Length)
            .FirstOrDefault();

        if (match != null)
            return (match, null, args[match.PathTokens.Length..]);

        // Commands resolve first; aliases are consulted only when no command path matches. A matched
        // alias resolves to its target command, so the rest of the pipeline runs the target unchanged.
        var aliasMatch = _aliases
            .Where(a => a.PathTokens.Length > 0 && IsPathMatch(args, a.PathTokens))
            .OrderByDescending(a => a.PathTokens.Length)
            .FirstOrDefault();

        if (aliasMatch != null)
            return (aliasMatch.Target, aliasMatch, args[aliasMatch.PathTokens.Length..]);

        return (_defaultCommand, null, args);
    }

    private TigerCliCommandGroupRegistration? ResolveGroup(string[] args)
    {
        return _commandGroups
            .Where(g => g.PathTokens.Length > 0 && IsPathMatch(args, g.PathTokens))
            .OrderByDescending(g => g.PathTokens.Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// A group is top-level when no other registered group is its immediate parent (its path
    /// minus the last token). Legacy multi-token group names with no registered parent stay
    /// top-level, so they still surface directly in top-level help and the menu.
    /// </summary>
    private bool IsTopLevelGroup(TigerCliCommandGroupRegistration group)
    {
        if (group.PathTokens.Length <= 1)
            return true;

        var parentTokens = group.PathTokens[..^1];
        return !_commandGroups.Any(g => g.PathTokens.SequenceEqual(parentTokens, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Registered groups whose path is exactly one token deeper than <paramref name="group"/>.</summary>
    private List<TigerCliCommandGroupRegistration> GetImmediateSubgroups(TigerCliCommandGroupRegistration group)
    {
        return _commandGroups
            .Where(g => g.PathTokens.Length == group.PathTokens.Length + 1
                && IsPathMatch(g.PathTokens, group.PathTokens))
            .ToList();
    }

    /// <summary>Commands whose path is exactly one token deeper than <paramref name="group"/>.</summary>
    private List<TigerCliCommandRegistration> GetImmediateGroupCommands(TigerCliCommandGroupRegistration group)
    {
        return _namedCommands
            .Where(cmd => cmd.PathTokens.Length == group.PathTokens.Length + 1
                && IsPathMatch(cmd.PathTokens, group.PathTokens))
            .ToList();
    }

    // ── Command menu ─────────────────────────────────────────────────

    private readonly record struct CommandMenuSelection(TigerCliCommandRegistration? Command, bool IsEmpty);

    /// <summary>
    /// Whether a command is eligible for the menu, resolving its app → group → command
    /// <see cref="CommandMenuMode"/> chain. The sentinel menu command is never eligible.
    /// </summary>
    private bool IsCommandMenuEligible(TigerCliCommandRegistration command)
    {
        if (command.IsCommandMenu)
            return false;

        return CommandMenuEligibility.IsEligible(
            _commandMenuMode,
            command.GroupCommandMenuMode ?? CommandMenuMode.Inherit,
            command.CommandMenuMode);
    }

    /// <summary>
    /// Whether an alias is eligible for the menu. An alias has its own chain (app level → alias) and
    /// does not inherit the target command's eligibility, so a target hidden from the menu can still
    /// be reached through a visible alias.
    /// </summary>
    private bool IsAliasCommandMenuEligible(TigerCliCommandAliasRegistration alias)
    {
        return CommandMenuEligibility.IsEligible(_commandMenuMode, alias.CommandMenuMode);
    }

    /// <summary>Menu-eligible commands that sit directly under <paramref name="group"/> (one token deeper).</summary>
    private List<TigerCliCommandRegistration> GetEligibleImmediateGroupCommands(TigerCliCommandGroupRegistration group)
    {
        return _namedCommands
            .Where(c => c.IsGroupChild
                && c.PathTokens.Length == group.PathTokens.Length + 1
                && IsPathMatch(c.PathTokens, group.PathTokens)
                && IsCommandMenuEligible(c))
            .ToList();
    }

    /// <summary>
    /// Whether <paramref name="group"/> contains at least one menu-eligible command anywhere in its
    /// subtree. Used to hide empty groups (and empty subgroups) from the menu.
    /// </summary>
    private bool GroupHasEligibleContent(TigerCliCommandGroupRegistration group)
    {
        return _namedCommands.Any(c => c.IsGroupChild
            && c.PathTokens.Length > group.PathTokens.Length
            && IsPathMatch(c.PathTokens, group.PathTokens)
            && IsCommandMenuEligible(c));
    }

    /// <summary>
    /// Presents the command menu and returns the chosen command, or a cancel/empty result. Top
    /// level lists eligible ungrouped commands plus groups that contain at least one eligible
    /// child; selecting a group opens a submenu (Escape returns to the top). The menu only selects
    /// a command — execution stays on the normal pipeline.
    /// </summary>
    private async Task<CommandMenuSelection> RunCommandMenuAsync(
        ICliAppShell shell,
        TimeSpan? timeout,
        CancellationToken ct,
        CultureInfo culture)
    {
        var topCommands = _namedCommands
            .Where(c => !c.IsGroupChild && IsCommandMenuEligible(c))
            .ToList();
        var menuAliases = _aliases
            .Where(IsAliasCommandMenuEligible)
            .ToList();
        var groups = _commandGroups
            .Where(g => IsTopLevelGroup(g) && GroupHasEligibleContent(g))
            .ToList();

        if (topCommands.Count == 0 && menuAliases.Count == 0 && groups.Count == 0)
            return new CommandMenuSelection(null, IsEmpty: true);

        var title = L("CommandMenu_Title", culture);
        var columns = BuildCommandMenuColumns();

        while (true)
        {
            var rows = new List<SelectRow>(topCommands.Count + menuAliases.Count + groups.Count);
            foreach (var command in topCommands)
                rows.Add(BuildCommandMenuRow(
                    command.Name!, command.Description, command.DescriptionResourceKey,
                    marker: string.Empty, culture));
            foreach (var menuAlias in menuAliases)
                rows.Add(BuildAliasMenuRow(menuAlias, culture));
            foreach (var menuGroup in groups)
                rows.Add(BuildCommandMenuRow(
                    menuGroup.Name, menuGroup.Description, menuGroup.DescriptionResourceKey,
                    marker: L("CommandMenu_GroupMarker", culture), culture));

            var picked = await TigerTui.MultiColumnSelectIndexResultAsync(
                shell, title, columns, rows, preselectIndex: null, timeout, ct).ConfigureAwait(false);

            if (!picked.IsOk)
                return new CommandMenuSelection(null, IsEmpty: false);

            if (picked.Value < topCommands.Count)
                return new CommandMenuSelection(topCommands[picked.Value], IsEmpty: false);

            // An alias selection runs its target command through the normal pipeline.
            if (picked.Value < topCommands.Count + menuAliases.Count)
                return new CommandMenuSelection(
                    menuAliases[picked.Value - topCommands.Count].Target, IsEmpty: false);

            var selectedGroup = groups[picked.Value - topCommands.Count - menuAliases.Count];
            var childPick = await RunCommandMenuGroupAsync(shell, selectedGroup, timeout, ct, culture)
                .ConfigureAwait(false);
            if (childPick != null)
                return new CommandMenuSelection(childPick, IsEmpty: false);
            // Escape inside the submenu falls back to the top-level menu.
        }
    }

    private async Task<TigerCliCommandRegistration?> RunCommandMenuGroupAsync(
        ICliAppShell shell,
        TigerCliCommandGroupRegistration group,
        TimeSpan? timeout,
        CancellationToken ct,
        CultureInfo culture)
    {
        // A group submenu lists its immediate commands and its immediate non-empty subgroups.
        // Selecting a subgroup opens its own submenu; Escape there returns to this level.
        var columns = BuildCommandMenuColumns();

        while (true)
        {
            var commands = GetEligibleImmediateGroupCommands(group);
            var subgroups = GetImmediateSubgroups(group)
                .Where(GroupHasEligibleContent)
                .ToList();

            var rows = new List<SelectRow>(commands.Count + subgroups.Count);
            foreach (var command in commands)
                rows.Add(BuildCommandMenuRow(
                    GroupRelativeName(command.PathTokens, group), command.Description,
                    command.DescriptionResourceKey, marker: string.Empty, culture));
            foreach (var subgroup in subgroups)
                rows.Add(BuildCommandMenuRow(
                    GroupRelativeName(subgroup.PathTokens, group), subgroup.Description,
                    subgroup.DescriptionResourceKey, marker: L("CommandMenu_GroupMarker", culture), culture));

            var picked = await TigerTui.MultiColumnSelectIndexResultAsync(
                shell, Esc(group.Name), columns, rows, preselectIndex: null, timeout, ct).ConfigureAwait(false);

            if (!picked.IsOk)
                return null;

            if (picked.Value < commands.Count)
                return commands[picked.Value];

            var selectedSubgroup = subgroups[picked.Value - commands.Count];
            var childPick = await RunCommandMenuGroupAsync(shell, selectedSubgroup, timeout, ct, culture)
                .ConfigureAwait(false);
            if (childPick != null)
                return childPick;
            // Escape inside the subgroup submenu falls back to this menu.
        }
    }

    private static string GroupRelativeName(string[] pathTokens, TigerCliCommandGroupRegistration group) =>
        string.Join(' ', pathTokens[group.PathTokens.Length..]);

    /// <summary>
    /// The three-column layout shared by the top-level menu and every group submenu: a left-aligned
    /// name/key column, a left-aligned star (fill) description column so a selected row's highlight spans
    /// the list, and a right-aligned muted marker/alias column that reserves its own width so it never
    /// disturbs the description alignment.
    /// </summary>
    private static IReadOnlyList<SelectColumn> BuildCommandMenuColumns() =>
    [
        new SelectColumn(sizing: CliColumnSizing.Auto, alignment: CliTextAlignment.Left),
        new SelectColumn(sizing: CliColumnSizing.Star, alignment: CliTextAlignment.Left),
        new SelectColumn(sizing: CliColumnSizing.Auto, alignment: CliTextAlignment.Right, style: ThemeStyle.MutedText),
    ];

    /// <summary>
    /// Builds a structured menu row: display name (literal), resolved description (trusted markup), and an
    /// optional right-aligned marker. The marker is a muted group indicator (<c>›</c>) or empty; the row's
    /// index maps back to the command/group the caller enumerated.
    /// </summary>
    private SelectRow BuildCommandMenuRow(
        string name,
        string? description,
        string? descriptionResourceKey,
        string marker,
        CultureInfo culture)
    {
        var resolvedDescription =
            TigerCliAppText.Resolve(description, descriptionResourceKey, culture, _appResources) ?? string.Empty;
        return new SelectRow(
            new SelectCell(name, formattingMode: CliFormattingMode.Raw),
            new SelectCell(resolvedDescription, formattingMode: CliFormattingMode.Preformatted),
            new SelectCell(marker, formattingMode: CliFormattingMode.Raw));
    }

    /// <summary>
    /// Builds a structured menu row for an alias: the alias name, its description (falling back to the
    /// target command's), and a right-aligned muted marker naming the target command path.
    /// </summary>
    private SelectRow BuildAliasMenuRow(TigerCliCommandAliasRegistration alias, CultureInfo culture)
    {
        var resolvedDescription =
            TigerCliAppText.Resolve(alias.Description, alias.DescriptionResourceKey, culture, _appResources)
            ?? TigerCliAppText.Resolve(
                alias.Target.Description, alias.Target.DescriptionResourceKey, culture, _appResources)
            ?? string.Empty;
        var marker = TigerCliResources.Format("CommandMenu_AliasMarker", culture, alias.TargetPath);
        return new SelectRow(
            new SelectCell(alias.Name, formattingMode: CliFormattingMode.Raw),
            new SelectCell(resolvedDescription, formattingMode: CliFormattingMode.Preformatted),
            new SelectCell(marker, formattingMode: CliFormattingMode.Raw));
    }

    private string[] StripFrameworkOptions(string[] args)
    {
        var result = new List<string>(args.Length);
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--non-interactive")
                continue;

            if (arg == "--theme")
            {
                // Consume the following value if present.
                if (i + 1 < args.Length)
                    i++;
                continue;
            }
            if (arg.StartsWith("--theme=", StringComparison.Ordinal))
                continue;

            if (arg == "--no-color")
                continue;
            // Only strip --color when its value is a recognized mode; otherwise leave it for the
            // app (which may define its own --color option).
            if (arg == "--color" && i + 1 < args.Length && TryParseColorMode(args[i + 1], out _))
            {
                i++; // consume the recognized mode value
                continue;
            }
            if (arg.StartsWith("--color=", StringComparison.Ordinal)
                && TryParseColorMode(arg["--color=".Length..], out _))
                continue;

            if (_cultureOptionEnabled)
            {
                if (arg == "--culture")
                {
                    // Consume the following value if present.
                    if (i + 1 < args.Length)
                        i++;
                    continue;
                }
                if (arg.StartsWith("--culture=", StringComparison.Ordinal))
                    continue;
            }

            result.Add(arg);
        }
        return result.ToArray();
    }

    private readonly record struct CultureResolution(
        CultureInfo Culture,
        bool Success,
        string? RequestedRaw);

    private CultureResolution ResolveRunCulture(string[] args)
    {
        if (!_cultureOptionEnabled)
            return new CultureResolution(DefaultCulture, Success: true, RequestedRaw: null);

        string? requested = null;
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--culture")
            {
                requested = i + 1 < args.Length ? args[i + 1] : string.Empty;
                break;
            }
            if (arg.StartsWith("--culture=", StringComparison.Ordinal))
            {
                requested = arg["--culture=".Length..];
                break;
            }
        }

        if (requested == null)
            return new CultureResolution(DefaultCulture, Success: true, RequestedRaw: null);

        var match = SupportedCultures.FirstOrDefault(
            c => string.Equals(c.Name, requested, StringComparison.OrdinalIgnoreCase));

        return match != null
            ? new CultureResolution(match, Success: true, RequestedRaw: requested)
            : new CultureResolution(DefaultCulture, Success: false, RequestedRaw: requested);
    }

    /// <summary>
    /// Resolves the framework <c>--color auto|never|16|256</c> option and the <c>--no-color</c>
    /// alias (equivalent to <c>--color never</c>). An explicit option wins over the app's configured
    /// default. <c>--color</c> is only claimed when its value is a recognized mode, so an app may
    /// still define its own <c>--color</c> option for other values. When no framework colour option
    /// is present, the app default is used.
    /// </summary>
    private CliColorMode ResolveRunColorMode(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--no-color")
                return CliColorMode.Never;

            string? value = null;
            if (arg == "--color")
                value = i + 1 < args.Length ? args[i + 1] : null;
            else if (arg.StartsWith("--color=", StringComparison.Ordinal))
                value = arg["--color=".Length..];
            else
                continue;

            if (value != null && TryParseColorMode(value, out var mode))
                return mode;
            // Unrecognized value: leave --color for the application; keep scanning.
        }

        return _defaultColorMode;
    }

    private static bool TryParseColorMode(string value, out CliColorMode mode)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "auto": mode = CliColorMode.Auto; return true;
            case "never": mode = CliColorMode.Never; return true;
            case "16": mode = CliColorMode.Standard16; return true;
            case "256": mode = CliColorMode.Ansi256; return true;
            default: mode = default; return false;
        }
    }

    private readonly record struct ThemeResolution(
        ITheme? Theme,
        bool Success,
        string? RequestedRaw,
        ThemeResolutionSource Source);

    private enum ThemeResolutionSource
    {
        None,
        CommandLine,
        Environment
    }

    /// <summary>
    /// Applies the app's <see cref="TigerThemeConfiguration"/> to the active console appearance:
    /// registers custom themes and makes the app's colour aliases and custom styles active for the run.
    /// Theme disabling is enforced at resolution/help time (a disabled theme stays registered globally
    /// but is unavailable for this app), so nothing is removed from the global theme registry here.
    /// </summary>
    private void ApplyThemeConfiguration()
    {
        foreach (var theme in _themeConfiguration.CustomThemes)
            TigerConsole.AddOrUpdateCustomTheme(theme);

        TigerConsole.ColorAliases = _themeConfiguration.ColorAliases;
        TigerConsole.CustomStyles = _themeConfiguration.CustomStyles;
    }

    /// <summary>
    /// The themes selectable by this app: every registered theme minus the app's disabled themes.
    /// Disabled themes behave like unknown themes for <c>--theme</c>, <c>TIGERCLI_THEME</c>, and help.
    /// </summary>
    private IReadOnlyList<string> GetEnabledThemeNames()
        => TigerConsole.GetThemeNames()
            .Where(name => !IsThemeDisabled(name))
            .ToArray();

    private bool IsThemeDisabled(string name)
        => _themeConfiguration.DisabledThemes.Any(
            disabled => string.Equals(disabled, name, StringComparison.OrdinalIgnoreCase));

    private ThemeResolution ResolveRunTheme(string[] args)
    {
        string? requested = null;
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--theme")
            {
                requested = i + 1 < args.Length ? args[i + 1] : string.Empty;
                break;
            }
            if (arg.StartsWith("--theme=", StringComparison.Ordinal))
            {
                requested = arg["--theme=".Length..];
                break;
            }
        }

        if (requested != null)
            return ResolveThemeName(requested, ThemeResolutionSource.CommandLine);

        var environmentRequested = Environment.GetEnvironmentVariable(ThemeEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(environmentRequested))
            return new ThemeResolution(
                null,
                Success: true,
                RequestedRaw: null,
                ThemeResolutionSource.None);

        return ResolveThemeName(environmentRequested, ThemeResolutionSource.Environment);
    }

    private ThemeResolution ResolveThemeName(string requested, ThemeResolutionSource source)
    {
        // "default" is reserved for API lookup (it aliases CurrentTheme); selecting it via
        // --theme or TIGERCLI_THEME is redundant, so it is not an accepted run choice. A theme the app
        // has disabled behaves exactly like an unknown theme.
        if (!string.Equals(requested, "default", StringComparison.OrdinalIgnoreCase)
            && !IsThemeDisabled(requested)
            && TigerConsole.TryGetTheme(requested, out var theme))
        {
            return new ThemeResolution(theme, Success: true, RequestedRaw: requested, source);
        }

        return new ThemeResolution(null, Success: false, RequestedRaw: requested, source);
    }

    private bool TryResolveInteractionMode(
        TigerCliCommandRegistration command,
        bool nonInteractiveRequested,
        out TigerCliInteractionMode mode)
    {
        mode = _commandInteractionModes.TryGetValue(command.HandlerType, out var commandMode)
            ? commandMode
            : _interactionMode;

        if (!nonInteractiveRequested)
            return true;

        if (mode == TigerCliInteractionMode.FullInteractive)
            return false;

        mode = TigerCliInteractionMode.NonInteractive;
        return true;
    }

    private TigerCliPromptMode ResolvePromptMode(TigerCliCommandRegistration command)
    {
        if (command.PromptMode is { } registrationMode)
            return registrationMode;

        if (_commandPromptModes.TryGetValue(command.HandlerType, out var commandMode))
            return commandMode;

        if (command.GroupPromptMode is { } groupMode)
            return groupMode;

        return _promptMode ?? TigerCliPromptMode.RequiredOnly;
    }

    private TigerCliEffectiveProviderMap BuildEffectiveProviderMap(TigerCliCommandRegistration command)
    {
        var result = new TigerCliEffectiveProviderMap();
        AddProviders(result, _providers, scope: 0);
        AddProviders(result, command.GroupProviders, scope: 1);
        AddProviders(result, command.Providers, scope: 2);
        return result;
    }

    private static void AddProviders(
        TigerCliEffectiveProviderMap map,
        IEnumerable<ITigerCliValueProvider> providers,
        int scope)
    {
        foreach (var provider in providers.OrderBy(provider => provider.Order))
        {
            map.Named[provider.Key] = new ProviderEntry(provider, scope);

            if (provider.PropertyName != null)
            {
                map.PropertyScoped[(provider.SettingsType, provider.PropertyName)] =
                    new ProviderEntry(provider, scope);
            }
        }
    }

    private static bool IsPathMatch(string[] args, string[] pathTokens)
    {
        if (args.Length < pathTokens.Length)
            return false;

        for (var i = 0; i < pathTokens.Length; i++)
        {
            if (args[i].StartsWith('-'))
                return false;
            if (!string.Equals(args[i], pathTokens[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    // ── Argument and option metadata ────────────────────────────────

    private static List<TigerCliArgumentMetadata> BuildArgumentMetadata(Type settingsType)
    {
        var result = new List<TigerCliArgumentMetadata>();
        foreach (var prop in settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<TigerCliArgumentAttribute>();
            if (attr != null)
                result.Add(new TigerCliArgumentMetadata(prop, attr));
        }

        var duplicateIndex = result
            .GroupBy(arg => arg.Index)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateIndex != null)
            throw new InvalidOperationException(
                $"Multiple [TigerCliArgument] properties use index {duplicateIndex.Key} on '{settingsType.Name}'.");

        return result.OrderBy(arg => arg.Index).ToList();
    }

    private static List<TigerCliOptionMetadata> BuildOptionMetadata(Type settingsType)
    {
        var result = new List<TigerCliOptionMetadata>();
        foreach (var prop in settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<TigerCliOptionAttribute>();
            if (attr != null)
                result.Add(new TigerCliOptionMetadata(prop, attr));
        }
        return result;
    }

    // ── Parsing ─────────────────────────────────────────────────────

    private static TigerCliParseResult ParseArgumentsAndOptions(
        string[] args,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TigerCliCommandRegistration? command)
    {
        var aliasMap = new Dictionary<string, TigerCliOptionMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var opt in options)
        {
            foreach (var alias in opt.Aliases)
                aliasMap[alias] = opt;
        }

        var argumentValues = new Dictionary<TigerCliArgumentMetadata, string>();
        var optionValues = new Dictionary<TigerCliOptionMetadata, List<string>>();
        int i = 0;
        int argumentIndex = 0;
        bool optionsStarted = false;

        while (i < args.Length)
        {
            var token = args[i];

            if (!token.StartsWith('-'))
            {
                if (optionsStarted)
                {
                    return new TigerCliParseResult
                    {
                        ResolvedCommand = command,
                        ArgumentValues = argumentValues,
                        OptionValues = optionValues,
                        ErrorResourceKey = "Error_UnexpectedPositional",
                        ErrorArgs = new object[] { Esc(token) },
                        ErrorExitKind = TigerCliExitKind.InvalidArguments
                    };
                }

                if (argumentIndex >= arguments.Count)
                {
                    return new TigerCliParseResult
                    {
                        ResolvedCommand = command,
                        ArgumentValues = argumentValues,
                        OptionValues = optionValues,
                        ErrorResourceKey = "Error_UnexpectedArgument",
                        ErrorArgs = new object[] { Esc(token) },
                        ErrorExitKind = TigerCliExitKind.InvalidArguments
                    };
                }

                argumentValues[arguments[argumentIndex]] = token;
                argumentIndex++;
                i++;
                continue;
            }

            optionsStarted = true;

            // Split --option=value or -o=value
            string optionName;
            string? inlineValue = null;
            var eqIndex = token.IndexOf('=');
            if (eqIndex > 0)
            {
                optionName = token[..eqIndex];
                inlineValue = token[(eqIndex + 1)..];
            }
            else
            {
                optionName = token;
            }

            // Look up option
            if (!aliasMap.TryGetValue(optionName, out var meta))
            {
                return new TigerCliParseResult
                {
                    ResolvedCommand = command,
                    ArgumentValues = argumentValues,
                    OptionValues = optionValues,
                    ErrorResourceKey = "Error_UnknownOption",
                    ErrorArgs = new object[] { Esc(optionName) },
                    ErrorExitKind = TigerCliExitKind.InvalidArguments
                };
            }

            // Consume value
            if (meta.TakesValue)
            {
                string value;
                if (inlineValue != null)
                {
                    value = inlineValue;
                }
                else
                {
                    i++;
                    if (i >= args.Length)
                    {
                        return new TigerCliParseResult
                        {
                            ResolvedCommand = command,
                            ArgumentValues = argumentValues,
                            OptionValues = optionValues,
                            ErrorResourceKey = "Error_OptionRequiresValue",
                            ErrorArgs = new object[] { Esc(optionName) },
                            ErrorExitKind = TigerCliExitKind.InvalidArguments
                        };
                    }
                    value = args[i];
                }

                // For key-value options: if value has no '=', consume a second token as the value part
                if (meta.ValueKind == OptionValueKind.RepeatedKeyValue && !value.Contains('='))
                {
                    i++;
                    if (i >= args.Length)
                    {
                        return new TigerCliParseResult
                        {
                            ResolvedCommand = command,
                            ArgumentValues = argumentValues,
                            OptionValues = optionValues,
                            ErrorResourceKey = "Error_OptionRequiresKVP",
                            ErrorArgs = new object[] { Esc(optionName) },
                            ErrorExitKind = TigerCliExitKind.InvalidArguments
                        };
                    }
                    value = value + "=" + args[i];
                }

                if (!optionValues.TryGetValue(meta, out var list))
                {
                    list = new List<string>();
                    optionValues[meta] = list;
                }
                list.Add(value);
            }
            else
            {
                // No-value switch (e.g. bool flag) — store "true" as the value
                if (!optionValues.TryGetValue(meta, out var list))
                {
                    list = new List<string>();
                    optionValues[meta] = list;
                }
                list.Add("true");
            }

            i++;
        }

        return new TigerCliParseResult
        {
            ResolvedCommand = command,
            ArgumentValues = argumentValues,
            OptionValues = optionValues
        };
    }

    // ── Binding ─────────────────────────────────────────────────────

    private static TigerCliSettings BindSettings(
        Type settingsType,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult)
    {
        var settings = (TigerCliSettings)Activator.CreateInstance(settingsType)!;

        foreach (var arg in arguments)
        {
            if (!parseResult.ArgumentValues.TryGetValue(arg, out var rawValue))
                continue;

            var boundValue = ConvertScalar(rawValue, arg.Property.PropertyType);
            if (boundValue != null)
                arg.Property.SetValue(settings, boundValue);
        }

        foreach (var opt in options)
        {
            if (!parseResult.OptionValues.TryGetValue(opt, out var rawValues) || rawValues.Count == 0)
                continue;

            object? boundValue = opt.ValueKind switch
            {
                OptionValueKind.Scalar => ConvertScalar(rawValues[^1], opt.Property.PropertyType),
                OptionValueKind.RepeatedScalar => ConvertRepeatedScalar(rawValues, opt.Property.PropertyType),
                OptionValueKind.RepeatedKeyValue => ConvertRepeatedKeyValue(rawValues),
                _ => null
            };

            if (boundValue != null)
                opt.Property.SetValue(settings, boundValue);
        }

        return settings;
    }

    private static string? ValidateScalarConversions(
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult,
        CultureInfo culture)
    {
        foreach (var arg in arguments)
        {
            if (!parseResult.ArgumentValues.TryGetValue(arg, out var rawValue))
                continue;

            if (!CanConvertScalar(rawValue, arg.Property.PropertyType))
            {
                return TigerCliResources.Format(
                    "Error_InvalidValue",
                    culture,
                    Esc($"<{arg.DisplayName}>"),
                    Esc(rawValue));
            }
        }

        foreach (var opt in options.Where(opt => opt.ValueKind == OptionValueKind.Scalar))
        {
            if (!parseResult.OptionValues.TryGetValue(opt, out var rawValues) || rawValues.Count == 0)
                continue;

            var rawValue = rawValues[^1];
            if (!CanConvertScalar(rawValue, opt.Property.PropertyType))
            {
                return TigerCliResources.Format(
                    "Error_InvalidValue",
                    culture,
                    Esc(GetPreferredAlias(opt)),
                    Esc(rawValue));
            }
        }

        return null;
    }

    private static bool CanConvertScalar(string raw, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying == typeof(string))
            return true;

        return ConvertScalar(raw, targetType) != null;
    }

    private static object? ConvertScalar(string raw, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying == typeof(string))
            return raw;

        if (underlying.IsEnum)
            return ConvertEnumValue(underlying, raw);

        try
        {
            return Convert.ChangeType(raw, underlying);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a raw token to an enum value. Beyond <see cref="Enum.TryParse(Type, string, bool, out object)"/>
    /// (which already accepts a member name, a comma-separated list of names for <c>[Flags]</c> enums, and a
    /// plain decimal value), this also accepts a <c>0x</c>-prefixed hexadecimal value so numeric flag masks
    /// like <c>0x0004</c> bind directly. Returns null when the token is not a recognizable enum value.
    /// </summary>
    private static object? ConvertEnumValue(Type enumType, string raw)
    {
        var token = raw.Trim();
        if (token.Length == 0)
            return null;

        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(token.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits)
                ? Enum.ToObject(enumType, bits)
                : null;
        }

        return Enum.TryParse(enumType, token, ignoreCase: true, out var enumVal) ? enumVal : null;
    }

    private static object ConvertRepeatedScalar(List<string> rawValues, Type targetType)
    {
        if (targetType == typeof(string[]))
            return rawValues.ToArray();

        // List<string>
        return new List<string>(rawValues);
    }

    private static List<KeyValuePair<string, string>> ConvertRepeatedKeyValue(List<string> rawValues)
    {
        var result = new List<KeyValuePair<string, string>>();
        foreach (var raw in rawValues)
        {
            var eqIndex = raw.IndexOf('=');
            if (eqIndex > 0)
                result.Add(new KeyValuePair<string, string>(raw[..eqIndex], raw[(eqIndex + 1)..]));
            else
                result.Add(new KeyValuePair<string, string>(raw, string.Empty));
        }
        return result;
    }

    // ── Multi-select ────────────────────────────────────────────────

    /// <summary>
    /// Resolves every <c>[TigerCliMultiSelect]</c> option. A value supplied on the command line is
    /// split (comma and/or repeated occurrences), validated against the option's provider (unless
    /// custom values are allowed), and bound into the collection property. A missing value is prompted
    /// with the inline checklist when interactive, promptable, and a provider exists; otherwise it is
    /// left as-is (so an existing edit-mode value is preserved). Runs after the generic prompt stage,
    /// which skips multi-select options.
    /// </summary>
    private static async Task<PromptResolutionResult> ResolveMultiSelectOptionsAsync(
        TigerCliSettings settings,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult,
        TigerCliInteractionMode interactionMode,
        TigerCliPromptMode promptMode,
        ICliAppShell? shell,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        TigerCliEffectiveProviderMap providers,
        CultureInfo culture,
        ResourceManager? appResources,
        bool editMode)
    {
        foreach (var opt in options)
        {
            if (!opt.UseMultiSelect)
                continue;

            var provider = FindProvider(
                providers,
                settings.GetType(),
                opt.Property.Name,
                GetProviderKeyCandidates(opt, editMode),
                preferNamedProvider: GetEffectiveProvider(opt, editMode) != null);

            var displayName = GetPreferredAlias(opt);
            var supplied = parseResult.OptionValues.TryGetValue(opt, out var rawValues) && rawValues.Count > 0;

            if (supplied)
            {
                var result = await ResolveSuppliedMultiSelectAsync(
                    settings, opt, provider, rawValues!, displayName,
                    arguments, options, promptTimeout, ct, culture).ConfigureAwait(false);
                if (!result.IsSuccess)
                    return result;
                continue;
            }

            // Nothing supplied. Only an interactive, promptable option with a provider shows a checklist;
            // otherwise leave the property untouched (edit-mode existing value is preserved).
            if (provider == null)
                continue;
            if (!ShouldPromptOption(opt, options, settings))
                continue;
            if (!ShouldPrompt(opt.Promptable, promptMode, opt.Required))
                continue;
            if (interactionMode == TigerCliInteractionMode.NonInteractive)
                continue;

            var promptResult = await PromptMultiSelectAsync(
                settings, opt, provider, displayName,
                arguments, options, parseResult,
                shell, promptTimeout, ct, culture, appResources).ConfigureAwait(false);
            if (!promptResult.IsSuccess)
                return promptResult;
        }

        return PromptResolutionResult.Success();
    }

    private static async Task<PromptResolutionResult> ResolveSuppliedMultiSelectAsync(
        TigerCliSettings settings,
        TigerCliOptionMetadata opt,
        ITigerCliValueProvider? provider,
        List<string> rawValues,
        string displayName,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        CultureInfo culture)
    {
        var tokens = SplitMultiSelectTokens(rawValues);

        if (tokens.Count == 0)
        {
            if (!opt.MultiSelectAllowEmpty)
                return PromptResolutionResult.Failure(
                    "Error_MultiSelectRequiresSelection",
                    new object[] { Esc(displayName) },
                    TigerCliExitKind.ValidationError);
            opt.Property.SetValue(settings, BuildMultiSelectCollection(opt, Array.Empty<object>()));
            return PromptResolutionResult.Success();
        }

        var keys = new List<object>(tokens.Count);
        var seen = new HashSet<object>();

        if (provider != null && !opt.MultiSelectAllowCustomValues)
        {
            var context = new TigerCliPromptContext(
                shell: null,
                settings.InteractionMode,
                promptTimeout,
                ct,
                culture,
                BuildProviderContextValues(settings, options, arguments));

            IReadOnlyList<TigerCliPromptChoice> choices;
            try
            {
                choices = await provider.GetChoicesAsync(settings, context).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TigerCliProviderException ex)
            {
                return ProviderReportedFailure(displayName, ex);
            }
            catch (TigerCliPromptProviderConfigurationException ex)
            {
                return PromptResolutionResult.FailureLiteral(Esc(ex.Message), TigerCliExitKind.ValidationError);
            }
            catch (Exception ex)
            {
                return PromptResolutionResult.Failure(
                    "Error_PromptProviderFailed",
                    new object[] { Esc(displayName), Esc(GetUserFacingExceptionMessage(ex)) },
                    TigerCliExitKind.UnhandledException);
            }

            foreach (var token in tokens)
            {
                var matched = TigerCliProviderValueMatcher.FindKey(choices, token, opt.ValueMatching);
                if (matched == null)
                    return PromptResolutionResult.Failure(
                        "Error_InvalidProviderValue",
                        new object[] { Esc(displayName), Esc(token) },
                        TigerCliExitKind.ValidationError);
                if (seen.Add(matched))
                    keys.Add(matched);
            }
        }
        else
        {
            foreach (var token in tokens)
            {
                var converted = ConvertMultiSelectToken(opt.MultiSelectElementType!, token);
                if (converted == null)
                    return PromptResolutionResult.Failure(
                        "Error_InvalidValue",
                        new object[] { Esc(displayName), Esc(token) },
                        TigerCliExitKind.ValidationError);
                if (seen.Add(converted))
                    keys.Add(converted);
            }
        }

        opt.Property.SetValue(settings, BuildMultiSelectCollection(opt, keys));
        return PromptResolutionResult.Success();
    }

    private static async Task<PromptResolutionResult> PromptMultiSelectAsync(
        TigerCliSettings settings,
        TigerCliOptionMetadata opt,
        ITigerCliValueProvider provider,
        string displayName,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult,
        ICliAppShell? shell,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        CultureInfo culture,
        ResourceManager? appResources)
    {
        var context = new TigerCliPromptContext(
            shell,
            settings.InteractionMode,
            promptTimeout,
            ct,
            culture,
            BuildProviderContextValues(settings, options, arguments));

        IReadOnlyList<TigerCliPromptChoice> choices;
        try
        {
            var resolved = await ResolveProviderChoicesAsync(settings, provider, context, culture, appResources).ConfigureAwait(false);
            if (resolved.Outcome == ProviderChoicesOutcome.Canceled)
                return PromptResolutionResult.UserCancellation();
            choices = resolved.Choices;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            return PromptResolutionResult.UserCancellation();
        }
        catch (TigerCliProviderException ex)
        {
            return ProviderReportedFailure(displayName, ex);
        }
        catch (TigerCliPromptProviderConfigurationException ex)
        {
            return PromptResolutionResult.FailureLiteral(Esc(ex.Message), TigerCliExitKind.ValidationError);
        }
        catch (Exception ex)
        {
            return PromptResolutionResult.Failure(
                "Error_PromptProviderFailed",
                new object[] { Esc(displayName), Esc(GetUserFacingExceptionMessage(ex)) },
                TigerCliExitKind.UnhandledException);
        }

        var prompt = FormatOptionPrompt(opt, culture, appResources);

        if (choices.Count == 0)
        {
            // No choices to pick from: an empty result is only acceptable when empties are allowed.
            if (!opt.MultiSelectAllowEmpty)
                return BuildProviderNoChoicesFailure(provider, displayName, culture, appResources);
            opt.Property.SetValue(settings, BuildMultiSelectCollection(opt, Array.Empty<object>()));
            RecordMultiSelectResolved(parseResult, opt, Array.Empty<object>());
            return PromptResolutionResult.Success();
        }

        var labels = choices.Select(choice => choice.Label).ToList();
        var preselected = ResolveMultiSelectPreselection(opt, settings, choices);

        AdoptSingletonCultureIfNoShell(shell, culture);
        var picked = shell == null
            ? await TigerTui.MultiSelectIndexesResultAsync(prompt, labels, preselected, timeout: promptTimeout, ct: ct).ConfigureAwait(false)
            : await TigerTui.MultiSelectIndexesResultAsync(shell, prompt, labels, preselected, timeout: promptTimeout, ct: ct).ConfigureAwait(false);

        if (!picked.IsOk)
            return PromptResolutionResult.UserCancellation();

        var indexes = picked.Value!;
        if (indexes.Length == 0 && !opt.MultiSelectAllowEmpty)
            return PromptResolutionResult.Failure(
                "Error_MultiSelectRequiresSelection",
                new object[] { Esc(displayName) },
                TigerCliExitKind.ValidationError);

        var keys = new List<object>(indexes.Length);
        foreach (var index in indexes)
            keys.Add(choices[index].Key);

        opt.Property.SetValue(settings, BuildMultiSelectCollection(opt, keys));
        RecordMultiSelectResolved(parseResult, opt, keys);
        return PromptResolutionResult.Success();
    }

    // Records a resolved multi-select in the parse ledger so later required-field validation sees the
    // option as provided. A no-selection outcome records a single empty token (mirrors the single-select
    // no-selection convention); binding already happened via the property setter.
    private static void RecordMultiSelectResolved(
        TigerCliParseResult parseResult, TigerCliOptionMetadata opt, IReadOnlyList<object> keys)
    {
        parseResult.OptionValues[opt] = keys.Count == 0
            ? [string.Empty]
            : keys.Select(k => k.ToString() ?? string.Empty).ToList();
    }

    private static List<int>? ResolveMultiSelectPreselection(
        TigerCliOptionMetadata opt, TigerCliSettings settings, IReadOnlyList<TigerCliPromptChoice> choices)
    {
        var current = ExtractMultiSelectCurrentKeys(opt, settings);
        if (current.Count == 0)
            return null;

        var indexes = new List<int>();
        for (var i = 0; i < choices.Count; i++)
        {
            if (current.Any(key => Equals(key, choices[i].Key)))
                indexes.Add(i);
        }

        return indexes.Count > 0 ? indexes : null;
    }

    private static IReadOnlyList<object> ExtractMultiSelectCurrentKeys(TigerCliOptionMetadata opt, TigerCliSettings settings)
    {
        if (opt.Property.GetValue(settings) is not System.Collections.IEnumerable enumerable)
            return Array.Empty<object>();

        var keys = new List<object>();
        foreach (var item in enumerable)
        {
            if (item != null)
                keys.Add(item);
        }

        return keys;
    }

    private static List<string> SplitMultiSelectTokens(IReadOnlyList<string> rawValues)
    {
        var tokens = new List<string>();
        foreach (var raw in rawValues)
        {
            foreach (var part in raw.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0)
                    tokens.Add(trimmed);
            }
        }

        return tokens;
    }

    private static object? ConvertMultiSelectToken(Type elementType, string token)
    {
        if (elementType == typeof(string))
            return token;
        if (elementType == typeof(Guid))
            return Guid.TryParse(token, out var guid) ? guid : null;

        try
        {
            return Convert.ChangeType(token, elementType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static object BuildMultiSelectCollection(TigerCliOptionMetadata opt, IReadOnlyList<object> keys)
    {
        var elementType = opt.MultiSelectElementType!;

        if (opt.MultiSelectIsArray)
        {
            var array = Array.CreateInstance(elementType, keys.Count);
            for (var i = 0; i < keys.Count; i++)
                array.SetValue(keys[i], i);
            return array;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var key in keys)
            list.Add(key);
        return list;
    }

    // ── Prompting ───────────────────────────────────────────────────

    private static async Task<PromptResolutionResult> ResolveMissingPromptableValuesAsync(
        TigerCliSettings settings,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult,
        TigerCliInteractionMode interactionMode,
        TigerCliPromptMode promptMode,
        ICliAppShell? promptShell,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        TigerCliEffectiveProviderMap providers,
        CultureInfo culture,
        ResourceManager? appResources,
        bool editMode,
        IFolderBrowser folderBrowser)
    {
        if (interactionMode != TigerCliInteractionMode.SemiInteractive)
            return PromptResolutionResult.Success();

        foreach (var arg in arguments.OrderBy(arg => arg.Index))
        {
            if (parseResult.ArgumentValues.ContainsKey(arg))
                continue;
            // Edit-only: non-editable arguments (selectors) are not prompted as editable fields
            // after load. Missing promptable selectors are resolved before the loader by
            // ResolveMissingSelectorArgumentsAsync and are already in the parse ledger here.
            if (editMode && !arg.Editable)
                continue;
            if (!ShouldPrompt(arg.Promptable, promptMode, required: true))
                continue;

            var context = new TigerCliPromptContext(
                promptShell,
                interactionMode,
                promptTimeout,
                ct,
                culture,
                BuildProviderContextValues(settings, options, arguments));

            var result = await PromptArgumentAsync(
                settings,
                arg,
                arguments,
                options,
                parseResult,
                context,
                promptShell,
                providers,
                promptTimeout,
                ct,
                culture,
                appResources,
                editMode).ConfigureAwait(false);
            if (!result.IsSuccess)
                return result;
        }

        var orderedOptions = BuildOptionPromptOrder(options, providers, settings.GetType(), editMode, out var dependencyCycle);
        if (dependencyCycle != null)
        {
            return PromptResolutionResult.Failure(
                "Error_OptionPromptDependencyCycle",
                new object[] { Esc(dependencyCycle) },
                TigerCliExitKind.ValidationError);
        }

        foreach (var opt in orderedOptions)
        {
            if (parseResult.OptionValues.ContainsKey(opt))
                continue;
            // Edit-only: non-editable options are not prompted as editable fields.
            if (editMode && !opt.Editable)
                continue;

            var required = IsOptionRequired(opt, options, settings);
            var result = await PromptMissingOptionAsync(
                settings,
                opt,
                options,
                arguments,
                parseResult,
                promptMode,
                required,
                promptShell,
                promptTimeout,
                ct,
                providers,
                culture,
                appResources,
                editMode,
                folderBrowser).ConfigureAwait(false);
            if (!result.IsSuccess)
                return result;
        }

        return PromptResolutionResult.Success();
    }

    /// <summary>
    /// Edit-only stage that runs <b>before</b> the <c>AsEdit</c> loader. Every positional argument
    /// is a selector that must be available before the existing object is loaded, so each one that
    /// is absent from the parse ledger is resolved here:
    /// <list type="bullet">
    /// <item>If the argument can be prompted in the current interaction mode (semi-interactive and
    /// promptable under the normal argument prompt rules), it is prompted with the normal argument
    /// prompt/provider machinery and the result is recorded in the parse ledger, so the loader sees
    /// the bound value.</item>
    /// <item>Otherwise (non-interactive, not promptable, or a prompt that yields no value) it fails
    /// with the standard missing-argument framework error and the loader is not called.</item>
    /// </list>
    /// Editability is not consulted here — a non-editable selector still must be resolved before
    /// load; <c>Editable = false</c> only means "not edited/re-prompted after load." Missing
    /// selectors are never seeded from the existing object, which has not been loaded yet.
    /// </summary>
    private static async Task<PromptResolutionResult> ResolveMissingSelectorArgumentsAsync(
        TigerCliSettings settings,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult,
        TigerCliInteractionMode interactionMode,
        TigerCliPromptMode promptMode,
        ICliAppShell? promptShell,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        TigerCliEffectiveProviderMap providers,
        CultureInfo culture,
        ResourceManager? appResources)
    {
        foreach (var arg in arguments.OrderBy(arg => arg.Index))
        {
            if (parseResult.ArgumentValues.ContainsKey(arg))
                continue;

            // Arguments are always required selectors, so promptability follows the normal
            // argument prompt rules (required: true).
            var canPrompt = interactionMode == TigerCliInteractionMode.SemiInteractive
                && ShouldPrompt(arg.Promptable, promptMode, required: true);

            if (canPrompt)
            {
                // Rebuild the provider context per selector so a later selector's provider can
                // observe earlier prompted selector values.
                var context = new TigerCliPromptContext(
                    promptShell,
                    interactionMode,
                    promptTimeout,
                    ct,
                    culture,
                    BuildProviderContextValues(settings, options, arguments));
                var result = await PromptArgumentAsync(
                    settings,
                    arg,
                    arguments,
                    options,
                    parseResult,
                    context,
                    promptShell,
                    providers,
                    promptTimeout,
                    ct,
                    culture,
                    appResources,
                    editMode: true).ConfigureAwait(false);
                if (!result.IsSuccess)
                    return result;
            }

            // Could not prompt, or the prompt produced no value: the selector is still missing.
            // Fail as a missing argument before the loader runs (the loader is not called).
            if (!parseResult.ArgumentValues.ContainsKey(arg))
                return PromptResolutionResult.Failure(
                    "Error_MissingArgument",
                    new object[] { Esc(arg.DisplayName) },
                    TigerCliExitKind.MissingRequiredArgument);
        }

        return PromptResolutionResult.Success();
    }

    /// <summary>
    /// Prompts for a single argument value using the normal argument prompt/provider machinery and,
    /// on success, writes the value into both the settings object and the parse ledger. Returns a
    /// cancel failure when the prompt is canceled, and Success (leaving the argument unresolved) when
    /// the prompt produces no value.
    /// </summary>
    private static async Task<PromptResolutionResult> PromptArgumentAsync(
        TigerCliSettings settings,
        TigerCliArgumentMetadata arg,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult,
        TigerCliPromptContext context,
        ICliAppShell? shell,
        TigerCliEffectiveProviderMap providers,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        CultureInfo culture,
        ResourceManager? appResources,
        bool editMode)
    {
        var prompt = FormatArgumentPrompt(arg, culture, appResources);
        // Seed the prompt with the current effective value as the default/preselect.
        var currentValue = GetPromptDefaultValue(arg.Property, settings);
        var provider = FindProvider(
            providers,
            settings.GetType(),
            arg.Property.Name,
            GetProviderKeyCandidates(arg, editMode),
            preferNamedProvider: GetEffectiveProvider(arg, editMode) != null);
        if (provider == null && !CanPrompt(arg.Property.PropertyType))
            return PromptResolutionResult.Success();

        var integerBounds = IntegerBounds.Empty;
        if (provider == null && IsIntegerTarget(arg.Property.PropertyType))
        {
            var boundsResult = await ResolveIntegerBoundsAsync(
                settings,
                $"<{arg.DisplayName}>",
                arg.MinValue,
                arg.MaxValue,
                arg.MinValueProvider,
                arg.MaxValueProvider,
                providers,
                arguments,
                options,
                promptTimeout,
                ct,
                culture).ConfigureAwait(false);
            if (boundsResult.Failure is { } failure)
                return failure;
            integerBounds = boundsResult.Bounds;
        }

        // Arguments are always required, so they never offer a no-selection choice.
        var valueResult = provider != null
            ? await PromptWithProviderAsync(
                settings,
                provider,
                context,
                prompt,
                $"<{arg.DisplayName}>",
                required: true,
                allowNoSelection: false,
                autoSelectSingleChoice: arg.AutoSelectSingleChoice,
                culture,
                appResources,
                currentValue).ConfigureAwait(false)
            : await PromptForAutomaticValueAsync(
                shell,
                prompt,
                arg.Property.PropertyType,
                enumNames: null,
                textValidator: IsIntegerTarget(arg.Property.PropertyType)
                    ? BuildIntegerPromptValidator(required: true, integerBounds, culture)
                    : BuildStringPromptValidator(
                        required: true,
                        arg.MinLength,
                        arg.MaxLength,
                        culture),
                isSecret: false,
                allowNoSelection: false,
                promptTimeout,
                ct,
                culture,
                appResources,
                IsIntegerTarget(arg.Property.PropertyType) ? null : currentValue).ConfigureAwait(false);

        if (!valueResult.IsSuccess)
            return valueResult;
        if (valueResult.IsCanceled)
            return PromptResolutionResult.UserCancellation();
        if (!valueResult.HasValue || valueResult.Value == null)
            return PromptResolutionResult.Success();

        var value = valueResult.Value;
        parseResult.ArgumentValues[arg] = ConvertPromptValueToRaw(value);
        arg.Property.SetValue(settings, value);
        return PromptResolutionResult.Success();
    }

    private static async Task<PromptResolutionResult> PromptMissingOptionAsync(
        TigerCliSettings settings,
        TigerCliOptionMetadata opt,
        List<TigerCliOptionMetadata> options,
        List<TigerCliArgumentMetadata> arguments,
        TigerCliParseResult parseResult,
        TigerCliPromptMode promptMode,
        bool required,
        ICliAppShell? shell,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        TigerCliEffectiveProviderMap providers,
        CultureInfo culture,
        ResourceManager? appResources,
        bool editMode,
        IFolderBrowser folderBrowser)
    {
        // Multi-select options are resolved by ResolveMultiSelectOptionsAsync (checklist prompt),
        // not by this single-select path.
        if (opt.UseMultiSelect)
            return PromptResolutionResult.Success();

        if (!ShouldPromptOption(opt, options, settings))
            return PromptResolutionResult.Success();

        if (!ShouldPrompt(opt.Promptable, promptMode, required))
            return PromptResolutionResult.Success();

        var provider = FindProvider(
            providers,
            settings.GetType(),
            opt.Property.Name,
            GetProviderKeyCandidates(opt, editMode),
            preferNamedProvider: GetEffectiveProvider(opt, editMode) != null);
        if (provider == null && !CanPrompt(opt.Property.PropertyType))
            return PromptResolutionResult.Success();

        var prompt = FormatOptionPrompt(opt, culture, appResources);
        // Seed the prompt with the current effective value as the default/preselect.
        var currentValue = GetPromptDefaultValue(opt.Property, settings);
        // Offer a no-selection (null) row only for optional nullable select-style prompts.
        var allowNoSelection = AllowsNoSelection(opt.Property, required);
        var integerBounds = IntegerBounds.Empty;
        if (provider == null && IsIntegerTarget(opt.Property.PropertyType))
        {
            var boundsResult = await ResolveIntegerBoundsAsync(
                settings,
                GetPreferredAlias(opt),
                opt.MinValue,
                opt.MaxValue,
                opt.MinValueProvider,
                opt.MaxValueProvider,
                providers,
                arguments,
                options,
                promptTimeout,
                ct,
                culture).ConfigureAwait(false);
            if (boundsResult.Failure is { } failure)
                return failure;
            integerBounds = boundsResult.Bounds;
        }
        var valueResult = provider != null
            ? await PromptWithProviderAsync(
                settings,
                provider,
                new TigerCliPromptContext(
                    shell,
                    settings.InteractionMode,
                    promptTimeout,
                    ct,
                    culture,
                    BuildProviderContextValues(settings, options, arguments)),
                prompt,
                GetPreferredAlias(opt),
                required,
                allowNoSelection,
                opt.AutoSelectSingleChoice,
                culture,
                appResources,
                currentValue).ConfigureAwait(false)
            : await PromptForAutomaticValueAsync(
                shell,
                prompt,
                opt.Property.PropertyType,
                opt.IsEnum ? opt.FilteredEnumValues : null,
                IsIntegerTarget(opt.Property.PropertyType)
                    ? BuildIntegerPromptValidator(required, integerBounds, culture)
                    : BuildStringPromptValidator(
                        required,
                        opt.MinLength,
                        opt.MaxLength,
                        culture),
                opt.Secret,
                allowNoSelection,
                promptTimeout,
                ct,
                culture,
                appResources,
                IsIntegerTarget(opt.Property.PropertyType) && required ? null : currentValue,
                opt.UseFolderPicker,
                folderBrowser).ConfigureAwait(false);

        if (!valueResult.IsSuccess)
            return valueResult;
        if (valueResult.IsCanceled)
            return PromptResolutionResult.UserCancellation();
        if (!valueResult.HasValue)
            return PromptResolutionResult.Success();

        // A null value here is a deliberate no-selection outcome (distinct from cancel): bind null
        // and record the field as resolved so it is not re-prompted and provider validation skips it.
        var value = valueResult.Value;
        parseResult.OptionValues[opt] = [value != null ? ConvertPromptValueToRaw(value) : string.Empty];
        opt.Property.SetValue(settings, value);
        return PromptResolutionResult.Success();
    }

    private static bool ShouldPrompt(TigerCliPromptable? promptable, TigerCliPromptMode promptMode, bool required)
    {
        if (promptable == TigerCliPromptable.No)
            return false;
        if (promptable is TigerCliPromptable.First or TigerCliPromptable.Normal or TigerCliPromptable.Last)
            return true;

        return promptMode switch
        {
            TigerCliPromptMode.No => false,
            TigerCliPromptMode.RequiredOnly => required,
            TigerCliPromptMode.Yes => true,
            _ => false
        };
    }

    private static IReadOnlyList<TigerCliOptionMetadata> BuildOptionPromptOrder(
        List<TigerCliOptionMetadata> options,
        TigerCliEffectiveProviderMap providers,
        Type settingsType,
        bool editMode,
        out string? dependencyCycle)
    {
        var originalIndex = options
            .Select((option, index) => (option, index))
            .ToDictionary(item => item.option, item => item.index);

        var hardEdges = BuildPromptDependencyEdges(options, option => GetOptionPromptDependencies(option, options));
        if (TryFindDependencyCycle(options, hardEdges, out dependencyCycle))
            return [];

        var allEdges = hardEdges.ToDictionary(
            pair => pair.Key,
            pair => new HashSet<TigerCliOptionMetadata>(pair.Value));

        foreach (var option in options)
        {
            foreach (var dependency in GetProviderOrderingDependencies(option, options, providers, settingsType, editMode))
            {
                if (ReferenceEquals(dependency, option))
                    continue;
                if (HasPath(dependency, option, allEdges))
                    continue;

                allEdges[option].Add(dependency);
            }
        }

        dependencyCycle = null;
        return TopologicalPromptOrder(options, allEdges, originalIndex);
    }

    private static Dictionary<TigerCliOptionMetadata, HashSet<TigerCliOptionMetadata>> BuildPromptDependencyEdges(
        List<TigerCliOptionMetadata> options,
        Func<TigerCliOptionMetadata, IEnumerable<TigerCliOptionMetadata>> getDependencies)
    {
        var result = options.ToDictionary(
            option => option,
            _ => new HashSet<TigerCliOptionMetadata>());

        foreach (var option in options)
        {
            foreach (var dependency in getDependencies(option))
                result[option].Add(dependency);
        }

        return result;
    }

    private static bool TryFindDependencyCycle(
        List<TigerCliOptionMetadata> options,
        Dictionary<TigerCliOptionMetadata, HashSet<TigerCliOptionMetadata>> edges,
        out string? dependencyCycle)
    {
        var states = new Dictionary<TigerCliOptionMetadata, VisitState>();
        var stack = new List<TigerCliOptionMetadata>();

        foreach (var option in options)
        {
            if (states.TryGetValue(option, out var state) && state == VisitState.Visited)
                continue;

            if (!VisitForCycle(option, edges, states, stack, out dependencyCycle))
                return true;
        }

        dependencyCycle = null;
        return false;
    }

    private static bool VisitForCycle(
        TigerCliOptionMetadata option,
        Dictionary<TigerCliOptionMetadata, HashSet<TigerCliOptionMetadata>> edges,
        Dictionary<TigerCliOptionMetadata, VisitState> states,
        List<TigerCliOptionMetadata> stack,
        out string? dependencyCycle)
    {
        states[option] = VisitState.Visiting;
        stack.Add(option);

        foreach (var dependency in edges[option])
        {
            if (states.TryGetValue(dependency, out var state))
            {
                if (state == VisitState.Visited)
                    continue;

                var cycleStart = stack.IndexOf(dependency);
                var cycle = cycleStart >= 0
                    ? stack[cycleStart..].Append(dependency)
                    : [dependency, dependency];
                dependencyCycle = string.Join(" -> ", cycle.Select(GetPreferredAlias));
                return false;
            }

            if (!VisitForCycle(dependency, edges, states, stack, out dependencyCycle))
                return false;
        }

        stack.RemoveAt(stack.Count - 1);
        states[option] = VisitState.Visited;
        dependencyCycle = null;
        return true;
    }

    private static IReadOnlyList<TigerCliOptionMetadata> TopologicalPromptOrder(
        List<TigerCliOptionMetadata> options,
        Dictionary<TigerCliOptionMetadata, HashSet<TigerCliOptionMetadata>> edges,
        Dictionary<TigerCliOptionMetadata, int> originalIndex)
    {
        var dependents = options.ToDictionary(
            option => option,
            _ => new List<TigerCliOptionMetadata>());
        var incomingCount = options.ToDictionary(
            option => option,
            _ => 0);

        foreach (var (option, dependencies) in edges)
        {
            foreach (var dependency in dependencies)
            {
                dependents[dependency].Add(option);
                incomingCount[option]++;
            }
        }

        var ready = options
            .Where(option => incomingCount[option] == 0)
            .OrderBy(GetPromptBucket)
            .ThenBy(option => originalIndex[option])
            .ToList();
        var ordered = new List<TigerCliOptionMetadata>(options.Count);

        while (ready.Count > 0)
        {
            var next = ready[0];
            ready.RemoveAt(0);
            ordered.Add(next);

            foreach (var dependent in dependents[next])
            {
                incomingCount[dependent]--;
                if (incomingCount[dependent] != 0)
                    continue;

                ready.Add(dependent);
                ready.Sort((left, right) =>
                {
                    var bucketComparison = GetPromptBucket(left).CompareTo(GetPromptBucket(right));
                    return bucketComparison != 0
                        ? bucketComparison
                        : originalIndex[left].CompareTo(originalIndex[right]);
                });
            }
        }

        return ordered;
    }

    private static bool HasPath(
        TigerCliOptionMetadata from,
        TigerCliOptionMetadata to,
        Dictionary<TigerCliOptionMetadata, HashSet<TigerCliOptionMetadata>> edges)
    {
        var seen = new HashSet<TigerCliOptionMetadata>();
        var stack = new Stack<TigerCliOptionMetadata>();
        stack.Push(from);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current))
                continue;
            if (ReferenceEquals(current, to))
                return true;

            foreach (var dependency in edges[current])
                stack.Push(dependency);
        }

        return false;
    }

    private static int GetPromptBucket(TigerCliOptionMetadata option) =>
        option.Promptable switch
        {
            TigerCliPromptable.First => 0,
            TigerCliPromptable.Last => 2,
            _ => 1
        };

    private static IEnumerable<TigerCliOptionMetadata> GetOptionPromptDependencies(
        TigerCliOptionMetadata option,
        List<TigerCliOptionMetadata> options)
    {
        var names = new[] { option.RequiredWhenOption, option.PromptWhenOption, option.DependsOnOption }
            .Concat(option.DependsOnOptions);
        var seen = new HashSet<TigerCliOptionMetadata>();

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var dependency = options.FirstOrDefault(opt => OptionNameMatches(opt, name));
            if (dependency != null && seen.Add(dependency))
                yield return dependency;
        }
    }

    private static IEnumerable<TigerCliOptionMetadata> GetProviderOrderingDependencies(
        TigerCliOptionMetadata option,
        List<TigerCliOptionMetadata> options,
        TigerCliEffectiveProviderMap providers,
        Type settingsType,
        bool editMode)
    {
        var provider = FindProvider(
            providers,
            settingsType,
            option.Property.Name,
            GetProviderKeyCandidates(option, editMode),
            preferNamedProvider: GetEffectiveProvider(option, editMode) != null);
        if (provider == null)
            yield break;

        var seen = new HashSet<TigerCliOptionMetadata>();
        foreach (var dependencyName in provider.DependsOn)
        {
            var dependency = options.FirstOrDefault(opt => OptionNameMatches(opt, dependencyName));
            if (dependency != null && seen.Add(dependency))
                yield return dependency;
        }
    }

    private enum VisitState
    {
        Visiting,
        Visited
    }

    private static bool IsOptionRequired(
        TigerCliOptionMetadata option,
        List<TigerCliOptionMetadata> options,
        TigerCliSettings settings)
    {
        if (option.Required)
            return true;

        return IsOptionConditionMatch(
            option.RequiredWhenOption,
            option.RequiredWhenValue,
            option.RequiredWhenValueIn,
            option.RequiredWhenValueNotIn,
            options,
            settings);
    }

    private static bool ShouldPromptOption(
        TigerCliOptionMetadata option,
        List<TigerCliOptionMetadata> options,
        TigerCliSettings settings)
    {
        if (option.PromptWhenOption == null
            && !HasConditionValues(option.PromptWhenValue, option.PromptWhenValueIn, option.PromptWhenValueNotIn))
            return true;

        return IsOptionConditionMatch(
            option.PromptWhenOption,
            option.PromptWhenValue,
            option.PromptWhenValueIn,
            option.PromptWhenValueNotIn,
            options,
            settings);
    }

    private static bool IsOptionConditionMatch(
        string? optionName,
        string? expectedValue,
        string[]? expectedValuesIn,
        string[]? expectedValuesNotIn,
        List<TigerCliOptionMetadata> options,
        TigerCliSettings settings)
    {
        if (string.IsNullOrWhiteSpace(optionName)
            || !HasConditionValues(expectedValue, expectedValuesIn, expectedValuesNotIn))
            return false;

        var option = options.FirstOrDefault(opt => OptionNameMatches(opt, optionName));
        if (option == null)
            return false;

        var actualValue = option.Property.GetValue(settings);
        if (actualValue == null)
            return false;

        if (expectedValue != null && OptionValueMatches(actualValue, option.Property.PropertyType, expectedValue))
            return true;

        if (expectedValuesIn is { Length: > 0 }
            && expectedValuesIn.Any(value => OptionValueMatches(actualValue, option.Property.PropertyType, value)))
            return true;

        if (expectedValuesNotIn is { Length: > 0 }
            && expectedValuesNotIn.All(value => !OptionValueMatches(actualValue, option.Property.PropertyType, value)))
            return true;

        return false;
    }

    private static bool HasConditionValues(
        string? expectedValue,
        string[]? expectedValuesIn,
        string[]? expectedValuesNotIn) =>
        expectedValue != null
        || expectedValuesIn is { Length: > 0 }
        || expectedValuesNotIn is { Length: > 0 };

    private static bool OptionNameMatches(TigerCliOptionMetadata option, string name)
    {
        var normalized = NormalizeOptionName(name);

        if (string.Equals(option.Property.Name, name, StringComparison.Ordinal)
            || string.Equals(option.Property.Name, normalized, StringComparison.OrdinalIgnoreCase))
            return true;

        return option.Aliases.Any(alias =>
            string.Equals(alias, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeOptionName(alias), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeOptionName(string name)
    {
        return name.TrimStart('-');
    }

    private static bool OptionValueMatches(object actualValue, Type propertyType, string expectedValue)
    {
        var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (underlying.IsEnum)
        {
            return Enum.TryParse(underlying, expectedValue, ignoreCase: true, out var expected)
                && Equals(actualValue, expected);
        }

        if (underlying == typeof(bool))
        {
            return bool.TryParse(expectedValue, out var expected)
                && actualValue is bool actual
                && actual == expected;
        }

        if (underlying == typeof(string))
        {
            return actualValue is string actual
                && string.Equals(actual, expectedValue, StringComparison.Ordinal);
        }

        return string.Equals(actualValue.ToString(), expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static ITigerCliValueProvider? FindProvider(
        TigerCliEffectiveProviderMap providers,
        Type settingsType,
        string propertyName,
        IEnumerable<string> namedKeys,
        bool preferNamedProvider)
    {
        providers.PropertyScoped.TryGetValue((settingsType, propertyName), out var propertyEntry);

        ProviderEntry? namedEntry = null;
        foreach (var key in namedKeys)
        {
            if (providers.Named.TryGetValue(key, out var candidate) &&
                (namedEntry == null || candidate.Scope > namedEntry.Scope))
            {
                namedEntry = candidate;
            }
        }

        if (propertyEntry == null)
            return namedEntry?.Provider;
        if (namedEntry == null)
            return propertyEntry.Provider;

        var namedWins = preferNamedProvider
            ? namedEntry.Scope >= propertyEntry.Scope
            : namedEntry.Scope > propertyEntry.Scope;

        return namedWins
            ? namedEntry.Provider
            : propertyEntry.Provider;
    }

    private static IReadOnlyDictionary<string, object?> BuildProviderContextValues(
        TigerCliSettings settings,
        List<TigerCliOptionMetadata> options,
        List<TigerCliArgumentMetadata> arguments)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Arguments first so that any option with a colliding key takes precedence.
        foreach (var argument in arguments)
        {
            var value = argument.Property.GetValue(settings);
            if (value == null)
                continue;

            values[argument.Property.Name] = value;
            values[NormalizeOptionName(argument.Property.Name)] = value;
            values[argument.DisplayName] = value;
            values[NormalizeOptionName(argument.DisplayName)] = value;
        }

        foreach (var option in options)
        {
            var value = option.Property.GetValue(settings);
            if (value == null)
                continue;

            values[option.Property.Name] = value;
            values[NormalizeOptionName(option.Property.Name)] = value;
            foreach (var alias in option.Aliases)
            {
                values[alias] = value;
                values[NormalizeOptionName(alias)] = value;
            }
        }

        return values;
    }

    // ── Edit-command support ────────────────────────────────────────

    /// <summary>
    /// Copies values from the loaded existing object into the live settings for every
    /// managed option property that was not supplied on the command line. Command-line
    /// values are present in the parse ledger and are skipped, so the command-line value
    /// always wins.
    /// </summary>
    /// <remarks>
    /// Positional selector arguments are resolved before the loader by
    /// <see cref="ResolveMissingSelectorArgumentsAsync"/> (supplied on the command line or
    /// prompted), so they are already in the parse ledger and are never seeded from the
    /// existing object here. Only options are merged.
    /// </remarks>
    private static void MergeExistingValues(
        TigerCliSettings settings,
        TigerCliSettings existing,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult)
    {
        // Options not supplied on the command line are seeded from the existing object but
        // left out of the ledger, so editable options can still be prompted in edit mode
        // with the existing value as the default.
        foreach (var opt in options)
        {
            if (parseResult.OptionValues.ContainsKey(opt))
                continue;
            opt.Property.SetValue(settings, opt.Property.GetValue(existing));
        }
    }

    /// <summary>
    /// Validates provider-backed fields against their provider's current choices.
    /// Applies in add and edit modes, interactive and non-interactive. Options are
    /// eligible when they are editable; arguments are eligible when an explicit
    /// <c>Provider</c>/<c>EditProvider</c> is configured (normal commands) or when they
    /// are editable (edit mode — selector arguments stay loader-authoritative). An
    /// implicit name-matched provider never makes an argument eligible. Eligible fields
    /// must also have <see cref="TigerCliOptionMetadata.ValidateAgainstProvider"/>
    /// enabled, a resolvable provider, and a present value. A provider that returns no
    /// choices is treated as "nothing to validate against". In edit mode a stale value
    /// of a promptable field is resolved earlier by the prompt stage; this stage rejects
    /// values that are still not valid choices.
    /// </summary>
    private static async Task<PromptResolutionResult> ValidateProviderBackedValuesAsync(
        TigerCliSettings settings,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult,
        HashSet<TigerCliOptionMetadata> prePromptOptions,
        HashSet<TigerCliArgumentMetadata> prePromptArguments,
        TigerCliEffectiveProviderMap providers,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        CultureInfo culture,
        bool editMode)
    {
        foreach (var arg in arguments)
        {
            // In edit mode provider validation is an editable-field concern: selector arguments
            // stay loader-authoritative (the edit loader decides whether the target exists). In
            // normal commands an explicit Provider makes the provider's choices authoritative for
            // supplied values; an implicit name-matched provider remains a prompting convenience
            // and never triggers argument validation.
            var eligible = editMode ? arg.Editable : GetEffectiveProvider(arg, editMode) != null;
            if (!eligible || !arg.ValidateAgainstProvider)
                continue;
            // Skip values that were chosen from a provider during prompting (valid by construction).
            if (parseResult.ArgumentValues.ContainsKey(arg) && !prePromptArguments.Contains(arg))
                continue;

            var provider = FindProvider(
                providers,
                settings.GetType(),
                arg.Property.Name,
                GetProviderKeyCandidates(arg, editMode),
                preferNamedProvider: GetEffectiveProvider(arg, editMode) != null);
            var result = await ValidateValueAgainstProviderAsync(
                settings, arg.Property, provider, $"<{arg.DisplayName}>",
                arg.ValueMatching, arguments, options, promptTimeout, ct, culture).ConfigureAwait(false);
            if (!result.IsSuccess)
                return result;
        }

        foreach (var opt in options)
        {
            if (!opt.Editable || !opt.ValidateAgainstProvider)
                continue;
            // Provider validation is scalar-only; repeated/key-value options are not validated.
            if (opt.ValueKind != OptionValueKind.Scalar)
                continue;
            // Skip values that were chosen from a provider during prompting (valid by construction).
            if (parseResult.OptionValues.ContainsKey(opt) && !prePromptOptions.Contains(opt))
                continue;

            var provider = FindProvider(
                providers,
                settings.GetType(),
                opt.Property.Name,
                GetProviderKeyCandidates(opt, editMode),
                preferNamedProvider: GetEffectiveProvider(opt, editMode) != null);
            var result = await ValidateValueAgainstProviderAsync(
                settings, opt.Property, provider, GetPreferredAlias(opt),
                opt.ValueMatching, arguments, options, promptTimeout, ct, culture).ConfigureAwait(false);
            if (!result.IsSuccess)
                return result;
        }

        return PromptResolutionResult.Success();
    }

    private static async Task<PromptResolutionResult> ValidateValueAgainstProviderAsync(
        TigerCliSettings settings,
        PropertyInfo property,
        ITigerCliValueProvider? provider,
        string displayName,
        TigerCliValueMatchPreset valueMatching,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        CultureInfo culture)
    {
        if (provider == null)
            return PromptResolutionResult.Success();

        var value = property.GetValue(settings);
        if (value == null)
            return PromptResolutionResult.Success();
        if (value is string text && text.Length == 0)
            return PromptResolutionResult.Success();

        var context = new TigerCliPromptContext(
            shell: null,
            settings.InteractionMode,
            promptTimeout,
            ct,
            culture,
            BuildProviderContextValues(settings, options, arguments));

        IReadOnlyList<TigerCliPromptChoice> choices;
        try
        {
            choices = await provider.GetChoicesAsync(settings, context).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Cooperative cancellation via the effective token propagates as a genuine cancellation;
            // it must never be reported as a provider/validation failure. An OperationCanceledException
            // whose token was NOT tripped falls through to the generic provider-failure handling below.
            throw;
        }
        catch (TigerCliProviderException ex)
        {
            return ProviderReportedFailure(displayName, ex);
        }
        catch (TigerCliPromptProviderConfigurationException ex)
        {
            return PromptResolutionResult.FailureLiteral(Esc(ex.Message), TigerCliExitKind.ValidationError);
        }
        catch (Exception ex)
        {
            return PromptResolutionResult.Failure(
                "Error_PromptProviderFailed",
                new object[] { Esc(displayName), Esc(GetUserFacingExceptionMessage(ex)) },
                TigerCliExitKind.UnhandledException);
        }

        // No choices means there is nothing to validate against.
        if (choices.Count == 0)
            return PromptResolutionResult.Success();

        var matchedKey = TigerCliProviderValueMatcher.FindKey(choices, value, valueMatching);
        if (matchedKey != null)
        {
            // Matching only decides whether the supplied value corresponds to a choice; the bound
            // value is always the provider's canonical key (e.g. "K:\" for a supplied "k:").
            BindCanonicalProviderKey(settings, property, matchedKey);
            return PromptResolutionResult.Success();
        }

        return PromptResolutionResult.Failure(
            "Error_InvalidProviderValue",
            new object[] { Esc(displayName), Esc(value.ToString() ?? string.Empty) },
            TigerCliExitKind.ValidationError);
    }

    /// <summary>
    /// Rebinds a validated single-select value to the provider's canonical key. String
    /// properties receive the key's string form; typed properties receive the key when it is
    /// already the property type (typed equality guaranteed the match), otherwise the existing
    /// value is left untouched.
    /// </summary>
    private static void BindCanonicalProviderKey(TigerCliSettings settings, PropertyInfo property, object matchedKey)
    {
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (targetType == typeof(string))
            property.SetValue(settings, matchedKey.ToString());
        else if (targetType.IsInstanceOfType(matchedKey))
            property.SetValue(settings, matchedKey);
    }

    private static int? FindChoiceIndex(IReadOnlyList<TigerCliPromptChoice> choices, object value) =>
        TigerCliProviderValueMatcher.FindIndex(choices, value);

    private static async Task<PromptResolutionResult> ValidateIntegerBoundsAsync(
        TigerCliSettings settings,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult,
        TigerCliEffectiveProviderMap providers,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        CultureInfo culture)
    {
        foreach (var arg in arguments)
        {
            if (!parseResult.ArgumentValues.ContainsKey(arg) || !IsIntegerTarget(arg.Property.PropertyType))
                continue;

            var boundsResult = await ResolveIntegerBoundsAsync(
                settings,
                $"<{arg.DisplayName}>",
                arg.MinValue,
                arg.MaxValue,
                arg.MinValueProvider,
                arg.MaxValueProvider,
                providers,
                arguments,
                options,
                promptTimeout,
                ct,
                culture).ConfigureAwait(false);
            if (boundsResult.Failure is { } failure)
                return failure;

            if (arg.Property.GetValue(settings) is int value)
            {
                var error = ValidateIntegerValue(value, boundsResult.Bounds, culture);
                if (error != null)
                    return PromptResolutionResult.FailureLiteral(Esc($"<{arg.DisplayName}>: {error}"), TigerCliExitKind.ValidationError);
            }
        }

        foreach (var opt in options)
        {
            if (!parseResult.OptionValues.ContainsKey(opt)
                || opt.ValueKind != OptionValueKind.Scalar
                || !IsIntegerTarget(opt.Property.PropertyType))
                continue;

            var displayName = GetPreferredAlias(opt);
            var boundsResult = await ResolveIntegerBoundsAsync(
                settings,
                displayName,
                opt.MinValue,
                opt.MaxValue,
                opt.MinValueProvider,
                opt.MaxValueProvider,
                providers,
                arguments,
                options,
                promptTimeout,
                ct,
                culture).ConfigureAwait(false);
            if (boundsResult.Failure is { } failure)
                return failure;

            if (opt.Property.GetValue(settings) is int value)
            {
                var error = ValidateIntegerValue(value, boundsResult.Bounds, culture);
                if (error != null)
                    return PromptResolutionResult.FailureLiteral(Esc($"{displayName}: {error}"), TigerCliExitKind.ValidationError);
            }
        }

        return PromptResolutionResult.Success();
    }

    private static async Task<(PromptResolutionResult? Failure, IntegerBounds Bounds)> ResolveIntegerBoundsAsync(
        TigerCliSettings settings,
        string displayName,
        int? minValue,
        int? maxValue,
        string? minValueProvider,
        string? maxValueProvider,
        TigerCliEffectiveProviderMap providers,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        CultureInfo culture)
    {
        if (minValue != null && minValueProvider != null)
        {
            return (IntegerBoundConfigurationError(
                $"{displayName} configures both MinValue and MinValueProvider."), IntegerBounds.Empty);
        }

        if (maxValue != null && maxValueProvider != null)
        {
            return (IntegerBoundConfigurationError(
                $"{displayName} configures both MaxValue and MaxValueProvider."), IntegerBounds.Empty);
        }

        var effectiveMin = minValue;
        var effectiveMax = maxValue;

        if (effectiveMin == null && minValueProvider != null)
        {
            var result = await ResolveIntegerBoundProviderAsync(
                settings,
                displayName,
                "MinValueProvider",
                minValueProvider,
                providers,
                arguments,
                options,
                promptTimeout,
                ct,
                culture).ConfigureAwait(false);
            if (result.Failure is { } failure)
                return (failure, IntegerBounds.Empty);
            effectiveMin = result.Value;
        }

        if (effectiveMax == null && maxValueProvider != null)
        {
            var result = await ResolveIntegerBoundProviderAsync(
                settings,
                displayName,
                "MaxValueProvider",
                maxValueProvider,
                providers,
                arguments,
                options,
                promptTimeout,
                ct,
                culture).ConfigureAwait(false);
            if (result.Failure is { } failure)
                return (failure, IntegerBounds.Empty);
            effectiveMax = result.Value;
        }

        if (effectiveMin is int min && effectiveMax is int max && min > max)
        {
            return (IntegerBoundConfigurationError(
                $"{displayName} has invalid integer bounds: minimum {min} is greater than maximum {max}."),
                IntegerBounds.Empty);
        }

        return (null, new IntegerBounds(effectiveMin, effectiveMax));
    }

    private static async Task<(PromptResolutionResult? Failure, int? Value)> ResolveIntegerBoundProviderAsync(
        TigerCliSettings settings,
        string displayName,
        string memberName,
        string providerKey,
        TigerCliEffectiveProviderMap providers,
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TimeSpan? promptTimeout,
        CancellationToken ct,
        CultureInfo culture)
    {
        if (!providers.Named.TryGetValue(providerKey, out var entry))
        {
            return (IntegerBoundConfigurationError(
                $"{displayName} {memberName} provider '{providerKey}' was not found."),
                null);
        }

        var context = new TigerCliPromptContext(
            shell: null,
            settings.InteractionMode,
            promptTimeout,
            ct,
            culture,
            BuildProviderContextValues(settings, options, arguments));

        IReadOnlyList<TigerCliPromptChoice> choices;
        try
        {
            choices = await entry.Provider.GetChoicesAsync(settings, context).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Cooperative cancellation via the effective token propagates as a genuine cancellation;
            // it must never be reported as a provider/validation failure. An OperationCanceledException
            // whose token was NOT tripped falls through to the generic provider-failure handling below.
            throw;
        }
        catch (TigerCliProviderException ex)
        {
            return (ProviderReportedFailure(displayName, ex), null);
        }
        catch (TigerCliPromptProviderConfigurationException ex)
        {
            return (PromptResolutionResult.FailureLiteral(Esc(ex.Message), TigerCliExitKind.ValidationError), null);
        }
        catch (Exception ex)
        {
            return (PromptResolutionResult.Failure(
                "Error_PromptProviderFailed",
                new object[] { Esc(displayName), Esc(GetUserFacingExceptionMessage(ex)) },
                TigerCliExitKind.UnhandledException), null);
        }

        if (choices.Count != 1)
        {
            return (IntegerBoundConfigurationError(
                $"{displayName} {memberName} provider '{providerKey}' must return exactly one value, but returned {choices.Count}."),
                null);
        }

        var rawValue = choices[0].Key;
        if (!TryResolveInteger(rawValue, culture, out var value))
        {
            return (IntegerBoundConfigurationError(
                $"{displayName} {memberName} provider '{providerKey}' returned '{rawValue}', which is not a valid integer."),
                null);
        }

        return (null, value);
    }

    private static PromptResolutionResult IntegerBoundConfigurationError(string message) =>
        PromptResolutionResult.FailureLiteral(Esc(message), TigerCliExitKind.ValidationError);

    private static bool TryResolveInteger(object value, CultureInfo culture, out int result)
    {
        switch (value)
        {
            case int intValue:
                result = intValue;
                return true;
            case string text:
                return int.TryParse(text, NumberStyles.Integer, culture, out result);
            case byte or sbyte or short or ushort:
                result = Convert.ToInt32(value, culture);
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                result = (int)longValue;
                return true;
            case uint uintValue when uintValue <= int.MaxValue:
                result = (int)uintValue;
                return true;
            default:
                result = default;
                return false;
        }
    }

    /// <summary>
    /// Central provider-name selection. In edit mode an explicit <c>EditProvider</c> (when
    /// configured) overrides <c>Provider</c>; otherwise <c>Provider</c> is used. In add/normal
    /// commands <c>EditProvider</c> is ignored and only <c>Provider</c> applies.
    /// </summary>
    private static string? GetEffectiveProvider(TigerCliOptionMetadata option, bool editMode)
        => editMode && option.EditProvider != null ? option.EditProvider : option.Provider;

    /// <inheritdoc cref="GetEffectiveProvider(TigerCliOptionMetadata, bool)"/>
    private static string? GetEffectiveProvider(TigerCliArgumentMetadata argument, bool editMode)
        => editMode && argument.EditProvider != null ? argument.EditProvider : argument.Provider;

    private static IReadOnlyList<string> GetProviderKeyCandidates(TigerCliOptionMetadata option, bool editMode)
    {
        if (GetEffectiveProvider(option, editMode) is { } provider)
            return [provider];

        var result = new List<string>();
        foreach (var alias in option.Aliases)
        {
            result.Add(alias.TrimStart('-'));
        }
        result.Add(option.Property.Name);
        return result;
    }

    private static IReadOnlyList<string> GetProviderKeyCandidates(TigerCliArgumentMetadata argument, bool editMode)
    {
        return GetEffectiveProvider(argument, editMode) is { } provider
            ? [provider]
            : [argument.DisplayName];
    }

    private static async Task<PromptResolutionResult> PromptWithProviderAsync(
        TigerCliSettings settings,
        ITigerCliValueProvider provider,
        TigerCliPromptContext context,
        string prompt,
        string displayName,
        bool required,
        bool allowNoSelection,
        bool autoSelectSingleChoice,
        CultureInfo culture,
        ResourceManager? appResources,
        object? currentValue = null)
    {
        IReadOnlyList<TigerCliPromptChoice> choices;
        try
        {
            // Interactive provider resolution may show a generic loading UI while a slow provider runs;
            // the wrapper returns either the loaded choices or a cancellation produced by that UI. The
            // provider call itself is unchanged, so failures and cooperative cancellation still flow into
            // the catch blocks below exactly as before.
            var resolved = await ResolveProviderChoicesAsync(settings, provider, context, culture, appResources).ConfigureAwait(false);
            if (resolved.Outcome == ProviderChoicesOutcome.Canceled)
                return PromptResolutionResult.Canceled(resolved.CanceledKind);
            choices = resolved.Choices;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // A provider that cooperatively observed the effective token cancelled the prompt; that is
            // not a provider failure. Fold it onto the existing prompt-cancellation path so the caller
            // surfaces the gentle "Cancelled." notice rather than a misleading Error_PromptProviderFailed. An
            // OperationCanceledException whose token was NOT tripped is treated as a genuine fault below.
            return PromptResolutionResult.Canceled(DialogResultKind.TokenCancel);
        }
        catch (TigerCliProviderException ex)
        {
            return ProviderReportedFailure(displayName, ex);
        }
        catch (TigerCliPromptProviderConfigurationException ex)
        {
            // Configuration exception messages are passed through verbatim (escaped).
            return PromptResolutionResult.FailureLiteral(
                Esc(ex.Message),
                TigerCliExitKind.ValidationError);
        }
        catch (Exception ex)
        {
            return PromptResolutionResult.Failure(
                "Error_PromptProviderFailed",
                new object[] { Esc(displayName), Esc(GetUserFacingExceptionMessage(ex)) },
                TigerCliExitKind.UnhandledException);
        }

        var offset = allowNoSelection ? 1 : 0;
        var selectableOutcomeCount = choices.Count + offset;
        if (selectableOutcomeCount == 0)
        {
            return required
                ? BuildProviderNoChoicesFailure(provider, displayName, culture, appResources)
                : PromptResolutionResult.Success();
        }

        // Optional nullable fields get a synthetic no-selection row at index 0 (a null label that
        // renders via the grid null-display path). It shifts the provider choices by one.
        var labels = new List<string?>(choices.Count + offset);
        if (allowNoSelection)
            labels.Add(null);
        labels.AddRange(choices.Select(choice => choice.Label));

        if (autoSelectSingleChoice && selectableOutcomeCount == 1)
        {
            return allowNoSelection
                ? PromptResolutionResult.Success(null)
                : PromptResolutionResult.Success(choices[0].Key);
        }

        // Preselect: a current value still offered → its (shifted) choice. A null or stale current
        // value → the no-selection row when offered (stale values are never injected). When no
        // no-selection row exists, a stale value leaves nothing preselected, as before.
        int? preselectIndex;
        var matchedChoice = currentValue != null ? FindChoiceIndex(choices, currentValue) : null;
        if (matchedChoice is int matched)
            preselectIndex = matched + offset;
        else
            preselectIndex = allowNoSelection ? 0 : (int?)null;

        AdoptSingletonCultureIfNoShell(context.Shell, culture);
        var selected = context.Shell == null
            ? await TigerTui.SelectIndexResultAsync(prompt, labels, preselectIndex, timeout: context.Timeout, ct: context.CancellationToken)
                .ConfigureAwait(false)
            : await TigerTui.SelectIndexResultAsync(context.Shell, prompt, labels, preselectIndex, timeout: context.Timeout, ct: context.CancellationToken)
                .ConfigureAwait(false);

        if (!selected.IsOk)
            return PromptResolutionResult.Canceled(selected.ResultKind);
        int index = selected.Value;
        if (allowNoSelection && index == 0)
            return PromptResolutionResult.Success(null);
        return PromptResolutionResult.Success(choices[index - offset].Key);
    }

    // How long a slow interactive provider may run before the generic loading UI appears. A fast
    // provider that finishes inside this window proceeds straight to the select with no visible loading
    // flash; only a provider still running after it shows the spinner. Kept small so the wait never feels
    // unexplained, but above the modal loop's ~10ms poll so a near-instant provider does not flicker.
    private static readonly TimeSpan ProviderLoadingDisplayThreshold = TimeSpan.FromMilliseconds(150);

    private enum ProviderChoicesOutcome { Loaded, Canceled }

    private readonly record struct ProviderChoicesResult(
        ProviderChoicesOutcome Outcome,
        IReadOnlyList<TigerCliPromptChoice> Choices,
        DialogResultKind CanceledKind)
    {
        public static ProviderChoicesResult Loaded(IReadOnlyList<TigerCliPromptChoice> choices) =>
            new(ProviderChoicesOutcome.Loaded, choices, DialogResultKind.NoResult);

        public static ProviderChoicesResult Canceled(DialogResultKind kind) =>
            new(ProviderChoicesOutcome.Canceled, Array.Empty<TigerCliPromptChoice>(), kind);
    }

    /// <summary>
    /// Resolves a provider's choices, showing a generic loading UI when interactive and the provider is
    /// slow enough to be noticeable. Non-interactive resolution (and any path without prompting) runs the
    /// provider directly with the effective token and no UI — unchanged from before. Async providers are
    /// awaited normally; only sync-origin providers (<see cref="ITigerCliValueProvider.IsAsync"/> is
    /// <c>false</c>) are offloaded here, and only on this interactive path, so the spinner can animate
    /// while the synchronous delegate runs. Provider exceptions propagate to the caller's existing
    /// handling; cancellation from the loading UI is returned as a <see cref="ProviderChoicesOutcome.Canceled"/>
    /// outcome carrying the modal's result kind.
    /// </summary>
    private static async Task<ProviderChoicesResult> ResolveProviderChoicesAsync(
        TigerCliSettings settings,
        ITigerCliValueProvider provider,
        TigerCliPromptContext context,
        CultureInfo culture,
        ResourceManager? appResources)
    {
        // No prompting → resolve directly, exactly as before, still honoring the effective token.
        if (context.InteractionMode == TigerCliInteractionMode.NonInteractive)
            return ProviderChoicesResult.Loaded(
                await provider.GetChoicesAsync(settings, context).ConfigureAwait(false));

        // A token the loading UI can cancel independently of the caller's original token.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        var loadContext = context.WithCancellationToken(linked.Token);

        // Async providers run as-is (no Task.Run). Sync-origin providers would otherwise block the calling
        // thread inside GetChoicesAsync, so they are offloaded — narrowly, only here — to let the spinner
        // render while they run.
        var loadTask = provider.IsAsync
            ? provider.GetChoicesAsync(settings, loadContext)
            : Task.Run(() => provider.GetChoicesAsync(settings, loadContext), linked.Token);

        if (!loadTask.IsCompleted)
        {
            var delayTask = Task.Delay(ProviderLoadingDisplayThreshold, linked.Token);
            var first = await Task.WhenAny(loadTask, delayTask).ConfigureAwait(false);
            if (!ReferenceEquals(first, loadTask) && !linked.IsCancellationRequested)
            {
                // Still running after the threshold → show the generic loading UI until it finishes (or
                // the user/caller/system cancels). The select that follows reuses the same shell.
                AdoptSingletonCultureIfNoShell(context.Shell, culture);
                var shell = context.Shell ?? InlineShell.Instance;
                var message = ResolveProviderLoadingMessage(provider, culture, appResources);

                var dr = await TigerTui.RunProviderLoadingAsync(
                    shell, loadTask, message, context.Timeout, context.CancellationToken).ConfigureAwait(false);

                if (dr.Kind != DialogResultKind.Ok)
                {
                    // Cancellation / timeout / system-cancel while loading: stop the provider cooperatively
                    // and fold onto the existing cancellation model via the modal's own result kind.
                    linked.Cancel();
                    ObserveAbandonedLoad(loadTask);
                    return ProviderChoicesResult.Canceled(dr.Kind);
                }
            }
        }

        // Fast path, or the loading modal completed on the provider finishing: await the task so the real
        // choices — or a provider exception, or a cooperative OperationCanceledException — flow to the
        // caller's existing success/failure/cancellation handling.
        return ProviderChoicesResult.Loaded(await loadTask.ConfigureAwait(false));
    }

    // A load abandoned by a cancelled loading UI keeps running until it observes cancellation; attach a
    // continuation that observes any fault so an abandoned provider failure never surfaces as an
    // unobserved task exception.
    private static void ObserveAbandonedLoad(Task task) =>
        _ = task.ContinueWith(static t => { _ = t.Exception; }, TaskScheduler.Default);

    // The message the loading UI shows for this provider: the provider's optional custom message
    // (literal, or an app resource key resolved through the same literal-or-key path descriptions use),
    // falling back to the generic localized default when none was configured or the key did not resolve.
    private static string ResolveProviderLoadingMessage(
        ITigerCliValueProvider provider, CultureInfo culture, ResourceManager? appResources)
    {
        if (provider.LoadingMessage is { } loadingMessage)
        {
            var resolved = TigerCliAppText.Resolve(
                loadingMessage.Text, loadingMessage.ResourceKey, culture, appResources);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;
        }

        return TigerCliResources.Get("Tui_Loading_Options", culture);
    }

    private static PromptResolutionResult BuildProviderNoChoicesFailure(
        ITigerCliValueProvider provider,
        string displayName,
        CultureInfo culture,
        ResourceManager? appResources)
    {
        if (provider.EmptyMessage is { } emptyMessage)
        {
            var resolved = TigerCliAppText.Resolve(
                emptyMessage.Text, emptyMessage.ResourceKey, culture, appResources);
            if (!string.IsNullOrEmpty(resolved))
                return PromptResolutionResult.FailureLiteral(Esc(resolved), TigerCliExitKind.ValidationError);
        }

        return PromptResolutionResult.Failure(
            "Error_PromptNoChoices",
            new object[] { Esc(displayName) },
            TigerCliExitKind.ValidationError);
    }

    /// <summary>
    /// Builds the terminal result for a provider that deliberately reported a failure by throwing
    /// <see cref="TigerCliProviderException"/>: the provider's user-facing message is reported for
    /// the field and the run maps through <see cref="TigerCliExitKind.ProviderError"/>. This is
    /// distinct from the empty-choices path (the provider produced nothing selectable) and from an
    /// arbitrary provider exception (an unexpected fault mapped through
    /// <see cref="TigerCliExitKind.UnhandledException"/>).
    /// </summary>
    private static PromptResolutionResult ProviderReportedFailure(string displayName, TigerCliProviderException ex) =>
        PromptResolutionResult.Failure(
            "Error_ProviderFailed",
            new object[] { Esc(displayName), Esc(ex.Message) },
            TigerCliExitKind.ProviderError);

    /// <summary>
    /// Reads the current effective settings value to use as a prompt default/preselect.
    /// In add mode this is the property's initializer (or the CLR default when there is no
    /// initializer); in edit mode it is the merged existing value. CLI-supplied values are
    /// excluded before prompting — they are recorded in the parse ledger and skipped — so this
    /// never overrides a value the user typed on the command line. The per-kind prompt helpers
    /// decide what is meaningful: null strings/bools yield no default, enums preselect the value
    /// (including the zero member for enums without an initializer, consistent with help's
    /// default display), and provider-backed values are preselected only when they match a
    /// current provider choice (stale/unknown defaults are never injected).
    /// </summary>
    private static object? GetPromptDefaultValue(PropertyInfo property, TigerCliSettings settings)
        => property.GetValue(settings);

    /// <summary>
    /// Whether a select-style prompt for this field should offer a synthetic "no-selection"
    /// (null) row as its first choice. True only when the field is optional under the current
    /// effective rules AND the target property can represent null. The requiredness decision is
    /// the same one used everywhere else (<see cref="IsOptionRequired"/>), so RequiredWhen and
    /// friends are honored. The caller passes the already-evaluated <paramref name="required"/>.
    /// Whether the prompt is actually select-style (enum / provider) is decided by the prompt
    /// helpers; non-select prompts (text input, confirm) simply ignore the flag.
    /// </summary>
    private static bool AllowsNoSelection(PropertyInfo property, bool required)
        => !required && IsNullableTarget(property);

    /// <summary>
    /// Whether the property can hold null: a <see cref="Nullable{T}"/> value type, or a reference
    /// type annotated as nullable (NRT). Non-nullable value types and not-null reference types
    /// return false, preserving existing behavior for non-nullable fields.
    /// </summary>
    private static bool IsNullableTarget(PropertyInfo property)
    {
        var type = property.PropertyType;
        if (Nullable.GetUnderlyingType(type) != null)
            return true;
        if (type.IsValueType)
            return false;
        // Reference type: honor the nullable-reference annotation; only an explicit nullable
        // annotation (string?, etc.) opts in. Unknown/oblivious annotations are treated as not-null.
        return new NullabilityInfoContext().Create(property).WriteState == NullabilityState.Nullable;
    }

    private static bool CanPrompt(Type propertyType)
    {
        var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (underlying == typeof(string))
            return true;
        if (underlying == typeof(int))
            return true;
        if (underlying.IsEnum)
            return true;
        if (propertyType == typeof(bool?))
            return true;

        return false;
    }

    private static bool IsIntegerTarget(Type propertyType)
    {
        var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return underlying == typeof(int);
    }

    private static async Task<PromptResolutionResult> PromptForAutomaticValueAsync(
        ICliAppShell? shell,
        string prompt,
        Type propertyType,
        string[]? enumNames,
        Func<string, string?>? textValidator,
        bool isSecret,
        bool allowNoSelection,
        TimeSpan? timeout,
        CancellationToken ct,
        CultureInfo culture,
        ResourceManager? appResources,
        object? currentValue = null,
        bool useFolderPicker = false,
        IFolderBrowser? folderBrowser = null)
    {
        var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (underlying == typeof(string))
        {
            // Text input is not select-style, so no-selection does not apply here. A null result
            // is a cancel (an empty-but-confirmed entry comes back as ""); seed with the current value.
            var initialValue = currentValue as string;
            AdoptSingletonCultureIfNoShell(shell, culture);

            // Folder-picker options resolve their missing value through InlineFolderSelect instead of
            // a text prompt; the current/default value seeds the initial folder. Cancel returns null.
            if (useFolderPicker)
            {
                var folder = shell == null
                    ? await TigerTui.SelectFolderResultAsync(prompt, initialValue, timeout: timeout, ct: ct).ConfigureAwait(false)
                    : await TigerTui.SelectFolderResultAsync(shell, prompt, initialValue, folderBrowser, timeout: timeout, ct: ct).ConfigureAwait(false);
                return folder.Value is string folderPath
                    ? PromptResolutionResult.Success(folderPath)
                    : PromptResolutionResult.Canceled(folder.ResultKind);
            }

            var value = isSecret
                ? shell == null
                    ? await TigerTui.SecretInputResultAsync(prompt, initialValue, timeout: timeout, ct: ct, validator: textValidator).ConfigureAwait(false)
                    : await TigerTui.SecretInputResultAsync(shell, prompt, initialValue, timeout: timeout, ct: ct, validator: textValidator).ConfigureAwait(false)
                : shell == null
                    ? await TigerTui.InputResultAsync(prompt, initialValue, timeout: timeout, ct: ct, validator: textValidator).ConfigureAwait(false)
                    : await TigerTui.InputResultAsync(shell, prompt, initialValue, timeout: timeout, ct: ct, validator: textValidator).ConfigureAwait(false);
            return value.Value is string text
                ? PromptResolutionResult.Success(text)
                : PromptResolutionResult.Canceled(value.ResultKind);
        }

        if (underlying == typeof(int))
        {
            var initialValue = currentValue is int current
                ? current.ToString(culture)
                : null;
            AdoptSingletonCultureIfNoShell(shell, culture);
            var value = shell == null
                ? await TigerTui.InputResultAsync(prompt, initialValue, timeout: timeout, ct: ct, validator: textValidator).ConfigureAwait(false)
                : await TigerTui.InputResultAsync(shell, prompt, initialValue, timeout: timeout, ct: ct, validator: textValidator).ConfigureAwait(false);
            if (value.Value is not string text)
                return PromptResolutionResult.Canceled(value.ResultKind);
            if (Nullable.GetUnderlyingType(propertyType) != null && text.Length == 0)
                return PromptResolutionResult.Success(null);
            return int.TryParse(text, NumberStyles.Integer, culture, out var parsed)
                ? PromptResolutionResult.Success(parsed)
                : PromptResolutionResult.Canceled();
        }

        if (propertyType == typeof(bool?))
        {
            // Confirm (yes/no) is out of scope for no-selection; null is a cancel. Preselect current.
            var preselect = currentValue as bool?;
            AdoptSingletonCultureIfNoShell(shell, culture);
            var confirm = shell == null
                ? await TigerTui.ConfirmResultAsync(prompt, preselect, timeout: timeout, ct: ct).ConfigureAwait(false)
                : await TigerTui.ConfirmResultAsync(shell, prompt, preselect, timeout: timeout, ct: ct).ConfigureAwait(false);
            // Match the simple ConfirmAsync adapter exactly: Yes/No/Escape-Cancel are answers (Cancel
            // folds to false), only Timeout/TokenCancel are treated as a prompt cancellation.
            bool? value = confirm.ResultKind switch
            {
                DialogResultKind.Yes => true,
                DialogResultKind.No => false,
                DialogResultKind.Cancel => false,
                _ => (bool?)null,
            };
            return value == null
                ? PromptResolutionResult.Canceled(confirm.ResultKind)
                : PromptResolutionResult.Success(value);
        }

        if (underlying.IsEnum)
        {
            AdoptSingletonCultureIfNoShell(shell, culture);
            // Flags multi-select is out of scope for no-selection; a null result is a cancel.
            if (underlying.IsDefined(typeof(FlagsAttribute), inherit: false))
            {
                var flags = await PromptFlagsEnumAsync(shell, prompt, underlying, timeout, ct, culture, appResources, currentValue).ConfigureAwait(false);
                return flags.IsOk ? PromptResolutionResult.Success(flags.Value) : PromptResolutionResult.Canceled(flags.ResultKind);
            }

            return await PromptEnumAsync(
                shell, prompt, underlying, enumNames, allowNoSelection, timeout, ct, culture, appResources, currentValue)
                .ConfigureAwait(false);
        }

        return PromptResolutionResult.Success();
    }

    private static async Task<PromptResolutionResult> PromptEnumAsync(
        ICliAppShell? shell,
        string prompt,
        Type enumType,
        string[]? enumNames,
        bool allowNoSelection,
        TimeSpan? timeout,
        CancellationToken ct,
        CultureInfo culture,
        ResourceManager? appResources,
        object? currentValue = null)
    {
        var filterSet = enumNames is { Length: > 0 }
            ? new HashSet<string>(enumNames, StringComparer.OrdinalIgnoreCase)
            : null;

        var values = new List<object>();
        var labels = new List<string?>();

        // Optional nullable enums get a synthetic no-selection row at index 0 (a null label that
        // renders via the grid null-display path), shifting the enum members by one.
        var offset = allowNoSelection ? 1 : 0;
        if (allowNoSelection)
            labels.Add(null);

        foreach (var value in Enum.GetValues(enumType))
        {
            if (value == null)
                continue;
            var name = value.ToString()!;
            if (filterSet != null && !filterSet.Contains(name))
                continue;

            values.Add(value);
            labels.Add(ResolveEnumMemberLabel(enumType, name, culture, appResources));
        }

        var labelsView = labels.ToArray();
        // Preselect the current enum value when present; otherwise the no-selection row when offered.
        int? preselectIndex;
        var matched = currentValue != null ? values.FindIndex(v => Equals(v, currentValue)) : -1;
        if (matched >= 0)
            preselectIndex = matched + offset;
        else
            preselectIndex = allowNoSelection ? 0 : (int?)null;

        var picked = shell == null
            ? await TigerTui.SelectIndexResultAsync(prompt, labelsView, preselectIndex, timeout: timeout, ct: ct).ConfigureAwait(false)
            : await TigerTui.SelectIndexResultAsync(shell, prompt, labelsView, preselectIndex, timeout: timeout, ct: ct).ConfigureAwait(false);

        if (!picked.IsOk)
            return PromptResolutionResult.Canceled(picked.ResultKind);
        int ix = picked.Value;
        if (allowNoSelection && ix == 0)
            return PromptResolutionResult.Success(null);
        return PromptResolutionResult.Success(values[ix - offset]);
    }

    private static Task<TigerTuiResult<object?>> PromptFlagsEnumAsync(
        ICliAppShell? shell,
        string prompt,
        Type enumType,
        TimeSpan? timeout,
        CancellationToken ct,
        CultureInfo culture,
        ResourceManager? appResources,
        object? currentValue = null)
    {
        var method = typeof(TigerCliApp)
            .GetMethod(nameof(PromptFlagsEnumTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(enumType);

        return (Task<TigerTuiResult<object?>>)method.Invoke(
            null,
            [shell, prompt, timeout, ct, culture, appResources, currentValue])!;
    }

    private static async Task<TigerTuiResult<object?>> PromptFlagsEnumTypedAsync<TEnum>(
        ICliAppShell? shell,
        string prompt,
        TimeSpan? timeout,
        CancellationToken ct,
        CultureInfo culture,
        ResourceManager? appResources,
        object? currentValue)
        where TEnum : struct, Enum
    {
        Func<TEnum, string> labelSelector = value =>
            ResolveEnumMemberLabel(typeof(TEnum), value.ToString(), culture, appResources);

        // Edit-only: preselect the current flags combination.
        var selected = currentValue is TEnum current ? current : default;

        var result = shell == null
            ? await TigerTui.MultiSelectFlagsResultAsync(prompt, selected, labelSelector, timeout, ct).ConfigureAwait(false)
            : await TigerTui.MultiSelectFlagsResultAsync(shell, prompt, selected, labelSelector, timeout, ct).ConfigureAwait(false);

        return result.IsOk
            ? TigerTuiResult<object?>.Ok(result.Value)
            : TigerTuiResult<object?>.FromKind(result.ResultKind);
    }

    private static string ResolveEnumMemberLabel(Type enumType, string memberName, CultureInfo culture, ResourceManager? appResources)
    {
        var field = enumType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
        return field != null
            ? TigerCliEnumText.Resolve(field, culture, appResources).Label
            : memberName;
    }

    private static string ConvertPromptValueToRaw(object value)
    {
        if (value is bool boolValue)
            return boolValue ? "true" : "false";

        return value.ToString() ?? string.Empty;
    }

    private static string FormatArgumentPrompt(
        TigerCliArgumentMetadata arg, CultureInfo culture, ResourceManager? appResources)
    {
        var description = TigerCliAppText.Resolve(arg.Description, arg.DescriptionResourceKey, culture, appResources);
        return !string.IsNullOrWhiteSpace(description)
            ? description
            : $"Enter {arg.DisplayName}";
    }

    private static string FormatOptionPrompt(
        TigerCliOptionMetadata opt, CultureInfo culture, ResourceManager? appResources)
    {
        var description = TigerCliAppText.Resolve(opt.Description, opt.DescriptionResourceKey, culture, appResources);
        return !string.IsNullOrWhiteSpace(description)
            ? description
            : $"Enter {GetPreferredAlias(opt)}";
    }

    private static Func<string, string?>? BuildStringPromptValidator(
        bool required,
        int? minLength,
        int? maxLength,
        CultureInfo culture)
    {
        if (!required && minLength == null && maxLength == null)
            return null;

        return value => ValidatePromptStringValue(value, required, minLength, maxLength, culture);
    }

    private static Func<string, string?>? BuildIntegerPromptValidator(
        bool required,
        IntegerBounds bounds,
        CultureInfo culture)
    {
        if (!required && bounds.Min == null && bounds.Max == null)
            return null;

        return value => ValidatePromptIntegerValue(value, required, bounds, culture);
    }

    private static string? ValidatePromptStringValue(
        string? value,
        bool required,
        int? minLength,
        int? maxLength,
        CultureInfo culture)
    {
        var length = value?.Length ?? 0;
        if (required && length == 0)
            return TigerCliResources.Get("PromptValidation_Required", culture);
        if (minLength is int min && length < min)
            return TigerCliResources.Format("PromptValidation_MinLength", culture, min);
        if (maxLength is int max && length > max)
            return TigerCliResources.Format("PromptValidation_MaxLength", culture, max);

        return null;
    }

    private static string? ValidatePromptIntegerValue(
        string? value,
        bool required,
        IntegerBounds bounds,
        CultureInfo culture)
    {
        if (string.IsNullOrEmpty(value))
            return required ? TigerCliResources.Get("PromptValidation_Required", culture) : null;

        if (!int.TryParse(value, NumberStyles.Integer, culture, out var parsed))
            return TigerCliResources.Get("PromptValidation_Integer", culture);

        return ValidateIntegerValue(parsed, bounds, culture);
    }

    private static string? ValidateIntegerValue(int value, IntegerBounds bounds, CultureInfo culture)
    {
        if (bounds.Min is int min && bounds.Max is int max && (value < min || value > max))
            return TigerCliResources.Format("PromptValidation_ValueRange", culture, min, max);
        if (bounds.Min is int minOnly && value < minOnly)
            return TigerCliResources.Format("PromptValidation_MinValue", culture, minOnly);
        if (bounds.Max is int maxOnly && value > maxOnly)
            return TigerCliResources.Format("PromptValidation_MaxValue", culture, maxOnly);

        return null;
    }

    private static string? ValidateStringValue(
        string? value,
        bool required,
        int? minLength,
        int? maxLength,
        string displayName)
    {
        var length = value?.Length ?? 0;
        if (required && length == 0)
            return $"{displayName} is required.";
        if (minLength is int min && length < min)
            return $"{displayName} must be at least {min} characters.";
        if (maxLength is int max && length > max)
            return $"{displayName} must be at most {max} characters.";

        return null;
    }

    private readonly record struct PromptResolutionResult(
        bool IsSuccess,
        string? ErrorResourceKey,
        object[]? ErrorArgs,
        bool ErrorIsLiteral,
        TigerCliExitKind ExitKind,
        bool HasValue,
        object? Value,
        bool IsCanceled = false,
        DialogResultKind? CanceledKind = null,
        bool IsUserCancellation = false)
    {
        public static PromptResolutionResult Success() => new(true, null, null, false, TigerCliExitKind.Success, false, null);

        // A prompted value to bind. <paramref name="value"/> may legitimately be null — that is a
        // "no-selection" outcome (bind null), distinct from cancellation (see <see cref="Canceled"/>).
        public static PromptResolutionResult Success(object? value) =>
            new(true, null, null, false, TigerCliExitKind.Success, true, value);

        // The user dismissed the prompt (Escape / timeout / token cancel). This is NOT the same as
        // selecting a no-selection row; the caller turns it into a UserCancellation outcome. The kindless
        // overload is for cancellations not driven by the modal loop (e.g. an unparseable entry).
        public static PromptResolutionResult Canceled() =>
            new(true, null, null, false, TigerCliExitKind.Success, false, null, IsCanceled: true);

        // Cancellation carrying the exact DialogResultKind reported by the modal loop (Cancel /
        // TokenCancel / Timeout / ...). The kind is preserved for internal branching; the current
        // exit mapping treats every cancellation kind the same (the gentle "Cancelled." notice).
        public static PromptResolutionResult Canceled(DialogResultKind kind) =>
            new(true, null, null, false, TigerCliExitKind.Success, false, null, IsCanceled: true, CanceledKind: kind);

        // The user deliberately cancelled a prompt (Escape / timeout / token / system cancel) after a
        // command was selected. This is a terminal, non-error outcome: the caller renders a gentle
        // "Cancelled." notice (muted, no error prefix) and maps to TigerCliExitKind.Cancelled. It is
        // distinct from a no-selection outcome (which binds null and succeeds) and from a genuine
        // failure (which carries an error resource key).
        public static PromptResolutionResult UserCancellation() =>
            new(false, null, null, false, TigerCliExitKind.Cancelled, false, null, IsUserCancellation: true);

        public static PromptResolutionResult Failure(string resourceKey, object[] args, TigerCliExitKind exitKind) =>
            new(false, resourceKey, args, false, exitKind, false, null);

        public static PromptResolutionResult FailureLiteral(string escapedMessage, TigerCliExitKind exitKind) =>
            new(false, escapedMessage, null, true, exitKind, false, null);
    }

    private readonly record struct IntegerBounds(int? Min, int? Max)
    {
        public static IntegerBounds Empty { get; } = new(null, null);
    }

    // ── Framework validation ────────────────────────────────────────

    private static TigerCliValidationResult ValidateCommandLineValuePolicy(
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult,
        CultureInfo culture)
    {
        foreach (var opt in options)
        {
            if (opt.AllowCommandLineValue || !parseResult.OptionValues.ContainsKey(opt))
                continue;

            return TigerCliValidationResult.Error(TigerCliResources.Format(
                "Error_OptionCommandLineValueNotAllowed",
                culture,
                Esc(GetPreferredAlias(opt))));
        }

        return TigerCliValidationResult.Success();
    }

    private static TigerCliValidationResult ValidateFrameworkRules(
        List<TigerCliArgumentMetadata> arguments,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult,
        TigerCliSettings settings,
        CultureInfo culture)
    {
        foreach (var arg in arguments)
        {
            if (!parseResult.ArgumentValues.ContainsKey(arg))
                continue;

            if (arg.Property.PropertyType != typeof(string))
                continue;

            var value = arg.Property.GetValue(settings) as string;
            var error = ValidateStringValue(
                value,
                required: true,
                arg.MinLength,
                arg.MaxLength,
                $"<{arg.DisplayName}>");
            if (error != null)
                return TigerCliValidationResult.Error(error);
        }

        foreach (var opt in options)
        {
            bool wasProvided = parseResult.OptionValues.ContainsKey(opt);
            bool isRequired = IsOptionRequired(opt, options, settings);

            // Required: must be provided by argv or an accepted prompt.
            if (isRequired && !wasProvided)
            {
                var name = opt.Aliases[^1]; // prefer long alias
                return TigerCliValidationResult.Error(
                    TigerCliResources.Format("Error_MissingOption", culture, Esc(name)));
            }

            if (wasProvided && opt.Property.PropertyType == typeof(string))
            {
                var name = opt.Aliases[^1];
                var value = opt.Property.GetValue(settings) as string;
                var error = ValidateStringValue(
                    value,
                    isRequired,
                    opt.MinLength,
                    opt.MaxLength,
                    name);
                if (error != null)
                    return TigerCliValidationResult.Error(error);
            }

            // Forbidden values: reject even if explicitly provided
            if (opt.ForbiddenValues is { Length: > 0 })
            {
                var currentValue = opt.Property.GetValue(settings);
                if (currentValue != null)
                {
                    foreach (var forbidden in opt.ForbiddenValues)
                    {
                        if (forbidden.Equals(currentValue))
                        {
                            var name = opt.Aliases[^1];
                            return TigerCliValidationResult.Error(
                                TigerCliResources.Format(
                                    "Error_ForbiddenValue",
                                    culture,
                                    Esc(name),
                                    Esc(currentValue.ToString() ?? string.Empty)));
                        }
                    }
                }
            }
        }

        // ExactlyOneOf group validation
        var exactlyOneOfResult = ValidateExactlyOneOfGroups(
            settings.GetType(), options, parseResult, culture);
        if (!exactlyOneOfResult.IsValid)
            return exactlyOneOfResult;

        return TigerCliValidationResult.Success();
    }

    private static TigerCliValidationResult ValidateExactlyOneOfGroups(
        Type settingsType,
        List<TigerCliOptionMetadata> options,
        TigerCliParseResult parseResult,
        CultureInfo culture)
    {
        var groups = settingsType.GetCustomAttributes(typeof(TigerCliExactlyOneOfAttribute), inherit: true)
            .Cast<TigerCliExactlyOneOfAttribute>();

        foreach (var group in groups)
        {
            var resolved = ResolveGroupOptions(group.PropertyNames, options);
            var providedCount = resolved.Count(opt => parseResult.OptionValues.ContainsKey(opt));

            if (providedCount != 1)
            {
                // Group.Description is app-supplied and passes through verbatim (escaped).
                var message = group.Description != null
                    ? Esc(group.Description)
                    : FormatExactlyOneOfMessage(resolved, culture);
                return TigerCliValidationResult.Error(message);
            }
        }

        return TigerCliValidationResult.Success();
    }

    // ── ExactlyOneOf helpers ─────────────────────────────────────────

    private static List<TigerCliOptionMetadata> ResolveGroupOptions(
        string[] propertyNames,
        List<TigerCliOptionMetadata> options)
    {
        var result = new List<TigerCliOptionMetadata>(propertyNames.Length);
        foreach (var name in propertyNames)
        {
            var match = options.FirstOrDefault(o =>
                string.Equals(o.Property.Name, name, StringComparison.Ordinal));
            if (match == null)
                throw new InvalidOperationException(
                    $"TigerCliExactlyOneOf references property '{name}' which has no [TigerCliOption] attribute.");
            result.Add(match);
        }
        return result;
    }

    private static string GetPreferredAlias(TigerCliOptionMetadata opt)
    {
        // Prefer the long alias (last in the array); fall back to first
        return opt.Aliases[^1];
    }

    private static string FormatExactlyOneOfMessage(List<TigerCliOptionMetadata> options, CultureInfo culture)
    {
        var names = options.Select(GetPreferredAlias).ToList();

        // 2 items: "Exactly one of A or B must be specified."
        // 3+ items: "Exactly one of A, B or C must be specified."
        if (names.Count == 2)
            return TigerCliResources.Format(
                "Error_ExactlyOneOfTwo", culture, Esc(names[0]), Esc(names[1]));

        var leading = string.Join(", ", names.Take(names.Count - 1).Select(Esc));
        return TigerCliResources.Format(
            "Error_ExactlyOneOfMany", culture, leading, Esc(names[^1]));
    }

    internal static List<string> GetExactlyOneOfNotes(
        Type settingsType,
        List<TigerCliOptionMetadata> options,
        CultureInfo culture)
    {
        var notes = new List<string>();
        var groups = settingsType.GetCustomAttributes(typeof(TigerCliExactlyOneOfAttribute), inherit: true)
            .Cast<TigerCliExactlyOneOfAttribute>();

        foreach (var group in groups)
        {
            var resolved = ResolveGroupOptions(group.PropertyNames, options);
            notes.Add(group.Description ?? FormatExactlyOneOfMessage(resolved, culture));
        }

        return notes;
    }

    // ── Execution ───────────────────────────────────────────────────

    private static async Task<int> ExecuteHandler(TigerCliCommandRegistration command, TigerCliSettings settings)
    {
        var handler = command.Factory != null
            ? command.Factory()
            : Activator.CreateInstance(command.HandlerType)!;
        var method = command.HandlerType.GetMethod(
            "ExecuteAsync",
            BindingFlags.Public | BindingFlags.Instance,
            [command.SettingsType]);

        if (method == null)
            throw new InvalidOperationException(
                $"No ExecuteAsync({command.SettingsType.Name}) method found on '{command.HandlerType.Name}'.");

        // A handler that throws synchronously (before returning its Task) surfaces from
        // reflection as TargetInvocationException; unwrap it so callers observe the handler's
        // real exception type — the same one an async handler's faulted task produces.
        Task task;
        try
        {
            task = (Task)method.Invoke(handler, [settings])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }

        await task;

        var resultProperty = task.GetType().GetProperty("Result");
        var result = resultProperty?.GetValue(task);
        if (result is int exitCode)
            return exitCode;

        if (result is Enum enumExitCode)
            return Convert.ToInt32(enumExitCode);

        throw new InvalidOperationException(
            $"ExecuteAsync({command.SettingsType.Name}) on '{command.HandlerType.Name}' must return Task<int> or Task<TExitCode>.");
    }

    private static string GetUserFacingExceptionMessage(Exception exception)
    {
        var unwrapped = UnwrapInvocationException(exception);
        return !string.IsNullOrEmpty(unwrapped.Message)
            ? unwrapped.Message
            : exception.Message;
    }

    private static Exception UnwrapInvocationException(Exception exception)
    {
        var current = exception;
        while (true)
        {
            if (current is TargetInvocationException { InnerException: { } targetInner })
            {
                current = targetInner;
                continue;
            }

            if (current is AggregateException { InnerExceptions.Count: 1 } aggregate &&
                aggregate.InnerExceptions[0] is { } aggregateInner)
            {
                current = aggregateInner;
                continue;
            }

            return current;
        }
    }

    // ── Help ────────────────────────────────────────────────────────

    private void PrintHelp(
        TigerCliCommandRegistration? command,
        CultureInfo culture,
        TigerCliCommandAliasRegistration? alias = null)
    {
        var appName = string.IsNullOrEmpty(_applicationName) ? "app" : _applicationName;
        var safeAppName = Esc(appName);

        if (command == null || command.Name == null)
        {
            // Root help. Title and Usage render as structured CliGrid blocks; the markup content
            // (and section ordering) is unchanged apart from the semantic Key/Value styling.
            var appDescription = TigerCliAppText.Resolve(_description, _descriptionResourceKey, culture, _appResources);
            TigerCliHelpRenderer.RenderTitleBlock($"[Key]{safeAppName}[/]", appDescription);
            TigerConsole.MarkupLine("");

            var optionsUsage = $"[Value]{FormatOptionsUsage(culture)}[/]";
            var hasCommandsOrGroups = _namedCommands.Count > 0 || _commandGroups.Count > 0;
            var usageLines = new List<string>();
            if (_defaultCommand != null)
                usageLines.Add($"[Key]{safeAppName}[/]{FormatArgumentUsage(_defaultCommand.SettingsType)} {optionsUsage}");
            if (hasCommandsOrGroups)
                usageLines.Add($"[Key]{safeAppName}[/] [Value]{FormatCommandUsage(culture)}[/] {optionsUsage}");
            TigerCliHelpRenderer.RenderSection($"[Accent]{Esc(L("Help_Usage", culture))}[/]", usageLines);
            TigerConsole.MarkupLine("");

            // Top-level help lists only immediate entries: ungrouped commands and
            // top-level command groups. A group's child commands and nested subgroups are
            // represented by the group entry, not flattened into this list.
            var topLevelCommands = _namedCommands.Where(cmd => !cmd.IsGroupChild && !cmd.IsCommandMenu).ToList();
            var topLevelGroups = _commandGroups.Where(IsTopLevelGroup).ToList();
            if (topLevelCommands.Count > 0 || topLevelGroups.Count > 0)
            {
                var commandItems = new List<(string NameMarkup, string? DescriptionMarkup)>();
                foreach (var cmd in topLevelCommands)
                {
                    var safeName = Esc(cmd.Name!);
                    // Description is trusted markup from AddCommand
                    var resolvedDesc = TigerCliAppText.Resolve(
                        cmd.Description, cmd.DescriptionResourceKey, culture, _appResources);
                    var defaultMarker = cmd.IsDefault ? $" [Muted]{Esc(L("Help_DefaultMarker", culture))}[/]" : "";
                    commandItems.Add(($"[Key]{safeName}[/]", resolvedDesc is null ? defaultMarker : $"{resolvedDesc}{defaultMarker}"));
                }
                foreach (var grp in topLevelGroups)
                {
                    var safeName = Esc(grp.Name);
                    // Description is trusted markup from SetDescription
                    var resolvedDesc = TigerCliAppText.Resolve(
                        grp.Description, grp.DescriptionResourceKey, culture, _appResources);
                    commandItems.Add(($"[Key]{safeName}[/]", resolvedDesc));
                }
                TigerCliHelpRenderer.RenderNameDescriptionSection(
                    $"[Accent]{Esc(L("Help_Commands", culture))}[/]",
                    commandItems);
                TigerConsole.MarkupLine("");
            }

            // Aliases are listed in their own section so command ownership is never confused: each
            // line shows the alias's description (falling back to the target's) and a muted marker
            // naming the target command path.
            var visibleAliases = _aliases.Where(a => !a.HiddenFromHelp).ToList();
            if (visibleAliases.Count > 0)
            {
                var aliasItems = new List<(string NameMarkup, string? DescriptionMarkup)>();
                foreach (var aliasEntry in visibleAliases)
                {
                    var safeName = Esc(aliasEntry.Name);
                    // Description is trusted markup from the alias or its target command.
                    var resolvedDesc = TigerCliAppText.Resolve(
                            aliasEntry.Description, aliasEntry.DescriptionResourceKey, culture, _appResources)
                        ?? TigerCliAppText.Resolve(
                            aliasEntry.Target.Description, aliasEntry.Target.DescriptionResourceKey, culture, _appResources);
                    var targetMarker =
                        $"[Muted]{TigerCliResources.Format("Help_AliasTargetMarker", culture, $"[Key]{Esc(aliasEntry.TargetPath)}[/]")}[/]";
                    aliasItems.Add(($"[Key]{safeName}[/]", resolvedDesc is null ? targetMarker : $"{resolvedDesc} {targetMarker}"));
                }
                TigerCliHelpRenderer.RenderNameDescriptionSection(
                    $"[Accent]{Esc(L("Help_Aliases", culture))}[/]",
                    aliasItems);
                TigerConsole.MarkupLine("");
            }

            // Show options from default command settings if available
            var settingsType = command?.SettingsType ?? _defaultCommand?.SettingsType;
            if (settingsType != null)
            {
                PrintArguments(settingsType, culture, _appResources);
                PrintOptions(settingsType, culture, _cultureOptionEnabled, _appResources, suppressNotes: true);
            }
            else
            {
                PrintHelpOnlyOption(culture, _cultureOptionEnabled);
            }

            // Consolidated notes section for root help
            var defaultNamed = _namedCommands.FirstOrDefault(c => c.IsDefault);
            var exactlyOneOfNotes = settingsType != null
                ? GetExactlyOneOfNotes(settingsType, BuildOptionMetadata(settingsType), culture)
                : [];
            if (defaultNamed != null || exactlyOneOfNotes.Count > 0)
            {
                TigerConsole.MarkupLine("");
                var notes = new List<string>();
                if (defaultNamed != null)
                    notes.Add($"[Muted]{Esc(L("Help_DefaultCommand", culture))} [Key]{Esc(defaultNamed.Name!)}[/][/]" );
                foreach (var note in exactlyOneOfNotes)
                    notes.Add($"[Muted]{Esc(note)}[/]");
                TigerCliHelpRenderer.RenderNoteSection($"[Accent]{Esc(L("Help_Notes", culture))}[/]", notes);
            }

            PrintExitCodeHelpHint(command, culture);
            PrintApplicationMetadataFooter(culture);
        }
        else
        {
            // Command-specific help. When reached through an alias, the page shows the alias identity
            // (name, description) and a note naming the target, but the arguments/options always come
            // from the target command's settings.
            var safeCmdName = Esc(alias?.Name ?? command.Name!);
            // Description is trusted markup from AddCommand/AddCommandAlias
            var resolvedDesc = alias != null
                ? TigerCliAppText.Resolve(alias.Description, alias.DescriptionResourceKey, culture, _appResources)
                    ?? TigerCliAppText.Resolve(command.Description, command.DescriptionResourceKey, culture, _appResources)
                : TigerCliAppText.Resolve(command.Description, command.DescriptionResourceKey, culture, _appResources);
            TigerCliHelpRenderer.RenderTitleBlock($"[Key]{safeAppName} {safeCmdName}[/]", resolvedDesc);
            TigerConsole.MarkupLine("");
            TigerCliHelpRenderer.RenderSection(
                $"[Accent]{Esc(L("Help_Usage", culture))}[/]",
                [$"[Key]{safeAppName} {safeCmdName}[/]{FormatArgumentUsage(command.SettingsType)} [Value]{FormatOptionsUsage(culture)}[/]"]);
            if (alias != null)
            {
                TigerConsole.MarkupLine("");
                TigerCliHelpRenderer.RenderHint(
                    $"[Muted]{TigerCliResources.Format("Help_AliasFor", culture, $"[Key]{Esc(alias.TargetPath)}[/]")}[/]");
            }
            TigerConsole.MarkupLine("");
            PrintArguments(command.SettingsType, culture, _appResources);
            PrintOptions(command.SettingsType, culture, _cultureOptionEnabled, _appResources);
            PrintExitCodeHelpHint(command, culture);
            PrintApplicationMetadataFooter(culture);
        }
    }

    private void PrintGroupHelp(TigerCliCommandGroupRegistration group, CultureInfo culture)
    {
        var appName = string.IsNullOrEmpty(_applicationName) ? "app" : _applicationName;
        var safeAppName = Esc(appName);
        var safeGroupName = Esc(group.Name);

        // Description is trusted markup from SetDescription
        var resolvedDesc = TigerCliAppText.Resolve(
            group.Description, group.DescriptionResourceKey, culture, _appResources);
        TigerCliHelpRenderer.RenderTitleBlock($"[Key]{safeAppName} {safeGroupName}[/]", resolvedDesc);
        TigerConsole.MarkupLine("");

        TigerCliHelpRenderer.RenderSection(
            $"[Accent]{Esc(L("Help_Usage", culture))}[/]",
            [$"[Key]{safeAppName} {safeGroupName}[/] [Value]{FormatCommandUsage(culture)}[/] [Value]{FormatOptionsUsage(culture)}[/]"]);
        TigerConsole.MarkupLine("");

        // Group help lists only this group's immediate entries — child commands and nested
        // subgroups — shown with their name relative to the group prefix. Deeper commands are
        // represented by their subgroup entry, mirroring how top-level help represents groups.
        var childCommands = GetImmediateGroupCommands(group);
        var childGroups = GetImmediateSubgroups(group);

        if (childCommands.Count > 0 || childGroups.Count > 0)
        {
            var commandItems = new List<(string NameMarkup, string? DescriptionMarkup)>();
            foreach (var child in childCommands)
            {
                var safeName = Esc(GroupRelativeName(child.PathTokens, group));
                // Description is trusted markup from AddCommand
                var childDesc = TigerCliAppText.Resolve(
                    child.Description, child.DescriptionResourceKey, culture, _appResources);
                commandItems.Add(($"[Key]{safeName}[/]", childDesc));
            }
            foreach (var childGroup in childGroups)
            {
                var safeName = Esc(GroupRelativeName(childGroup.PathTokens, group));
                // Description is trusted markup from SetDescription
                var childDesc = TigerCliAppText.Resolve(
                    childGroup.Description, childGroup.DescriptionResourceKey, culture, _appResources);
                commandItems.Add(($"[Key]{safeName}[/]", childDesc));
            }
            TigerCliHelpRenderer.RenderNameDescriptionSection(
                $"[Accent]{Esc(L("Help_Commands", culture))}[/]",
                commandItems);
            TigerConsole.MarkupLine("");
        }

        PrintApplicationMetadataFooter(culture);
    }

    private void PrintVersion(CultureInfo culture, bool showVersion, bool showProductVersion)
    {
        var displayName = GetApplicationDisplayName();
        if (showVersion)
        {
            TigerConsole.MarkupLine(Esc(TigerCliResources.Format(
                "App_VersionLine", culture, displayName, _metadata.Version ?? "unknown")));
        }

        if (showProductVersion)
        {
            TigerConsole.MarkupLine(Esc(TigerCliResources.Format(
                "App_ProductVersionLine", culture, displayName, _metadata.ProductVersion ?? _metadata.Version ?? "unknown")));
        }
    }

    private string GetApplicationDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(_metadata.DisplayName))
            return _metadata.DisplayName;

        return string.IsNullOrWhiteSpace(_applicationName) ? "app" : _applicationName;
    }

    private static string ResolveCommandTitle(string appTitle, TigerCliCommandRegistration command)
    {
        if (!string.IsNullOrWhiteSpace(command.TitleSet))
            return command.TitleSet;

        if (!string.IsNullOrWhiteSpace(command.TitleAppend))
            return $"{appTitle} - {command.TitleAppend}";

        return appTitle;
    }

    private void PrintApplicationMetadataFooter(CultureInfo culture)
    {
        var hasCopyright = !string.IsNullOrWhiteSpace(_metadata.Copyright);
        var hasLinks = _metadata.Links.Count > 0;
        if (!hasCopyright && !hasLinks)
            return;

        TigerConsole.MarkupLine("");

        var links = _metadata.Links
            .Select(link => new
            {
                Label = ResolveApplicationLinkLabel(link, culture),
                link.Url
            })
            .Select(link => ($"[Key]{Esc(link.Label)}[/]", $"[Link]{Esc(link.Url)}[/]"))
            .ToArray();
        TigerCliHelpRenderer.RenderMetadataFooter(hasCopyright ? Esc(_metadata.Copyright!) : null, links);
    }

    private static string ResolveApplicationLinkLabel(TigerCliApplicationLink link, CultureInfo culture)
    {
        return link.LabelResourceKey != null
            ? L(link.LabelResourceKey, culture)
            : link.Label;
    }

    private void PrintExitCodeHelpHint(TigerCliCommandRegistration? command, CultureInfo culture)
    {
        if (ResolveExitCodeHelpType(command) == null)
            return;

        TigerConsole.MarkupLine("");
        TigerCliHelpRenderer.RenderHint($"[Muted]{Esc(L("Help_Hint_ExitCodes", culture))}[/]");
    }

    private void PrintExitCodeHelp(TigerCliCommandRegistration? command, bool leadingBlankLine, CultureInfo culture)
    {
        var exitCodeType = ResolveExitCodeHelpType(command);

        if (leadingBlankLine)
            TigerConsole.MarkupLine("");

        if (exitCodeType == null)
        {
            TigerCliHelpRenderer.RenderHint($"[Muted]{Esc(L("Help_Hint_NoExitCodeEnum", culture))}[/]");
            return;
        }

        // Enum type heading: TigerText.Text (or ResourceKey) -> Display.GetName() ->
        // DescriptionAttribute.Description -> type Name. The DescriptionAttribute fallback
        // is included here (and only here) to preserve the historical heading semantic
        // where [Description("...")] on the enum type acts as the visible heading.
        var typeText = TigerCliEnumText.Resolve(exitCodeType, culture, _appResources);
        string heading;
        if (!string.Equals(typeText.Label, exitCodeType.Name, StringComparison.Ordinal))
        {
            heading = typeText.Label;
        }
        else
        {
            heading = exitCodeType.GetCustomAttribute<DescriptionAttribute>()?.Description
                ?? typeText.Label;
        }

        var fields = exitCodeType
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field =>
            {
                var resolved = TigerCliEnumText.Resolve(field, culture, _appResources);
                return new
                {
                    Label = resolved.Label,
                    Value = Convert.ToInt32(field.GetRawConstantValue()),
                    Description = resolved.Description
                };
            })
            .ToArray();

        TigerCliHelpRenderer.RenderExitCodeSection(
            $"[Accent]{Esc(L("Help_ExitCodes", culture))}[/]",
            Esc(heading),
            fields.Select(field => (field.Value, Esc(field.Label), field.Description is null ? null : Esc(field.Description))).ToArray());
    }

    private Type? ResolveExitCodeHelpType(TigerCliCommandRegistration? command)
    {
        if (command?.Name != null && command.ExitCodeType != null)
            return command.ExitCodeType;

        return _exitCodePolicy.DocumentedExitCodeType;
    }

    // Usage-line argument tokens (" <arg> <arg>…"), each styled as a semantic Value; empty when the
    // settings type declares no positional arguments.
    private static string FormatArgumentUsage(Type settingsType)
    {
        var arguments = BuildArgumentMetadata(settingsType);
        if (arguments.Count == 0)
            return string.Empty;

        return " " + string.Join(" ", arguments.Select(arg => $"[Value]<{Esc(arg.DisplayName)}>[/]"));
    }

    private static string FormatCommandUsage(CultureInfo culture) =>
        $"<{Esc(L("Help_Placeholder_Command", culture))}>";

    private static string FormatOptionsUsage(CultureInfo culture) =>
        Esc($"[{L("Help_Placeholder_Options", culture)}]");

    private static void PrintArguments(Type settingsType, CultureInfo culture, ResourceManager? appResources)
    {
        var arguments = BuildArgumentMetadata(settingsType);
        if (arguments.Count == 0)
            return;

        var items = arguments.Select(arg =>
        {
            var description = TigerCliAppText.Resolve(arg.Description, arg.DescriptionResourceKey, culture, appResources);
            return (
                $"<[Key]{Esc(arg.DisplayName)}[/]>",
                (IReadOnlyList<string>)(string.IsNullOrEmpty(description) ? [] : [description]));
        }).ToArray();

        TigerCliHelpRenderer.RenderDetailSection(
            $"[Accent]{Esc(L("Help_Arguments", culture))}[/]",
            items);
        TigerConsole.MarkupLine("");
    }

    private void PrintOptions(
        Type settingsType,
        CultureInfo culture,
        bool cultureOptionEnabled,
        ResourceManager? appResources,
        bool suppressNotes = false)
    {
        var options = BuildOptionMetadata(settingsType);

        // Create a default settings instance to read default values
        TigerCliSettings? defaultInstance = null;
        try
        {
            defaultInstance = (TigerCliSettings)Activator.CreateInstance(settingsType)!;
        }
        catch
        {
            // If we can't create a default instance, proceed without defaults
        }

        var commandLineOptions = options
            .Where(opt => opt.AllowCommandLineValue)
            .ToList();
        var promptedOptions = options
            .Where(opt => !opt.AllowCommandLineValue)
            .ToList();

        var optionItems = commandLineOptions.Select(opt => BuildOptionHelpItem(opt, culture, appResources, defaultInstance)).ToList();
        AppendBuiltinOptionHelpItems(optionItems, culture, cultureOptionEnabled);
        TigerCliHelpRenderer.RenderDetailSection(
            $"[Accent]{Esc(L("Help_Options", culture))}[/]",
            optionItems);

        if (promptedOptions.Count > 0)
        {
            TigerConsole.MarkupLine("");
            var promptedItems = promptedOptions.Select(opt =>
            {
                var details = new List<string>();
                var description = TigerCliAppText.Resolve(opt.Description, opt.DescriptionResourceKey, culture, appResources);
                if (!string.IsNullOrEmpty(description))
                    details.Add(description);

                var promptTextKey = opt.Secret
                    ? "Help_PromptedSecretValue"
                    : "Help_PromptedValue";
                details.Add(Esc(L(promptTextKey, culture)));
                details.Add(Esc(L("Help_PromptedCommandLineNotAllowed", culture)));
                return ($"[Key]{Esc(GetPromptedValueDisplayName(opt))}[/]", (IReadOnlyList<string>)details);
            }).ToArray();

            TigerCliHelpRenderer.RenderDetailSection(
                $"[Accent]{Esc(L("Help_PromptedValues", culture))}[/]",
                promptedItems);
        }

        // ExactlyOneOf notes (suppressed when root help prints a consolidated Notes section)
        if (!suppressNotes)
            PrintExactlyOneOfNotes(settingsType, options, culture);
    }

    private static string GetPromptedValueDisplayName(TigerCliOptionMetadata option)
    {
        var longAlias = option.Aliases.FirstOrDefault(alias => alias.StartsWith("--", StringComparison.Ordinal));
        if (longAlias != null)
            return longAlias[2..];

        return DeriveDisplayName(option.Property.Name);
    }

    private static string DeriveDisplayName(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return "value";

        var result = new List<char>(propertyName.Length + 4);
        for (var i = 0; i < propertyName.Length; i++)
        {
            var ch = propertyName[i];
            if (i > 0 && char.IsUpper(ch))
                result.Add('-');
            result.Add(char.ToLowerInvariant(ch));
        }

        return new string(result.ToArray());
    }

    private static string ResolveOptionValuePlaceholder(TigerCliOptionMetadata option, CultureInfo culture)
    {
        if (option.ExplicitValueName == null
            && string.Equals(option.ValuePlaceholder, "value", StringComparison.Ordinal))
        {
            return L("Help_Placeholder_Value", culture);
        }

        return option.ValuePlaceholder!;
    }

    private static (string SignatureMarkup, IReadOnlyList<string> DetailMarkups) BuildOptionHelpItem(
        TigerCliOptionMetadata option,
        CultureInfo culture,
        ResourceManager? appResources,
        TigerCliSettings? defaultInstance)
    {
        var signature = string.Join(", ", option.Aliases.Select(alias => $"[Key]{Esc(alias)}[/]"));
        if (option.TakesValue && option.ValuePlaceholder != null)
            signature += $" [Value]<{Esc(ResolveOptionValuePlaceholder(option, culture))}>[/]";

        var details = new List<string>();
        var description = TigerCliAppText.Resolve(option.Description, option.DescriptionResourceKey, culture, appResources);
        if (!string.IsNullOrEmpty(description))
            details.Add(description);
        if (option.Required)
            details.Add(Esc(L("Help_Required", culture)));
        if (option.IsRepeatable)
        {
            details.Add(Esc(L("Help_Repeatable", culture)));
            if (option.ValueKind == OptionValueKind.RepeatedKeyValue)
                details.Add(Esc(TigerCliResources.Format("Help_Examples", culture, Esc(option.Aliases[0]))));
        }

        if (defaultInstance != null && option.GetDefaultDisplayValue(defaultInstance) is { } defaultDisplay)
            details.Add(Esc(TigerCliResources.Format("Help_Default", culture, Esc(defaultDisplay))));

        return (signature, details);
    }

    private static void PrintExactlyOneOfNotes(
        Type settingsType,
        List<TigerCliOptionMetadata> options,
        CultureInfo culture)
    {
        var notes = GetExactlyOneOfNotes(settingsType, options, culture);
        if (notes.Count == 0)
            return;

        TigerConsole.MarkupLine("");
        TigerCliHelpRenderer.RenderNoteSection(
            $"[Accent]{Esc(L("Help_Notes", culture))}[/]",
            notes.Select(note => $"[Muted]{Esc(note)}[/]").ToArray());
    }

    private void PrintHelpOnlyOption(CultureInfo culture, bool cultureOptionEnabled)
    {
        var optionItems = new List<(string SignatureMarkup, IReadOnlyList<string> DetailMarkups)>();
        AppendBuiltinOptionHelpItems(optionItems, culture, cultureOptionEnabled);
        TigerCliHelpRenderer.RenderDetailSection(
            $"[Accent]{Esc(L("Help_Options", culture))}[/]",
            optionItems);
    }

    private void AppendBuiltinOptionHelpItems(
        List<(string SignatureMarkup, IReadOnlyList<string> DetailMarkups)> optionItems,
        CultureInfo culture,
        bool cultureOptionEnabled)
    {
        optionItems.Add(("[Key]-h[/], [Key]--help[/]", [Esc(L("Help_Builtin_Help_Desc", culture))]));
        if (_metadata.VersionEnabled)
        {
            optionItems.Add(("[Key]--version[/]", [Esc(L("Help_Builtin_Version_Desc", culture))]));
            optionItems.Add(("[Key]--version-full[/]", [Esc(L("Help_Builtin_VersionFull_Desc", culture))]));
        }
        optionItems.Add(("[Key]--help-errors[/]", [Esc(L("Help_Builtin_HelpErrors_Desc", culture))]));
        optionItems.Add(("[Key]--non-interactive[/]", [Esc(L("Help_Builtin_NonInteractive_Desc", culture))]));

        var themePlaceholder = Esc(L("Help_Builtin_Theme_ValuePlaceholder", culture));
        var themeDetails = new List<string> { Esc(L("Help_Builtin_Theme_Desc", culture)) };
        var selectableThemes = string.Join(" | ", GetEnabledThemeNames());
        if (selectableThemes.Length > 0)
            themeDetails.Add($"[Value]{Esc(selectableThemes)}[/]");
        optionItems.Add(($"[Key]--theme[/] [Value]<{themePlaceholder}>[/]", themeDetails));

        if (cultureOptionEnabled)
        {
            var placeholder = Esc(L("Help_Builtin_Culture_ValuePlaceholder", culture));
            optionItems.Add(($"[Key]--culture[/] [Value]<{placeholder}>[/]", [Esc(L("Help_Builtin_Culture_Desc", culture))]));
        }
    }

    private static string L(string key, CultureInfo culture) => TigerCliResources.Get(key, culture);

    /// <summary>Shorthand for <see cref="CliMarkupParser.Escape"/>.</summary>
    private static string Esc(string value) => CliMarkupParser.Escape(value);
}

