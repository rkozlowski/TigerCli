using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Deliberate provider failure (<see cref="TigerCliProviderException"/>): the provider's message is
/// reported for the field, the run maps through <see cref="TigerCliExitKind.ProviderError"/>, and
/// the failure is never confused with empty choices, unexpected faults, or cancellation.
/// </summary>
public sealed class TigerCliProviderFailureTests
{
    private sealed class ConnectionSettings : TigerCliSettings
    {
        [TigerCliOption("--connection", Provider = "connections", Description = "Connection")]
        public string Connection { get; set; } = "";
    }

    private sealed class ConnectionEcho : TigerCliAsyncCommandHandler<ConnectionSettings>
    {
        public override Task<int> ExecuteAsync(ConnectionSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"value={settings.Connection}"));
            return Task.FromResult(0);
        }
    }

    private static TigerCliApp BuildApp(
        Func<TigerCliProviderContext, IReadOnlyList<OptionItem<string>>> provider,
        Action<TigerCliAppBuilder>? configure = null)
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("provider-failure-test")
            .SetDefaultCommand<ConnectionEcho>()
            .ConfigureProviders(providers => providers.Add("connections", provider));
        configure?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public async Task SuppliedValue_ProviderException_ReportsMessage_NotEmptyChoices()
    {
        var app = BuildApp(_ => throw new TigerCliProviderException("Schema version 12 is not supported."));

        var result = await RunAsync(app, ["--connection", "Local", "--non-interactive"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Cannot load choices for --connection: Schema version 12 is not supported.", result.Stderr);
        // A deliberate provider failure must not read like "nothing to choose from".
        Assert.DoesNotContain("No prompt choices available", result.Stderr);
        Assert.DoesNotContain("is not an available choice", result.Stderr);
    }

    [Fact]
    public async Task ProviderException_MapsThroughProviderErrorKind()
    {
        var app = BuildApp(
            _ => throw new TigerCliProviderException("Backend incompatible."),
            builder => builder
                .UseExitCodes(0, 1)
                .ExitKind(TigerCliExitKind.ProviderError, 42));

        var result = await RunAsync(app, ["--connection", "Local", "--non-interactive"]);

        Assert.Equal(42, result.ExitCode);
    }

    [Fact]
    public async Task ProviderException_MapsThroughExecutionCategory()
    {
        var app = BuildApp(
            _ => throw new TigerCliProviderException("Backend incompatible."),
            builder => builder
                .UseExitCodes(0, 1)
                .ExitCategory(TigerCliExitCategory.Execution, 21));

        var result = await RunAsync(app, ["--connection", "Local", "--non-interactive"]);

        Assert.Equal(21, result.ExitCode);
    }

    [Fact]
    public async Task GenericProviderException_StillMapsToUnhandledException()
    {
        var app = BuildApp(
            _ => throw new InvalidOperationException("boom"),
            builder => builder
                .UseExitCodes(0, 1)
                .ExitKind(TigerCliExitKind.ProviderError, 42)
                .ExitKind(TigerCliExitKind.UnhandledException, 70));

        var result = await RunAsync(app, ["--connection", "Local", "--non-interactive"]);

        // An arbitrary exception is an unexpected fault, not a deliberate provider failure.
        Assert.Equal(70, result.ExitCode);
        Assert.Contains("Prompt value provider failed for --connection", result.Stderr);
    }

    [Fact]
    public async Task ProviderCancellation_RemainsCancellation_NotProviderFailure()
    {
        // Validation-time provider cancellation propagates as a genuine cancellation (documented on
        // TigerCliProviderContext.CancellationToken); it must never be rewritten into a provider
        // failure, even when the app maps ProviderError explicitly.
        using var cts = new CancellationTokenSource();
        var app = BuildApp(
            context =>
            {
                cts.Cancel();
                context.CancellationToken.ThrowIfCancellationRequested();
                return [];
            },
            builder => builder
                .UseExitCodes(0, 1)
                .ExitKind(TigerCliExitKind.ProviderError, 42));

        var shell = new TestShell();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                app.RunAsync(["--connection", "Local", "--non-interactive"], shell, ct: cts.Token));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        Assert.DoesNotContain("Cannot load choices", stderr.ToString());
    }

    [Fact]
    public void ProviderException_RequiresMessage()
    {
        Assert.Throws<ArgumentException>(() => new TigerCliProviderException(" "));
        Assert.Throws<ArgumentException>(() => new TigerCliProviderException(" ", new InvalidOperationException()));
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        TigerCliApp app,
        string[] args)
    {
        var shell = new TestShell();
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
