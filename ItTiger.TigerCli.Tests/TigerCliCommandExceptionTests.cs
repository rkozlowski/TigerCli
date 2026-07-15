using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Exceptions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Classified command failures (<see cref="TigerCliCommandException"/>): a handler — typically a
/// reusable command library — expresses failure meaning with <see cref="TigerCliExitKind"/> and an
/// optional stable error id, while the app keeps ownership of the numeric exit codes through its
/// exit-code policy.
/// </summary>
public sealed class TigerCliCommandExceptionTests
{
    private sealed class EmptySettings : TigerCliSettings
    {
    }

    private sealed class ThrowingCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public static TigerCliCommandException? ToThrow;

        public override Task<int> ExecuteAsync(EmptySettings settings) =>
            throw ToThrow!;
    }

    private static TigerCliApp BuildApp(Action<TigerCliAppBuilder>? configure = null)
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("command-exception-test")
            .SetDefaultCommand<ThrowingCommand>();
        configure?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public async Task GenericFail_Default_ReportsMessage_AndMapsThroughPolicy()
    {
        ThrowingCommand.ToThrow = new TigerCliCommandException("Target folder is read-only.");
        var app = BuildApp(builder => builder
            .UseExitCodes(0, 1)
            .ExitKind(TigerCliExitKind.GenericFail, 13));

        var result = await RunAsync(app, ["--non-interactive"]);

        Assert.Equal(13, result.ExitCode);
        Assert.Contains("Target folder is read-only.", result.Stderr);
    }

    [Fact]
    public async Task ErrorId_IsAppendedToReportedMessage()
    {
        ThrowingCommand.ToThrow = new TigerCliCommandException(
            "Query timed out.", TigerCliExitKind.GenericFail, errorId: "TQ0007");
        var app = BuildApp(builder => builder.UseExitCodes(0, 1));

        var result = await RunAsync(app, ["--non-interactive"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Query timed out. (TQ0007)", result.Stderr);
    }

    [Fact]
    public async Task ValidationKind_MapsThroughValidationCategory()
    {
        ThrowingCommand.ToThrow = new TigerCliCommandException(
            "Filter expression references an unknown column.", TigerCliExitKind.ValidationError);
        var app = BuildApp(builder => builder
            .UseExitCodes(0, 1)
            .ExitCategory(TigerCliExitCategory.Validation, 8)
            .ExitKind(TigerCliExitKind.UnhandledException, 70));

        var result = await RunAsync(app, ["--non-interactive"]);

        // The classified kind resolves through the policy — not the UnhandledException mapping.
        Assert.Equal(8, result.ExitCode);
    }

    [Fact]
    public void SuccessAndCancellationKinds_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TigerCliCommandException("x", TigerCliExitKind.Success));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TigerCliCommandException("x", TigerCliExitKind.HelpShown));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TigerCliCommandException("x", TigerCliExitKind.Cancelled));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TigerCliCommandException("x", (TigerCliExitKind)999));
        Assert.Throws<ArgumentException>(() =>
            new TigerCliCommandException(" "));
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
