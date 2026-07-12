using System.Globalization;
using System.Resources;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliProviderEmptyMessageTests
{
    private sealed class FakeResources : ResourceManager
    {
        private readonly Dictionary<string, string> _values;

        public FakeResources(Dictionary<string, string> values)
            : base("FakeResources", typeof(FakeResources).Assembly)
            => _values = values;

        public override string? GetString(string name, CultureInfo? culture)
            => _values.TryGetValue(name, out var value) ? value : null;

        public override string? GetString(string name) => GetString(name, CultureInfo.CurrentUICulture);
    }

    private sealed class RequiredProviderSettings : TigerCliSettings
    {
        [TigerCliOption("--connection", Required = true, Provider = "connections", Description = "Connection")]
        public string Connection { get; set; } = string.Empty;
    }

    private sealed class OptionalNullableProviderSettings : TigerCliSettings
    {
        [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string? Database { get; set; }
    }

    private sealed class AutoSelectOptionalNullableProviderSettings : TigerCliSettings
    {
        [TigerCliOption(
            "--database",
            Provider = "databases",
            Promptable = TigerCliPromptable.Normal,
            AutoSelectSingleChoice = true,
            Description = "Database")]
        public string? Database { get; set; }
    }

    private sealed class RequiredProviderCommand : TigerCliAsyncCommandHandler<RequiredProviderSettings>
    {
        public override Task<int> ExecuteAsync(RequiredProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Connection));
            return Task.FromResult(0);
        }
    }

    private sealed class OptionalNullableProviderCommand :
        TigerCliAsyncCommandHandler<OptionalNullableProviderSettings>
    {
        public override Task<int> ExecuteAsync(OptionalNullableProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Database ?? "<null>"));
            return Task.FromResult(0);
        }
    }

    private sealed class AutoSelectOptionalNullableProviderCommand :
        TigerCliAsyncCommandHandler<AutoSelectOptionalNullableProviderSettings>
    {
        public override Task<int> ExecuteAsync(AutoSelectOptionalNullableProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Database ?? "<null>"));
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task RequiredNoChoices_WithoutCustomMessage_UsesGenericMessage()
    {
        var shell = new TestShell();
        var app = RequiredApp(providerConfigure: null);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("No prompt choices available for --connection.", result.Stderr);
    }

    [Fact]
    public async Task RequiredNoChoices_WithCustomLiteralMessage_UsesCustomMessage()
    {
        var shell = new TestShell();
        var app = RequiredApp(options => options.EmptyMessage("No destination groups are configured."));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("No destination groups are configured.", result.Stderr);
        Assert.DoesNotContain("No prompt choices available", result.Stderr);
    }

    [Fact]
    public async Task RequiredNoChoices_WithResourceMessage_ResolvesAppResource()
    {
        var shell = new TestShell();
        var resources = new FakeResources(new()
        {
            ["NoDestinationGroups"] = "No localized destination groups."
        });
        var app = RequiredApp(
            options => options.EmptyMessageResource(
                "NoDestinationGroups",
                fallback: "No destination groups are configured."),
            resources);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("No localized destination groups.", result.Stderr);
        Assert.DoesNotContain("NoDestinationGroups", result.Stderr);
        Assert.DoesNotContain("No destination groups are configured.", result.Stderr);
    }

    [Fact]
    public async Task RequiredNoChoices_WithMissingResourceMessage_FallsBackToLiteral()
    {
        var shell = new TestShell();
        var app = RequiredApp(options => options.EmptyMessageResource(
            "Missing_Key",
            fallback: "No destination groups are configured."));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("No destination groups are configured.", result.Stderr);
        Assert.DoesNotContain("Missing_Key", result.Stderr);
    }

    [Fact]
    public async Task OptionalNullableNoChoices_DoesNotUseCustomEmptyMessage()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = OptionalNullableApp<OptionalNullableProviderCommand>(
            "databases",
            options => options.EmptyMessage("No databases are configured."));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("<null>", result.Stdout);
        Assert.Contains("(None)", shell.Terminal.LastRenderedText);
        Assert.DoesNotContain("No databases are configured.", result.Stderr);
    }

    [Fact]
    public async Task OptionalNullableNoChoices_WithAutoSelectNone_DoesNotUseCustomEmptyMessage()
    {
        var shell = new TestShell();
        var app = OptionalNullableApp<AutoSelectOptionalNullableProviderCommand>(
            "databases",
            options => options.EmptyMessage("No databases are configured."));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("<null>", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
        Assert.DoesNotContain("No databases are configured.", result.Stderr);
    }

    [Fact]
    public async Task ProviderFailure_DoesNotUseCustomEmptyMessage()
    {
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-empty-test")
            .SetDefaultCommand<RequiredProviderCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.UnhandledException, 46)
            .ConfigureProviders(providers =>
                providers.Add(
                    "connections",
                    (Func<TigerCliProviderContext, IReadOnlyList<string>>)
                        (_ => throw new InvalidOperationException("source unavailable")),
                    configure: options => options.EmptyMessage("No destination groups are configured.")))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("source unavailable", result.Stderr);
        Assert.DoesNotContain("No destination groups are configured.", result.Stderr);
    }

    [Fact]
    public async Task ProviderCancellation_DoesNotUseCustomEmptyMessage()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-empty-test")
            .SetDefaultCommand<RequiredProviderCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 46)
            .ConfigureProviders(providers =>
                providers.AddAsync(
                    "connections",
                    async context =>
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
                        return Array.Empty<string>();
                    },
                    configure: options => options.EmptyMessage("No destination groups are configured.")))
            .Build();

        var result = await RunCapturedWithCancellationAsync(app, [], shell, cts.Token);

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("Cancelled.", result.Stderr);
        Assert.DoesNotContain("No destination groups are configured.", result.Stderr);
    }

    [Fact]
    public async Task NonInteractiveMissingRequired_DoesNotInvokeProviderForCustomEmptyMessage()
    {
        var called = false;
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-empty-test")
            .SetDefaultCommand<RequiredProviderCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers =>
                providers.Add(
                    "connections",
                    _ =>
                    {
                        called = true;
                        return Array.Empty<string>();
                    },
                    configure: options => options.EmptyMessage("No destination groups are configured.")))
            .Build();

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.False(called);
        Assert.Contains("Missing required option: --connection", result.Stderr);
        Assert.DoesNotContain("No destination groups are configured.", result.Stderr);
    }

    private static TigerCliApp RequiredApp(
        Action<TigerCliProviderOptions>? providerConfigure,
        ResourceManager? resources = null)
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-empty-test")
            .SetDefaultCommand<RequiredProviderCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers =>
                providers.Add("connections", _ => Array.Empty<string>(), configure: providerConfigure));

        if (resources != null)
            builder.UseAppResources(resources);

        return builder.Build();
    }

    private static TigerCliApp OptionalNullableApp<TCommand>(
        string providerKey,
        Action<TigerCliProviderOptions>? providerConfigure)
        where TCommand : class, new()
    {
        return TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-empty-test")
            .SetDefaultCommand<TCommand>()
            .ConfigureProviders(providers =>
                providers.Add(providerKey, _ => Array.Empty<string>(), configure: providerConfigure))
            .Build();
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args,
        TestShell shell)
    {
        return await RunCapturedWithCancellationAsync(
            app,
            args,
            shell,
            TestContext.Current.CancellationToken);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedWithCancellationAsync(
        TigerCliApp app,
        string[] args,
        TestShell shell,
        CancellationToken ct)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await app.RunAsync(args, shell, ct: ct);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
