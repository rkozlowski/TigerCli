using System.Globalization;
using System.Reflection;
using System.Resources;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Resources;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Fluent builder for a <see cref="TigerCliApp"/>, obtained from
/// <see cref="TigerCliApp.CreateBuilder"/>. Configures app metadata, commands and command groups,
/// interaction/prompt modes, providers, themes, cultures, and exit-code policy; call
/// <see cref="Build"/> to produce the immutable app. All configuration methods return this
/// builder for chaining.
/// </summary>
public sealed class TigerCliAppBuilder
{
    private const string WebsiteLinkKind = "Website";
    private const string RepositoryLinkKind = "Repository";
    private const string DocumentationLinkKind = "Documentation";

    private string _applicationName = string.Empty;
    private bool _applicationNameSet;
    private string? _displayName;
    private bool _displayNameSet;
    private string? _description;
    private string? _descriptionResourceKey;
    private bool _descriptionSet;
    private bool _versionEnabled;
    private string? _version;
    private string? _productVersion;
    private bool _versionSet;
    private string? _copyright;
    private bool _copyrightSet;
    private readonly List<TigerCliApplicationLink> _links = new();
    private readonly HashSet<string> _explicitStandardLinkKinds = new(StringComparer.Ordinal);
    private readonly List<TigerCliCommandRegistration> _namedCommands = new();
    private readonly List<TigerCliCommandGroupRegistration> _commandGroups = new();
    private readonly List<TigerCliCommandAliasRegistration> _aliases = new();
    private Type? _defaultHandlerType;
    private Func<object>? _defaultFactory;
    private string? _defaultDescription;
    private bool _defaultResolveHandlerResultAsExitKind;
    private TigerCliExitCodePolicy _exitCodePolicy = new();
    private TigerCliInteractionMode _interactionMode = TigerCliInteractionMode.SemiInteractive;
    private TigerCliPromptMode? _promptMode;
    private readonly Dictionary<Type, TigerCliInteractionMode> _commandInteractionModes = new();
    private readonly Dictionary<Type, TigerCliPromptMode> _commandPromptModes = new();
    private readonly List<ITigerCliValueProvider> _providers = new();
    private string? _defaultCultureName;
    private List<string>? _supportedCultureNames;
    private bool _cultureOptionEnabled = true;
    private CliColorMode _defaultColorMode = CliColorMode.Auto;
    private CliOutputPresetDefaults? _defaultOutputPresets;
    private ResourceManager? _appResources;
    private IFolderBrowser? _folderBrowser;
    private readonly TigerThemeConfiguration _themeConfiguration = new();
    private bool _processCancellationEnabled = true;
    private bool _terminalTitleManagementEnabled = true;
    private bool _spinnerTitlePrefixEnabled = true;
    private CommandMenuMode _commandMenuMode = CommandMenuMode.Disabled;
    private bool _commandMenuRegistered;
    private string? _commandMenuName;
    private bool _commandMenuIsDefault;
    private string? _commandMenuDescription;
    private string? _commandMenuDescriptionResourceKey;

    internal TigerCliAppBuilder() { }

    /// <summary>
    /// Overrides the command/executable name used in usage output. For a normal executable app,
    /// prefer setting the project's <c>AssemblyName</c> and calling
    /// <see cref="UseAssemblyMetadata(Assembly, bool)"/>: the assembly name is the natural source for
    /// the command name and also controls the produced executable name, so setting the name here as
    /// well risks drifting from the real executable. Use this override only when the CLI command name
    /// must intentionally differ from the assembly name, or in tests and small synthetic examples.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
    public TigerCliAppBuilder SetApplicationName(string name)
    {
        _applicationName = name ?? throw new ArgumentNullException(nameof(name));
        _applicationNameSet = true;
        return this;
    }

    /// <summary>
    /// Overrides the human/product name used by display-oriented framework output such as
    /// <c>--version</c>. Falls back to the application name when neither this call nor assembly
    /// metadata supplies one. For a normal app, prefer the project's <c>Product</c> (or <c>Title</c>)
    /// property via <see cref="UseAssemblyMetadata(Assembly, bool)"/>; use this override only when the
    /// display name must differ from the assembly product, or in tests and small examples.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="displayName"/> is null, empty, or whitespace.</exception>
    public TigerCliAppBuilder SetDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        _displayName = displayName;
        _displayNameSet = true;
        return this;
    }

    /// <summary>
    /// Sets an optional description for the application, displayed in help output.
    /// The description may contain TigerCli markup.
    /// </summary>
    /// <param name="description">Fallback description used when no resource key is
    /// provided, when no <see cref="ResourceManager"/> is registered via
    /// <see cref="UseAppResources"/>, or when the resource key fails to resolve.</param>
    /// <param name="resourceKey">Optional resource key resolved through the registered
    /// app <see cref="ResourceManager"/> against the active run culture. Missing keys
    /// silently fall back to <paramref name="description"/>; the raw key is never shown.</param>
    public TigerCliAppBuilder AddDescription(string description, string? resourceKey = null)
    {
        _description = description ?? throw new ArgumentNullException(nameof(description));
        _descriptionResourceKey = resourceKey;
        _descriptionSet = true;
        return this;
    }

    /// <summary>
    /// Overrides the short user-facing version, sets the product/informational version to
    /// <paramref name="productVersion"/> ?? <paramref name="version"/>, and enables the built-in
    /// global <c>--version</c> / <c>--version-full</c> options. For a normal app, prefer the project's
    /// <c>Version</c> / <c>InformationalVersion</c> via <see cref="UseAssemblyMetadata(Assembly, bool)"/>
    /// so the app and its assembly report one version; use this override only when the CLI version must
    /// differ, or in tests and small examples.
    /// </summary>
    /// <param name="version">The short version shown by <c>--version</c>; must not be null/empty/whitespace.</param>
    /// <param name="productVersion">Optional full version shown by <c>--version-full</c>; defaults to <paramref name="version"/>.</param>
    /// <exception cref="ArgumentException">A supplied value is null, empty, or whitespace.</exception>
    public TigerCliAppBuilder SetVersion(string version, string? productVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (productVersion != null)
            ArgumentException.ThrowIfNullOrWhiteSpace(productVersion);

        _versionEnabled = true;
        _version = version;
        _productVersion = productVersion ?? version;
        _versionSet = true;
        return this;
    }

    /// <summary>
    /// The recommended way for a normal executable app to supply its identity and product metadata:
    /// define it once in the project file (for example <c>AssemblyName</c>, <c>Product</c>,
    /// <c>Description</c>, <c>Version</c>, <c>Copyright</c>, <c>RepositoryUrl</c>,
    /// <c>PackageProjectUrl</c>) and import it here, instead of duplicating it with the manual builder
    /// setters. Reads default app metadata (application name, display name, description, version,
    /// copyright, and well-known links) from the entry assembly, falling back to TigerCli's own
    /// assembly when no entry assembly is available. Assembly metadata acts as defaults: explicit
    /// builder calls such as <see cref="SetApplicationName"/> or <see cref="SetVersion"/> override
    /// assembly-provided values. <paramref name="enableVersion"/> (default <c>true</c>) also
    /// enables the built-in <c>--version</c> / <c>--version-full</c>; passing <c>false</c> reads
    /// the version value without enabling the options.
    /// </summary>
    /// <remarks>
    /// The assembly name supplies the application name. Display name comes from
    /// <see cref="AssemblyProductAttribute"/> then <see cref="AssemblyTitleAttribute"/>. Description
    /// comes from <see cref="AssemblyDescriptionAttribute"/>. Version comes from the simple part of
    /// <see cref="AssemblyInformationalVersionAttribute"/> <c>InformationalVersion</c> when possible,
    /// then the assembly name version, then <c>unknown</c>; product version uses the full informational
    /// version and falls back to the short version. Copyright comes from
    /// <see cref="AssemblyCopyrightAttribute"/>.
    /// </remarks>
    public TigerCliAppBuilder UseAssemblyMetadata(bool enableVersion = true)
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(TigerCliAppBuilder).Assembly;
        return UseAssemblyMetadata(assembly, enableVersion);
    }

    /// <summary>
    /// Reads default app metadata from the provided <paramref name="assembly"/>. See
    /// <see cref="UseAssemblyMetadata(bool)"/> for what is read and how explicit calls override it.
    /// </summary>
    /// <remarks>
    /// Well-known <see cref="AssemblyMetadataAttribute"/> link keys are also read:
    /// <c>Website</c> as a website link; <c>Documentation</c>, <c>ProjectUrl</c>, and
    /// <c>PackageProjectUrl</c> as a documentation link; and <c>Repository</c>,
    /// <c>RepositoryUrl</c>, <c>SourceCode</c>, and <c>SourceCodeUrl</c> as a source-code link.
    /// Explicit calls to <see cref="AddWebsite"/>, <see cref="AddDocumentation"/>, and
    /// <see cref="AddRepository"/> replace the corresponding assembly-derived standard link.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <c>null</c>.</exception>
    public TigerCliAppBuilder UseAssemblyMetadata(Assembly assembly, bool enableVersion = true)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        ApplyAssemblyMetadata(assembly);
        if (enableVersion)
            _versionEnabled = true;

        return this;
    }

    /// <summary>
    /// Reads default app metadata from <c>typeof(TMarker).Assembly</c>. Use the
    /// <see cref="UseAssemblyMetadata(Assembly, bool)"/> overload for static marker/factory
    /// classes, which cannot be type arguments.
    /// </summary>
    public TigerCliAppBuilder UseAssemblyMetadata<TMarker>(bool enableVersion = true)
    {
        return UseAssemblyMetadata(typeof(TMarker).Assembly, enableVersion);
    }

    private void ApplyAssemblyMetadata(Assembly assembly)
    {
        var applicationName = assembly.GetName().Name;
        if (!_applicationNameSet && !string.IsNullOrWhiteSpace(applicationName))
            _applicationName = applicationName;

        var displayName =
            FirstNonWhiteSpace(
                assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product,
                assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
        if (!_displayNameSet && displayName != null)
            _displayName = displayName;

        var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
        if (!_descriptionSet && !string.IsNullOrWhiteSpace(description))
        {
            _description = description;
            _descriptionResourceKey = null;
        }

        if (!_versionSet)
        {
            var (version, productVersion) = ResolveAssemblyVersions(assembly);
            _version = version;
            _productVersion = productVersion;
        }

        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        if (!_copyrightSet && !string.IsNullOrWhiteSpace(copyright))
            _copyright = copyright;

        AddAssemblyMetadataLinks(assembly);
    }

    private void AddAssemblyMetadataLinks(Assembly assembly)
    {
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Key))
            .GroupBy(attribute => attribute.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(attribute => attribute.Value).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                StringComparer.OrdinalIgnoreCase);

        var website = FirstMetadataValue(metadata, "Website");
        if (website != null)
            SetStandardLink(WebsiteLinkKind, "AppLink_Website", website, explicitLink: false);

        var documentation = FirstMetadataValue(metadata, "Documentation", "ProjectUrl", "PackageProjectUrl");
        if (documentation != null)
            SetStandardLink(DocumentationLinkKind, "AppLink_Documentation", documentation, explicitLink: false);

        var repository = FirstMetadataValue(metadata, "Repository", "RepositoryUrl", "SourceCode", "SourceCodeUrl");
        if (repository != null)
            SetStandardLink(RepositoryLinkKind, "AppLink_SourceCode", repository, explicitLink: false);
    }

    private static string? FirstMetadataValue(IReadOnlyDictionary<string, string?> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? FirstNonWhiteSpace(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private void SetStandardLink(string standardKind, string labelResourceKey, string url, bool explicitLink)
    {
        if (!explicitLink && _explicitStandardLinkKinds.Contains(standardKind))
            return;

        var link = new TigerCliApplicationLink(
            TigerCliResources.Get(labelResourceKey, CultureInfo.GetCultureInfo("en-US")),
            url,
            labelResourceKey,
            standardKind);

        var existingIndex = _links.FindIndex(existing =>
            string.Equals(existing.StandardKind, standardKind, StringComparison.Ordinal));
        if (existingIndex >= 0)
            _links[existingIndex] = link;
        else
            _links.Add(link);

        if (explicitLink)
            _explicitStandardLinkKinds.Add(standardKind);
    }

    /// <summary>
    /// Overrides the copyright text shown in the help footer. For a normal app, prefer the project's
    /// <c>Copyright</c> property via <see cref="UseAssemblyMetadata(Assembly, bool)"/>; use this
    /// override only when the footer copyright must differ, or in tests and small examples.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="copyright"/> is null, empty, or whitespace.</exception>
    public TigerCliAppBuilder AddCopyright(string copyright)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(copyright);
        _copyright = copyright;
        _copyrightSet = true;
        return this;
    }

    /// <summary>
    /// Adds a help-footer link with an app-owned <paramref name="label"/> and a visible/copyable
    /// <paramref name="url"/>, rendered with TigerCli link styling.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="label"/> or <paramref name="url"/> is null, empty, or whitespace.</exception>
    public TigerCliAppBuilder AddLink(string label, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        _links.Add(new TigerCliApplicationLink(label, url));
        return this;
    }

    /// <summary>
    /// Adds or replaces the standard help-footer website link, using the localized framework label
    /// "Website". For a normal app, prefer emitting the URL as assembly metadata (for example a
    /// <c>Website</c> <see cref="AssemblyMetadataAttribute"/>) and calling
    /// <see cref="UseAssemblyMetadata(Assembly, bool)"/>; use this override to set or replace the link
    /// explicitly. This overrides any website link read from assembly metadata.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="url"/> is null, empty, or whitespace.</exception>
    public TigerCliAppBuilder AddWebsite(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        SetStandardLink(WebsiteLinkKind, "AppLink_Website", url, explicitLink: true);
        return this;
    }

    /// <summary>
    /// Adds or replaces the standard help-footer source-code link, using the localized framework
    /// label "Source code". For a normal app, prefer the project's <c>RepositoryUrl</c> (surfaced as a
    /// <c>RepositoryUrl</c> <see cref="AssemblyMetadataAttribute"/>) via
    /// <see cref="UseAssemblyMetadata(Assembly, bool)"/>; use this override to set or replace the link
    /// explicitly. This overrides any repository/source-code link read from assembly metadata.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="url"/> is null, empty, or whitespace.</exception>
    public TigerCliAppBuilder AddRepository(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        SetStandardLink(RepositoryLinkKind, "AppLink_SourceCode", url, explicitLink: true);
        return this;
    }

    /// <summary>
    /// Adds or replaces the standard help-footer documentation link, using the localized framework
    /// label "Documentation". For a normal app, prefer the project's <c>PackageProjectUrl</c>
    /// (surfaced as a <c>Documentation</c> or <c>PackageProjectUrl</c>
    /// <see cref="AssemblyMetadataAttribute"/>) via <see cref="UseAssemblyMetadata(Assembly, bool)"/>;
    /// use this override to set or replace the link explicitly. This overrides any documentation/project
    /// URL read from assembly metadata.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="url"/> is null, empty, or whitespace.</exception>
    public TigerCliAppBuilder AddDocumentation(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        SetStandardLink(DocumentationLinkKind, "AppLink_Documentation", url, explicitLink: true);
        return this;
    }

    /// <summary>
    /// Registers the default command — the handler that runs when no command name is supplied.
    /// The handler is created with its parameterless constructor; use the factory overload for
    /// constructor dependencies.
    /// </summary>
    public TigerCliAppBuilder SetDefaultCommand<THandler>() where THandler : class, new()
    {
        _defaultHandlerType = typeof(THandler);
        _defaultFactory = null;
        _defaultDescription = null;
        _defaultResolveHandlerResultAsExitKind = false;
        return this;
    }

    /// <summary>
    /// Registers the default command created by <paramref name="factory"/>. This mirrors the
    /// command-group factory overload for the app's default/top-level command, letting a default
    /// command receive constructor dependencies without a DI container. The parameterless overload
    /// <see cref="SetDefaultCommand{THandler}()"/> keeps the new-constructor model.
    /// </summary>
    public TigerCliAppBuilder SetDefaultCommand<THandler>(Func<THandler> factory) where THandler : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        _defaultHandlerType = typeof(THandler);
        _defaultFactory = () => factory();
        _defaultDescription = null;
        _defaultResolveHandlerResultAsExitKind = false;
        return this;
    }

    /// <summary>
    /// Registers a parameterless delegate as the default command. Delegate commands are intended
    /// for tiny utilities, demos, smoke apps, and simple automation; use command handler and
    /// settings classes when the command needs binding, prompting, providers, or reusable behavior.
    /// A normally completed action resolves through the app's success exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddDefaultCommand(Action handler, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddDefaultDelegate(
            _ =>
            {
                handler();
                return Task.FromResult((int)TigerCliExitKind.Success);
            },
            description,
            resolveHandlerResultAsExitKind: true);
    }

    /// <summary>
    /// Registers a settings-aware action as the default delegate command. The action receives the
    /// framework-created <see cref="TigerCliSettings"/> instance and successful completion resolves
    /// through the app's success exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddDefaultCommand(
        Action<TigerCliSettings> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddDefaultDelegate(
            settings =>
            {
                handler(settings);
                return Task.FromResult((int)TigerCliExitKind.Success);
            },
            description,
            resolveHandlerResultAsExitKind: true);
    }

    /// <summary>
    /// Registers a raw-integer-returning delegate as the default command. The returned integer is
    /// used as the process exit code without exit-policy remapping.
    /// </summary>
    public TigerCliAppBuilder AddDefaultCommand(Func<int> handler, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddDefaultDelegate(
            _ => Task.FromResult(handler()),
            description,
            resolveHandlerResultAsExitKind: false);
    }

    /// <summary>
    /// Registers a settings-aware raw-integer-returning delegate as the default command. The
    /// returned integer is used as the process exit code without exit-policy remapping.
    /// </summary>
    public TigerCliAppBuilder AddDefaultCommand(
        Func<TigerCliSettings, int> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddDefaultDelegate(
            settings => Task.FromResult(handler(settings)),
            description,
            resolveHandlerResultAsExitKind: false);
    }

    /// <summary>
    /// Registers an exit-kind-returning delegate as the default command. The returned
    /// <see cref="TigerCliExitKind"/> is resolved through the app's exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddDefaultCommand(
        Func<TigerCliExitKind> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddDefaultDelegate(
            _ => Task.FromResult((int)handler()),
            description,
            resolveHandlerResultAsExitKind: true);
    }

    /// <summary>
    /// Registers a settings-aware exit-kind-returning delegate as the default command. The returned
    /// <see cref="TigerCliExitKind"/> is resolved through the app's exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddDefaultCommand(
        Func<TigerCliSettings, TigerCliExitKind> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddDefaultDelegate(
            settings => Task.FromResult((int)handler(settings)),
            description,
            resolveHandlerResultAsExitKind: true);
    }

    /// <summary>
    /// Registers an asynchronous raw-integer-returning delegate as the default command. The
    /// returned integer is used as the process exit code without exit-policy remapping.
    /// </summary>
    public TigerCliAppBuilder AddDefaultCommand(
        Func<Task<int>> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddDefaultDelegate(
            _ => handler(),
            description,
            resolveHandlerResultAsExitKind: false);
    }

    /// <summary>
    /// Registers an asynchronous, settings-aware raw-integer-returning delegate as the default
    /// command. The returned integer is used as the process exit code without exit-policy remapping.
    /// </summary>
    public TigerCliAppBuilder AddDefaultCommand(
        Func<TigerCliSettings, Task<int>> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddDefaultDelegate(handler, description, resolveHandlerResultAsExitKind: false);
    }

    /// <summary>
    /// Registers an asynchronous exit-kind-returning delegate as the default command. The returned
    /// <see cref="TigerCliExitKind"/> is resolved through the app's exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddDefaultCommand(
        Func<Task<TigerCliExitKind>> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddDefaultDelegate(
            async _ => (int)await handler().ConfigureAwait(false),
            description,
            resolveHandlerResultAsExitKind: true);
    }

    /// <summary>
    /// Registers an asynchronous, settings-aware exit-kind-returning delegate as the default
    /// command. The returned <see cref="TigerCliExitKind"/> is resolved through the app's
    /// exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddDefaultCommand(
        Func<TigerCliSettings, Task<TigerCliExitKind>> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddDefaultDelegate(
            async settings => (int)await handler(settings).ConfigureAwait(false),
            description,
            resolveHandlerResultAsExitKind: true);
    }

    private TigerCliAppBuilder AddDefaultDelegate(
        Func<TigerCliSettings, Task<int>> handler,
        string? description,
        bool resolveHandlerResultAsExitKind)
    {
        _defaultHandlerType = typeof(DefaultDelegateCommandHandler);
        _defaultFactory = () => new DefaultDelegateCommandHandler(handler);
        _defaultDescription = description;
        _defaultResolveHandlerResultAsExitKind = resolveHandlerResultAsExitKind;
        return this;
    }

    /// <summary>
    /// Registers a parameterless delegate as a flat named command. Delegate commands are intended
    /// for tiny utilities, demos, smoke apps, and simple automation; use command handler and
    /// settings classes when the command needs binding, prompting, providers, or reusable behavior.
    /// A normally completed action resolves through the app's success exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddCommand(
        string name,
        Action handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddNamedDelegate(
            name,
            _ =>
            {
                handler();
                return Task.FromResult((int)TigerCliExitKind.Success);
            },
            description,
            resolveHandlerResultAsExitKind: true);
    }

    /// <summary>
    /// Registers a settings-aware action as a flat named delegate command. The action receives the
    /// framework-created <see cref="TigerCliSettings"/> instance and successful completion resolves
    /// through the app's success exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddCommand(
        string name,
        Action<TigerCliSettings> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddNamedDelegate(
            name,
            settings =>
            {
                handler(settings);
                return Task.FromResult((int)TigerCliExitKind.Success);
            },
            description,
            resolveHandlerResultAsExitKind: true);
    }

    /// <summary>
    /// Registers a raw-integer-returning delegate as a flat named command. The returned integer is
    /// used as the process exit code without exit-policy remapping.
    /// </summary>
    public TigerCliAppBuilder AddCommand(
        string name,
        Func<int> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddNamedDelegate(
            name,
            _ => Task.FromResult(handler()),
            description,
            resolveHandlerResultAsExitKind: false);
    }

    /// <summary>
    /// Registers a settings-aware raw-integer-returning delegate as a flat named command. The
    /// returned integer is used as the process exit code without exit-policy remapping.
    /// </summary>
    public TigerCliAppBuilder AddCommand(
        string name,
        Func<TigerCliSettings, int> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddNamedDelegate(
            name,
            settings => Task.FromResult(handler(settings)),
            description,
            resolveHandlerResultAsExitKind: false);
    }

    /// <summary>
    /// Registers an exit-kind-returning delegate as a flat named command. The returned
    /// <see cref="TigerCliExitKind"/> is resolved through the app's exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddCommand(
        string name,
        Func<TigerCliExitKind> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddNamedDelegate(
            name,
            _ => Task.FromResult((int)handler()),
            description,
            resolveHandlerResultAsExitKind: true);
    }

    /// <summary>
    /// Registers a settings-aware exit-kind-returning delegate as a flat named command. The
    /// returned <see cref="TigerCliExitKind"/> is resolved through the app's exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddCommand(
        string name,
        Func<TigerCliSettings, TigerCliExitKind> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddNamedDelegate(
            name,
            settings => Task.FromResult((int)handler(settings)),
            description,
            resolveHandlerResultAsExitKind: true);
    }

    /// <summary>
    /// Registers an asynchronous raw-integer-returning delegate as a flat named command. The
    /// returned integer is used as the process exit code without exit-policy remapping.
    /// </summary>
    public TigerCliAppBuilder AddCommand(
        string name,
        Func<Task<int>> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddNamedDelegate(
            name,
            _ => handler(),
            description,
            resolveHandlerResultAsExitKind: false);
    }

    /// <summary>
    /// Registers an asynchronous, settings-aware raw-integer-returning delegate as a flat named
    /// command. The returned integer is used as the process exit code without exit-policy remapping.
    /// </summary>
    public TigerCliAppBuilder AddCommand(
        string name,
        Func<TigerCliSettings, Task<int>> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddNamedDelegate(name, handler, description, resolveHandlerResultAsExitKind: false);
    }

    /// <summary>
    /// Registers an asynchronous exit-kind-returning delegate as a flat named command. The returned
    /// <see cref="TigerCliExitKind"/> is resolved through the app's exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddCommand(
        string name,
        Func<Task<TigerCliExitKind>> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddNamedDelegate(
            name,
            async _ => (int)await handler().ConfigureAwait(false),
            description,
            resolveHandlerResultAsExitKind: true);
    }

    /// <summary>
    /// Registers an asynchronous, settings-aware exit-kind-returning delegate as a flat named
    /// command. The returned <see cref="TigerCliExitKind"/> is resolved through the app's
    /// exit-code policy.
    /// </summary>
    public TigerCliAppBuilder AddCommand(
        string name,
        Func<TigerCliSettings, Task<TigerCliExitKind>> handler,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return AddNamedDelegate(
            name,
            async settings => (int)await handler(settings).ConfigureAwait(false),
            description,
            resolveHandlerResultAsExitKind: true);
    }

    private TigerCliAppBuilder AddNamedDelegate(
        string name,
        Func<TigerCliSettings, Task<int>> handler,
        string? description,
        bool resolveHandlerResultAsExitKind)
    {
        AddNamedCommand(
            typeof(NamedDelegateCommandHandler),
            name,
            configure: null,
            description,
            descriptionResourceKey: null,
            () => new NamedDelegateCommandHandler(handler),
            resolveHandlerResultAsExitKind);
        return this;
    }

    /// <summary>
    /// Registers a named command. <paramref name="description"/> is the fallback help
    /// text; <paramref name="descriptionResourceKey"/>, when provided and registered
    /// via <see cref="UseAppResources"/>, is resolved against the active run culture
    /// at help-render time. Missing keys silently fall back to <paramref name="description"/>.
    /// </summary>
    public TigerCliAppBuilder AddCommand<THandler>(
        string name,
        string? description = null,
        string? descriptionResourceKey = null) where THandler : class, new()
    {
        return AddCommand<THandler>(name, configure: null, description, descriptionResourceKey);
    }

    /// <summary>
    /// Registers a named command and configures metadata for that registration.
    /// </summary>
    public TigerCliAppBuilder AddCommand<THandler>(
        string name,
        Action<TigerCliCommandBuilder>? configure,
        string? description = null,
        string? descriptionResourceKey = null) where THandler : class, new()
    {
        AddNamedCommand(typeof(THandler), name, configure, description, descriptionResourceKey, factory: null);
        return this;
    }

    /// <summary>
    /// Registers a named command created by <paramref name="factory"/>. Command factories let a
    /// top-level command receive constructor dependencies without a DI container, mirroring the
    /// command-group factory overload. The non-factory overloads keep the parameterless-constructor
    /// model.
    /// </summary>
    public TigerCliAppBuilder AddCommand<THandler>(
        string name,
        Func<THandler> factory,
        string? description = null,
        string? descriptionResourceKey = null) where THandler : class
    {
        return AddCommand(name, factory, configure: null, description, descriptionResourceKey);
    }

    /// <summary>
    /// Registers a named command created by <paramref name="factory"/> and configures metadata for
    /// that registration.
    /// </summary>
    public TigerCliAppBuilder AddCommand<THandler>(
        string name,
        Func<THandler> factory,
        Action<TigerCliCommandBuilder>? configure,
        string? description = null,
        string? descriptionResourceKey = null) where THandler : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        AddNamedCommand(typeof(THandler), name, configure, description, descriptionResourceKey, () => factory());
        return this;
    }

    private void AddNamedCommand(
        Type handlerType,
        string name,
        Action<TigerCliCommandBuilder>? configure,
        string? description,
        string? descriptionResourceKey,
        Func<object>? factory,
        bool resolveHandlerResultAsExitKind = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command name must not be empty.", nameof(name));
        var nameTokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (nameTokens.Any(token => token.StartsWith('-')))
            throw new ArgumentException("Command path tokens must not start with '-'.", nameof(name));
        if (nameTokens.Length != 1)
            throw new ArgumentException(
                "Command name must be a single token. Multi-token command paths must be owned by a command group registered via AddCommandGroup.",
                nameof(name));

        var commandBuilder = new TigerCliCommandBuilder();
        configure?.Invoke(commandBuilder);
        _namedCommands.Add(new TigerCliCommandRegistration(
            name,
            description,
            handlerType,
            descriptionResourceKey,
            factory,
            promptMode: commandBuilder.PromptMode,
            providers: commandBuilder.Providers,
            editLoader: commandBuilder.EditLoader,
            titleAppend: commandBuilder.TitleAppend,
            titleSet: commandBuilder.TitleSet,
            commandMenuMode: commandBuilder.CommandMenuMode,
            resolveHandlerResultAsExitKind: resolveHandlerResultAsExitKind));
    }

    /// <summary>
    /// Adds a command group: a command-path prefix that owns a set of child commands.
    /// The <paramref name="name"/> is the group prefix and is chosen by the consuming
    /// app. Commands added through <paramref name="configure"/> behave like normal
    /// commands whose path begins with that prefix, so parsing, binding, help and
    /// execution stay on the same pipeline. Top-level help lists the group as a single
    /// entry; the group's own help lists its child commands and any nested subgroups
    /// registered via <see cref="TigerCliCommandGroupBuilder.AddCommandGroup"/>.
    /// </summary>
    public TigerCliAppBuilder AddCommandGroup(string name, Action<TigerCliCommandGroupBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var groupBuilder = new TigerCliCommandGroupBuilder(name);
        configure(groupBuilder);

        groupBuilder.Collect(
            _commandGroups, _namedCommands, [], inheritedPromptMode: null, CommandMenuMode.Inherit);
        return this;
    }

    /// <summary>
    /// Registers a root-level alias: an alternate single-token entry point that maps to an existing
    /// command path (for example <c>import</c> → <c>card ingest</c>). Commands resolve first; aliases
    /// are consulted only when no command path matches. The alias owns no handler, settings, or
    /// providers — the target command owns parsing, prompting, validation, and execution. Through
    /// <paramref name="configure"/> the alias may own its own description, help visibility, and
    /// command-menu opinion.
    /// </summary>
    /// <param name="alias">The single-token alias name. Multi-token aliases are not supported, and
    /// tokens must not start with <c>-</c>.</param>
    /// <param name="targetCommandPath">The existing command path the alias maps to, such as
    /// <c>card ingest</c>. Validated against the resolved command tree at <see cref="Build"/> time;
    /// it must name a command, not a command group.</param>
    /// <param name="configure">Optional alias presentation configuration.</param>
    public TigerCliAppBuilder AddCommandAlias(
        string alias,
        string targetCommandPath,
        Action<TigerCliCommandAliasBuilder>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias name must not be empty.", nameof(alias));

        var aliasTokens = alias.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (aliasTokens.Any(token => token.StartsWith('-')))
            throw new ArgumentException("Alias path tokens must not start with '-'.", nameof(alias));
        if (aliasTokens.Length != 1)
            throw new ArgumentException(
                "Alias name must be a single token. Multi-token aliases are not supported.",
                nameof(alias));

        if (string.IsNullOrWhiteSpace(targetCommandPath))
            throw new ArgumentException("Alias target command path must not be empty.", nameof(targetCommandPath));

        var aliasBuilder = new TigerCliCommandAliasBuilder();
        configure?.Invoke(aliasBuilder);
        _aliases.Add(new TigerCliCommandAliasRegistration(
            aliasTokens[0],
            targetCommandPath.Trim(),
            aliasBuilder.Description,
            aliasBuilder.DescriptionResourceKey,
            aliasBuilder.CommandMenuMode,
            aliasBuilder.HiddenFromHelp));
        return this;
    }

    /// <summary>
    /// Enables the discoverable command menu: a picker that lists eligible commands, lets the user
    /// choose one, then runs it through the normal parse/bind/prompt/validate/execute pipeline. The
    /// menu is a UX layer above prompting (the user knows the app and TigerCli helps choose the
    /// command); it is not a separate execution system.
    /// </summary>
    /// <param name="mode">The app-level node in the eligibility chain. <see cref="CommandMenuMode.Enabled"/>
    /// opts every command in (unless overridden below); <see cref="CommandMenuMode.Inherit"/> turns the
    /// menu surface on but lists only commands/groups that opt in themselves; <see cref="CommandMenuMode.Disabled"/>
    /// registers no menu command (the mode is still recorded for resolution).</param>
    /// <param name="commandName">When null, the menu becomes the app's default/root command. When
    /// non-null, a root-level command with that name opens the menu. This API registers either a
    /// default or a named menu command, never both, and the null form conflicts with a configured
    /// default command.</param>
    /// <param name="description">Optional help/description text for a named menu command.</param>
    /// <param name="descriptionResourceKey">Optional resource key for <paramref name="description"/>.</param>
    public TigerCliAppBuilder UseCommandMenu(
        CommandMenuMode mode = CommandMenuMode.Enabled,
        string? commandName = null,
        string? description = null,
        string? descriptionResourceKey = null)
    {
        if (!string.IsNullOrWhiteSpace(commandName)
            && commandName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(token => token.StartsWith('-')))
            throw new ArgumentException("Command path tokens must not start with '-'.", nameof(commandName));

        _commandMenuMode = mode;
        _commandMenuRegistered = mode != CommandMenuMode.Disabled;
        _commandMenuName = string.IsNullOrWhiteSpace(commandName) ? null : commandName.Trim();
        _commandMenuIsDefault = _commandMenuName == null;
        _commandMenuDescription = description;
        _commandMenuDescriptionResourceKey = descriptionResourceKey;
        return this;
    }

    /// <summary>
    /// Configures the app-level default interaction mode, which decides whether TigerCli may
    /// prompt or show TUI controls. The default is <c>SemiInteractive</c>.
    /// </summary>
    public TigerCliAppBuilder SetInteractionMode(TigerCliInteractionMode mode)
    {
        _interactionMode = mode;
        return this;
    }

    /// <summary>
    /// Configures a command-level interaction mode override for the command handled by
    /// <typeparamref name="THandler"/>, taking precedence over the app-level mode.
    /// </summary>
    public TigerCliAppBuilder SetCommandInteractionMode<THandler>(TigerCliInteractionMode mode) where THandler : class, new()
    {
        _commandInteractionModes[typeof(THandler)] = mode;
        return this;
    }

    /// <summary>
    /// Configures the app-level default prompt mode, which decides which missing values are
    /// prompted for. The framework default is <c>RequiredOnly</c>.
    /// </summary>
    public TigerCliAppBuilder SetDefaultPromptMode(TigerCliPromptMode mode)
    {
        _promptMode = mode;
        return this;
    }

    /// <summary>
    /// Configures a type-level prompt mode override for the command handled by
    /// <typeparamref name="THandler"/>, taking precedence over group- and app-level prompt modes.
    /// </summary>
    public TigerCliAppBuilder SetCommandPromptMode<THandler>(TigerCliPromptMode mode) where THandler : class
    {
        _commandPromptModes[typeof(THandler)] = mode;
        return this;
    }

    /// <summary>
    /// Configures app-level named dynamic value providers, used by provider-backed prompts and by
    /// validation of supplied provider-backed values. See <see cref="TigerCliProviderConfiguration"/>
    /// for the registration surface. May be called multiple times; registrations accumulate.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
    public TigerCliAppBuilder ConfigureProviders(Action<TigerCliProviderConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(new TigerCliProviderConfiguration(_providers));
        return this;
    }

    /// <summary>
    /// Compatibility surface for older property-targeted provider-backed prompt registration.
    /// Prefer <see cref="ConfigureProviders"/> or the group/command <c>AddProvider</c> /
    /// <c>AddAsyncProvider</c> registrations for new code.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
    public TigerCliAppBuilder ConfigurePrompts<TSettings>(
        Action<TigerCliPromptConfiguration<TSettings>> configure)
        where TSettings : TigerCliSettings
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(new TigerCliPromptConfiguration<TSettings>(_providers));
        return this;
    }

    /// <summary>
    /// Configures the raw-integer exit-code policy with its mandatory outcome baseline: every
    /// success outcome maps to <paramref name="successCode"/> and every error outcome to
    /// <paramref name="errorCode"/>. Refine with <see cref="ExitCategory(TigerCliExitCategory, int)"/>,
    /// <see cref="ExitRange(TigerCliExitKind, TigerCliExitKind, int)"/>, and
    /// <see cref="ExitKind(TigerCliExitKind, int)"/> directly on the builder chain. Resolution
    /// picks the most specific configured layer (kind → range → category → baseline),
    /// independent of configuration order.
    /// </summary>
    public TigerCliAppBuilder UseExitCodes(int successCode, int errorCode)
    {
        _exitCodePolicy = new TigerCliExitCodePolicy(successCode, errorCode);
        return this;
    }

    /// <summary>
    /// Configures the enum-backed exit-code policy with its mandatory outcome baseline: every
    /// success outcome maps to <paramref name="successCode"/> and every error outcome to
    /// <paramref name="errorCode"/>. <typeparamref name="TExitCode"/> is also registered as the
    /// documented exit-code enum for <c>--help-errors</c>. Refine with the enum-backed
    /// <c>ExitCategory</c>/<c>ExitRange</c>/<c>ExitKind</c> overloads directly on the builder chain.
    /// </summary>
    public TigerCliAppBuilder UseExitCodes<TExitCode>(TExitCode successCode, TExitCode errorCode)
        where TExitCode : struct, Enum
    {
        _exitCodePolicy = new TigerCliExitCodePolicy(
            Convert.ToInt32(successCode),
            Convert.ToInt32(errorCode),
            typeof(TExitCode));
        return this;
    }

    /// <summary>
    /// Overrides the exit code for every framework kind in <paramref name="category"/>. Beats the
    /// outcome baseline; loses to <see cref="ExitRange(TigerCliExitKind, TigerCliExitKind, int)"/> and
    /// <see cref="ExitKind(TigerCliExitKind, int)"/>. Refines the current exit-code policy (the
    /// framework default baseline when <c>UseExitCodes</c> has not been called).
    /// </summary>
    public TigerCliAppBuilder ExitCategory(TigerCliExitCategory category, int exitCode)
    {
        _exitCodePolicy.SetCategory(category, exitCode);
        return this;
    }

    /// <summary>
    /// Maps the inclusive band of framework kinds <c>[start, end]</c> (by declared value) to
    /// consecutive exit codes starting at <paramref name="firstExitCode"/>. Beats category and
    /// baseline; loses to <see cref="ExitKind(TigerCliExitKind, int)"/>. The band is bounded strictly
    /// by the explicit start and end; throws when <paramref name="start"/> comes after
    /// <paramref name="end"/> or either is undefined.
    /// </summary>
    public TigerCliAppBuilder ExitRange(TigerCliExitKind start, TigerCliExitKind end, int firstExitCode)
    {
        _exitCodePolicy.SetRange(start, end, firstExitCode);
        return this;
    }

    /// <summary>
    /// Overrides the exit code for a single framework kind. Beats range, category, and baseline.
    /// </summary>
    public TigerCliAppBuilder ExitKind(TigerCliExitKind kind, int exitCode)
    {
        _exitCodePolicy.SetKind(kind, exitCode);
        return this;
    }

    /// <summary>Enum-backed overload of <see cref="ExitCategory(TigerCliExitCategory, int)"/>.</summary>
    public TigerCliAppBuilder ExitCategory<TExitCode>(TigerCliExitCategory category, TExitCode exitCode)
        where TExitCode : struct, Enum
        => ExitCategory(category, Convert.ToInt32(exitCode));

    /// <summary>
    /// Enum-backed overload of <see cref="ExitRange(TigerCliExitKind, TigerCliExitKind, int)"/>. Each
    /// following kind maps to the next underlying enum value (<c>firstExitCode + 1</c>, <c>+ 2</c>, …);
    /// the offsets are arithmetic on the underlying value and need not be defined members of
    /// <typeparamref name="TExitCode"/>.
    /// </summary>
    public TigerCliAppBuilder ExitRange<TExitCode>(TigerCliExitKind start, TigerCliExitKind end, TExitCode firstExitCode)
        where TExitCode : struct, Enum
        => ExitRange(start, end, Convert.ToInt32(firstExitCode));

    /// <summary>Enum-backed overload of <see cref="ExitKind(TigerCliExitKind, int)"/>.</summary>
    public TigerCliAppBuilder ExitKind<TExitCode>(TigerCliExitKind kind, TExitCode exitCode)
        where TExitCode : struct, Enum
        => ExitKind(kind, Convert.ToInt32(exitCode));

    /// <summary>
    /// Sets the default UI culture for framework-owned strings. The default
    /// culture is always implicitly part of the supported set.
    /// </summary>
    public TigerCliAppBuilder SetDefaultCulture(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        _defaultCultureName = cultureName;
        return this;
    }

    /// <summary>
    /// Sets the supported UI cultures. If no default culture is configured,
    /// the first supported culture becomes the default. The default culture
    /// (whether explicit or implicit) is always included in the supported set.
    /// </summary>
    public TigerCliAppBuilder SetSupportedCultures(params string[] cultureNames)
    {
        ArgumentNullException.ThrowIfNull(cultureNames);
        _supportedCultureNames = new List<string>(cultureNames);
        return this;
    }

    /// <summary>
    /// Disables the framework-owned <c>--culture</c> option. By default, the
    /// option is available and selects among configured supported cultures.
    /// </summary>
    public TigerCliAppBuilder DisableCultureOption()
    {
        _cultureOptionEnabled = false;
        return this;
    }

    /// <summary>
    /// Sets the default <see cref="CliColorMode"/> for the app. Applied to
    /// <see cref="ItTiger.TigerCli.Terminal.TigerConsole.ColorMode"/> at run time unless overridden
    /// by the framework <c>--color</c> / <c>--no-color</c> option. Defaults to
    /// <see cref="CliColorMode.Auto"/>.
    /// </summary>
    public TigerCliAppBuilder SetColorMode(CliColorMode mode)
    {
        _defaultColorMode = mode;
        return this;
    }

    /// <summary>
    /// Sets app-level default table style presets for structured output builders. These defaults are
    /// used only during this app's run, and only when an individual <see cref="CliDetails"/>,
    /// <see cref="CliList{T}"/>, or direct <see cref="CliTable"/> has not applied its own preset/style.
    /// </summary>
    public TigerCliAppBuilder SetDefaultOutputPresets(
        CliTableStylePreset details,
        CliTableStylePreset list,
        CliTableStylePreset? table = null)
    {
        _defaultOutputPresets = new CliOutputPresetDefaults(details, list, table);
        return this;
    }

    /// <summary>
    /// Registers an application-owned <see cref="ResourceManager"/> used to
    /// resolve <c>TigerTextAttribute.ResourceKey</c> /
    /// <c>DescriptionResourceKey</c> values on enum types and members. Lookups
    /// always pass the run's resolved <see cref="CultureInfo"/>; the framework
    /// never mutates <see cref="CultureInfo.CurrentUICulture"/> for these.
    /// </summary>
    public TigerCliAppBuilder UseAppResources(ResourceManager resourceManager)
    {
        ArgumentNullException.ThrowIfNull(resourceManager);
        _appResources = resourceManager;
        return this;
    }

    /// <summary>
    /// Overrides the filesystem source used by folder-picker prompts (options marked with
    /// <see cref="TigerCliFolderSelectAttribute"/>). Defaults to a real <c>FileSystemFolderBrowser</c>
    /// when not set. Primarily useful for tests or for sandboxing folder navigation to a controlled tree.
    /// </summary>
    public TigerCliAppBuilder UseFolderBrowser(IFolderBrowser folderBrowser)
    {
        ArgumentNullException.ThrowIfNull(folderBrowser);
        _folderBrowser = folderBrowser;
        return this;
    }

    /// <summary>
    /// Configures the app's theme/style/colour-alias policy: add custom themes, disable framework
    /// themes, register raw colour aliases, and register custom semantic styles. The application owns
    /// this policy; opt-in theme/style libraries expose their own extension methods on
    /// <see cref="TigerThemeConfiguration"/>, and nothing is registered unless invoked here. May be
    /// called multiple times; each call further configures the same app-scoped registry.
    /// </summary>
    public TigerCliAppBuilder ConfigureThemes(Action<TigerThemeConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_themeConfiguration);
        return this;
    }

    /// <summary>
    /// Disables TigerCli-managed process/system cancellation handling. By default TigerCli registers
    /// cooperative handlers for Ctrl-C / SIGINT, SIGTERM, and (where supported) SIGQUIT at run start, so
    /// those signals surface as <see cref="DialogResultKind.SystemCancel"/> through the prompt/modal
    /// pipeline and let modal <c>finally</c> blocks restore terminal state. Call this when the host owns
    /// signal handling itself.
    /// </summary>
    public TigerCliAppBuilder DisableProcessCancellation()
    {
        _processCancellationEnabled = false;
        return this;
    }

    /// <summary>
    /// Disables TigerCli-managed terminal window title updates for the app run. When disabled, app
    /// titles, command title metadata, and spinner title prefixing are all skipped.
    /// </summary>
    public TigerCliAppBuilder DisableTerminalTitleManagement()
    {
        _terminalTitleManagementEnabled = false;
        return this;
    }

    /// <summary>
    /// Disables prepending active <see cref="Tui.Controls.SpinnerTicker"/> frames to the terminal window
    /// title. The base app/command title is still written unless terminal title management is disabled.
    /// </summary>
    public TigerCliAppBuilder DisableSpinnerTitlePrefix()
    {
        _spinnerTitlePrefixEnabled = false;
        return this;
    }

    /// <summary>
    /// Validates the configuration and produces the immutable <see cref="TigerCliApp"/>. Cross-cutting
    /// registration conflicts are detected here rather than at registration time — e.g. a default
    /// command menu combined with a default command, a command-menu name colliding with a command
    /// path, or an alias whose path conflicts with commands/groups/aliases or whose target does not
    /// name an existing command.
    /// </summary>
    /// <exception cref="InvalidOperationException">The configuration contains conflicting or unresolved registrations.</exception>
    public TigerCliApp Build()
    {
        if (_commandMenuRegistered && _commandMenuIsDefault && _defaultHandlerType != null)
            throw new InvalidOperationException(
                "A default command menu (UseCommandMenu with commandName: null) cannot be combined with a default command. Use a named command menu, or remove the default command.");

        if (_commandMenuRegistered && _commandMenuName != null)
        {
            var menuTokens = _commandMenuName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (_namedCommands.Any(c => c.PathTokens.SequenceEqual(menuTokens, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    $"A command with the path '{_commandMenuName}' is already registered; the command menu cannot reuse it.");
        }

        var (defaultCulture, supportedCultures) = ResolveCultures();
        var metadata = new TigerCliApplicationMetadata(
            _displayName ?? _applicationName,
            _version,
            _productVersion ?? _version,
            _versionEnabled,
            _copyright,
            _links.ToArray());

        return new TigerCliApp(
            _applicationName,
            metadata,
            _description,
            _descriptionResourceKey,
            _namedCommands,
            _commandGroups,
            _defaultHandlerType,
            _defaultFactory,
            _defaultDescription,
            _defaultResolveHandlerResultAsExitKind,
            _exitCodePolicy,
            _interactionMode,
            _commandInteractionModes,
            _promptMode,
            _commandPromptModes,
            _providers,
            defaultCulture,
            supportedCultures,
            _cultureOptionEnabled,
            _defaultColorMode,
            _defaultOutputPresets,
            _appResources,
            _themeConfiguration,
            _folderBrowser,
            _processCancellationEnabled,
            _terminalTitleManagementEnabled,
            _spinnerTitlePrefixEnabled,
            _commandMenuMode,
            _commandMenuRegistered,
            _commandMenuName,
            _commandMenuIsDefault,
            _commandMenuDescription,
            _commandMenuDescriptionResourceKey,
            _aliases);
    }

    private static (string Version, string ProductVersion) ResolveAssemblyVersions(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var version = GetShortVersion(informationalVersion);
            return (version, informationalVersion);
        }

        var assemblyVersion = assembly.GetName().Version?.ToString();
        var resolvedVersion = string.IsNullOrWhiteSpace(assemblyVersion) ? "unknown" : assemblyVersion;
        return (resolvedVersion, resolvedVersion);
    }

    private static string GetShortVersion(string informationalVersion)
    {
        var metadataStart = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        if (metadataStart <= 0)
            return informationalVersion;

        var version = informationalVersion[..metadataStart];
        return string.IsNullOrWhiteSpace(version) ? informationalVersion : version;
    }

    private (CultureInfo Default, IReadOnlyList<CultureInfo> Supported) ResolveCultures()
    {
        var defaultExplicit = !string.IsNullOrWhiteSpace(_defaultCultureName);
        var supportedExplicit = _supportedCultureNames is { Count: > 0 };

        if (!defaultExplicit && !supportedExplicit)
        {
            var en = CultureInfo.GetCultureInfo("en-US");
            return (en, new[] { en });
        }

        if (defaultExplicit && !supportedExplicit)
        {
            var def = ResolveCulture(_defaultCultureName!);
            return (def, new[] { def });
        }

        if (!defaultExplicit && supportedExplicit)
        {
            var supported = DedupePreserveOrder(_supportedCultureNames!.Select(ResolveCulture));
            return (supported[0], supported);
        }

        // Both specified — default first, then explicit supported (deduped).
        var explicitDefault = ResolveCulture(_defaultCultureName!);
        var combined = new List<CultureInfo> { explicitDefault };
        combined.AddRange(_supportedCultureNames!.Select(ResolveCulture));
        return (explicitDefault, DedupePreserveOrder(combined));
    }

    private static CultureInfo ResolveCulture(string cultureName)
    {
        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException ex)
        {
            throw new InvalidOperationException(
                $"Culture '{cultureName}' is not a known .NET culture identifier.", ex);
        }
    }

    private static IReadOnlyList<CultureInfo> DedupePreserveOrder(IEnumerable<CultureInfo> source)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CultureInfo>();
        foreach (var c in source)
        {
            if (seen.Add(c.Name))
                result.Add(c);
        }
        return result;
    }
}
