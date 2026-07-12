using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliPromptableOrderingTests
{
    private enum AuthenticationType
    {
        Integrated,
        SqlPassword
    }

    private sealed class BucketSettings : TigerCliSettings
    {
        [TigerCliOption("--normal", Promptable = TigerCliPromptable.Normal)]
        public string? Normal { get; set; }

        [TigerCliOption("--last", Promptable = TigerCliPromptable.Last)]
        public string? Last { get; set; }

        [TigerCliOption("--first", Promptable = TigerCliPromptable.First)]
        public string? First { get; set; }
    }

    private sealed class BucketCommand : TigerCliAsyncCommandHandler<BucketSettings>
    {
        public override Task<int> ExecuteAsync(BucketSettings settings)
        {
            TigerConsole.MarkupLine($"first={CliMarkupParser.Escape(settings.First ?? "<null>")}");
            TigerConsole.MarkupLine($"normal={CliMarkupParser.Escape(settings.Normal ?? "<null>")}");
            TigerConsole.MarkupLine($"last={CliMarkupParser.Escape(settings.Last ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class NoPromptSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Promptable = TigerCliPromptable.No)]
        public string? Name { get; set; }
    }

    private sealed class NoPromptCommand : TigerCliAsyncCommandHandler<NoPromptSettings>
    {
        public override Task<int> ExecuteAsync(NoPromptSettings settings)
        {
            TigerConsole.MarkupLine($"name={CliMarkupParser.Escape(settings.Name ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class ConditionalLastSettings : TigerCliSettings
    {
        [TigerCliOption("--authentication", Promptable = TigerCliPromptable.First)]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

        [TigerCliOption("--database",
            Promptable = TigerCliPromptable.Last,
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword")]
        public string? Database { get; set; }
    }

    private sealed class ConditionalLastCommand : TigerCliAsyncCommandHandler<ConditionalLastSettings>
    {
        public override Task<int> ExecuteAsync(ConditionalLastSettings settings)
        {
            TigerConsole.MarkupLine($"authentication={settings.Authentication}");
            TigerConsole.MarkupLine($"database={CliMarkupParser.Escape(settings.Database ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class DatabaseSettings : TigerCliSettings
    {
        [TigerCliOption("--database",
            Provider = "database",
            Promptable = TigerCliPromptable.Last)]
        public string? Database { get; set; }

        [TigerCliOption("--server", Promptable = TigerCliPromptable.Normal)]
        public string? Server { get; set; }

        [TigerCliOption("--authentication", Promptable = TigerCliPromptable.First)]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

        [TigerCliOption("--password",
            Promptable = TigerCliPromptable.Normal,
            Secret = true,
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword")]
        public string? Password { get; set; }

        [TigerCliOption("--trust-server-certificate")]
        public bool TrustServerCertificate { get; set; }
    }

    private sealed class DatabaseCommand : TigerCliAsyncCommandHandler<DatabaseSettings>
    {
        public override Task<int> ExecuteAsync(DatabaseSettings settings)
        {
            TigerConsole.MarkupLine($"server={CliMarkupParser.Escape(settings.Server ?? "<null>")}");
            TigerConsole.MarkupLine($"authentication={settings.Authentication}");
            TigerConsole.MarkupLine($"database={CliMarkupParser.Escape(settings.Database ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderNoSettings : TigerCliSettings
    {
        [TigerCliOption("--database",
            Provider = "database",
            Promptable = TigerCliPromptable.No)]
        public string? Database { get; set; }
    }

    private sealed class ProviderNoCommand : TigerCliAsyncCommandHandler<ProviderNoSettings>
    {
        public override Task<int> ExecuteAsync(ProviderNoSettings settings)
        {
            TigerConsole.MarkupLine($"database={CliMarkupParser.Escape(settings.Database ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderHintOptionalSettings : TigerCliSettings
    {
        [TigerCliOption("--database",
            Provider = "database",
            Promptable = TigerCliPromptable.Last)]
        public string? Database { get; set; }

        [TigerCliOption("--server")]
        public string? Server { get; set; }
    }

    private sealed class ProviderHintOptionalCommand :
        TigerCliAsyncCommandHandler<ProviderHintOptionalSettings>
    {
        public override Task<int> ExecuteAsync(ProviderHintOptionalSettings settings)
        {
            TigerConsole.MarkupLine($"server={CliMarkupParser.Escape(settings.Server ?? "<null>")}");
            TigerConsole.MarkupLine($"database={CliMarkupParser.Escape(settings.Database ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class ExplicitDependsOnOptionSettings : TigerCliSettings
    {
        [TigerCliOption("--database",
            Provider = "database",
            Promptable = TigerCliPromptable.First,
            DependsOnOption = "--server")]
        public string? Database { get; set; }

        [TigerCliOption("--server", Promptable = TigerCliPromptable.Last)]
        public string? Server { get; set; }
    }

    private sealed class ExplicitDependsOnOptionCommand :
        TigerCliAsyncCommandHandler<ExplicitDependsOnOptionSettings>
    {
        public override Task<int> ExecuteAsync(ExplicitDependsOnOptionSettings settings)
        {
            TigerConsole.MarkupLine($"server={CliMarkupParser.Escape(settings.Server ?? "<null>")}");
            TigerConsole.MarkupLine($"database={CliMarkupParser.Escape(settings.Database ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class ExplicitDependsOnOptionsSettings : TigerCliSettings
    {
        [TigerCliOption("--database",
            Provider = "database",
            Promptable = TigerCliPromptable.First,
            DependsOnOptions = new[] { "--server", "--authentication", "--encrypt" })]
        public string? Database { get; set; }

        [TigerCliOption("--server", Promptable = TigerCliPromptable.Normal)]
        public string? Server { get; set; }

        [TigerCliOption("--authentication", Promptable = TigerCliPromptable.Normal)]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

        [TigerCliOption("--encrypt", Promptable = TigerCliPromptable.Normal)]
        public string? Encrypt { get; set; }
    }

    private sealed class ExplicitDependsOnOptionsCommand :
        TigerCliAsyncCommandHandler<ExplicitDependsOnOptionsSettings>
    {
        public override Task<int> ExecuteAsync(ExplicitDependsOnOptionsSettings settings)
        {
            TigerConsole.MarkupLine($"server={CliMarkupParser.Escape(settings.Server ?? "<null>")}");
            TigerConsole.MarkupLine($"authentication={settings.Authentication}");
            TigerConsole.MarkupLine($"encrypt={CliMarkupParser.Escape(settings.Encrypt ?? "<null>")}");
            TigerConsole.MarkupLine($"database={CliMarkupParser.Escape(settings.Database ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class ExplicitDependencyOptionalTargetSettings : TigerCliSettings
    {
        [TigerCliOption("--database",
            Provider = "database",
            Promptable = TigerCliPromptable.First,
            DependsOnOption = "--server")]
        public string? Database { get; set; }

        [TigerCliOption("--server")]
        public string? Server { get; set; }
    }

    private sealed class ExplicitDependencyOptionalTargetCommand :
        TigerCliAsyncCommandHandler<ExplicitDependencyOptionalTargetSettings>
    {
        public override Task<int> ExecuteAsync(ExplicitDependencyOptionalTargetSettings settings)
        {
            TigerConsole.MarkupLine($"server={CliMarkupParser.Escape(settings.Server ?? "<null>")}");
            TigerConsole.MarkupLine($"database={CliMarkupParser.Escape(settings.Database ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class RequiredWhenImplicitDependencySettings : TigerCliSettings
    {
        [TigerCliOption("--database",
            Provider = "database",
            Promptable = TigerCliPromptable.First,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword")]
        public string? Database { get; set; }

        [TigerCliOption("--authentication", Promptable = TigerCliPromptable.Last)]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;
    }

    private sealed class RequiredWhenImplicitDependencyCommand :
        TigerCliAsyncCommandHandler<RequiredWhenImplicitDependencySettings>
    {
        public override Task<int> ExecuteAsync(RequiredWhenImplicitDependencySettings settings)
        {
            TigerConsole.MarkupLine($"authentication={settings.Authentication}");
            TigerConsole.MarkupLine($"database={CliMarkupParser.Escape(settings.Database ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class PromptWhenImplicitDependencySettings : TigerCliSettings
    {
        [TigerCliOption("--database",
            Provider = "database",
            Promptable = TigerCliPromptable.First,
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword")]
        public string? Database { get; set; }

        [TigerCliOption("--authentication", Promptable = TigerCliPromptable.Last)]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;
    }

    private sealed class PromptWhenImplicitDependencyCommand :
        TigerCliAsyncCommandHandler<PromptWhenImplicitDependencySettings>
    {
        public override Task<int> ExecuteAsync(PromptWhenImplicitDependencySettings settings)
        {
            TigerConsole.MarkupLine($"authentication={settings.Authentication}");
            TigerConsole.MarkupLine($"database={CliMarkupParser.Escape(settings.Database ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class DeduplicatedDependencySettings : TigerCliSettings
    {
        [TigerCliOption("--database",
            Provider = "database",
            Promptable = TigerCliPromptable.First,
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword",
            DependsOnOption = "--authentication",
            DependsOnOptions = new[] { "--authentication", "Authentication" })]
        public string? Database { get; set; }

        [TigerCliOption("--authentication", Promptable = TigerCliPromptable.Last)]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;
    }

    private sealed class DeduplicatedDependencyCommand :
        TigerCliAsyncCommandHandler<DeduplicatedDependencySettings>
    {
        public override Task<int> ExecuteAsync(DeduplicatedDependencySettings settings)
        {
            TigerConsole.MarkupLine($"authentication={settings.Authentication}");
            TigerConsole.MarkupLine($"database={CliMarkupParser.Escape(settings.Database ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class DependsOnCycleSettings : TigerCliSettings
    {
        [TigerCliOption("--a", Promptable = TigerCliPromptable.Normal, DependsOnOptions = new[] { "--b" })]
        public string? A { get; set; }

        [TigerCliOption("--b", Promptable = TigerCliPromptable.Normal, DependsOnOptions = new[] { "--a" })]
        public string? B { get; set; }
    }

    private sealed class DependsOnCycleCommand : TigerCliAsyncCommandHandler<DependsOnCycleSettings>
    {
        public override Task<int> ExecuteAsync(DependsOnCycleSettings settings)
        {
            TigerConsole.MarkupLine($"a={CliMarkupParser.Escape(settings.A ?? "<null>")}");
            TigerConsole.MarkupLine($"b={CliMarkupParser.Escape(settings.B ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task PromptableNo_PreventsPrompting()
    {
        var app = App<NoPromptCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var shell = ShellWithText("ignored");

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --name", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task PromptableNormal_BehavesLikeExplicitPromptable()
    {
        var app = App<BucketCommand>(builder => builder.SetDefaultPromptMode(TigerCliPromptMode.No));
        var shell = ShellWithText("first");
        EnqueueText(shell, "normal");
        EnqueueText(shell, "last");

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("first=first", result.Stdout);
        Assert.Contains("normal=normal", result.Stdout);
        Assert.Contains("last=last", result.Stdout);
    }

    [Fact]
    public async Task PromptableBuckets_OrderFirstBeforeNormalBeforeLast()
    {
        var app = App<BucketCommand>();
        var shell = ShellWithText("first");
        EnqueueText(shell, "normal");
        EnqueueText(shell, "last");

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("first=first", result.Stdout);
        Assert.Contains("normal=normal", result.Stdout);
        Assert.Contains("last=last", result.Stdout);
    }

    [Fact]
    public async Task PromptableLast_DoesNotPromptWhenPromptWhenConditionIsFalse()
    {
        var app = App<ConditionalLastCommand>();
        var shell = new TestShell();
        EnqueueSelect(shell, index: 0);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("authentication=Integrated", result.Stdout);
        Assert.Contains("database=<null>", result.Stdout);
        Assert.Equal(1, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task PromptableLast_DoesNotPromptInNonInteractiveMode()
    {
        var app = App<ConditionalLastCommand>();
        var shell = new TestShell();

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("authentication=Integrated", result.Stdout);
        Assert.Contains("database=<null>", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task RequiredWhenDependencyOrdering_WorksAcrossPromptableBuckets()
    {
        var app = App<ConditionalLastCommand>();
        var shell = new TestShell();
        EnqueueSelect(shell, index: 1);
        EnqueueText(shell, "main");

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("authentication=SqlPassword", result.Stdout);
        Assert.Contains("database=main", result.Stdout);
    }

    [Fact]
    public async Task ProviderDependsOn_PromptsLastProviderAfterSourceValues()
    {
        var calls = 0;
        var app = DatabaseApp(ctx =>
        {
            calls++;
            Assert.True(ctx.TryGetValue<string>("--server", out var server));
            Assert.True(ctx.TryGetValue<AuthenticationType>("--authentication", out var authentication));
            Assert.True(ctx.TryGetValue<bool>("--trust-server-certificate", out var trust));
            Assert.Equal("sql.example", server);
            Assert.Equal(AuthenticationType.SqlPassword, authentication);
            Assert.True(trust);
            return ["main"];
        });
        var shell = new TestShell();
        EnqueueSelect(shell, index: 1);
        EnqueueText(shell, "sql.example");
        EnqueueText(shell, "secret");
        // index 1 skips the synthetic no-selection row of this optional nullable provider field.
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(
            app,
            ["--trust-server-certificate"],
            shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, calls);
        Assert.Contains("database=main", result.Stdout);
    }

    [Fact]
    public async Task ProviderDependsOn_IsOrderingHintNotRequirement()
    {
        var calls = 0;
        var app = App<ProviderHintOptionalCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add(
                "database",
                ctx =>
                {
                    calls++;
                    Assert.False(ctx.TryGetValue<string>("--server", out _));
                    return (IReadOnlyList<string>)["main"];
                },
                dependsOn: ["--server"])));
        var shell = new TestShell();
        // index 1 skips the synthetic no-selection row of this optional nullable provider field.
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, calls);
        Assert.Contains("server=<null>", result.Stdout);
        Assert.Contains("database=main", result.Stdout);
    }

    [Fact]
    public async Task DependsOnOption_AffectsProviderOrdering()
    {
        var calls = 0;
        var app = App<ExplicitDependsOnOptionCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("database", ctx =>
            {
                calls++;
                Assert.True(ctx.TryGetValue<string>("--server", out var server));
                Assert.Equal("sql.example", server);
                return (IReadOnlyList<string>)["main"];
            })));
        var shell = new TestShell();
        EnqueueText(shell, "sql.example");
        // index 1 skips the synthetic no-selection row of this optional nullable provider field.
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, calls);
        Assert.Contains("server=sql.example", result.Stdout);
        Assert.Contains("database=main", result.Stdout);
    }

    [Fact]
    public async Task DependsOnOptions_AffectsProviderOrderingForMultipleDependencies()
    {
        var calls = 0;
        var app = App<ExplicitDependsOnOptionsCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("database", ctx =>
            {
                calls++;
                Assert.True(ctx.TryGetValue<string>("--server", out var server));
                Assert.True(ctx.TryGetValue<AuthenticationType>("--authentication", out var authentication));
                Assert.True(ctx.TryGetValue<string>("--encrypt", out var encrypt));
                Assert.Equal("sql.example", server);
                Assert.Equal(AuthenticationType.SqlPassword, authentication);
                Assert.Equal("yes", encrypt);
                return (IReadOnlyList<string>)["main"];
            })));
        var shell = new TestShell();
        EnqueueText(shell, "sql.example");
        EnqueueSelect(shell, index: 1);
        EnqueueText(shell, "yes");
        // index 1 skips the synthetic no-selection row of this optional nullable provider field.
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, calls);
        Assert.Contains("server=sql.example", result.Stdout);
        Assert.Contains("authentication=SqlPassword", result.Stdout);
        Assert.Contains("encrypt=yes", result.Stdout);
        Assert.Contains("database=main", result.Stdout);
    }

    [Fact]
    public async Task RequiredWhenOption_StillImpliesDependencyWithoutExplicitDependsOn()
    {
        var calls = 0;
        var app = App<RequiredWhenImplicitDependencyCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("database", ctx =>
            {
                calls++;
                Assert.True(ctx.TryGetValue<AuthenticationType>("--authentication", out var authentication));
                Assert.Equal(AuthenticationType.SqlPassword, authentication);
                return (IReadOnlyList<string>)["main"];
            })));
        var shell = new TestShell();
        EnqueueSelect(shell, index: 1);
        EnqueueSelect(shell, index: 0);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, calls);
        Assert.Contains("authentication=SqlPassword", result.Stdout);
        Assert.Contains("database=main", result.Stdout);
    }

    [Fact]
    public async Task PromptWhenOption_StillImpliesDependencyWithoutExplicitDependsOn()
    {
        var calls = 0;
        var app = App<PromptWhenImplicitDependencyCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("database", ctx =>
            {
                calls++;
                Assert.True(ctx.TryGetValue<AuthenticationType>("--authentication", out var authentication));
                Assert.Equal(AuthenticationType.SqlPassword, authentication);
                return (IReadOnlyList<string>)["main"];
            })));
        var shell = new TestShell();
        EnqueueSelect(shell, index: 1);
        // index 1 skips the synthetic no-selection row of this optional nullable provider field.
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, calls);
        Assert.Contains("authentication=SqlPassword", result.Stdout);
        Assert.Contains("database=main", result.Stdout);
    }

    [Fact]
    public async Task ExplicitDependencies_AreDeduplicatedWithImplicitDependencies()
    {
        var calls = 0;
        var app = App<DeduplicatedDependencyCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("database", ctx =>
            {
                calls++;
                Assert.True(ctx.TryGetValue<AuthenticationType>("--authentication", out var authentication));
                Assert.Equal(AuthenticationType.SqlPassword, authentication);
                return (IReadOnlyList<string>)["main"];
            })));
        var shell = new TestShell();
        EnqueueSelect(shell, index: 1);
        // index 1 skips the synthetic no-selection row of this optional nullable provider field.
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, calls);
        Assert.Contains("authentication=SqlPassword", result.Stdout);
        Assert.Contains("database=main", result.Stdout);
    }

    [Fact]
    public async Task ExplicitDependency_DoesNotMakeDependencyRequired()
    {
        var calls = 0;
        var app = App<ExplicitDependencyOptionalTargetCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("database", ctx =>
            {
                calls++;
                Assert.False(ctx.TryGetValue<string>("--server", out _));
                return (IReadOnlyList<string>)["main"];
            })));
        var shell = new TestShell();
        // index 1 skips the synthetic no-selection row of this optional nullable provider field.
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, calls);
        Assert.Contains("server=<null>", result.Stdout);
        Assert.Contains("database=main", result.Stdout);
    }

    [Fact]
    public async Task ExplicitDependency_DoesNotMakeTargetPromptable()
    {
        var calls = 0;
        var app = App<ExplicitDependencyOptionalTargetCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("database", ctx =>
            {
                calls++;
                Assert.False(ctx.TryGetValue<string>("--server", out _));
                return (IReadOnlyList<string>)["main"];
            })));
        var shell = new TestShell();
        // index 1 skips the synthetic no-selection row of this optional nullable provider field.
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, calls);
        // Two keystrokes (DownArrow + Enter) for the single database prompt: one to move off the
        // no-selection row, one to confirm. Only the database field is prompted (server is not).
        Assert.Equal(2, shell.Terminal.ReadCount);
        Assert.Contains("server=<null>", result.Stdout);
        Assert.Contains("database=main", result.Stdout);
    }

    [Fact]
    public async Task DependsOnOptions_CycleDetectionStillWorks()
    {
        var app = App<DependsOnCycleCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var shell = new TestShell();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Cyclic option prompt dependency detected", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ProviderContext_ReadsAlreadyBoundArgvValuesTypeSafely()
    {
        var app = DatabaseApp(ctx =>
        {
            Assert.True(ctx.TryGetValue<string>("--server", out var server));
            Assert.True(ctx.TryGetValue<AuthenticationType>("--authentication", out var authentication));
            Assert.Equal("argv-server", server);
            Assert.Equal(AuthenticationType.SqlPassword, authentication);
            return ["main"];
        });
        var shell = new TestShell();
        EnqueueText(shell, "secret");
        // index 1 skips the synthetic no-selection row of this optional nullable provider field.
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(
            app,
            ["--server", "argv-server", "--authentication", "SqlPassword"],
            shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=main", result.Stdout);
    }

    [Fact]
    public async Task SecretPromptedValue_IsAvailableToProviderContextButNotRenderedByProvider()
    {
        string? providerPassword = null;
        var app = DatabaseApp(ctx =>
        {
            Assert.True(ctx.TryGetValue<string>("--password", out providerPassword));
            return ["main"];
        });
        var shell = new TestShell();
        EnqueueSelect(shell, index: 1);
        EnqueueText(shell, "server");
        EnqueueText(shell, "secret");
        // index 1 skips the synthetic no-selection row of this optional nullable provider field.
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("secret", providerPassword);
        Assert.Contains("database=main", result.Stdout);
        Assert.DoesNotContain("secret", result.Stdout);
    }

    [Fact]
    public async Task Provider_IsNotCalledWhenPromptableNo()
    {
        var calls = 0;
        var app = App<ProviderNoCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("database", _ =>
            {
                calls++;
                return (IReadOnlyList<string>)["main"];
            })));

        var result = await RunCapturedAsync(app, [], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, calls);
        Assert.Contains("database=<null>", result.Stdout);
    }

    [Fact]
    public async Task Provider_IsNotCalledWhenPromptWhenFalse()
    {
        var calls = 0;
        var app = App<ConditionalLastCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("database", _ =>
            {
                calls++;
                return (IReadOnlyList<string>)["main"];
            })));
        var shell = new TestShell();
        EnqueueSelect(shell, index: 0);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, calls);
        Assert.Contains("database=<null>", result.Stdout);
    }

    [Fact]
    public async Task CommandGroupFactory_WorksWithPromptableLastAndProviderDependsOn()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("promptable-test")
            .AddCommandGroup("connections", group =>
            {
                group.AddProvider(
                    "database",
                    _ => (IReadOnlyList<string>)["main"],
                    dependsOn: ["--server"]);
                group.AddCommand("add", () => new DatabaseCommand());
            })
            .Build();
        var shell = new TestShell();
        EnqueueSelect(shell, index: 0);
        EnqueueText(shell, "server");
        // index 1 skips the synthetic no-selection row of this optional nullable provider field.
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(app, ["connections", "add"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("server=server", result.Stdout);
        Assert.Contains("database=main", result.Stdout);
    }

    private static TigerCliApp DatabaseApp(Func<TigerCliProviderContext, IReadOnlyList<string>> provider)
    {
        return App<DatabaseCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add(
                "database",
                provider,
                dependsOn:
                [
                    "--server",
                    "--authentication",
                    "--password",
                    "--trust-server-certificate"
                ])));
    }

    private static TigerCliApp App<TCommand>(Action<TigerCliAppBuilder>? configure = null)
        where TCommand : class, new()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("promptable-test")
            .SetDefaultCommand<TCommand>();

        configure?.Invoke(builder);
        return builder.Build();
    }

    private static TestShell ShellWithText(string value)
    {
        var shell = new TestShell();
        EnqueueText(shell, value);
        return shell;
    }

    private static void EnqueueText(TestShell shell, string value)
    {
        foreach (var ch in value)
        {
            var key = char.IsLetter(ch)
                ? Enum.Parse<ConsoleKey>(char.ToUpperInvariant(ch).ToString())
                : ConsoleKey.Spacebar;
            shell.Terminal.EnqueueKey(key, keyChar: ch);
        }
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
    }

    private static void EnqueueSelect(TestShell shell, int index)
    {
        for (var i = 0; i < index; i++)
            shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
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
