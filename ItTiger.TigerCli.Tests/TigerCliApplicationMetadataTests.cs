using System.Reflection;
using System.Reflection.Emit;
using ItTiger.TigerCli.Commands;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliApplicationMetadataTests
{
    private static class StaticMarker
    {
    }

    private sealed class EmptySettings : TigerCliSettings
    {
    }

    private sealed class EmptyCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings) => Task.FromResult(0);
    }

    private sealed class CountingCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        private readonly Action _onRun;

        public CountingCommand(Action onRun)
        {
            _onRun = onRun;
        }

        public override Task<int> ExecuteAsync(EmptySettings settings)
        {
            _onRun();
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task SetDisplayName_StoresAndUsesDisplayName()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tiger-wrap")
            .SetDisplayName("TigerWrap")
            .SetVersion("1.2.3")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--version"]);

        Assert.Equal("TigerWrap", app.ApplicationMetadata.DisplayName);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("TigerWrap version 1.2.3" + Environment.NewLine, result.Stdout);
    }

    [Fact]
    public async Task DisplayName_FallsBackToApplicationName()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tiger-wrap")
            .SetVersion("1.2.3")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--version"]);

        Assert.Equal("tiger-wrap", app.ApplicationMetadata.DisplayName);
        Assert.Equal("tiger-wrap version 1.2.3" + Environment.NewLine, result.Stdout);
    }

    [Fact]
    public async Task SetVersion_ExplicitVersion_EnablesVersionOptions()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetVersion("1.2.3")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--version"]);
        var full = await RunCapturedAsync(app, ["--version-full"]);
        var help = await RunCapturedAsync(app, ["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("1.2.3", app.ApplicationMetadata.Version);
        Assert.Equal("1.2.3", app.ApplicationMetadata.ProductVersion);
        Assert.Contains("tool version 1.2.3", result.Stdout);
        Assert.Contains("tool product version 1.2.3", full.Stdout);
        Assert.Contains("--version", help.Stdout);
        Assert.Contains("--version-full", help.Stdout);
    }

    [Fact]
    public async Task SetVersion_ProductVersion_UsesShortAndFullVersionsSeparately()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetVersion("0.5.0", productVersion: "0.5.0+build")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var version = await RunCapturedAsync(app, ["--version"]);
        var productVersion = await RunCapturedAsync(app, ["--version-full"]);

        Assert.Equal("0.5.0", app.ApplicationMetadata.Version);
        Assert.Equal("0.5.0+build", app.ApplicationMetadata.ProductVersion);
        Assert.Equal("tool version 0.5.0" + Environment.NewLine, version.Stdout);
        Assert.Equal("tool product version 0.5.0+build" + Environment.NewLine, productVersion.Stdout);
    }

    [Theory]
    [InlineData("--version", "--version-full")]
    [InlineData("--version-full", "--version")]
    public async Task VersionOptionsTogether_PrintShortThenProductVersion(string first, string second)
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetVersion("0.5.0", productVersion: "0.5.0+build")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, [first, second]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            "tool version 0.5.0" + Environment.NewLine +
            "tool product version 0.5.0+build" + Environment.NewLine,
            result.Stdout);
    }

    [Fact]
    public async Task UseAssemblyMetadata_PopulatesApplicationNameFromAssemblyName()
    {
        var assembly = CreateMetadataAssembly(
            assemblyName: "assembly-tool",
            informationalVersion: "1.2.3");

        var app = TigerCliApp.CreateBuilder()
            .UseAssemblyMetadata(assembly)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--version"]);

        Assert.Equal("assembly-tool", app.ApplicationMetadata.DisplayName);
        Assert.Contains("assembly-tool version 1.2.3", result.Stdout);
    }

    [Fact]
    public void SetApplicationName_OverridesAssemblyDerivedApplicationName()
    {
        var assembly = CreateMetadataAssembly(assemblyName: "assembly-tool");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("custom-name")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal("custom-name", app.ApplicationMetadata.DisplayName);
    }

    [Fact]
    public void UseAssemblyMetadata_ReadsDisplayProductName()
    {
        var assembly = CreateMetadataAssembly(product: "Assembly Product");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal("Assembly Product", app.ApplicationMetadata.DisplayName);
    }

    [Fact]
    public void UseAssemblyMetadata_FallsBackToTitleThenApplicationName()
    {
        var titleAssembly = CreateMetadataAssembly(title: "Assembly Title");

        var titleApp = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(titleAssembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var noDisplayAssembly = CreateMetadataAssembly();

        var applicationNameApp = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(noDisplayAssembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal("Assembly Title", titleApp.ApplicationMetadata.DisplayName);
        Assert.Equal("tool", applicationNameApp.ApplicationMetadata.DisplayName);
    }

    [Fact]
    public async Task UseAssemblyMetadata_ReadsDescription()
    {
        var assembly = CreateMetadataAssembly(description: "Assembly description.");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Assembly description.", result.Stdout);
    }

    [Fact]
    public async Task AddDescription_OverridesAssemblyDerivedDescription()
    {
        var assembly = CreateMetadataAssembly(description: "Assembly description.");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .AddDescription("Explicit description.")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Explicit description.", result.Stdout);
        Assert.DoesNotContain("Assembly description.", result.Stdout);
    }

    [Fact]
    public void UseAssemblyMetadata_ReadsInformationalVersion()
    {
        var assembly = CreateMetadataAssembly(informationalVersion: "9.8.7-preview+abc123");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal("9.8.7-preview", app.ApplicationMetadata.Version);
        Assert.Equal("9.8.7-preview+abc123", app.ApplicationMetadata.ProductVersion);
        Assert.False(app.ApplicationMetadata.VersionEnabled);
    }

    [Fact]
    public void UseAssemblyMetadata_ProductVersion_UsesInformationalVersionAsIs()
    {
        var assembly = CreateMetadataAssembly(
            informationalVersion: "0.5.0+20260614.165940");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal("0.5.0", app.ApplicationMetadata.Version);
        Assert.Equal("0.5.0+20260614.165940", app.ApplicationMetadata.ProductVersion);
    }

    [Fact]
    public void UseAssemblyMetadata_FallsBackToAssemblyNameVersionThenUnknown()
    {
        var versionedAssembly = CreateMetadataAssembly(assemblyVersion: new Version(2, 3, 4, 5));

        var versionedApp = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(versionedAssembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var unknownApp = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(new UnknownVersionAssembly(), enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal("2.3.4.5", versionedApp.ApplicationMetadata.Version);
        Assert.Equal("2.3.4.5", versionedApp.ApplicationMetadata.ProductVersion);
        Assert.Equal("unknown", unknownApp.ApplicationMetadata.Version);
        Assert.Equal("unknown", unknownApp.ApplicationMetadata.ProductVersion);
    }

    [Fact]
    public async Task UseAssemblyMetadata_ReadsCopyright()
    {
        var assembly = CreateMetadataAssembly(copyright: "Copyright (c) Assembly Owner");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Copyright (c) Assembly Owner", result.Stdout);
    }

    [Theory]
    [InlineData("Website", "Website", "https://example.com/site")]
    [InlineData("ProjectUrl", "Documentation", "https://example.com/project")]
    [InlineData("PackageProjectUrl", "Documentation", "https://example.com/package")]
    [InlineData("Documentation", "Documentation", "https://example.com/docs")]
    [InlineData("Repository", "Source code", "https://example.com/repo")]
    [InlineData("RepositoryUrl", "Source code", "https://example.com/repository-url")]
    [InlineData("SourceCode", "Source code", "https://example.com/source")]
    [InlineData("SourceCodeUrl", "Source code", "https://example.com/source-url")]
    public async Task UseAssemblyMetadata_ReadsSupportedLinkMetadataKeys(
        string metadataKey,
        string expectedLabel,
        string url)
    {
        var assembly = CreateMetadataAssembly(metadata: [(metadataKey, url)]);

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains(expectedLabel, result.Stdout);
        Assert.Contains(url, result.Stdout);
    }

    [Fact]
    public async Task UseAssemblyMetadata_EnablesVersionOptionByDefault()
    {
        var assembly = CreateMetadataAssembly(informationalVersion: "1.2.3");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--version"]);

        Assert.True(app.ApplicationMetadata.VersionEnabled);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tool version 1.2.3", result.Stdout);
    }

    [Fact]
    public async Task UseAssemblyMetadata_EnableVersionFalse_DoesNotEnableVersionOption()
    {
        var assembly = CreateMetadataAssembly(informationalVersion: "1.2.3");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--version"]);

        Assert.Equal("1.2.3", app.ApplicationMetadata.Version);
        Assert.Equal("1.2.3", app.ApplicationMetadata.ProductVersion);
        Assert.False(app.ApplicationMetadata.VersionEnabled);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--version'", result.Stderr);
    }

    [Fact]
    public void UseAssemblyMetadata_WithAssembly_UsesProvidedAssembly()
    {
        var assembly = CreateMetadataAssembly(product: "Provided Product");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal("Provided Product", app.ApplicationMetadata.DisplayName);
    }

    [Fact]
    public void UseAssemblyMetadata_WithAssembly_WorksForStaticClassAssemblies()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(typeof(StaticMarker).Assembly, enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal(ResolveExpectedAssemblyVersion(typeof(StaticMarker).Assembly), app.ApplicationMetadata.Version);
    }

    [Fact]
    public void UseAssemblyMetadata_WithMarkerType_UsesMarkerAssembly()
    {
        var expectedVersion = ResolveExpectedAssemblyVersion(typeof(TigerCliApplicationMetadataTests).Assembly);

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata<TigerCliApplicationMetadataTests>(enableVersion: false)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal(expectedVersion, app.ApplicationMetadata.Version);
    }

    [Fact]
    public async Task WithoutSetVersion_VersionOptionIsNotEnabled()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--version"]);
        var full = await RunCapturedAsync(app, ["--version-full"]);

        Assert.False(app.ApplicationMetadata.VersionEnabled);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--version'", result.Stderr);
        Assert.NotEqual(0, full.ExitCode);
        Assert.Contains("Unknown option: '--version-full'", full.Stderr);
    }

    [Fact]
    public async Task VersionOption_ExitsSuccessfullyAndDoesNotRunCommandHandler()
    {
        var runs = 0;
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetVersion("1.2.3")
            .SetDefaultCommand(() => new CountingCommand(() => runs++))
            .Build();

        var result = await RunCapturedAsync(app, ["--version"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, runs);
        Assert.Contains("tool version 1.2.3", result.Stdout);
    }

    [Fact]
    public async Task VersionOption_WinsWhenHelpIsAlsoRequested()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetVersion("1.2.3")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help", "--version"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tool version 1.2.3", result.Stdout);
        Assert.DoesNotContain("Usage:", result.Stdout);
    }

    [Fact]
    public async Task VersionFullOption_ExitsSuccessfullyAndDoesNotRunCommandHandler()
    {
        var runs = 0;
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetVersion("1.2.3", productVersion: "1.2.3+build")
            .SetDefaultCommand(() => new CountingCommand(() => runs++))
            .Build();

        var result = await RunCapturedAsync(app, ["--version-full"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, runs);
        Assert.Contains("tool product version 1.2.3+build", result.Stdout);
    }

    [Fact]
    public void ExplicitBuilderCalls_OverrideAssemblyMetadata()
    {
        var assembly = CreateMetadataAssembly(
            product: "Assembly Product",
            informationalVersion: "9.9.9",
            metadata: [("Documentation", "https://example.com/assembly-docs")]);

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly)
            .SetDisplayName("Explicit Product")
            .SetVersion("1.2.3")
            .AddDocumentation("https://example.com/explicit-docs")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal("Explicit Product", app.ApplicationMetadata.DisplayName);
        Assert.Equal("1.2.3", app.ApplicationMetadata.Version);
        Assert.Contains(app.ApplicationMetadata.Links, link =>
            link.Url == "https://example.com/explicit-docs");
        Assert.DoesNotContain(app.ApplicationMetadata.Links, link =>
            link.Url == "https://example.com/assembly-docs");
    }

    [Fact]
    public void AssemblyMetadata_DoesNotOverrideExplicitBuilderCalls()
    {
        var assembly = CreateMetadataAssembly(
            product: "Assembly Product",
            informationalVersion: "9.9.9",
            copyright: "Assembly copyright");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDisplayName("Explicit Product")
            .AddDescription("Explicit description.")
            .SetVersion("1.2.3")
            .AddCopyright("Explicit copyright")
            .UseAssemblyMetadata(assembly)
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal("Explicit Product", app.ApplicationMetadata.DisplayName);
        Assert.Equal("1.2.3", app.ApplicationMetadata.Version);
        Assert.Equal("1.2.3", app.ApplicationMetadata.ProductVersion);
        Assert.Equal("Explicit copyright", app.ApplicationMetadata.Copyright);
        Assert.True(app.ApplicationMetadata.VersionEnabled);
    }

    [Fact]
    public void AddCopyright_OverridesAssemblyDerivedCopyright()
    {
        var assembly = CreateMetadataAssembly(copyright: "Assembly copyright");

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .AddCopyright("Explicit copyright")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Equal("Explicit copyright", app.ApplicationMetadata.Copyright);
    }

    [Fact]
    public void StandardLinkReplacement_AvoidsDuplicates()
    {
        var assembly = CreateMetadataAssembly(metadata: [("Documentation", "https://example.com/assembly-docs")]);

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAssemblyMetadata(assembly, enableVersion: false)
            .AddDocumentation("https://example.com/explicit-docs")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        Assert.Single(app.ApplicationMetadata.Links);
        Assert.Contains(app.ApplicationMetadata.Links, link =>
            link.Url == "https://example.com/explicit-docs");
        Assert.DoesNotContain(app.ApplicationMetadata.Links, link =>
            link.Url == "https://example.com/assembly-docs");
    }

    [Fact]
    public async Task Copyright_AppearsInHelpFooter()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .AddCopyright("Copyright (c) IT Tiger")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Copyright (c) IT Tiger", result.Stdout);
    }

    [Fact]
    public async Task AddLink_AppearsInHelpFooterAsVisibleLinkText()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .AddLink("Docs", "https://example.com/docs")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Docs  https://example.com/docs", result.Stdout);
    }

    [Fact]
    public async Task ConvenienceLinks_UseExpectedEnglishLabels()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .AddWebsite("https://example.com")
            .AddRepository("https://github.com/example/tool")
            .AddDocumentation("https://example.com/docs")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Website", result.Stdout);
        Assert.Contains("Source code", result.Stdout);
        Assert.Contains("Documentation", result.Stdout);
        Assert.Contains("https://github.com/example/tool", result.Stdout);
    }

    [Fact]
    public async Task HelpWithoutMetadata_DoesNotAddMetadataFooter()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("Copyright", result.Stdout);
        Assert.DoesNotContain("Website", result.Stdout);
        Assert.DoesNotContain("--version", result.Stdout);
    }

    [Fact]
    public void InvalidMetadataArguments_AreRejected()
    {
        var builder = TigerCliApp.CreateBuilder();

        Assert.Throws<ArgumentException>(() => builder.SetDisplayName(""));
        Assert.Throws<ArgumentException>(() => builder.SetVersion(""));
        Assert.Throws<ArgumentException>(() => builder.SetVersion("1.2.3", productVersion: ""));
        Assert.Throws<ArgumentException>(() => builder.AddCopyright(" "));
        Assert.Throws<ArgumentException>(() => builder.AddLink("", "https://example.com"));
        Assert.Throws<ArgumentException>(() => builder.AddLink("Docs", ""));
        Assert.Throws<ArgumentException>(() => builder.AddWebsite(""));
        Assert.Throws<ArgumentException>(() => builder.AddRepository(""));
        Assert.Throws<ArgumentException>(() => builder.AddDocumentation(""));
    }

    [Fact]
    public async Task VersionAndBuiltinHelpLabels_AreLocalized()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("narzedzie")
            .SetSupportedCultures("en-US", "pl-PL")
            .SetVersion("1.2.3")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var version = await RunCapturedAsync(app, ["--culture", "pl-PL", "--version"]);
        var productVersion = await RunCapturedAsync(app, ["--culture", "pl-PL", "--version-full"]);
        var help = await RunCapturedAsync(app, ["--culture", "pl-PL", "--help"]);

        Assert.Contains("narzedzie wersja 1.2.3", version.Stdout);
        Assert.Contains("narzedzie wersja produktu 1.2.3", productVersion.Stdout);
        Assert.Contains("Wyświetl wersję", help.Stdout);
        Assert.Contains("Wyświetl wersję produktu", help.Stdout);
    }

    [Fact]
    public async Task DeveloperProvidedLinkLabels_ArePreserved()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCulture("pl-PL")
            .AddLink("Custom Docs", "https://example.com/docs")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Custom Docs  https://example.com/docs", result.Stdout);
        Assert.DoesNotContain("Dokumentacja", result.Stdout);
    }

    [Fact]
    public async Task ConvenienceLinkLabels_AreLocalized()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetSupportedCultures("en-US", "pl-PL")
            .AddWebsite("https://example.com")
            .AddRepository("https://github.com/example/tool")
            .AddDocumentation("https://example.com/docs")
            .SetDefaultCommand<EmptyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL", "--help"]);

        Assert.Contains("Strona WWW", result.Stdout);
        Assert.Contains("Kod źródłowy", result.Stdout);
        Assert.Contains("Dokumentacja", result.Stdout);
        Assert.DoesNotContain("Source code", result.Stdout);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app, string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static Assembly CreateMetadataAssembly(
        string? assemblyName = null,
        string? product = null,
        string? title = null,
        string? description = null,
        string? informationalVersion = null,
        Version? assemblyVersion = null,
        string? copyright = null,
        params (string Key, string Value)[] metadata)
    {
        var name = new AssemblyName(assemblyName ?? $"TigerCliMetadataTests_{Guid.NewGuid():N}");
        if (assemblyVersion != null)
            name.Version = assemblyVersion;

        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        SetStringAttribute<AssemblyProductAttribute>(assembly, product);
        SetStringAttribute<AssemblyTitleAttribute>(assembly, title);
        SetStringAttribute<AssemblyDescriptionAttribute>(assembly, description);
        SetStringAttribute<AssemblyInformationalVersionAttribute>(assembly, informationalVersion);
        SetStringAttribute<AssemblyCopyrightAttribute>(assembly, copyright);

        foreach (var (key, value) in metadata)
            SetAssemblyMetadataAttribute(assembly, key, value);

        return assembly;
    }

    private static void SetStringAttribute<TAttribute>(AssemblyBuilder assembly, string? value)
        where TAttribute : Attribute
    {
        if (value == null)
            return;

        var constructor = typeof(TAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException($"{typeof(TAttribute).Name} string constructor was not found.");
        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [value]));
    }

    private static void SetAssemblyMetadataAttribute(AssemblyBuilder assembly, string key, string value)
    {
        var constructor = typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])
            ?? throw new InvalidOperationException("AssemblyMetadataAttribute constructor was not found.");
        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [key, value]));
    }

    private static string ResolveExpectedAssemblyVersion(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataStart = informationalVersion.IndexOf('+', StringComparison.Ordinal);
            return metadataStart > 0 ? informationalVersion[..metadataStart] : informationalVersion;
        }

        var assemblyVersion = assembly.GetName().Version?.ToString();
        return string.IsNullOrWhiteSpace(assemblyVersion) ? "unknown" : assemblyVersion;
    }

    private sealed class UnknownVersionAssembly : Assembly
    {
        public override AssemblyName GetName(bool copiedName) => new("UnknownVersionAssembly");

        public override object[] GetCustomAttributes(bool inherit) => Array.Empty<Attribute>();

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) =>
            (object[])Array.CreateInstance(attributeType, 0);

        public override bool IsDefined(Type attributeType, bool inherit) => false;
    }
}
