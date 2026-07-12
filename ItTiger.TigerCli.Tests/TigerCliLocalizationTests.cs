using System.Globalization;
using System.Resources;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliLocalizationTests
{
    private sealed class FakeResources : ResourceManager
    {
        private readonly Dictionary<string, Dictionary<string, string>> _byCulture;

        public FakeResources(Dictionary<string, Dictionary<string, string>> byCulture)
            : base("FakeResources", typeof(FakeResources).Assembly)
        {
            _byCulture = byCulture;
        }

        public override string? GetString(string name, CultureInfo? culture)
        {
            culture ??= CultureInfo.InvariantCulture;
            if (_byCulture.TryGetValue(culture.Name, out var dict) && dict.TryGetValue(name, out var value))
                return value;
            return null;
        }

        public override string? GetString(string name) => GetString(name, CultureInfo.CurrentUICulture);
    }

    private sealed class EchoSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Description = "Application-owned description.")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class EchoCommand : TigerCliAsyncCommandHandler<EchoSettings>
    {
        public override Task<int> ExecuteAsync(EchoSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class ValidatingSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Description = "Name")]
        public string Name { get; set; } = string.Empty;

        public override TigerCliValidationResult Validate() =>
            TigerCliValidationResult.Error("Custom message.");
    }

    private sealed class ValidatingCommand : TigerCliAsyncCommandHandler<ValidatingSettings>
    {
        public override Task<int> ExecuteAsync(ValidatingSettings settings) => Task.FromResult(0);
    }

    private sealed class ExplicitArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "target", Description = "Target")]
        public string Target { get; set; } = string.Empty;
    }

    private sealed class ExplicitArgumentCommand : TigerCliAsyncCommandHandler<ExplicitArgumentSettings>
    {
        public override Task<int> ExecuteAsync(ExplicitArgumentSettings settings) => Task.FromResult(0);
    }

    private sealed class ExplicitValueNameSettings : TigerCliSettings
    {
        [TigerCliOption("--path", ValueName = "file-path", Description = "Path")]
        public string Path { get; set; } = string.Empty;
    }

    private sealed class ExplicitValueNameCommand : TigerCliAsyncCommandHandler<ExplicitValueNameSettings>
    {
        public override Task<int> ExecuteAsync(ExplicitValueNameSettings settings) => Task.FromResult(0);
    }

    private sealed class CultureEchoSettings : TigerCliSettings
    {
    }

    private sealed class CultureEchoCommand : TigerCliAsyncCommandHandler<CultureEchoSettings>
    {
        public override Task<int> ExecuteAsync(CultureEchoSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Culture.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class TextHelperSettings : TigerCliSettings
    {
        [TigerCliOption("--mode")]
        public string Mode { get; set; } = "t";

        [TigerCliOption("--text")]
        public string Text { get; set; } = "Hello";

        [TigerCliOption("--key")]
        public string Key { get; set; } = "Helper_Text";

        [TigerCliOption("--value")]
        public string Value { get; set; } = "alpha";
    }

    private sealed class TextHelperCommand : TigerCliAsyncCommandHandler<TextHelperSettings>
    {
        public override Task<int> ExecuteAsync(TextHelperSettings settings)
        {
            var output = settings.Mode switch
            {
                "f" => settings.F(settings.Text, settings.Value),
                "e" => settings.E(settings.Text, settings.Value),
                "bykey" => settings.TextByKey(settings.Key, "fallback"),
                "formatbykey" => settings.FormatTextByKey(settings.Key, "fallback {0}", settings.Value),
                "escapedbykey" => settings.EscapedFormatTextByKey(settings.Key, "fallback {0}", settings.Value),
                _ => settings.T(settings.Text)
            };

            TigerConsole.MarkupLine(output);
            return Task.FromResult(0);
        }
    }

    // ── Builder semantics ───────────────────────────────────────────

    [Fact]
    public void NoConfiguration_DefaultsToEnUsOnly()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .Build();

        Assert.Equal("en-US", app.DefaultCulture.Name);
        Assert.Single(app.SupportedCultures);
        Assert.Equal("en-US", app.SupportedCultures[0].Name);
    }

    [Fact]
    public void TigerCliSettings_DefaultCulture_IsEnUs()
    {
        var settings = new CultureEchoSettings();

        Assert.Equal("en-US", settings.Culture.Name);
    }

    [Fact]
    public void SetDefaultCulture_AddsToSupportedSet()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .SetDefaultCulture("pl-PL")
            .Build();

        Assert.Equal("pl-PL", app.DefaultCulture.Name);
        Assert.Single(app.SupportedCultures);
        Assert.Equal("pl-PL", app.SupportedCultures[0].Name);
    }

    [Fact]
    public void SetSupportedCultures_FirstBecomesDefault()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .SetSupportedCultures("pl-PL", "en-US")
            .Build();

        Assert.Equal("pl-PL", app.DefaultCulture.Name);
        Assert.Equal(2, app.SupportedCultures.Count);
        Assert.Equal("pl-PL", app.SupportedCultures[0].Name);
        Assert.Equal("en-US", app.SupportedCultures[1].Name);
    }

    [Fact]
    public void SetDefaultCulture_AndSetSupportedCultures_UnionWithDefaultFirst()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .SetDefaultCulture("pl-PL")
            .SetSupportedCultures("en-US")
            .Build();

        Assert.Equal("pl-PL", app.DefaultCulture.Name);
        Assert.Equal(2, app.SupportedCultures.Count);
        Assert.Equal("pl-PL", app.SupportedCultures[0].Name);
        Assert.Equal("en-US", app.SupportedCultures[1].Name);
    }

    [Fact]
    public void SetDefaultCulture_WhenAlreadyListedInSupported_DoesNotDuplicate()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .SetDefaultCulture("pl-PL")
            .SetSupportedCultures("pl-PL", "en-US")
            .Build();

        Assert.Equal(2, app.SupportedCultures.Count);
        Assert.Equal("pl-PL", app.SupportedCultures[0].Name);
        Assert.Equal("en-US", app.SupportedCultures[1].Name);
    }

    // ── Default en-US behavior ──────────────────────────────────────

    [Fact]
    public async Task DefaultRun_RendersEnglishHelp()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Usage:", result.Stdout);
        Assert.Contains("Options:", result.Stdout);
        Assert.Contains("Show help", result.Stdout);
    }

    [Fact]
    public async Task EnglishHelp_RendersFrameworkOwnedUsagePlaceholders()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .AddCommand<EchoCommand>("provider", "Provider smoke")
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("tool [options]", result.Stdout);
        Assert.Contains("tool <command> [options]", result.Stdout);
        Assert.Contains("--name <value>", result.Stdout);
    }

    [Fact]
    public async Task EnglishHelp_RendersDefaultCommandMarkerText()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .AddCommand<EchoCommand>("run", "Run")
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("run", result.Stdout);
        Assert.Contains("(default)", result.Stdout);
    }

    [Fact]
    public async Task DefaultRun_RendersEnglishParseError()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--name", "x", "--unknown"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--unknown'", result.Stderr);
    }

    // ── pl-PL opt-in ────────────────────────────────────────────────

    [Fact]
    public async Task PolishOnlyApp_RendersPolishHelp()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("narzedzie")
            .SetDefaultCommand<EchoCommand>()
            .SetDefaultCulture("pl-PL")
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Użycie:", result.Stdout);
        Assert.Contains("Opcje:", result.Stdout);
        Assert.Contains("Wyświetl pomoc", result.Stdout);
        Assert.DoesNotContain("Usage:", result.Stdout);
    }

    [Fact]
    public async Task PolishHelp_LocalizesFrameworkOwnedUsagePlaceholders()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("parser-test")
            .SetSupportedCultures("en-US", "pl-PL")
            .SetDefaultCommand<EchoCommand>()
            .AddCommand<EchoCommand>("provider", "Provider smoke")
            .Build();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL", "--help"]);

        Assert.Contains("parser-test [opcje]", result.Stdout);
        Assert.Contains("parser-test <polecenie> [opcje]", result.Stdout);
        Assert.Contains("--name <wartość>", result.Stdout);
        Assert.Contains("provider", result.Stdout);
        Assert.Contains("--name", result.Stdout);
        Assert.DoesNotContain("<command>", result.Stdout);
        Assert.DoesNotContain("[options]", result.Stdout);
        Assert.DoesNotContain("--name <value>", result.Stdout);
    }

    [Fact]
    public async Task PolishHelp_DoesNotLocalizeExplicitArgumentNames()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetSupportedCultures("en-US", "pl-PL")
            .AddCommand<ExplicitArgumentCommand>("run", "Run")
            .Build();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL", "run", "--help"]);

        Assert.Contains("tool run <target> [opcje]", result.Stdout);
        Assert.Contains("<target>", result.Stdout);
    }

    [Fact]
    public async Task PolishHelp_DoesNotLocalizeExplicitOptionValueNames()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetSupportedCultures("en-US", "pl-PL")
            .SetDefaultCommand<ExplicitValueNameCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL", "--help"]);

        Assert.Contains("--path <file-path>", result.Stdout);
        Assert.DoesNotContain("--path <wartość>", result.Stdout);
    }

    [Fact]
    public async Task PolishOnlyApp_RendersPolishParseError()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("narzedzie")
            .SetDefaultCommand<EchoCommand>()
            .SetDefaultCulture("pl-PL")
            .Build();

        var result = await RunCapturedAsync(app, ["--name", "x", "--unknown"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Nieznana opcja: '--unknown'", result.Stderr);
    }

    [Fact]
    public async Task PolishOnlyApp_RendersPolishMissingRequiredOptionError()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("narzedzie")
            .SetDefaultCommand<EchoCommand>()
            .SetDefaultCulture("pl-PL")
            .Build();

        var result = await RunCapturedAsync(app, ["--non-interactive"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Brak wymaganej opcji: --name", result.Stderr);
    }

    [Fact]
    public async Task PolishOnlyApp_RendersPolishValidationWrapper_AndDoesNotLocalizeUserMessage()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("narzedzie")
            .SetDefaultCommand<ValidatingCommand>()
            .SetDefaultCulture("pl-PL")
            .Build();

        var result = await RunCapturedAsync(app, ["--name", "x"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Błąd walidacji: Custom message.", result.Stderr);
    }

    // ── --culture override ──────────────────────────────────────────

    [Fact]
    public async Task BilingualApp_DefaultIsFirstSupported_AndCultureFlagOverrides()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .SetSupportedCultures("pl-PL", "en-US")
            .Build();

        var defaultRun = await RunCapturedAsync(app, ["--help"]);
        Assert.Contains("Użycie:", defaultRun.Stdout);

        var englishRun = await RunCapturedAsync(app, ["--culture", "en-US", "--help"]);
        Assert.Contains("Usage:", englishRun.Stdout);
        Assert.DoesNotContain("Użycie:", englishRun.Stdout);
    }

    [Fact]
    public async Task CommandHandlerSettings_ReceivesResolvedDefaultCulture()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCulture("pl-PL")
            .SetDefaultCommand<CultureEchoCommand>()
            .Build();

        var result = await RunCapturedAsync(app, []);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("pl-PL", result.Stdout);
    }

    [Fact]
    public async Task CommandHandlerSettings_ReceivesCultureOverride()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetSupportedCultures("en-US", "pl-PL")
            .SetDefaultCommand<CultureEchoCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("pl-PL", result.Stdout);
    }

    [Fact]
    public async Task CultureFlag_SupportsEqualsForm()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .SetSupportedCultures("en-US", "pl-PL")
            .Build();

        var result = await RunCapturedAsync(app, ["--culture=pl-PL", "--help"]);

        Assert.Contains("Użycie:", result.Stdout);
    }

    [Fact]
    public async Task UnsupportedCulture_FailsThroughInvalidArgumentsPolicy()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.InvalidArguments, 77)
            .Build();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL", "--help"]);

        Assert.Equal(77, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("Unsupported --culture value 'pl-PL'", result.Stderr);
        Assert.Contains("Supported: en-US", result.Stderr);
    }

    [Fact]
    public async Task UnsupportedCulture_ErrorRendersInDefaultCulture()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("narzedzie")
            .SetDefaultCommand<EchoCommand>()
            .SetDefaultCulture("pl-PL")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.InvalidArguments, 77)
            .Build();

        var result = await RunCapturedAsync(app, ["--culture", "fr-FR", "--help"]);

        Assert.Equal(77, result.ExitCode);
        Assert.Contains("Nieobsługiwana wartość --culture 'fr-FR'", result.Stderr);
    }

    // ── --culture stripping ─────────────────────────────────────────

    [Fact]
    public async Task CultureFlag_IsStripped_BeforeParsing()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .SetSupportedCultures("en-US", "pl-PL")
            .Build();

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL", "--name", "alpha"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("alpha", result.Stdout);
    }

    // ── Settings app text helpers ──────────────────────────────────

    [Fact]
    public async Task TextHelper_T_SourceText_ReturnsResourceValueForActiveCulture()
    {
        var resources = new FakeResources(new()
        {
            ["en-US"] = new() { ["Hello"] = "Hello" },
            ["pl-PL"] = new() { ["Hello"] = "Cześć" }
        });
        var app = CreateTextHelperApp(resources);

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Cześć", result.Stdout);
    }

    [Fact]
    public async Task TextHelper_T_SourceText_FallsBackWhenAppResourcesAreMissing()
    {
        var app = CreateTextHelperApp(resources: null);

        var result = await RunCapturedAsync(app, []);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello", result.Stdout);
    }

    [Fact]
    public async Task TextHelper_T_SourceText_FallsBackWhenKeyIsMissingOrEmpty()
    {
        var resources = new FakeResources(new()
        {
            ["en-US"] = new() { ["Empty"] = "" }
        });
        var app = CreateTextHelperApp(resources);

        var missing = await RunCapturedAsync(app, ["--text", "Missing"]);
        var empty = await RunCapturedAsync(app, ["--text", "Empty"]);

        Assert.Equal(0, missing.ExitCode);
        Assert.Contains("Missing", missing.Stdout);
        Assert.Equal(0, empty.ExitCode);
        Assert.Contains("Empty", empty.Stdout);
    }

    [Fact]
    public async Task TextHelper_F_SourceText_FormatsLocalizedResourceWithoutEscaping()
    {
        var resources = new FakeResources(new()
        {
            ["en-US"] = new() { ["fallback {0}"] = "value={0}" }
        });
        var app = CreateTextHelperApp(resources);

        var result = await RunCapturedAsync(
            app,
            ["--mode", "f", "--text", "fallback {0}", "--value", "[red]alpha[/]"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=alpha", result.Stdout);
        Assert.DoesNotContain("[red]alpha[/]", result.Stdout);
    }

    [Fact]
    public async Task TextHelper_E_SourceText_FormatsLocalizedResourceAndEscapesArguments()
    {
        var resources = new FakeResources(new()
        {
            ["en-US"] = new() { ["fallback {0}"] = "value={0}" }
        });
        var app = CreateTextHelperApp(resources);

        var result = await RunCapturedAsync(
            app,
            ["--mode", "e", "--text", "fallback {0}", "--value", "[alpha]"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=[alpha]", result.Stdout);
    }

    [Fact]
    public async Task TextHelper_F_SourceText_UsesSettingsCulture()
    {
        var resources = new FakeResources(new()
        {
            ["en-US"] = new() { ["fallback {0}"] = "value={0}" },
            ["pl-PL"] = new() { ["fallback {0}"] = "wartość={0}" }
        });
        var app = CreateTextHelperApp(resources);

        var result = await RunCapturedAsync(
            app,
            ["--culture", "pl-PL", "--mode", "f", "--text", "fallback {0}", "--value", "beta"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("wartość=beta", result.Stdout);
    }

    [Fact]
    public async Task TextHelper_E_SourceText_UsesSettingsCulture()
    {
        var resources = new FakeResources(new()
        {
            ["en-US"] = new() { ["fallback {0}"] = "value={0}" },
            ["pl-PL"] = new() { ["fallback {0}"] = "wartość={0}" }
        });
        var app = CreateTextHelperApp(resources);

        var result = await RunCapturedAsync(
            app,
            ["--culture", "pl-PL", "--mode", "e", "--text", "fallback {0}", "--value", "[beta]"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("wartość=[beta]", result.Stdout);
    }

    [Fact]
    public async Task TextHelper_TextByKey_ReturnsResourceValueForActiveCulture()
    {
        var resources = new FakeResources(new()
        {
            ["en-US"] = new() { ["Helper_Text"] = "Hello" },
            ["pl-PL"] = new() { ["Helper_Text"] = "Cześć" }
        });
        var app = CreateTextHelperApp(resources);

        var result = await RunCapturedAsync(app, ["--culture", "pl-PL", "--mode", "bykey"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Cześć", result.Stdout);
    }

    [Fact]
    public async Task TextHelper_FormatTextByKey_FormatsLocalizedResourceWithoutEscaping()
    {
        var resources = new FakeResources(new()
        {
            ["en-US"] = new() { ["Helper_Format"] = "value={0}" },
            ["pl-PL"] = new() { ["Helper_Format"] = "wartość={0}" }
        });
        var app = CreateTextHelperApp(resources);

        var result = await RunCapturedAsync(
            app,
            ["--culture", "pl-PL", "--mode", "formatbykey", "--key", "Helper_Format", "--value", "beta"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("wartość=beta", result.Stdout);
    }

    [Fact]
    public async Task TextHelper_EscapedFormatTextByKey_FormatsLocalizedResourceAndEscapesArguments()
    {
        var resources = new FakeResources(new()
        {
            ["en-US"] = new() { ["Helper_Format"] = "value={0}" }
        });
        var app = CreateTextHelperApp(resources);

        var result = await RunCapturedAsync(
            app,
            ["--mode", "escapedbykey", "--key", "Helper_Format", "--value", "[alpha]"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("value=[alpha]", result.Stdout);
    }

    // ── DisableCultureOption ────────────────────────────────────────

    [Fact]
    public async Task DisableCultureOption_RejectsCultureFlagAsUnknownOption()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .SetSupportedCultures("en-US", "pl-PL")
            .DisableCultureOption()
            .Build();

        var result = await RunCapturedAsync(app, ["--name", "x", "--culture", "pl-PL"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown option: '--culture'", result.Stderr);
    }

    [Fact]
    public async Task DisableCultureOption_HelpDoesNotListCulture()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .DisableCultureOption()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.DoesNotContain("--culture", result.Stdout);
    }

    [Fact]
    public async Task CultureOptionEnabled_ByDefault_HelpListsCulture()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("--culture", result.Stdout);
    }

    // ── Developer-provided descriptions remain unchanged ────────────

    [Fact]
    public async Task DeveloperOptionDescription_NotLocalized()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("narzedzie")
            .SetDefaultCommand<EchoCommand>()
            .SetDefaultCulture("pl-PL")
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Application-owned description.", result.Stdout);
    }

    // ── CurrentUICulture not mutated ────────────────────────────────

    [Fact]
    public async Task RunAsync_DoesNotMutateCurrentUICulture()
    {
        var original = CultureInfo.CurrentUICulture;
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EchoCommand>()
            .SetDefaultCulture("pl-PL")
            .Build();

        await RunCapturedAsync(app, ["--help"]);

        Assert.Same(original, CultureInfo.CurrentUICulture);
    }

    // ── TUI strings localized via shell.Culture ─────────────────────

    [Fact]
    public void TestShell_DefaultCulture_IsEnUs()
    {
        var shell = new TestShell();
        Assert.Equal("en-US", shell.Culture.Name);
    }

    [Fact]
    public void TestShell_AcceptsCultureFromConstructor()
    {
        var shell = new TestShell(culture: CultureInfo.GetCultureInfo("pl-PL"));
        Assert.Equal("pl-PL", shell.Culture.Name);
    }

    [Fact]
    public async Task ConfirmAsync_UsesPolishLabels_WhenShellCultureIsPolish()
    {
        var shell = new TestShell(culture: CultureInfo.GetCultureInfo("pl-PL"));
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var modal = Tui.TigerTui.ConfirmAsync(shell, "Pytanie", ct: TestContext.Current.CancellationToken);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Contains("Tak", shell.Terminal.LastRenderedText);
        Assert.Contains("Nie", shell.Terminal.LastRenderedText);

        await modal;
    }

    [Fact]
    public async Task ConfirmAsync_UsesEnglishLabels_WhenShellCultureIsEnglish()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var modal = Tui.TigerTui.ConfirmAsync(shell, "Question", ct: TestContext.Current.CancellationToken);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Contains("Yes", shell.Terminal.LastRenderedText);
        Assert.Contains("No", shell.Terminal.LastRenderedText);

        await modal;
    }

    [Fact]
    public async Task EmptySelect_RendersPolishEmptyStateText()
    {
        var shell = new TestShell(culture: CultureInfo.GetCultureInfo("pl-PL"));
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);

        var modal = Tui.TigerTui.SelectIndexAsync(shell, "Tytuł", Array.Empty<string>(),
            ct: TestContext.Current.CancellationToken);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Contains("Brak dostępnych elementów", shell.Terminal.LastRenderedText);

        await modal;
    }

    [Fact]
    public async Task MultiSelectHint_RendersPolishHint()
    {
        var shell = new TestShell(culture: CultureInfo.GetCultureInfo("pl-PL"));
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var modal = Tui.TigerTui.MultiSelectIndexesAsync(shell, "Tytuł", new[] { "a", "b" },
            ct: TestContext.Current.CancellationToken);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Contains("Spacja", shell.Terminal.LastRenderedText);
        Assert.Contains("Potwierd", shell.Terminal.LastRenderedText);

        await modal;
    }

    // ── Helpers ─────────────────────────────────────────────────────

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

    private static TigerCliApp CreateTextHelperApp(ResourceManager? resources)
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetSupportedCultures("en-US", "pl-PL")
            .SetDefaultCommand<TextHelperCommand>();

        if (resources != null)
            builder.UseAppResources(resources);

        return builder.Build();
    }
}
