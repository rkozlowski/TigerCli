using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers optional nullable select-style prompts that offer a synthetic "no-selection" (null)
/// row. The row is offered only for optional nullable enum / provider-backed selects; it binds
/// null when chosen and is distinct from cancellation.
/// </summary>
public sealed class TigerCliNoSelectionPromptTests
{
    private enum Hue { Red, Green, Blue }

    private enum Auth { Integrated, Sql }

    // ── Enum settings ────────────────────────────────────────────────

    private sealed class NullableEnumSettings : TigerCliSettings
    {
        [TigerCliOption("--color", Promptable = TigerCliPromptable.Normal, Description = "Color")]
        public Hue? Color { get; set; }
    }

    private sealed class NullableEnumDefaultSettings : TigerCliSettings
    {
        [TigerCliOption("--color", Promptable = TigerCliPromptable.Normal, Description = "Color")]
        public Hue? Color { get; set; } = Hue.Green;
    }

    private sealed class RequiredNullableEnumSettings : TigerCliSettings
    {
        [TigerCliOption("--color", Required = true, Promptable = TigerCliPromptable.Normal, Description = "Color")]
        public Hue? Color { get; set; }
    }

    private sealed class NonNullableEnumSettings : TigerCliSettings
    {
        [TigerCliOption("--color", Promptable = TigerCliPromptable.Normal, Description = "Color")]
        public Hue Color { get; set; }
    }

    private sealed class ConditionalRequiredEnumSettings : TigerCliSettings
    {
        [TigerCliOption("--auth", Promptable = TigerCliPromptable.No, Description = "Auth")]
        public Auth Auth { get; set; }

        [TigerCliOption("--color",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--auth",
            RequiredWhenValue = "Sql",
            Description = "Color")]
        public Hue? Color { get; set; }
    }

    // ── Provider settings ────────────────────────────────────────────

    private sealed class NullableProviderSettings : TigerCliSettings
    {
        [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string? Database { get; set; }
    }

    private sealed class AutoSelectNullableProviderSettings : TigerCliSettings
    {
        [TigerCliOption(
            "--database",
            Provider = "databases",
            Promptable = TigerCliPromptable.Normal,
            AutoSelectSingleChoice = true,
            Description = "Database")]
        public string? Database { get; set; }
    }

    private sealed class NullableProviderValidDefaultSettings : TigerCliSettings
    {
        [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string? Database { get; set; } = "Reports";
    }

    private sealed class NullableProviderStaleDefaultSettings : TigerCliSettings
    {
        [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string? Database { get; set; } = "bogus";
    }

    private sealed class RequiredNullableProviderSettings : TigerCliSettings
    {
        [TigerCliOption("--database", Provider = "databases", Required = true, Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string? Database { get; set; }
    }

    private sealed class ConditionalRequiredProviderSettings : TigerCliSettings
    {
        [TigerCliOption("--auth", Promptable = TigerCliPromptable.No, Description = "Auth")]
        public Auth Auth { get; set; }

        [TigerCliOption("--database",
            Provider = "databases",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--auth",
            RequiredWhenValue = "Sql",
            Description = "Database")]
        public string? Database { get; set; }
    }

    private sealed class NoneChoiceProviderSettings : TigerCliSettings
    {
        [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string? Database { get; set; }
    }

    private sealed class NullableProviderEditSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string? Database { get; set; }
    }

    // ── Commands ─────────────────────────────────────────────────────

    private sealed class NullableEnumCommand : TigerCliAsyncCommandHandler<NullableEnumSettings>
    {
        public override Task<int> ExecuteAsync(NullableEnumSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"color={s.Color?.ToString() ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    private sealed class NullableEnumDefaultCommand : TigerCliAsyncCommandHandler<NullableEnumDefaultSettings>
    {
        public override Task<int> ExecuteAsync(NullableEnumDefaultSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"color={s.Color?.ToString() ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    private sealed class RequiredNullableEnumCommand : TigerCliAsyncCommandHandler<RequiredNullableEnumSettings>
    {
        public override Task<int> ExecuteAsync(RequiredNullableEnumSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"color={s.Color?.ToString() ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    private sealed class NonNullableEnumCommand : TigerCliAsyncCommandHandler<NonNullableEnumSettings>
    {
        public override Task<int> ExecuteAsync(NonNullableEnumSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"color={s.Color}"));
            return Task.FromResult(0);
        }
    }

    private sealed class ConditionalRequiredEnumCommand : TigerCliAsyncCommandHandler<ConditionalRequiredEnumSettings>
    {
        public override Task<int> ExecuteAsync(ConditionalRequiredEnumSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"color={s.Color?.ToString() ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    private sealed class NullableProviderCommand : TigerCliAsyncCommandHandler<NullableProviderSettings>
    {
        public override Task<int> ExecuteAsync(NullableProviderSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={s.Database ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    private sealed class AutoSelectNullableProviderCommand :
        TigerCliAsyncCommandHandler<AutoSelectNullableProviderSettings>
    {
        public override Task<int> ExecuteAsync(AutoSelectNullableProviderSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={s.Database ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    private sealed class NullableProviderValidDefaultCommand : TigerCliAsyncCommandHandler<NullableProviderValidDefaultSettings>
    {
        public override Task<int> ExecuteAsync(NullableProviderValidDefaultSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={s.Database ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    private sealed class NullableProviderStaleDefaultCommand : TigerCliAsyncCommandHandler<NullableProviderStaleDefaultSettings>
    {
        public override Task<int> ExecuteAsync(NullableProviderStaleDefaultSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={s.Database ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    private sealed class RequiredNullableProviderCommand : TigerCliAsyncCommandHandler<RequiredNullableProviderSettings>
    {
        public override Task<int> ExecuteAsync(RequiredNullableProviderSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={s.Database ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    private sealed class ConditionalRequiredProviderCommand : TigerCliAsyncCommandHandler<ConditionalRequiredProviderSettings>
    {
        public override Task<int> ExecuteAsync(ConditionalRequiredProviderSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={s.Database ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    private sealed class NoneChoiceProviderCommand : TigerCliAsyncCommandHandler<NoneChoiceProviderSettings>
    {
        public override Task<int> ExecuteAsync(NoneChoiceProviderSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={s.Database ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    private sealed class NullableProviderEditCommand : TigerCliAsyncCommandHandler<NullableProviderEditSettings>
    {
        public override Task<int> ExecuteAsync(NullableProviderEditSettings s)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={s.Database ?? "<null>"}"));
            return Task.FromResult(0);
        }
    }

    // ── Enum tests ───────────────────────────────────────────────────

    [Fact]
    public async Task NullableEnum_Optional_OffersNoSelectionRow()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = AddApp<NullableEnumCommand>();

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("(None)", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task NullableEnum_Optional_NullDefault_PreselectsNoSelection_AndBindsNull()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the preselected no-selection row
        var app = AddApp<NullableEnumCommand>();

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("color=<null>", result.Stdout);
    }

    [Fact]
    public async Task NullableEnum_SelectingNoSelection_BindsNull_EvenWhenDefaultHasValue()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Home);  // move off the preselected Green to the no-selection row
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = AddApp<NullableEnumDefaultCommand>();

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("color=<null>", result.Stdout);
    }

    [Fact]
    public async Task NullableEnum_ValueDefault_PreselectsThatValue()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the preselected default (Green)
        var app = AddApp<NullableEnumDefaultCommand>();

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("color=Green", result.Stdout);
    }

    [Fact]
    public async Task NullableEnum_Required_DoesNotOfferNoSelection()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // first row is a real enum member, not no-selection
        var app = AddApp<RequiredNullableEnumCommand>();

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("(None)", shell.Terminal.LastRenderedText);
        Assert.Contains("color=Red", result.Stdout);
    }

    [Fact]
    public async Task NullableEnum_ConditionallyRequired_DoesNotOfferNoSelectionWhenConditionApplies()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = AddApp<ConditionalRequiredEnumCommand>();

        var result = await RunAsync(app, ["--auth", "Sql"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("(None)", shell.Terminal.LastRenderedText);
        Assert.Contains("color=Red", result.Stdout);
    }

    [Fact]
    public async Task NonNullableEnum_DoesNotOfferNoSelection()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = AddApp<NonNullableEnumCommand>();

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("(None)", shell.Terminal.LastRenderedText);
        Assert.Contains("color=Red", result.Stdout);
    }

    [Fact]
    public async Task NullableEnum_Cancel_DoesNotBindNull_AndReportsCanceled()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var app = AddApp<NullableEnumCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 46));

        var result = await RunAsync(app, [], shell);

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("Cancelled.", result.Stderr);
        Assert.DoesNotContain("color=", result.Stdout);
    }

    [Fact]
    public async Task NullableEnum_CliValue_SkipsPromptingAndWins()
    {
        var shell = new TestShell();
        var app = AddApp<NullableEnumCommand>();

        var result = await RunAsync(app, ["--color", "Blue"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("color=Blue", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    // ── Provider tests ───────────────────────────────────────────────

    [Fact]
    public async Task NullableProvider_Optional_OffersNoSelectionRow()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = AddProviderApp<NullableProviderCommand>(Databases);

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("(None)", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task NullableProvider_SelectingNoSelection_BindsNull()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // no-selection is preselected for a null default
        var app = AddProviderApp<NullableProviderCommand>(Databases);

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=<null>", result.Stdout);
    }

    [Fact]
    public async Task NullableProvider_NoChoices_OffersNoSelectionRow()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = AddProviderApp<NullableProviderCommand>(() => []);

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("(None)", shell.Terminal.LastRenderedText);
        Assert.Contains("database=<null>", result.Stdout);
        Assert.True(shell.Terminal.ReadCount > 0);
    }

    [Fact]
    public async Task NullableProvider_NoChoices_AutoSelectsNoSelectionWhenEnabled()
    {
        var shell = new TestShell();
        var app = AddProviderApp<AutoSelectNullableProviderCommand>(() => []);

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=<null>", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
        Assert.DoesNotContain("(None)", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task NullableProvider_OneChoiceWithNoSelection_ShowsPromptEvenWhenAutoSelectEnabled()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accepts preselected no-selection
        var app = AddProviderApp<AutoSelectNullableProviderCommand>(() => Strings("master"));

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("(None)", shell.Terminal.LastRenderedText);
        Assert.Contains("master", shell.Terminal.LastRenderedText);
        Assert.Contains("database=<null>", result.Stdout);
        Assert.True(shell.Terminal.ReadCount > 0);
    }

    [Fact]
    public async Task NullableProvider_ValidDefault_PreselectsMatchingChoice()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept preselected "Reports"
        var app = AddProviderApp<NullableProviderValidDefaultCommand>(Databases);

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=Reports", result.Stdout);
    }

    [Fact]
    public async Task NullableProvider_SelectingRealChoice_BindsThatChoice()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow); // off no-selection (0) onto first choice (master)
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = AddProviderApp<NullableProviderCommand>(Databases);

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=master", result.Stdout);
    }

    [Fact]
    public async Task NullableProvider_Required_DoesNotOfferNoSelection()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // first row is a real provider choice
        var app = AddProviderApp<RequiredNullableProviderCommand>(Databases);

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("(None)", shell.Terminal.LastRenderedText);
        Assert.Contains("database=master", result.Stdout);
    }

    [Fact]
    public async Task NullableProvider_ConditionallyRequired_DoesNotOfferNoSelectionWhenConditionApplies()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = AddProviderApp<ConditionalRequiredProviderCommand>(Databases);

        var result = await RunAsync(app, ["--auth", "Sql"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("(None)", shell.Terminal.LastRenderedText);
        Assert.Contains("database=master", result.Stdout);
    }

    [Fact]
    public async Task NullableProvider_NoSelection_SkipsProviderValidation()
    {
        // Provider validation is on by default; selecting no-selection binds null which must be
        // skipped by provider validation (no "not an available choice" error).
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = AddProviderApp<NullableProviderCommand>(Databases, builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=<null>", result.Stdout);
    }

    [Fact]
    public async Task NullableProvider_StaleOptionalDefault_NotInjected_PreselectsNoSelection()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // stale default → no-selection is preselected
        var app = AddProviderApp<NullableProviderStaleDefaultCommand>(Databases);

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=<null>", result.Stdout);
        Assert.DoesNotContain("bogus", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task NullableProvider_RealNoneChoice_DoesNotConflictWithSyntheticNull()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow); // off synthetic null (0) onto the real "None" (1)
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = AddProviderApp<NoneChoiceProviderCommand>(() => Strings("None", "master"));

        var result = await RunAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        // The real provider choice "None" binds the string "None" — distinct from the synthetic
        // null no-selection row at index 0.
        Assert.Contains("database=None", result.Stdout);
    }

    [Fact]
    public async Task NullableProvider_CliValue_SkipsPromptingAndWins()
    {
        var shell = new TestShell();
        var app = AddProviderApp<NullableProviderCommand>(Databases);

        var result = await RunAsync(app, ["--database", "master"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=master", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    // ── Edit-mode provider tests ─────────────────────────────────────

    [Fact]
    public async Task EditNullableProvider_ExistingNull_PreselectsNoSelection()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = EditProviderApp(
            _ => TigerCliEditLoad<NullableProviderEditSettings>.Found(new NullableProviderEditSettings { Database = null }),
            Databases);

        var result = await RunAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=<null>", result.Stdout);
    }

    [Fact]
    public async Task EditNullableProvider_ExistingValid_PreselectsThatChoice()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = EditProviderApp(
            _ => TigerCliEditLoad<NullableProviderEditSettings>.Found(new NullableProviderEditSettings { Database = "Reports" }),
            Databases);

        var result = await RunAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=Reports", result.Stdout);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static IReadOnlyList<string> Databases() => Strings("master", "Reports", "ReportingArchive");

    private static IReadOnlyList<string> Strings(params string[] values) => values;

    private static TigerCliApp AddApp<TCommand>(Action<TigerCliAppBuilder>? configure = null)
        where TCommand : class, new()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("noselection-test")
            .SetDefaultCommand<TCommand>();
        configure?.Invoke(builder);
        return builder.Build();
    }

    private static TigerCliApp AddProviderApp<TCommand>(
        Func<IReadOnlyList<string>> provider,
        Action<TigerCliAppBuilder>? configure = null)
        where TCommand : class, new()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("noselection-test")
            .SetDefaultCommand<TCommand>()
            .ConfigureProviders(providers => providers.Add("databases", _ => provider()));
        configure?.Invoke(builder);
        return builder.Build();
    }

    private static TigerCliApp EditProviderApp(
        Func<NullableProviderEditSettings, TigerCliEditLoad<NullableProviderEditSettings>> loader,
        Func<IReadOnlyList<string>> provider)
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("noselection-test")
            .AddCommand<NullableProviderEditCommand>("edit", b => b.AsEdit(loader))
            .ConfigureProviders(providers => providers.Add("databases", _ => provider()))
            .Build();
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        TigerCliApp app,
        string[] args,
        ICliAppShell shell)
    {
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
