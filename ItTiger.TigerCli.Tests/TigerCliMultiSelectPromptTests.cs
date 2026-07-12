using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliMultiSelectPromptTests
{
    // ── [Flags] enum multi-select (works through the generic [TigerCliOption] path) ──

    [Flags]
    private enum Perm
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4
    }

    private sealed class FlagSettings : TigerCliSettings
    {
        [TigerCliOption("--perms", Promptable = TigerCliPromptable.Normal, Description = "Permissions.")]
        public Perm? Perms { get; set; }
    }

    private sealed class FlagCommand : TigerCliAsyncCommandHandler<FlagSettings>
    {
        public override Task<int> ExecuteAsync(FlagSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"perms={(int?)settings.Perms}"));
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task Flags_NonInteractive_CommaSeparatedNames()
    {
        var app = App<FlagCommand>();
        var result = await RunCapturedAsync(app, ["--perms", "Read,Write", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("perms=3", result.Stdout); // Read|Write
    }

    [Fact]
    public async Task Flags_NonInteractive_NumericValue()
    {
        var app = App<FlagCommand>();
        var result = await RunCapturedAsync(app, ["--perms", "5", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("perms=5", result.Stdout); // Read|Execute
    }

    [Fact]
    public async Task Flags_NonInteractive_HexValue()
    {
        var app = App<FlagCommand>();
        var result = await RunCapturedAsync(app, ["--perms", "0x6", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("perms=6", result.Stdout); // Write|Execute
    }

    [Fact]
    public async Task Flags_Interactive_MultiSelect_Preselected()
    {
        var shell = new TestShell();
        // Labels are single-bit values [Read, Write, Execute]; Read starts checked (preselect current).
        // Toggle Execute on, keep Read, leave Write off.
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);  // -> Write
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);  // -> Execute
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' '); // toggle Execute on
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<FlagCommandSeeded>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("perms=5", result.Stdout); // Read|Execute
    }

    private sealed class SeededFlagSettings : TigerCliSettings
    {
        [TigerCliOption("--perms", Promptable = TigerCliPromptable.Normal, Description = "Permissions.")]
        public Perm? Perms { get; set; } = Perm.Read;
    }

    private sealed class FlagCommandSeeded : TigerCliAsyncCommandHandler<SeededFlagSettings>
    {
        public override Task<int> ExecuteAsync(SeededFlagSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"perms={(int?)settings.Perms}"));
            return Task.FromResult(0);
        }
    }

    // ── Dynamic string multi-select from a provider ──

    private sealed class TagSettings : TigerCliSettings
    {
        [TigerCliOption("--tags", Provider = "tags", Promptable = TigerCliPromptable.Normal, Description = "Tags.")]
        [TigerCliMultiSelect]
        public string[]? Tags { get; set; }
    }

    private sealed class TagCommand : TigerCliAsyncCommandHandler<TagSettings>
    {
        public override Task<int> ExecuteAsync(TagSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"tags=[{string.Join(",", settings.Tags ?? [])}]"));
            return Task.FromResult(0);
        }
    }

    private static TigerCliApp TagApp(Action<TigerCliAppBuilder>? configure = null)
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("ms-test")
            .SetDefaultCommand<TagCommand>()
            .ConfigureProviders(providers =>
                providers.Add("tags", _ => new List<string> { "red", "green", "blue" }));
        configure?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public async Task Strings_NonInteractive_CommaSeparated_Validated()
    {
        var result = await RunCapturedAsync(TagApp(), ["--tags", "red,blue", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tags=[red,blue]", result.Stdout);
    }

    [Fact]
    public async Task Strings_NonInteractive_RepeatedOption_Works()
    {
        var result = await RunCapturedAsync(TagApp(), ["--tags", "red", "--tags", "green", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tags=[red,green]", result.Stdout);
    }

    [Fact]
    public async Task Strings_NonInteractive_UnknownValue_Rejected()
    {
        var app = TagApp(b => b.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var result = await RunCapturedAsync(app, ["--tags", "red,purple", "--non-interactive"], new TestShell());

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("purple", result.Stderr);
        Assert.Contains("not an available choice", result.Stderr);
    }

    [Fact]
    public async Task Strings_NonInteractive_DuplicatesCollapsed_OrderPreserved()
    {
        var result = await RunCapturedAsync(TagApp(), ["--tags", "blue,red,blue", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tags=[blue,red]", result.Stdout);
    }

    [Fact]
    public async Task Strings_Interactive_Checklist_BindsChecked()
    {
        var shell = new TestShell();
        // choices [red, green, blue]; check red and blue.
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' '); // red on
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' '); // blue on
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunCapturedAsync(TagApp(), [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tags=[red,blue]", result.Stdout);
        Assert.True(shell.Terminal.ReadCount > 0);
    }

    [Fact]
    public async Task Strings_Interactive_EmptyConfirm_AllowedBindsEmpty()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // confirm nothing checked

        var result = await RunCapturedAsync(TagApp(), [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tags=[]", result.Stdout);
    }

    [Fact]
    public async Task Strings_Interactive_Preselected_CurrentValuesKeptOnConfirm()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // confirm without changing the seeded selection

        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("ms-test")
            .SetDefaultCommand<SeededTagCommand>()
            .ConfigureProviders(providers =>
                providers.Add("tags", _ => new List<string> { "red", "green", "blue" }))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tags=[green]", result.Stdout);
    }

    private sealed class SeededTagSettings : TigerCliSettings
    {
        [TigerCliOption("--tags", Provider = "tags", Promptable = TigerCliPromptable.Normal, Description = "Tags.")]
        [TigerCliMultiSelect]
        public string[]? Tags { get; set; } = ["green"];
    }

    private sealed class SeededTagCommand : TigerCliAsyncCommandHandler<SeededTagSettings>
    {
        public override Task<int> ExecuteAsync(SeededTagSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"tags=[{string.Join(",", settings.Tags ?? [])}]"));
            return Task.FromResult(0);
        }
    }

    // ── AllowEmpty / AllowCustomValues ──

    private sealed class RequiredSelectionSettings : TigerCliSettings
    {
        [TigerCliOption("--tags", Provider = "tags", Description = "Tags.")]
        [TigerCliMultiSelect(AllowEmpty = false)]
        public string[]? Tags { get; set; }
    }

    private sealed class RequiredSelectionCommand : TigerCliAsyncCommandHandler<RequiredSelectionSettings>
    {
        public override Task<int> ExecuteAsync(RequiredSelectionSettings settings) => Task.FromResult(0);
    }

    [Fact]
    public async Task AllowEmptyFalse_NonInteractive_EmptyRejected()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("ms-test")
            .SetDefaultCommand<RequiredSelectionCommand>()
            .ConfigureProviders(providers => providers.Add("tags", _ => new List<string> { "red", "green" }))
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .Build();

        var result = await RunCapturedAsync(app, ["--tags", "", "--non-interactive"], new TestShell());

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("At least one value", result.Stderr);
    }

    private sealed class CustomTagSettings : TigerCliSettings
    {
        [TigerCliOption("--tags", Provider = "tags", Description = "Tags.")]
        [TigerCliMultiSelect(AllowCustomValues = true)]
        public string[]? Tags { get; set; }
    }

    private sealed class CustomTagCommand : TigerCliAsyncCommandHandler<CustomTagSettings>
    {
        public override Task<int> ExecuteAsync(CustomTagSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"tags=[{string.Join(",", settings.Tags ?? [])}]"));
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task AllowCustomValues_KeepsUnknownTokens()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("ms-test")
            .SetDefaultCommand<CustomTagCommand>()
            .ConfigureProviders(providers => providers.Add("tags", _ => new List<string> { "red", "green" }))
            .Build();

        var result = await RunCapturedAsync(app, ["--tags", "red,custom", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("tags=[red,custom]", result.Stdout);
    }

    // ── Key/label multi-select (TigerWrap language-options shape) ──

    private sealed class LangSettings : TigerCliSettings
    {
        [TigerCliOption("--language-options", Provider = "langopts", Promptable = TigerCliPromptable.Normal, Description = "Language options.")]
        [TigerCliMultiSelect]
        public long[]? LanguageOptions { get; set; }
    }

    private sealed class LangCommand : TigerCliAsyncCommandHandler<LangSettings>
    {
        public override Task<int> ExecuteAsync(LangSettings settings)
        {
            long combined = 0;
            foreach (var value in settings.LanguageOptions ?? [])
                combined |= value;
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"flags=0x{combined:X}"));
            return Task.FromResult(0);
        }
    }

    private static TigerCliApp LangApp(Action<TigerCliAppBuilder>? configure = null)
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("ms-test")
            .SetDefaultCommand<LangCommand>()
            .ConfigureProviders(providers => providers.Add<long>("langopts", _ =>
            [
                new OptionItem<long>(0x4, "Use DateOnly (0x0004)"),
                new OptionItem<long>(0x2, "Use TimeOnly (0x0002)"),
                new OptionItem<long>(0x1, "Use Utc (0x0001)")
            ]));
        configure?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public async Task KeyLabel_NonInteractive_ByKey_OrsIntoCombinedFlag()
    {
        var result = await RunCapturedAsync(LangApp(), ["--language-options", "4,2", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("flags=0x6", result.Stdout); // 0x4 | 0x2
    }

    [Fact]
    public async Task KeyLabel_NonInteractive_ByLabel_Resolves()
    {
        var result = await RunCapturedAsync(
            LangApp(),
            ["--language-options", "Use DateOnly (0x0004),Use Utc (0x0001)", "--non-interactive"],
            new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("flags=0x5", result.Stdout); // 0x4 | 0x1
    }

    [Fact]
    public async Task KeyLabel_Interactive_Checklist_OrsSelectedKeys()
    {
        var shell = new TestShell();
        // choices [0x4, 0x2, 0x1]; check first (0x4) and third (0x1).
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunCapturedAsync(LangApp(), [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("flags=0x5", result.Stdout); // 0x4 | 0x1
    }

    // ── Configuration errors ──

    private sealed class BadElementSettings : TigerCliSettings
    {
        [TigerCliOption("--values", Description = "Values.")]
        [TigerCliMultiSelect]
        public List<double>? Values { get; set; }
    }

    private sealed class BadElementCommand : TigerCliAsyncCommandHandler<BadElementSettings>
    {
        public override Task<int> ExecuteAsync(BadElementSettings settings) => Task.FromResult(0);
    }

    [Fact]
    public async Task UnsupportedElementType_IsRejectedClearly()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("ms-test")
            .SetDefaultCommand<BadElementCommand>()
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => app.RunAsync(["--values", "1.5"], new TestShell(), ct: TestContext.Current.CancellationToken));

        Assert.Contains("[TigerCliMultiSelect]", ex.Message);
    }

    private sealed class BadCustomSettings : TigerCliSettings
    {
        [TigerCliOption("--values", Description = "Values.")]
        [TigerCliMultiSelect(AllowCustomValues = true)]
        public long[]? Values { get; set; }
    }

    private sealed class BadCustomCommand : TigerCliAsyncCommandHandler<BadCustomSettings>
    {
        public override Task<int> ExecuteAsync(BadCustomSettings settings) => Task.FromResult(0);
    }

    [Fact]
    public async Task AllowCustomValues_OnNonStringCollection_IsRejected()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("ms-test")
            .SetDefaultCommand<BadCustomCommand>()
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => app.RunAsync(["--values", "1"], new TestShell(), ct: TestContext.Current.CancellationToken));

        Assert.Contains("AllowCustomValues", ex.Message);
    }

    // ── Helpers ──

    private static TigerCliApp App<TCommand>(Action<TigerCliAppBuilder>? configure = null)
        where TCommand : class, new()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("ms-test")
            .SetDefaultCommand<TCommand>();
        configure?.Invoke(builder);
        return builder.Build();
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args,
        TestShell shell,
        TimeSpan? promptTimeout = null)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args, shell, promptTimeout, TestContext.Current.CancellationToken);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
