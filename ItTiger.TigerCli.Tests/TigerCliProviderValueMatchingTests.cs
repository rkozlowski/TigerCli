using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliProviderValueMatchingTests
{
    // ---- Settings -------------------------------------------------------------------------

    private sealed class DefaultMatchSettings : TigerCliSettings
    {
        [TigerCliOption("--connection", Provider = "connections", Description = "Connection")]
        public string Connection { get; set; } = "";
    }

    private sealed class ExactMatchSettings : TigerCliSettings
    {
        [TigerCliOption(
            "--connection",
            Provider = "connections",
            ValueMatching = TigerCliValueMatchPreset.Exact,
            Description = "Connection")]
        public string Connection { get; set; } = "";
    }

    private sealed class MediaRootSettings : TigerCliSettings
    {
        [TigerCliOption(
            "--media-root",
            Provider = "media-roots",
            ValueMatching = TigerCliValueMatchPreset.FileSystemPath,
            Description = "Media root")]
        public string MediaRoot { get; set; } = "";
    }

    private sealed class MediaRootMultiSettings : TigerCliSettings
    {
        [TigerCliOption(
            "--roots",
            Provider = "media-roots",
            ValueMatching = TigerCliValueMatchPreset.FileSystemPath,
            Description = "Media roots")]
        [TigerCliMultiSelect]
        public List<string> Roots { get; set; } = new();
    }

    private sealed class MultiSelectDefaultSettings : TigerCliSettings
    {
        [TigerCliOption("--roots", Provider = "connections", Description = "Roots")]
        [TigerCliMultiSelect]
        public List<string> Roots { get; set; } = new();
    }

    // Positional argument with an explicit provider: supplied values are validated.
    private sealed class CityArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "city", Provider = "cities", Description = "City")]
        public string City { get; set; } = "";
    }

    // Same argument opting out: the provider is suggestions-only for prompting.
    private sealed class CityArgumentOptOutSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "city", Provider = "cities", ValidateAgainstProvider = false,
            Description = "City")]
        public string City { get; set; } = "";
    }

    // No explicit Provider; the display name happens to match a registered provider key.
    // Implicit name matching drives prompting only and must not validate supplied values.
    private sealed class ImplicitProviderArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "cities", Description = "City")]
        public string City { get; set; } = "";
    }

    // ---- Commands -------------------------------------------------------------------------

    private sealed class ConnectionEcho : TigerCliAsyncCommandHandler<DefaultMatchSettings>
    {
        public override Task<int> ExecuteAsync(DefaultMatchSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"value={settings.Connection}"));
            return Task.FromResult(0);
        }
    }

    private sealed class ExactEcho : TigerCliAsyncCommandHandler<ExactMatchSettings>
    {
        public override Task<int> ExecuteAsync(ExactMatchSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"value={settings.Connection}"));
            return Task.FromResult(0);
        }
    }

    private sealed class MediaRootEcho : TigerCliAsyncCommandHandler<MediaRootSettings>
    {
        public override Task<int> ExecuteAsync(MediaRootSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"value={settings.MediaRoot}"));
            return Task.FromResult(0);
        }
    }

    private sealed class MediaRootMultiEcho : TigerCliAsyncCommandHandler<MediaRootMultiSettings>
    {
        public override Task<int> ExecuteAsync(MediaRootMultiSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"value={string.Join("|", settings.Roots)}"));
            return Task.FromResult(0);
        }
    }

    private sealed class MultiSelectDefaultEcho : TigerCliAsyncCommandHandler<MultiSelectDefaultSettings>
    {
        public override Task<int> ExecuteAsync(MultiSelectDefaultSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"value={string.Join("|", settings.Roots)}"));
            return Task.FromResult(0);
        }
    }

    private sealed class CityArgumentEcho : TigerCliAsyncCommandHandler<CityArgumentSettings>
    {
        public override Task<int> ExecuteAsync(CityArgumentSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"value={settings.City}"));
            return Task.FromResult(0);
        }
    }

    private sealed class CityArgumentOptOutEcho : TigerCliAsyncCommandHandler<CityArgumentOptOutSettings>
    {
        public override Task<int> ExecuteAsync(CityArgumentOptOutSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"value={settings.City}"));
            return Task.FromResult(0);
        }
    }

    private sealed class ImplicitProviderArgumentEcho : TigerCliAsyncCommandHandler<ImplicitProviderArgumentSettings>
    {
        public override Task<int> ExecuteAsync(ImplicitProviderArgumentSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"value={settings.City}"));
            return Task.FromResult(0);
        }
    }

    // ---- Default preset: case-insensitive string matching ---------------------------------

    [Fact]
    public async Task Default_MatchesKeyCaseInsensitively_AndBindsProviderKey()
    {
        var app = BuildConnectionApp<ConnectionEcho>(
            new OptionItem<string>("Local", "Local connection"));

        var result = await RunAsync(app, ["--connection", "local", "--non-interactive"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=Local", result.Stdout); // bound value is the provider key, not "local"
    }

    [Fact]
    public async Task Default_MatchesLabelCaseInsensitively_AndBindsProviderKey()
    {
        var app = BuildConnectionApp<ConnectionEcho>(
            new OptionItem<string>("conn-1", "Local Connection"));

        var result = await RunAsync(app, ["--connection", "local connection", "--non-interactive"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=conn-1", result.Stdout); // matched by label, bound to key
    }

    [Fact]
    public async Task Default_UnknownValue_Fails()
    {
        var app = BuildConnectionApp<ConnectionEcho>(
            new OptionItem<string>("Local", "Local connection"));

        var result = await RunAsync(app, ["--connection", "nope", "--non-interactive"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("is not an available choice", result.Stderr);
    }

    // ---- Exact preset ---------------------------------------------------------------------

    [Fact]
    public async Task Exact_IsCaseSensitive()
    {
        var app = BuildConnectionApp<ExactEcho>(
            new OptionItem<string>("Local", "Local connection"));

        var wrongCase = await RunAsync(app, ["--connection", "local", "--non-interactive"]);
        Assert.NotEqual(0, wrongCase.ExitCode);
        Assert.Contains("is not an available choice", wrongCase.Stderr);

        var exact = await RunAsync(
            BuildConnectionApp<ExactEcho>(new OptionItem<string>("Local", "Local connection")),
            ["--connection", "Local", "--non-interactive"]);
        Assert.Equal(0, exact.ExitCode);
        Assert.Contains("value=Local", exact.Stdout);
    }

    [Fact]
    public async Task Exact_DoesNotPathNormalize()
    {
        var app = BuildMediaRootApp<ExactEcho>(
            provider: "connections",
            new OptionItem<string>("K:\\", "Card"));

        // Exact: "K:" must not be widened to "K:\".
        var result = await RunAsync(app, ["--connection", "K:", "--non-interactive"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("is not an available choice", result.Stderr);
    }

    // ---- FileSystemPath preset (app-level, Windows) ---------------------------------------

    [Theory]
    [InlineData("K:\\")]
    [InlineData("k:\\")]
    [InlineData("K:")]
    [InlineData("k:")]
    public async Task FileSystemPath_DriveRoot_Matches_AndBindsProviderKey(string supplied)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows filesystem path semantics.");

        var app = BuildMediaRootApp<MediaRootEcho>(
            provider: "media-roots",
            new OptionItem<string>("K:\\", "K:\\ (Card)"));

        var result = await RunAsync(app, ["--media-root", supplied, "--non-interactive"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=K:\\", result.Stdout); // bound value is the provider key
    }

    [Theory]
    [InlineData("K:\\Xxx")]
    [InlineData("K:\\Xxx\\")]
    [InlineData("k:\\xxx")]
    [InlineData("k:/xxx")]
    [InlineData("K:/Xxx/")]
    public async Task FileSystemPath_AbsolutePath_Matches_AndBindsProviderKey(string supplied)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows filesystem path semantics.");

        var app = BuildMediaRootApp<MediaRootEcho>(
            provider: "media-roots",
            new OptionItem<string>("K:\\Xxx\\", "K:\\Xxx"));

        var result = await RunAsync(app, ["--media-root", supplied, "--non-interactive"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=K:\\Xxx\\", result.Stdout);
    }

    [Fact]
    public async Task FileSystemPath_RootedWithoutDrive_DoesNotMatchDriveRoot()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows filesystem path semantics.");

        var app = BuildMediaRootApp<MediaRootEcho>(
            provider: "media-roots",
            new OptionItem<string>("C:\\", "C drive"));

        var result = await RunAsync(app, ["--media-root", "\\", "--non-interactive"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("is not an available choice", result.Stderr);
    }

    [Fact]
    public async Task FileSystemPath_DriveRelative_DoesNotMatchAbsolute()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows filesystem path semantics.");

        var app = BuildMediaRootApp<MediaRootEcho>(
            provider: "media-roots",
            new OptionItem<string>("K:\\xxx", "Card"));

        var result = await RunAsync(app, ["--media-root", "K:xxx", "--non-interactive"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("is not an available choice", result.Stderr);
    }

    // ---- FileSystemPath preset (matcher unit tests, platform-parameterized) ----------------

    [Theory]
    [InlineData("K:\\", "K:\\")]
    [InlineData("K:\\", "k:\\")]
    [InlineData("K:\\", "K:")]
    [InlineData("K:\\", "k:")]
    [InlineData("K:\\Xxx\\", "K:\\Xxx")]
    [InlineData("K:\\Xxx\\", "K:\\Xxx\\")]
    [InlineData("K:\\Xxx\\", "k:\\xxx")]
    [InlineData("K:\\Xxx\\", "k:/xxx")]
    [InlineData("K:\\Xxx\\", "K:/Xxx/")]
    public void Matcher_Windows_PathsMatch(string providerKey, string supplied)
    {
        Assert.True(TigerCliProviderValueMatcher.FileSystemPathsMatch(providerKey, supplied, windows: true));
    }

    [Theory]
    [InlineData("C:\\", "\\")]
    [InlineData("K:\\xxx", "K:xxx")]
    [InlineData("K:\\", "C:\\")]
    [InlineData("K:\\Xxx", "K:\\Yyy")]
    public void Matcher_Windows_PathsDoNotMatch(string providerKey, string supplied)
    {
        Assert.False(TigerCliProviderValueMatcher.FileSystemPathsMatch(providerKey, supplied, windows: true));
    }

    [Theory]
    [InlineData("/mnt/data", "/mnt/data/")]
    [InlineData("/mnt/data/", "/mnt/data")]
    public void Matcher_Unix_TrailingSeparatorIsInsignificant(string providerKey, string supplied)
    {
        Assert.True(TigerCliProviderValueMatcher.FileSystemPathsMatch(providerKey, supplied, windows: false));
    }

    [Fact]
    public void Matcher_Unix_IsCaseSensitive()
    {
        Assert.False(TigerCliProviderValueMatcher.FileSystemPathsMatch("/mnt/Data", "/mnt/data", windows: false));
    }

    // ---- Multi-select ---------------------------------------------------------------------

    [Fact]
    public async Task MultiSelect_Default_MatchesCaseInsensitively_AndBindsProviderKeys()
    {
        var app = BuildMediaRootApp<MultiSelectDefaultEcho>(
            provider: "connections",
            new OptionItem<string>("Local", "Local"),
            new OptionItem<string>("Remote", "Remote"));

        var result = await RunAsync(app, ["--roots", "local,REMOTE", "--non-interactive"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=Local|Remote", result.Stdout);
    }

    [Fact]
    public async Task MultiSelect_FileSystemPath_Matches_AndCollapsesDuplicates()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows filesystem path semantics.");

        var app = BuildMediaRootApp<MediaRootMultiEcho>(
            provider: "media-roots",
            new OptionItem<string>("K:\\", "K:\\ (Card)"));

        // Two distinct spellings of the same drive root collapse to one canonical key.
        var result = await RunAsync(app, ["--roots", "K:,k:\\", "--non-interactive"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=K:\\", result.Stdout);
        Assert.DoesNotContain("value=K:\\|", result.Stdout); // exactly one bound value
    }

    // ---- Positional arguments (explicit provider) ------------------------------------------

    [Fact]
    public async Task Argument_ExplicitProvider_UnknownSuppliedValue_Fails()
    {
        var app = BuildCityApp<CityArgumentEcho>(
            new OptionItem<string>("Galway", "Galway (Connacht)"));

        var result = await RunAsync(app, ["Atlantis"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("is not an available choice", result.Stderr);
        Assert.DoesNotContain("value=", result.Stdout); // handler did not run
    }

    [Fact]
    public async Task Argument_ExplicitProvider_MatchesKeyCaseInsensitively_AndBindsProviderKey()
    {
        var app = BuildCityApp<CityArgumentEcho>(
            new OptionItem<string>("Galway", "Galway (Connacht)"));

        var result = await RunAsync(app, ["galway"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=Galway", result.Stdout); // bound value is the provider key
    }

    [Fact]
    public async Task Argument_ExplicitProvider_MatchesLabel_AndBindsProviderKey()
    {
        var app = BuildCityApp<CityArgumentEcho>(
            new OptionItem<string>("Galway", "Galway (Connacht)"));

        var result = await RunAsync(app, ["galway (connacht)"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=Galway", result.Stdout); // matched by label, bound to key
    }

    [Fact]
    public async Task Argument_ValidateAgainstProviderFalse_AcceptsCustomValue()
    {
        var app = BuildCityApp<CityArgumentOptOutEcho>(
            new OptionItem<string>("Galway", "Galway (Connacht)"));

        var result = await RunAsync(app, ["Atlantis"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=Atlantis", result.Stdout); // suggestions-only provider
    }

    [Fact]
    public async Task Argument_ExplicitProvider_NonInteractive_UnknownValue_FailsWithoutPrompting()
    {
        var app = BuildCityApp<CityArgumentEcho>(
            new OptionItem<string>("Galway", "Galway (Connacht)"));
        var shell = new TestShell();

        var result = await RunAsync(app, ["Atlantis", "--non-interactive"], shell);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("is not an available choice", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount); // no prompt was rendered or read
    }

    [Fact]
    public async Task Argument_ImplicitNameMatchedProvider_IsNotValidated()
    {
        // The argument display name matches the registered provider key, which is enough for
        // prompting, but implicit matches must not make supplied values provider-validated.
        var app = BuildCityApp<ImplicitProviderArgumentEcho>(
            new OptionItem<string>("Galway", "Galway (Connacht)"));

        var result = await RunAsync(app, ["Atlantis", "--non-interactive"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=Atlantis", result.Stdout);
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static TigerCliApp BuildCityApp<TCommand>(params OptionItem<string>[] items)
        where TCommand : class, new() =>
        TigerCliApp.CreateBuilder()
            .SetApplicationName("match-test")
            .SetDefaultCommand<TCommand>()
            .ConfigureProviders(providers =>
                providers.Add<string>("cities", _ => items))
            .Build();

    private static TigerCliApp BuildConnectionApp<TCommand>(params OptionItem<string>[] items)
        where TCommand : class, new() =>
        BuildMediaRootApp<TCommand>("connections", items);

    private static TigerCliApp BuildMediaRootApp<TCommand>(
        string provider,
        params OptionItem<string>[] items)
        where TCommand : class, new() =>
        TigerCliApp.CreateBuilder()
            .SetApplicationName("match-test")
            .SetDefaultCommand<TCommand>()
            .ConfigureProviders(providers =>
                providers.Add<string>(provider, _ => items))
            .Build();

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        TigerCliApp app,
        string[] args,
        TestShell? shell = null)
    {
        shell ??= new TestShell();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args, shell, ct: TestContext.Current.CancellationToken);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
