using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using ItTiger.Core;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliEnumTextTests
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

    // ── Enums under test ────────────────────────────────────────────

    private enum BareExitCode
    {
        Ok = 0,
        Bad = 1,
    }

    [Description("Tool response codes")]
    private enum DescOnlyExitCode
    {
        [Description("Operation succeeded.")]
        Ok = 0,

        [Description("Invalid command-line arguments.")]
        InvalidArguments = 1002,
    }

    [TigerText("TigerText heading")]
    private enum TigerTextExitCode
    {
        [TigerText("Success label", Description = "Success description")]
        Ok = 0,

        [TigerText("Failure label", Description = "Failure description")]
        Bad = 1,
    }

    private enum SourceTextExitCode
    {
        [TigerText("Source label", Description = "Source description")]
        Ok = 0,

        [TigerText("Missing source label", Description = "Missing source description")]
        Missing = 1,
    }

    private enum ResourceKeyExitCode
    {
        [TigerText(
            "FallbackLabel",
            ResourceKey = "Ok_Label",
            Description = "Fallback description",
            DescriptionResourceKey = "Ok_Description")]
        Ok = 0,

        [TigerText("Fallback for missing key", ResourceKey = "Missing_Key")]
        Missing = 1,
    }

    private sealed class TestRes { } // marker type for DisplayAttribute.ResourceType

    private enum DisplayExitCode
    {
        [Display(Name = "Display label", Description = "Display description")]
        Ok = 0,

        [Display(Name = "Other label")]
        Other = 1,
    }

    private enum SelectColors
    {
        [TigerText("Czerwony")]
        Red,

        [TigerText("Zielony")]
        Green,

        [TigerText("Niebieski")]
        Blue,
    }

    [Flags]
    private enum SelectFlags
    {
        None = 0,
        [TigerText("Czytaj")] Read = 1,
        [TigerText("Pisz")] Write = 2,
        [TigerText("Uruchom")] Execute = 4,
    }

    // ── Settings + commands ─────────────────────────────────────────

    private sealed class SelectColorSettings : TigerCliSettings
    {
        [TigerCliOption("--color", Required = true, Description = "Color")]
        public SelectColors Color { get; set; }
    }

    private sealed class SelectColorCommand : TigerCliAsyncCommandHandler<SelectColorSettings>
    {
        public override Task<int> ExecuteAsync(SelectColorSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Color.ToString()));
            return Task.FromResult(0);
        }
    }

    private sealed class SelectFlagsSettings : TigerCliSettings
    {
        [TigerCliOption("--features", Required = true, Description = "Features")]
        public SelectFlags Features { get; set; }
    }

    private sealed class SelectFlagsCommand : TigerCliAsyncCommandHandler<SelectFlagsSettings>
    {
        public override Task<int> ExecuteAsync(SelectFlagsSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Features.ToString()));
            return Task.FromResult(0);
        }
    }

    private sealed class NoopSettings : TigerCliSettings { }

    private sealed class NoopCommand : TigerCliAsyncCommandHandler<NoopSettings>
    {
        public override Task<int> ExecuteAsync(NoopSettings settings) => Task.FromResult(0);
    }

    // ── Resolver: enum member labels ────────────────────────────────

    private static string ResolveMemberLabel<TEnum>(TEnum value, ResourceManager? rm = null, string cultureName = "en-US")
        where TEnum : struct, Enum
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var field = typeof(TEnum).GetField(value.ToString(),
            BindingFlags.Public | BindingFlags.Static)!;
        var resolver = typeof(TigerCliApp).Assembly
            .GetType("ItTiger.TigerCli.Resources.TigerCliEnumText")!;
        var method = resolver.GetMethod("Resolve",
            BindingFlags.Public | BindingFlags.Static,
            new[] { typeof(FieldInfo), typeof(CultureInfo), typeof(ResourceManager) })!;
        var result = method.Invoke(null, new object?[] { field, culture, rm })!;
        return (string)result.GetType().GetProperty("Label")!.GetValue(result)!;
    }

    private static string? ResolveMemberDescription<TEnum>(TEnum value, ResourceManager? rm = null, string cultureName = "en-US")
        where TEnum : struct, Enum
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var field = typeof(TEnum).GetField(value.ToString(),
            BindingFlags.Public | BindingFlags.Static)!;
        var resolver = typeof(TigerCliApp).Assembly
            .GetType("ItTiger.TigerCli.Resources.TigerCliEnumText")!;
        var method = resolver.GetMethod("Resolve",
            BindingFlags.Public | BindingFlags.Static,
            new[] { typeof(FieldInfo), typeof(CultureInfo), typeof(ResourceManager) })!;
        var result = method.Invoke(null, new object?[] { field, culture, rm })!;
        return (string?)result.GetType().GetProperty("Description")!.GetValue(result);
    }

    [Fact]
    public void Bare_Member_FallsBackToName()
    {
        Assert.Equal("Ok", ResolveMemberLabel(BareExitCode.Ok));
        Assert.Null(ResolveMemberDescription(BareExitCode.Ok));
    }

    [Fact]
    public void DescriptionAttribute_OnlyAffectsDescription()
    {
        // Label stays the enum name; description comes from [Description].
        Assert.Equal("Ok", ResolveMemberLabel(DescOnlyExitCode.Ok));
        Assert.Equal("Operation succeeded.", ResolveMemberDescription(DescOnlyExitCode.Ok));
    }

    [Fact]
    public void TigerText_FallbackText_UsedAsLabelAndDescription()
    {
        Assert.Equal("Success label", ResolveMemberLabel(TigerTextExitCode.Ok));
        Assert.Equal("Success description", ResolveMemberDescription(TigerTextExitCode.Ok));
    }

    [Fact]
    public void TigerText_Text_IsUsedAsResourceKey_WhenResourceKeyIsOmitted()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new() { ["Source label"] = "Source label (en)" },
            ["pl-PL"] = new() { ["Source label"] = "Etykieta źródłowa" },
        });

        Assert.Equal("Source label (en)", ResolveMemberLabel(SourceTextExitCode.Ok, rm, "en-US"));
        Assert.Equal("Etykieta źródłowa", ResolveMemberLabel(SourceTextExitCode.Ok, rm, "pl-PL"));
    }

    [Fact]
    public void TigerText_Description_IsUsedAsResourceKey_WhenDescriptionResourceKeyIsOmitted()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new() { ["Source description"] = "Source description (en)" },
            ["pl-PL"] = new() { ["Source description"] = "Opis źródłowy" },
        });

        Assert.Equal("Source description (en)", ResolveMemberDescription(SourceTextExitCode.Ok, rm, "en-US"));
        Assert.Equal("Opis źródłowy", ResolveMemberDescription(SourceTextExitCode.Ok, rm, "pl-PL"));
    }

    [Fact]
    public void TigerText_ResourceKey_ResolvedViaAppResources()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new() { ["Ok_Label"] = "OK (en)", ["Ok_Description"] = "Operation OK (en)" },
            ["pl-PL"] = new() { ["Ok_Label"] = "OK (pl)", ["Ok_Description"] = "Operacja OK (pl)" },
        });

        Assert.Equal("OK (en)", ResolveMemberLabel(ResourceKeyExitCode.Ok, rm, "en-US"));
        Assert.Equal("OK (pl)", ResolveMemberLabel(ResourceKeyExitCode.Ok, rm, "pl-PL"));
        Assert.Equal("Operation OK (en)", ResolveMemberDescription(ResourceKeyExitCode.Ok, rm, "en-US"));
        Assert.Equal("Operacja OK (pl)", ResolveMemberDescription(ResourceKeyExitCode.Ok, rm, "pl-PL"));
    }

    [Fact]
    public void TigerText_ExplicitResourceKey_WinsOverSourceTextKey()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new()
            {
                ["Ok_Label"] = "Explicit label",
                ["FallbackLabel"] = "Source-text label",
            },
        });

        Assert.Equal("Explicit label", ResolveMemberLabel(ResourceKeyExitCode.Ok, rm));
    }

    [Fact]
    public void TigerText_ExplicitDescriptionResourceKey_WinsOverSourceTextKey()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new()
            {
                ["Ok_Description"] = "Explicit description",
                ["Fallback description"] = "Source-text description",
            },
        });

        Assert.Equal("Explicit description", ResolveMemberDescription(ResourceKeyExitCode.Ok, rm));
    }

    [Fact]
    public void TigerText_MissingResourceKey_FallsBackToText()
    {
        var rm = new FakeResources(new() { ["en-US"] = new() }); // no keys

        // ResourceKey is set but not found → fall back to Text.
        Assert.Equal("Fallback for missing key", ResolveMemberLabel(ResourceKeyExitCode.Missing, rm));
    }

    [Fact]
    public void TigerText_MissingSourceTextResource_FallsBackToTextAndDescription()
    {
        var rm = new FakeResources(new() { ["en-US"] = new() }); // no source-text keys

        Assert.Equal("Missing source label", ResolveMemberLabel(SourceTextExitCode.Missing, rm));
        Assert.Equal("Missing source description", ResolveMemberDescription(SourceTextExitCode.Missing, rm));
    }

    [Fact]
    public void NoAppResources_FallsBackToText()
    {
        // ResourceKey is set but no ResourceManager is registered → fall back to Text.
        Assert.Equal("FallbackLabel", ResolveMemberLabel(ResourceKeyExitCode.Ok, rm: null));
    }

    [Fact]
    public void DescriptionAttribute_RemainsLiteralEvenWhenMatchingAppResourceExists()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new()
            {
                ["Operation succeeded."] = "Localized operation succeeded.",
            },
        });

        Assert.Equal("Operation succeeded.", ResolveMemberDescription(DescOnlyExitCode.Ok, rm));
    }

    [Fact]
    public void DisplayAttribute_NameAndDescription_Used()
    {
        Assert.Equal("Display label", ResolveMemberLabel(DisplayExitCode.Ok));
        Assert.Equal("Display description", ResolveMemberDescription(DisplayExitCode.Ok));

        Assert.Equal("Other label", ResolveMemberLabel(DisplayExitCode.Other));
        Assert.Null(ResolveMemberDescription(DisplayExitCode.Other));
    }

    // ── --help-errors integration ───────────────────────────────────

    [Fact]
    public async Task HelpErrors_PreservesExistingDescriptionAttributeBehavior()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseExitCodes<DescOnlyExitCode>(DescOnlyExitCode.Ok, DescOnlyExitCode.Ok)
            .Build();

        var result = await RunCapturedAsync(app, ["--help-errors"]);

        // Type heading from [Description("Tool response codes")] still wins.
        Assert.Contains("Tool response codes", result.Stdout);
        // Enum member NAME is still the label (DescriptionAttribute is description-only).
        Assert.Contains("InvalidArguments", result.Stdout);
        // DescriptionAttribute value renders as the description line.
        Assert.Contains("Invalid command-line arguments.", result.Stdout);
    }

    [Fact]
    public async Task HelpErrors_UsesTigerTextLabelsAndDescriptions()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseExitCodes<TigerTextExitCode>(TigerTextExitCode.Ok, TigerTextExitCode.Ok)
            .Build();

        var result = await RunCapturedAsync(app, ["--help-errors"]);

        Assert.Contains("TigerText heading", result.Stdout);
        Assert.Contains("Success label", result.Stdout);
        Assert.Contains("Success description", result.Stdout);
        Assert.Contains("Failure label", result.Stdout);
        Assert.Contains("Failure description", result.Stdout);
        // Plain enum names should NOT appear (label takes over).
        Assert.DoesNotContain("  Ok\r", result.Stdout);
        Assert.DoesNotContain("  Ok\n", result.Stdout);
    }

    [Fact]
    public async Task HelpErrors_UsesAppResourcesForTigerTextResourceKey()
    {
        var rm = new FakeResources(new()
        {
            ["en-US"] = new() { ["Ok_Label"] = "Resolved-EN", ["Ok_Description"] = "Resolved-Desc-EN" },
        });

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseAppResources(rm)
            .UseExitCodes<ResourceKeyExitCode>(ResourceKeyExitCode.Ok, ResourceKeyExitCode.Ok)
            .Build();

        var result = await RunCapturedAsync(app, ["--help-errors"]);

        Assert.Contains("Resolved-EN", result.Stdout);
        Assert.Contains("Resolved-Desc-EN", result.Stdout);
        // Missing key entry falls back to its Text.
        Assert.Contains("Fallback for missing key", result.Stdout);
    }

    [Fact]
    public async Task HelpErrors_UsesDisplayAttributeFallback()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseExitCodes<DisplayExitCode>(DisplayExitCode.Ok, DisplayExitCode.Ok)
            .Build();

        var result = await RunCapturedAsync(app, ["--help-errors"]);

        Assert.Contains("Display label", result.Stdout);
        Assert.Contains("Display description", result.Stdout);
    }

    [Fact]
    public async Task HelpErrors_BareEnumStillRendersNames()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseExitCodes<BareExitCode>(BareExitCode.Ok, BareExitCode.Ok)
            .Build();

        var result = await RunCapturedAsync(app, ["--help-errors"]);

        Assert.Contains("BareExitCode", result.Stdout);
        Assert.Contains("Ok", result.Stdout);
        Assert.Contains("Bad", result.Stdout);
    }

    // ── Enum prompts ────────────────────────────────────────────────

    [Fact]
    public async Task SelectEnumPrompt_RendersTigerTextLabels_AndBindsEnumValue()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<SelectColorCommand>()
            .Build();

        var ct = TestContext.Current.CancellationToken;

        var stdoutWriter = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var stderrWriter = new StringWriter();
        Console.SetOut(stdoutWriter);
        Console.SetError(stderrWriter);
        int exitCode;
        try
        {
            exitCode = await app.RunAsync(Array.Empty<string>(), shell, promptTimeout: TimeSpan.FromSeconds(3), ct: ct);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        Assert.Equal(0, exitCode);
        Assert.Contains("Green", stdoutWriter.ToString());
        Assert.Contains("Czerwony", shell.Terminal.LastRenderedText);
        Assert.Contains("Zielony", shell.Terminal.LastRenderedText);
        Assert.Contains("Niebieski", shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("Red", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task FlagsEnumPrompt_RendersTigerTextLabels_AndBindsFlagsValue()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<SelectFlagsCommand>()
            .Build();

        var ct = TestContext.Current.CancellationToken;

        var stdoutWriter = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var stderrWriter = new StringWriter();
        Console.SetOut(stdoutWriter);
        Console.SetError(stderrWriter);
        int exitCode;
        try
        {
            exitCode = await app.RunAsync(Array.Empty<string>(), shell, promptTimeout: TimeSpan.FromSeconds(3), ct: ct);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        Assert.Equal(0, exitCode);
        Assert.Contains("Read", stdoutWriter.ToString());
        Assert.Contains("Write", stdoutWriter.ToString());
        Assert.Contains("Czytaj", shell.Terminal.LastRenderedText);
        Assert.Contains("Pisz", shell.Terminal.LastRenderedText);
        Assert.Contains("Uruchom", shell.Terminal.LastRenderedText);
    }

    // ── CLI parsing untouched by labels ─────────────────────────────

    [Fact]
    public async Task CommandLineEnumParsing_StillAcceptsMemberName_NotLocalizedLabel()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<SelectColorCommand>()
            .Build();

        // Member name binds.
        var byName = await RunCapturedAsync(app, ["--color", "Green"]);
        Assert.Equal(0, byName.ExitCode);
        Assert.Contains("Green", byName.Stdout);

        // Polish label is for UI display only — it must NOT bind to Green from the command line.
        var byLabel = await RunCapturedAsync(app, ["--color", "Zielony"]);
        Assert.NotEqual(0, byLabel.ExitCode);
        Assert.DoesNotContain("Green", byLabel.Stdout);
        Assert.Contains("Invalid value for --color: Zielony.", byLabel.Stderr);
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
}
