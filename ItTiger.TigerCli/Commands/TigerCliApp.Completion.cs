using System.Globalization;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Commands;

// Shell completion: the framework-owned `--completion <shell>` script option, the hidden
// `--tigercli-complete` protocol request, and the static completion engine behind it. The engine
// reads command metadata only. It never constructs handlers, calls command factories, applies
// contributed global options or app theme configuration, prompts, or calls providers, so completing
// a command line has no side effects beyond writing the response. Writing it is the only console
// interaction (WriteCompletionOutput): no sink, theme, colour mode, terminal title, or cancellation
// handler is set up. A future dynamic value source must keep to the same boundary.
public sealed partial class TigerCliApp
{
    private readonly bool _shellCompletionEnabled;

    /// <summary>
    /// Handles the two completion entry points before any other run stage (raw-tail split, theme,
    /// terminal title, cancellation registration), so neither produces terminal side effects.
    /// </summary>
    private bool TryRunShellCompletion(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (!_shellCompletionEnabled || args.Length == 0)
            return false;

        if (args[0] == TigerCliCompletionProtocol.RequestToken)
        {
            exitCode = RunCompletionRequest(args[1..]);
            return true;
        }

        // `app --completion <shell>` is an exclusive root form; anything else on the line leaves
        // `--completion` to the normal parser, which rejects it as an unknown option.
        string? shellName;
        const string inlinePrefix = TigerCliCompletionProtocol.ScriptOption + "=";
        if (args.Length == 1 && args[0] == TigerCliCompletionProtocol.ScriptOption)
            shellName = null;
        else if (args.Length == 2 && args[0] == TigerCliCompletionProtocol.ScriptOption)
            shellName = args[1];
        else if (args.Length == 1 && args[0].StartsWith(inlinePrefix, StringComparison.Ordinal))
            shellName = args[0][inlinePrefix.Length..];
        else
            return false;

        exitCode = WriteCompletionScript(shellName);
        return true;
    }

    private int WriteCompletionScript(string? shellName)
    {
        var culture = DefaultCulture;
        if (string.IsNullOrEmpty(shellName))
        {
            WriteUsageError(culture, TigerCliResources.Format(
                "Error_OptionRequiresValue", culture, Esc(TigerCliCompletionProtocol.ScriptOption)), []);
            return _exitCodePolicy.Resolve(TigerCliExitKind.InvalidArguments);
        }

        if (!TigerCliCompletionProtocol.TryParseShell(shellName, out var shell))
        {
            WriteUsageError(culture, TigerCliResources.Format(
                "Error_UnsupportedCompletionShell", culture,
                Esc(shellName),
                Esc(string.Join(", ", TigerCliCompletionProtocol.ShellNames))), []);
            return _exitCodePolicy.Resolve(TigerCliExitKind.InvalidArguments);
        }

        var commandName = string.IsNullOrWhiteSpace(_applicationName) ? "app" : _applicationName;
        WriteCompletionOutput(shell, TigerCliCompletionScripts.Generate(shell, commandName));
        return _exitCodePolicy.Resolve(TigerCliExitKind.Success);
    }

    private int RunCompletionRequest(string[] request)
    {
        if (request.Length == 0 || !TigerCliCompletionProtocol.TryParseShell(request[0], out var shell))
            return _exitCodePolicy.Resolve(TigerCliExitKind.InvalidArguments);

        var words = request[1..].Select(TigerCliCompletionProtocol.DecodeWord).ToArray();
        var result = words.Length == 0
            ? Complete([], string.Empty)
            : Complete(words[..^1], words[^1]);

        WriteCompletionOutput(shell, TigerCliCompletionProtocol.FormatResponse(shell, result));
        return _exitCodePolicy.Resolve(TigerCliExitKind.Success);
    }

    /// <summary>
    /// Writes a completion script or response to stdout in the encoding that shell's protocol uses,
    /// and sets up nothing else. Bash, Zsh, and Fish read UTF-8, TigerCli's standard redirected-output
    /// encoding. PowerShell decodes a program's output with its <c>[Console]::OutputEncoding</c>, which
    /// follows the console code page, so a PowerShell answer is written in that code page: the
    /// generated script selects UTF-8 for each request, and where it cannot (Constrained Language
    /// Mode) the answer still decodes for every character the code page has.
    /// </summary>
    private static void WriteCompletionOutput(TigerCliCompletionShell shell, string text)
    {
        if (shell != TigerCliCompletionShell.Pwsh)
            ConsoleOutputEncoding.EnsureApplied();
        Console.Out.Write(text);
        Console.Out.Flush();
    }

    /// <summary>
    /// Completes <paramref name="currentWord"/> given the complete words before it (the words after
    /// the program name). Follows the same grammar as <see cref="RunAsync(string[])"/>: the first
    /// <c>--</c> closes the parsed head, command paths resolve longest-first with aliases after
    /// commands, positionals precede options, and framework and contributed global options are only
    /// valid in a command's options area or in a root informational form.
    /// </summary>
    internal TigerCliCompletionResult Complete(IReadOnlyList<string> precedingWords, string currentWord)
    {
        ArgumentNullException.ThrowIfNull(precedingWords);
        ArgumentNullException.ThrowIfNull(currentWord);

        var words = precedingWords.ToArray();
        var culture = _cultureOptionEnabled ? ResolveRunCulture(words).Culture : DefaultCulture;
        var context = new CompletionContext(currentWord, culture);

        // Everything after the first `--` belongs to the command's raw tail and is never TigerCli
        // input, so only the shell's own fallback applies there.
        var separatorIndex = Array.IndexOf(words, RawArgumentSeparator);
        if (separatorIndex >= 0)
        {
            CompleteRawTail(words[..separatorIndex], context);
            return context.ToResult();
        }

        AddCommandPathCandidates(words, context);

        var (command, alias, rest) = ResolveCommand(words);
        var matchedNamedCommand = alias != null || command is { Name: not null };
        var group = matchedNamedCommand ? null : ResolveGroup(words);
        if (group != null)
        {
            // A group prefix selects no command; only its informational options can follow.
            CompleteOptionsOnly(words[group.PathTokens.Length..], BuildCommandInformationCatalog(culture), context);
            return context.ToResult();
        }

        if (!matchedNamedCommand)
            CompleteRootInformation(words, context);

        if (command != null)
            CompleteCommandArea(command, rest, context);

        return context.ToResult();
    }

    private void CompleteRawTail(string[] head, CompletionContext context)
    {
        var (command, alias, _) = ResolveCommand(head);
        var matchedNamedCommand = alias != null || command is { Name: not null };
        if (!matchedNamedCommand && ResolveGroup(head) != null)
            return;

        if (command is { IsCommandMenu: false } && BuildRawArgumentsMetadata(command.SettingsType) != null)
            context.Fallback = TigerCliCompletionFallback.Files;
    }

    /// <summary>
    /// Offers the next command-path token: child commands and groups of the path typed so far, and
    /// root aliases at the root. The command-menu sentinel and help-hidden aliases are not offered,
    /// matching generated help.
    /// </summary>
    private void AddCommandPathCandidates(string[] words, CompletionContext context)
    {
        if (words.Any(word => word.StartsWith('-')))
            return;

        var paths = new List<(string[] PathTokens, string? Description, TigerCliCompletionKind Kind)>();
        foreach (var command in _namedCommands.Where(command => !command.IsCommandMenu))
        {
            paths.Add((command.PathTokens,
                ResolveCompletionText(command.Description, command.DescriptionResourceKey, context.Culture),
                TigerCliCompletionKind.Command));
        }

        foreach (var group in _commandGroups)
        {
            paths.Add((group.PathTokens,
                ResolveCompletionText(group.Description, group.DescriptionResourceKey, context.Culture),
                TigerCliCompletionKind.Group));
        }

        if (words.Length == 0)
        {
            foreach (var alias in _aliases.Where(alias => !alias.HiddenFromHelp))
            {
                var description =
                    ResolveCompletionText(alias.Description, alias.DescriptionResourceKey, context.Culture)
                    ?? ResolveCompletionText(alias.Target.Description, alias.Target.DescriptionResourceKey, context.Culture);
                paths.Add((alias.PathTokens, description, TigerCliCompletionKind.Alias));
            }
        }

        var children = paths
            .Where(path => path.PathTokens.Length > words.Length
                && words.Select((word, i) => string.Equals(word, path.PathTokens[i], StringComparison.OrdinalIgnoreCase)).All(match => match))
            .ToList();
        if (children.Count == 0)
            return;

        context.CommandPathAvailable = true;

        // Entries whose next token completes their own path carry their description; a deeper path
        // only contributes the intermediate token (a group prefix with no registration of its own).
        foreach (var child in children.Where(child => child.PathTokens.Length == words.Length + 1))
            context.Add(child.PathTokens[words.Length], child.Description, child.Kind);
        foreach (var child in children.Where(child => child.PathTokens.Length > words.Length + 1))
            context.Add(child.PathTokens[words.Length], null, TigerCliCompletionKind.Group);
    }

    /// <summary>
    /// Root informational forms (<c>app --help</c>, <c>app --version --theme light</c>) and the
    /// exclusive <c>app --completion &lt;shell&gt;</c> form.
    /// </summary>
    private void CompleteRootInformation(string[] words, CompletionContext context)
    {
        if (_shellCompletionEnabled
            && words.Length == 1
            && words[0] == TigerCliCompletionProtocol.ScriptOption)
        {
            AddShellNames(prefix: string.Empty, context);
            return;
        }

        var catalog = BuildRootInformationCatalog(context.Culture, includeScriptOption: words.Length == 0);
        CompleteOptionsOnly(words, catalog, context);
    }

    /// <summary>A position where only options from <paramref name="catalog"/> may appear.</summary>
    private void CompleteOptionsOnly(string[] tokens, List<CompletionOption> catalog, CompletionContext context)
    {
        var walk = WalkOptions(tokens, 0, catalog);
        if (!walk.Valid || walk.PendingKeyValuePart)
            return;

        if (walk.Pending != null)
        {
            AddValueCandidates(walk.Pending, prefix: string.Empty, context);
            return;
        }

        if (!context.CurrentWord.StartsWith('-') || TryAddInlineValueCandidates(catalog, context))
            return;

        AddOptionNames(catalog, walk.Used, context);
    }

    private void CompleteCommandArea(TigerCliCommandRegistration command, string[] rest, CompletionContext context)
    {
        var arguments = command.IsCommandMenu ? [] : BuildArgumentMetadata(command.SettingsType);
        var rawArguments = command.IsCommandMenu ? null : BuildRawArgumentsMetadata(command.SettingsType);
        var catalog = BuildCommandOptionCatalog(command, context.Culture);

        var positionalAreaLength = MeasurePositionalArea(rest, arguments.Count);
        var walk = WalkOptions(rest, positionalAreaLength, catalog);
        if (!walk.Valid || walk.PendingKeyValuePart)
            return;

        if (walk.Pending != null)
        {
            AddValueCandidates(walk.Pending, prefix: string.Empty, context);
            return;
        }

        if (context.CurrentWord.StartsWith('-'))
        {
            if (TryAddInlineValueCandidates(catalog, context))
                return;

            // Options may start before every positional is supplied (missing positionals are
            // prompted or rejected later), so the full option set is valid here.
            AddOptionNames(catalog, walk.Used, context);
            AddRawSeparator(rawArguments, context);
            return;
        }

        var optionsStarted = rest.Length > positionalAreaLength;
        if (!optionsStarted && positionalAreaLength < arguments.Count)
        {
            AddArgumentValueCandidates(arguments[positionalAreaLength], context);
            return;
        }

        // Only options (and a declared raw tail) can follow. Offer them on an empty word unless the
        // position also accepts a command-path token, where options would only add noise.
        if (context.CurrentWord.Length == 0 && !context.CommandPathAvailable)
        {
            AddOptionNames(catalog, walk.Used, context);
            AddRawSeparator(rawArguments, context);
        }
    }

    // ── Option catalogs ─────────────────────────────────────────────

    private enum CompletionValueSource
    {
        None,
        AppOption,
        Theme,
        ColorMode,
        Culture,
        Shell,
        FreeText
    }

    /// <summary>One option the grammar accepts at a position, with every spelling of its name.</summary>
    private sealed class CompletionOption
    {
        public required IReadOnlyList<string> Names { get; init; }
        public string? Description { get; init; }

        /// <summary>Where value candidates come from; <see cref="CompletionValueSource.None"/> for a switch.</summary>
        public CompletionValueSource ValueSource { get; init; }

        public TigerCliOptionMetadata? Metadata { get; init; }
        public bool Repeatable { get; init; }
        public bool TakesValue => ValueSource != CompletionValueSource.None;
        public bool IsKeyValue => Metadata?.ValueKind == OptionValueKind.RepeatedKeyValue;
    }

    private List<CompletionOption> BuildCommandOptionCatalog(TigerCliCommandRegistration command, CultureInfo culture)
    {
        var catalog = new List<CompletionOption>();
        if (!command.IsCommandMenu)
        {
            // Prompt-only options reject command-line values, so they are not offered.
            foreach (var option in BuildOptionMetadata(command.SettingsType).Where(option => option.AllowCommandLineValue))
            {
                catalog.Add(new CompletionOption
                {
                    Names = option.Aliases,
                    Description = ResolveCompletionText(option.Description, option.DescriptionResourceKey, culture),
                    ValueSource = option.TakesValue ? CompletionValueSource.AppOption : CompletionValueSource.None,
                    Metadata = option,
                    Repeatable = option.IsRepeatable
                });
            }
        }

        AddGlobalOptions(catalog, culture);

        // The command menu runs interactively by definition and rejects --non-interactive.
        AddExecutionOptions(catalog, culture, includeNonInteractive: !command.IsCommandMenu);
        catalog.AddRange(BuildCommandInformationCatalog(culture));
        return catalog;
    }

    private List<CompletionOption> BuildRootInformationCatalog(CultureInfo culture, bool includeScriptOption)
    {
        var catalog = BuildCommandInformationCatalog(culture);
        if (_metadata.VersionEnabled)
        {
            catalog.Add(Switch(["--version"], L("Help_Builtin_Version_Desc", culture)));
            catalog.Add(Switch(["--version-full"], L("Help_Builtin_VersionFull_Desc", culture)));
        }

        if (_shellCompletionEnabled && includeScriptOption)
        {
            catalog.Add(new CompletionOption
            {
                Names = [TigerCliCompletionProtocol.ScriptOption],
                Description = L("Help_Builtin_Completion_Desc", culture),
                ValueSource = CompletionValueSource.Shell
            });
        }

        AddExecutionOptions(catalog, culture, includeNonInteractive: true);
        AddGlobalOptions(catalog, culture);
        return catalog;
    }

    private static List<CompletionOption> BuildCommandInformationCatalog(CultureInfo culture) =>
    [
        Switch(["-h", "--help"], L("Help_Builtin_Help_Desc", culture)),
        Switch(["--help-errors"], L("Help_Builtin_HelpErrors_Desc", culture)),
        Switch(["--help-env"], L("Help_Builtin_HelpEnv_Desc", culture))
    ];

    private void AddExecutionOptions(List<CompletionOption> catalog, CultureInfo culture, bool includeNonInteractive)
    {
        if (includeNonInteractive)
            catalog.Add(Switch(["--non-interactive"], L("Help_Builtin_NonInteractive_Desc", culture)));

        catalog.Add(new CompletionOption
        {
            Names = ["--theme"],
            Description = L("Help_Builtin_Theme_Desc", culture),
            ValueSource = CompletionValueSource.Theme
        });
        catalog.Add(new CompletionOption
        {
            Names = ["--color"],
            Description = L("Help_Builtin_Color_Desc", culture),
            ValueSource = CompletionValueSource.ColorMode
        });
        catalog.Add(Switch(["--no-color"], L("Help_Builtin_NoColor_Desc", culture)));

        if (_cultureOptionEnabled)
        {
            catalog.Add(new CompletionOption
            {
                Names = ["--culture"],
                Description = L("Help_Builtin_Culture_Desc", culture),
                ValueSource = CompletionValueSource.Culture
            });
        }
    }

    private void AddGlobalOptions(List<CompletionOption> catalog, CultureInfo culture)
    {
        // Contributed globals are offered by name only; their apply callbacks never run here.
        foreach (var option in _globalOptions)
        {
            catalog.Add(new CompletionOption
            {
                Names = [option.Name],
                Description = ResolveCompletionText(option.Description, option.DescriptionResourceKey, culture),
                ValueSource = CompletionValueSource.FreeText
            });
        }
    }

    private static CompletionOption Switch(string[] names, string description) =>
        new() { Names = names, Description = description };

    private static CompletionOption? FindOption(List<CompletionOption> catalog, string name) =>
        catalog.FirstOrDefault(option =>
            option.Names.Any(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)));

    // ── Walking the options area ────────────────────────────────────

    private readonly record struct OptionWalk(
        bool Valid,
        HashSet<CompletionOption> Used,
        CompletionOption? Pending,
        bool PendingKeyValuePart);

    /// <summary>
    /// Walks complete option tokens the way the parser consumes them: a value-taking option without an
    /// inline value consumes the next token, and a key-value option given only a key consumes one more.
    /// A non-option token or an unknown option makes the line invalid, so nothing further is offered.
    /// </summary>
    private static OptionWalk WalkOptions(IReadOnlyList<string> tokens, int start, List<CompletionOption> catalog)
    {
        var used = new HashSet<CompletionOption>();
        CompletionOption? pending = null;
        var pendingKeyValuePart = false;

        for (var i = start; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (pendingKeyValuePart)
            {
                pendingKeyValuePart = false;
                continue;
            }

            if (pending != null)
            {
                pendingKeyValuePart = pending.IsKeyValue && !token.Contains('=');
                pending = null;
                continue;
            }

            if (!token.StartsWith('-'))
                return new OptionWalk(false, used, null, false);

            var equalsIndex = token.IndexOf('=');
            var option = FindOption(catalog, equalsIndex > 0 ? token[..equalsIndex] : token);
            if (option == null)
                return new OptionWalk(false, used, null, false);

            used.Add(option);
            if (!option.TakesValue)
                continue;

            if (equalsIndex < 0)
                pending = option;
            else
                pendingKeyValuePart = option.IsKeyValue && !token[(equalsIndex + 1)..].Contains('=');
        }

        return new OptionWalk(true, used, pending, pendingKeyValuePart);
    }

    private static void AddOptionNames(
        List<CompletionOption> catalog,
        HashSet<CompletionOption> used,
        CompletionContext context)
    {
        foreach (var option in catalog)
        {
            if (used.Contains(option) && !option.Repeatable)
                continue;

            foreach (var name in option.Names)
                context.Add(name, option.Description, TigerCliCompletionKind.Option);
        }
    }

    /// <summary>
    /// Completes <c>--option=value</c> as one word. Returns <c>true</c> whenever the current word is in
    /// that form, because an option name never contains <c>=</c>.
    /// </summary>
    private bool TryAddInlineValueCandidates(List<CompletionOption> catalog, CompletionContext context)
    {
        var equalsIndex = context.CurrentWord.IndexOf('=');
        if (equalsIndex <= 0)
            return false;

        var option = FindOption(catalog, context.CurrentWord[..equalsIndex]);
        if (option is { TakesValue: true })
            AddValueCandidates(option, context.CurrentWord[..(equalsIndex + 1)], context);
        return true;
    }

    private static void AddRawSeparator(TigerCliRawArgumentsMetadata? rawArguments, CompletionContext context)
    {
        if (rawArguments == null)
            return;

        var description = rawArguments.Description != null
            ? TigerCliCompletionProtocol.MarkupToPlainText(rawArguments.Description)
            : TigerCliResources.Format("Help_RawArguments", context.Culture, RawArgumentSeparator);
        context.Add(RawArgumentSeparator, description, TigerCliCompletionKind.Separator);
    }

    // ── Values ──────────────────────────────────────────────────────
    //
    // Value candidates come only from static metadata: enum members, bool? literals, and the
    // framework's own theme, colour-mode, culture, and shell lists. Provider-backed values are app
    // data and are deliberately not offered; a future dynamic-completion source belongs here.

    private void AddValueCandidates(CompletionOption option, string prefix, CompletionContext context)
    {
        switch (option.ValueSource)
        {
            case CompletionValueSource.AppOption:
                AddOptionValueCandidates(option.Metadata!, prefix, context);
                break;
            case CompletionValueSource.Theme:
                foreach (var theme in GetEnabledThemeNames())
                    context.Add(prefix + theme, null, TigerCliCompletionKind.Value);
                break;
            case CompletionValueSource.ColorMode:
                foreach (var mode in new[] { "auto", "never", "16", "256" })
                    context.Add(prefix + mode, null, TigerCliCompletionKind.Value);
                break;
            case CompletionValueSource.Culture:
                foreach (var supported in SupportedCultures)
                    context.Add(prefix + supported.Name, supported.NativeName, TigerCliCompletionKind.Value);
                break;
            case CompletionValueSource.Shell:
                AddShellNames(prefix, context);
                break;
            case CompletionValueSource.FreeText:
                context.Fallback = TigerCliCompletionFallback.Files;
                break;
        }
    }

    private static void AddShellNames(string prefix, CompletionContext context)
    {
        foreach (var shell in TigerCliCompletionProtocol.ShellNames)
            context.Add(prefix + shell, null, TigerCliCompletionKind.Value);
    }

    private static void AddOptionValueCandidates(TigerCliOptionMetadata option, string prefix, CompletionContext context)
    {
        if (option.Provider != null || option.EditProvider != null || option.UseMultiSelect)
            return;

        if (option.IsEnum)
        {
            foreach (var name in option.FilteredEnumValues)
                context.Add(prefix + name, null, TigerCliCompletionKind.Value);
            return;
        }

        var valueType = Nullable.GetUnderlyingType(option.Property.PropertyType) ?? option.Property.PropertyType;
        if (valueType == typeof(bool))
        {
            context.Add(prefix + "true", null, TigerCliCompletionKind.Value);
            context.Add(prefix + "false", null, TigerCliCompletionKind.Value);
            return;
        }

        if (option.UseFolderPicker)
            context.Fallback = TigerCliCompletionFallback.Directories;
        else if (option.UseFilePicker
            || option.ValueKind == OptionValueKind.RepeatedScalar
            || (option.ValueKind == OptionValueKind.Scalar && valueType == typeof(string)))
            context.Fallback = TigerCliCompletionFallback.Files;
    }

    private static void AddArgumentValueCandidates(TigerCliArgumentMetadata argument, CompletionContext context)
    {
        if (argument.Provider != null || argument.EditProvider != null)
            return;

        var valueType = Nullable.GetUnderlyingType(argument.Property.PropertyType) ?? argument.Property.PropertyType;
        if (valueType.IsEnum)
        {
            foreach (var name in Enum.GetNames(valueType))
                context.Add(name, null, TigerCliCompletionKind.Value);
        }
        else if (valueType == typeof(bool))
        {
            context.Add("true", null, TigerCliCompletionKind.Value);
            context.Add("false", null, TigerCliCompletionKind.Value);
        }
        else if (valueType == typeof(string))
        {
            context.Fallback = TigerCliCompletionFallback.Files;
        }
    }

    private string? ResolveCompletionText(string? fallback, string? resourceKey, CultureInfo culture)
    {
        var markup = TigerCliAppText.Resolve(fallback, resourceKey, culture, _appResources);
        return string.IsNullOrWhiteSpace(markup) ? null : TigerCliCompletionProtocol.MarkupToPlainText(markup);
    }

    /// <summary>Collects candidates for one request, filtering by the current word and removing duplicates.</summary>
    private sealed class CompletionContext(string currentWord, CultureInfo culture)
    {
        private readonly List<TigerCliCompletionCandidate> _candidates = [];
        private readonly HashSet<string> _values = new(StringComparer.Ordinal);

        public string CurrentWord { get; } = currentWord;
        public CultureInfo Culture { get; } = culture;
        public TigerCliCompletionFallback Fallback { get; set; }

        /// <summary>Whether a command-path token is valid at this position.</summary>
        public bool CommandPathAvailable { get; set; }

        public void Add(string value, string? description, TigerCliCompletionKind kind)
        {
            // Command paths, option names, and enum values all match case-insensitively at run time.
            if (!value.StartsWith(CurrentWord, StringComparison.OrdinalIgnoreCase) || !_values.Add(value))
                return;

            _candidates.Add(new TigerCliCompletionCandidate(value, description, kind));
        }

        public TigerCliCompletionResult ToResult() => new(_candidates.ToArray(), Fallback);
    }
}
