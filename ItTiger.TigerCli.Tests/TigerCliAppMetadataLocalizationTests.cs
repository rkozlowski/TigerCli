using System.Globalization;
using System.Resources;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliAppMetadataLocalizationTests
{
    // ── In-memory ResourceManager for tests ─────────────────────────

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

    // ── Settings + handlers under test ──────────────────────────────

    private sealed class GreetSettings : TigerCliSettings
    {
        [TigerCliOption("-n|--name",
            Description = "Name to greet.",
            DescriptionResourceKey = "Opt_Name_Description")]
        public string Name { get; set; } = "World";
    }

    private sealed class GreetCommand : TigerCliAsyncCommandHandler<GreetSettings>
    {
        public override Task<int> ExecuteAsync(GreetSettings settings) => Task.FromResult(0);
    }

    private sealed class ProcessSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "target",
            Description = "Target to process.",
            DescriptionResourceKey = "Arg_Target_Description")]
        public string Target { get; set; } = string.Empty;
    }

    private sealed class ProcessCommand : TigerCliAsyncCommandHandler<ProcessSettings>
    {
        public override Task<int> ExecuteAsync(ProcessSettings settings) => Task.FromResult(0);
    }

    private sealed class NoKeyOptionSettings : TigerCliSettings
    {
        [TigerCliOption("--flag", Description = "Plain English description.")]
        public string Flag { get; set; } = string.Empty;
    }

    private sealed class NoKeyCommand : TigerCliAsyncCommandHandler<NoKeyOptionSettings>
    {
        public override Task<int> ExecuteAsync(NoKeyOptionSettings settings) => Task.FromResult(0);
    }

    private sealed class MarkupOptionSettings : TigerCliSettings
    {
        [TigerCliOption("--name",
            Description = "Fallback",
            DescriptionResourceKey = "Opt_Markup_Description")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class MarkupCommand : TigerCliAsyncCommandHandler<MarkupOptionSettings>
    {
        public override Task<int> ExecuteAsync(MarkupOptionSettings settings) => Task.FromResult(0);
    }

    private sealed class ProviderSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "connection")]
        public string ConnectionName { get; set; } = string.Empty;
    }

    private sealed class ProviderCommand : TigerCliAsyncCommandHandler<ProviderSettings>
    {
        public override Task<int> ExecuteAsync(ProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.ConnectionName));
            return Task.FromResult(0);
        }
    }

    // ── App description ─────────────────────────────────────────────

    [Fact]
    public async Task AppDescription_ResourceKey_ResolvesByCulture()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new() { ["App_Description"] = "Hello tool" },
            ["pl-PL"] = new() { ["App_Description"] = "Witaj narzędzie" },
        });

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetSupportedCultures("en-US", "pl-PL")
            .UseAppResources(rm)
            .AddDescription("Fallback description", resourceKey: "App_Description")
            .SetDefaultCommand<GreetCommand>()
            .Build();

        var en = await RunCapturedAsync(app, ["--help"]);
        Assert.Contains("Hello tool", en.Stdout);
        Assert.DoesNotContain("Fallback description", en.Stdout);

        var pl = await RunCapturedAsync(app, ["--culture", "pl-PL", "--help"]);
        Assert.Contains("Witaj narzędzie", pl.Stdout);
    }

    [Fact]
    public async Task AppDescription_MissingResourceKey_FallsBackToText()
    {
        var rm = new FakeResources(new() { ["en-US"] = new() });

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAppResources(rm)
            .AddDescription("Fallback description", resourceKey: "Missing_Key")
            .SetDefaultCommand<GreetCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Fallback description", result.Stdout);
        // Resource key never surfaces.
        Assert.DoesNotContain("Missing_Key", result.Stdout);
    }

    [Fact]
    public async Task AppDescription_WithoutResourceKey_RendersFallback()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .AddDescription("Plain description.")
            .SetDefaultCommand<GreetCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Plain description.", result.Stdout);
    }

    // ── Command description ─────────────────────────────────────────

    [Fact]
    public async Task CommandDescription_ResourceKey_ResolvesByCulture()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new() { ["Cmd_Greet_Description"] = "Greet someone" },
            ["pl-PL"] = new() { ["Cmd_Greet_Description"] = "Pozdrów kogoś" },
        });

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetSupportedCultures("en-US", "pl-PL")
            .UseAppResources(rm)
            .AddCommand<GreetCommand>("greet", "Fallback greet", descriptionResourceKey: "Cmd_Greet_Description")
            .Build();

        var rootEn = await RunCapturedAsync(app, ["--help"]);
        Assert.Contains("Greet someone", rootEn.Stdout);
        Assert.DoesNotContain("Fallback greet", rootEn.Stdout);

        var rootPl = await RunCapturedAsync(app, ["--culture", "pl-PL", "--help"]);
        Assert.Contains("Pozdrów kogoś", rootPl.Stdout);

        var cmdEn = await RunCapturedAsync(app, ["greet", "--help"]);
        Assert.Contains("Greet someone", cmdEn.Stdout);

        var cmdPl = await RunCapturedAsync(app, ["--culture", "pl-PL", "greet", "--help"]);
        Assert.Contains("Pozdrów kogoś", cmdPl.Stdout);
    }

    [Fact]
    public async Task CommandDescription_MissingResourceKey_FallsBackToText()
    {
        var rm = new FakeResources(new() { ["en-US"] = new() });

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAppResources(rm)
            .AddCommand<GreetCommand>("greet", "Fallback greet", descriptionResourceKey: "Cmd_Greet_Description")
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Fallback greet", result.Stdout);
        Assert.DoesNotContain("Cmd_Greet_Description", result.Stdout);
    }

    // ── Option description ──────────────────────────────────────────

    [Fact]
    public async Task OptionDescriptionResourceKey_ResolvesByCulture()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new() { ["Opt_Name_Description"] = "Name (English)" },
            ["pl-PL"] = new() { ["Opt_Name_Description"] = "Imię (po polsku)" },
        });

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetSupportedCultures("en-US", "pl-PL")
            .UseAppResources(rm)
            .SetDefaultCommand<GreetCommand>()
            .Build();

        var en = await RunCapturedAsync(app, ["--help"]);
        Assert.Contains("Name (English)", en.Stdout);
        Assert.DoesNotContain("Name to greet.", en.Stdout);

        var pl = await RunCapturedAsync(app, ["--culture", "pl-PL", "--help"]);
        Assert.Contains("Imię (po polsku)", pl.Stdout);
    }

    [Fact]
    public async Task OptionDescription_NoResourceKey_RendersFallback()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<NoKeyCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Plain English description.", result.Stdout);
    }

    [Fact]
    public async Task OptionDescription_MissingKey_FallsBackToText()
    {
        var rm = new FakeResources(new() { ["en-US"] = new() });

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAppResources(rm)
            .SetDefaultCommand<GreetCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Name to greet.", result.Stdout);
        Assert.DoesNotContain("Opt_Name_Description", result.Stdout);
    }

    // ── Argument description ────────────────────────────────────────

    [Fact]
    public async Task ArgumentDescriptionResourceKey_ResolvesByCulture()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new() { ["Arg_Target_Description"] = "Target (EN)" },
            ["pl-PL"] = new() { ["Arg_Target_Description"] = "Cel (PL)" },
        });

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetSupportedCultures("en-US", "pl-PL")
            .UseAppResources(rm)
            .SetDefaultCommand<ProcessCommand>()
            .Build();

        var en = await RunCapturedAsync(app, ["--help"]);
        Assert.Contains("Target (EN)", en.Stdout);
        Assert.DoesNotContain("Target to process.", en.Stdout);

        var pl = await RunCapturedAsync(app, ["--culture", "pl-PL", "--help"]);
        Assert.Contains("Cel (PL)", pl.Stdout);
    }

    [Fact]
    public async Task ArgumentDescription_MissingKey_FallsBackToText()
    {
        var rm = new FakeResources(new() { ["en-US"] = new() });

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAppResources(rm)
            .SetDefaultCommand<ProcessCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("Target to process.", result.Stdout);
        Assert.DoesNotContain("Arg_Target_Description", result.Stdout);
    }

    // ── Markup trust / escaping is preserved ────────────────────────

    [Fact]
    public async Task ResolvedDescriptionMarkup_IsTreatedAsTrustedMarkup()
    {
        // Resolved description renders through MarkupLine the same way the
        // fallback Description does today. The literal "[green]" should be
        // consumed by the markup parser, not show up verbatim in stripped
        // output captured by TigerConsole's ANSI-stripping console writer.
        var rm = new FakeResources(new()
        {
            ["en-US"] = new() { ["Opt_Markup_Description"] = "[green]bold[/] tail" },
        });

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAppResources(rm)
            .SetDefaultCommand<MarkupCommand>()
            .Build();

        var result = await RunCapturedAsync(app, ["--help"]);

        Assert.Contains("bold tail", result.Stdout);
        Assert.DoesNotContain("[green]", result.Stdout);
        Assert.DoesNotContain("[/]", result.Stdout);
    }

    // ── Provider context exposes culture ────────────────────────────

    [Fact]
    public async Task ProviderContext_ExposesActiveCulture()
    {
        var seenCultures = new List<string>();

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetSupportedCultures("en-US", "pl-PL")
            .SetDefaultCommand<ProviderCommand>()
            .ConfigurePrompts<ProviderSettings>(prompts =>
            {
                prompts.For(s => s.ConnectionName).SelectFrom((_, ctx) =>
                {
                    seenCultures.Add(ctx.Culture.Name);
                    return new[] { new OptionItem<string>("local", "Local") };
                });
            })
            .Build();

        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunWithShellAsync(app, ["--culture", "pl-PL"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Single(seenCultures);
        Assert.Equal("pl-PL", seenCultures[0]);
    }

    [Fact]
    public async Task ProviderLabels_AreLocalizedThroughCtxCulture()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new() { ["Conn_Local_Label"] = "Local" },
            ["pl-PL"] = new() { ["Conn_Local_Label"] = "Lokalne" },
        });

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetSupportedCultures("en-US", "pl-PL")
            .UseAppResources(rm)
            .SetDefaultCommand<ProviderCommand>()
            .ConfigurePrompts<ProviderSettings>(prompts =>
            {
                prompts.For(s => s.ConnectionName).SelectFrom((_, ctx) =>
                    new[]
                    {
                        new OptionItem<string>("local",
                            rm.GetString("Conn_Local_Label", ctx.Culture) ?? "Local")
                    });
            })
            .Build();

        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunWithShellAsync(app, ["--culture", "pl-PL"], shell);

        Assert.Equal(0, result.ExitCode);
        // Polish label rendered into the prompt, while bound value stays "local".
        Assert.Contains("Lokalne", shell.Terminal.LastRenderedText);
        Assert.Contains("local", result.Stdout);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunWithShellAsync(
        TigerCliApp app, string[] args, TestShell shell)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args, shell,
                promptTimeout: TimeSpan.FromSeconds(3),
                ct: TestContext.Current.CancellationToken);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
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
}
