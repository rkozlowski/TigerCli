using System.ComponentModel;
using ItTiger.TigerCli.Commands;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliExitCodeTests
{
    [Description("Tool response codes")]
    private enum ToolExitCode
    {
        [Description("Operation completed successfully.")]
        Ok = 0,

        [Description("Invalid command-line arguments.")]
        InvalidArguments = 1002,

        [Description("Validation failed.")]
        ValidationError = 1004,

        [Description("Unhandled CLI exception.")]
        UnhandledException = 1005,

        [Description("Internal error.")]
        InternalError = 2000
    }

    [Description("Command response codes")]
    private enum CommandExitCode
    {
        [Description("Command completed successfully.")]
        Ok = 0,

        [Description("Command-specific failure.")]
        CommandFailed = 11
    }

    private sealed class EmptySettings : TigerCliSettings
    {
    }

    private sealed class RequiredSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true)]
        public string? Name { get; set; }
    }

    private sealed class RawIntCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings) => Task.FromResult(42);
    }

    private sealed class RequiredRawIntCommand : TigerCliAsyncCommandHandler<RequiredSettings>
    {
        public override Task<int> ExecuteAsync(RequiredSettings settings) => Task.FromResult(0);
    }

    private sealed class EnumCommand : TigerCliAsyncCommandHandler<EmptySettings, ToolExitCode>
    {
        public override Task<ToolExitCode> ExecuteAsync(EmptySettings settings) => Task.FromResult(ToolExitCode.InvalidArguments);
    }

    private sealed class CommandSpecificEnumCommand : TigerCliAsyncCommandHandler<EmptySettings, CommandExitCode>
    {
        public override Task<CommandExitCode> ExecuteAsync(EmptySettings settings) => Task.FromResult(CommandExitCode.CommandFailed);
    }

    [Fact]
    public async Task DefaultPolicy_UsesSuccessHelpAndGenericFailDefaults()
    {
        var noCommandApp = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .Build();

        var noCommand = await RunCapturedAsync(noCommandApp, []);
        Assert.Equal(-1, noCommand.ExitCode);

        var help = await RunCapturedAsync(noCommandApp, ["--help"]);
        Assert.Equal(0, help.ExitCode);

        var validationApp = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<RequiredRawIntCommand>()
            .Build();

        var validation = await RunCapturedAsync(validationApp, ["--non-interactive"]);
        Assert.Equal(-1, validation.ExitCode);
    }

    [Fact]
    public async Task RawIntPolicy_MapsFrameworkExitKinds()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<RequiredRawIntCommand>()
            .UseExitCodes(0, 9)
                .ExitKind(TigerCliExitKind.HelpShown, 3)
                .ExitKind(TigerCliExitKind.ValidationError, 8)
            .Build();

        var help = await RunCapturedAsync(app, ["--help"]);
        Assert.Equal(3, help.ExitCode);

        var validation = await RunCapturedAsync(app, ["--non-interactive"]);
        Assert.Equal(8, validation.ExitCode);
    }

    [Fact]
    public async Task EnumPolicy_MapsFrameworkExitKindsToUnderlyingInt()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<RawIntCommand>()
            .UseExitCodes<ToolExitCode>(ToolExitCode.Ok, ToolExitCode.InternalError)
                .ExitKind(TigerCliExitKind.InvalidArguments, ToolExitCode.InvalidArguments)
                .ExitKind(TigerCliExitKind.UnhandledException, ToolExitCode.UnhandledException)
            .Build();

        var result = await RunCapturedAsync(app, ["--unknown"]);

        Assert.Equal(1002, result.ExitCode);
    }

    [Fact]
    public async Task EnumCommandHandler_ReturnsUnderlyingIntExitCode()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<EnumCommand>()
            .Build();

        var result = await RunCapturedAsync(app, []);

        Assert.Equal(1002, result.ExitCode);
    }

    [Fact]
    public async Task RawIntCommandHandler_RemainsSupported()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<RawIntCommand>()
            .Build();

        var result = await RunCapturedAsync(app, []);

        Assert.Equal(42, result.ExitCode);
    }

    [Fact]
    public async Task HelpErrors_PrintsDocumentedGlobalEnumAndReturnsHelpCode()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseExitCodes<ToolExitCode>(ToolExitCode.Ok, ToolExitCode.InternalError)
            .Build();

        var result = await RunCapturedAsync(app, ["--help-errors"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Exit codes:", result.Stdout);
        Assert.Contains("Tool response codes", result.Stdout);
        Assert.Contains("1002", result.Stdout);
        Assert.Contains("InvalidArguments", result.Stdout);
        Assert.Contains("Invalid command-line arguments.", result.Stdout);
    }

    [Fact]
    public async Task HelpErrors_CommandSpecificEnumOverridesGlobalEnum()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseExitCodes<ToolExitCode>(ToolExitCode.Ok, ToolExitCode.InternalError)
            .AddCommand<CommandSpecificEnumCommand>("run")
            .Build();

        var result = await RunCapturedAsync(app, ["run", "--help-errors"]);

        Assert.Contains("Command response codes", result.Stdout);
        Assert.Contains("CommandFailed", result.Stdout);
        Assert.DoesNotContain("Tool response codes", result.Stdout);
        Assert.DoesNotContain("InvalidArguments", result.Stdout);
    }

    [Fact]
    public async Task FrameworkFailures_FallBackToConfiguredGenericFailWhenSpecificKindIsUnmapped()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<RequiredRawIntCommand>()
            .UseExitCodes(0, -9)
            .Build();

        var result = await RunCapturedAsync(app, ["--non-interactive"]);

        Assert.Equal(-9, result.ExitCode);
    }

    [Fact]
    public async Task HelpErrors_WithoutDocumentedEnumPrintsClearMessageAndReturnsHelpCode()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.HelpShown, 33)
            .Build();

        var result = await RunCapturedAsync(app, ["--help-errors"]);

        Assert.Equal(33, result.ExitCode);
        Assert.Contains("No documented exit-code enum is configured.", result.Stdout);
    }

    [Fact]
    public async Task Help_ShowsExitCodeHintOnlyWhenDocumentedEnumIsAvailable()
    {
        var rawApp = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .Build();

        var rawHelp = await RunCapturedAsync(rawApp, ["--help"]);
        Assert.DoesNotContain("For a list of exit codes, use --help-errors.", rawHelp.Stdout);

        var enumApp = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseExitCodes<ToolExitCode>(ToolExitCode.Ok, ToolExitCode.InternalError)
            .Build();

        var enumHelp = await RunCapturedAsync(enumApp, ["--help"]);
        Assert.Contains("For a list of exit codes, use --help-errors.", enumHelp.Stdout);
    }

    [Fact]
    public async Task HelpAndHelpErrors_PrintsNormalHelpThenExitCodeHelp()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .UseExitCodes<ToolExitCode>(ToolExitCode.Ok, ToolExitCode.InternalError)
            .Build();

        var result = await RunCapturedAsync(app, ["--help", "--help-errors"]);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Stdout.IndexOf("Usage:", StringComparison.Ordinal) <
            result.Stdout.IndexOf("Exit codes:", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // Layered exit model: outcome baseline -> category -> range -> kind.
    // These exercise the internal policy directly so the resolution matrix is
    // covered without driving a full app run.
    // ---------------------------------------------------------------------

    private enum WrapExitCode
    {
        Ok = 0,
        Error = 1,
        UsageError = 2,
        NoCommand = 9,
        RangeStart = 200
    }

    [Fact]
    public void ExitKind_DeclaredOrderIsLocked()
    {
        // Range(...) walks kinds in ascending value order, so these values are contract.
        Assert.Equal(0, (int)TigerCliExitKind.Success);
        Assert.Equal(1, (int)TigerCliExitKind.HelpShown);
        Assert.Equal(2, (int)TigerCliExitKind.GenericFail);
        Assert.Equal(3, (int)TigerCliExitKind.InvalidArguments);
        Assert.Equal(4, (int)TigerCliExitKind.MissingRequiredArgument);
        Assert.Equal(5, (int)TigerCliExitKind.ValidationError);
        Assert.Equal(6, (int)TigerCliExitKind.InteractiveNotAllowed);
        Assert.Equal(7, (int)TigerCliExitKind.NoCommand);
        Assert.Equal(8, (int)TigerCliExitKind.UnhandledException);
        Assert.Equal(9, (int)TigerCliExitKind.Cancelled);
        Assert.Equal(10, (int)TigerCliExitKind.ProviderError);
    }

    [Fact]
    public void CancelledKind_RollsUpToCancelledCategory_AndErrorOutcomeBaselineByDefault()
    {
        var policy = new TigerCliExitCodePolicy(0, 1);

        // With only the baseline configured, Cancelled falls to the error outcome — it is an Error
        // outcome, just its own category (never Usage or Validation).
        Assert.Equal(1, policy.Resolve(TigerCliExitKind.Cancelled));
    }

    [Fact]
    public void CancelledCategory_IsIndependentOfUsageAndValidation()
    {
        var policy = new TigerCliExitCodePolicy(0, 1);
        policy.SetCategory(TigerCliExitCategory.Usage, 7);
        policy.SetCategory(TigerCliExitCategory.Validation, 8);

        // Mapping Usage/Validation must not affect Cancelled: it is a distinct category.
        Assert.Equal(1, policy.Resolve(TigerCliExitKind.Cancelled));

        policy.SetCategory(TigerCliExitCategory.Cancelled, 42);
        Assert.Equal(42, policy.Resolve(TigerCliExitKind.Cancelled));
        // The category override leaves the other kinds' mappings untouched.
        Assert.Equal(7, policy.Resolve(TigerCliExitKind.InvalidArguments));
        Assert.Equal(8, policy.Resolve(TigerCliExitKind.ValidationError));
    }

    [Fact]
    public void CancelledCategory_CanBeMappedToSuccessForNeutralCancellation()
    {
        // An app that wants Escape to be neutral maps the Cancelled category onto the success code.
        var policy = new TigerCliExitCodePolicy(0, 1);
        policy.SetCategory(TigerCliExitCategory.Cancelled, 0);

        Assert.Equal(0, policy.Resolve(TigerCliExitKind.Cancelled));
    }

    [Fact]
    public void Baseline_MapsSuccessOutcomesToSuccessAndErrorOutcomesToError()
    {
        var policy = new TigerCliExitCodePolicy(0, 1);

        Assert.Equal(0, policy.Resolve(TigerCliExitKind.Success));
        Assert.Equal(0, policy.Resolve(TigerCliExitKind.HelpShown));

        Assert.Equal(1, policy.Resolve(TigerCliExitKind.InvalidArguments));    // Usage
        Assert.Equal(1, policy.Resolve(TigerCliExitKind.ValidationError));     // Validation
        Assert.Equal(1, policy.Resolve(TigerCliExitKind.GenericFail));         // Execution
        Assert.Equal(1, policy.Resolve(TigerCliExitKind.UnhandledException));  // Unexpected
    }

    [Fact]
    public void Category_OverridesOutcomeBaseline()
    {
        var policy = new TigerCliExitCodePolicy(0, 1);
        policy.SetCategory(TigerCliExitCategory.Usage, 7);

        Assert.Equal(7, policy.Resolve(TigerCliExitKind.InvalidArguments));
        Assert.Equal(7, policy.Resolve(TigerCliExitKind.NoCommand));
        // Unmapped categories still fall back to the outcome baseline.
        Assert.Equal(1, policy.Resolve(TigerCliExitKind.ValidationError));
        Assert.Equal(1, policy.Resolve(TigerCliExitKind.GenericFail));
        Assert.Equal(0, policy.Resolve(TigerCliExitKind.Success));
    }

    [Fact]
    public void Kind_OverridesCategory()
    {
        var policy = new TigerCliExitCodePolicy(0, 1);
        policy.SetCategory(TigerCliExitCategory.Usage, 7);
        policy.SetKind(TigerCliExitKind.NoCommand, 9);

        Assert.Equal(9, policy.Resolve(TigerCliExitKind.NoCommand));
        Assert.Equal(7, policy.Resolve(TigerCliExitKind.InvalidArguments));
    }

    [Fact]
    public void Range_OverridesCategory_AndMapsConsecutively()
    {
        var policy = new TigerCliExitCodePolicy(0, 1);
        policy.SetCategory(TigerCliExitCategory.Usage, 7);
        policy.SetRange(TigerCliExitKind.InvalidArguments, TigerCliExitKind.NoCommand, 100);

        Assert.Equal(100, policy.Resolve(TigerCliExitKind.InvalidArguments));
        Assert.Equal(101, policy.Resolve(TigerCliExitKind.MissingRequiredArgument));
        Assert.Equal(102, policy.Resolve(TigerCliExitKind.ValidationError));
        Assert.Equal(103, policy.Resolve(TigerCliExitKind.InteractiveNotAllowed));
        Assert.Equal(104, policy.Resolve(TigerCliExitKind.NoCommand));
    }

    [Fact]
    public void Kind_OverridesRange_RegardlessOfConfigurationOrder()
    {
        var policy = new TigerCliExitCodePolicy(0, 1);
        // Kind set before the range that also covers it; precedence is by layer, not call order.
        policy.SetKind(TigerCliExitKind.ValidationError, 500);
        policy.SetRange(TigerCliExitKind.InvalidArguments, TigerCliExitKind.NoCommand, 100);

        Assert.Equal(500, policy.Resolve(TigerCliExitKind.ValidationError));
        Assert.Equal(100, policy.Resolve(TigerCliExitKind.InvalidArguments));
    }

    [Fact]
    public void SuccessKinds_UseSuccessBaselineUnlessOverridden()
    {
        var policy = new TigerCliExitCodePolicy(0, 1);
        Assert.Equal(0, policy.Resolve(TigerCliExitKind.HelpShown));

        policy.SetKind(TigerCliExitKind.HelpShown, 3);
        Assert.Equal(3, policy.Resolve(TigerCliExitKind.HelpShown));
        Assert.Equal(0, policy.Resolve(TigerCliExitKind.Success));

        var categoryPolicy = new TigerCliExitCodePolicy(0, 1);
        categoryPolicy.SetCategory(TigerCliExitCategory.Success, 5);
        Assert.Equal(5, categoryPolicy.Resolve(TigerCliExitKind.Success));
        Assert.Equal(5, categoryPolicy.Resolve(TigerCliExitKind.HelpShown));
    }

    [Fact]
    public void Range_IsBoundedByExplicitStartAndEnd()
    {
        var policy = new TigerCliExitCodePolicy(0, 1);
        policy.SetRange(TigerCliExitKind.InvalidArguments, TigerCliExitKind.ValidationError, 100);

        Assert.Equal(100, policy.Resolve(TigerCliExitKind.InvalidArguments));
        Assert.Equal(101, policy.Resolve(TigerCliExitKind.MissingRequiredArgument));
        Assert.Equal(102, policy.Resolve(TigerCliExitKind.ValidationError));

        // Kinds outside the explicit band are untouched (fall back to baseline).
        Assert.Equal(1, policy.Resolve(TigerCliExitKind.GenericFail));          // value 2, below start
        Assert.Equal(1, policy.Resolve(TigerCliExitKind.InteractiveNotAllowed));// value 6, above end
        Assert.Equal(1, policy.Resolve(TigerCliExitKind.NoCommand));            // value 7, above end
    }

    [Fact]
    public void Range_RejectsStartAfterEnd()
    {
        var policy = new TigerCliExitCodePolicy(0, 1);
        Assert.Throws<ArgumentException>(() =>
            policy.SetRange(TigerCliExitKind.NoCommand, TigerCliExitKind.InvalidArguments, 100));
    }

    [Fact]
    public async Task LayeredBuilder_ResolvesCategoryAndBaselineThroughPublicApi()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<RequiredRawIntCommand>()
            .UseExitCodes<WrapExitCode>(WrapExitCode.Ok, WrapExitCode.Error)
            .ExitCategory(TigerCliExitCategory.Usage, WrapExitCode.UsageError)
            .Build();

        // Invalid argument -> Usage category override.
        var invalid = await RunCapturedAsync(app, ["--unknown"]);
        Assert.Equal((int)WrapExitCode.UsageError, invalid.ExitCode);

        // Help -> Success outcome baseline.
        var help = await RunCapturedAsync(app, ["--help"]);
        Assert.Equal((int)WrapExitCode.Ok, help.ExitCode);

        // Validation is unmapped here -> error outcome baseline.
        var validation = await RunCapturedAsync(app, ["--non-interactive"]);
        Assert.Equal((int)WrapExitCode.Error, validation.ExitCode);
    }

    [Fact]
    public async Task LayeredBuilder_RangeThenKindResolveThroughPublicApi()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<RequiredRawIntCommand>()
            .UseExitCodes<WrapExitCode>(WrapExitCode.Ok, WrapExitCode.Error)
            .ExitRange(TigerCliExitKind.InvalidArguments, TigerCliExitKind.NoCommand, WrapExitCode.RangeStart)
            .ExitKind(TigerCliExitKind.InvalidArguments, WrapExitCode.UsageError)
            .Build();

        // InvalidArguments has an explicit Kind override that beats the range.
        var invalid = await RunCapturedAsync(app, ["--unknown"]);
        Assert.Equal((int)WrapExitCode.UsageError, invalid.ExitCode);

        // ValidationError sits at offset 2 within the range and has no kind override.
        var validation = await RunCapturedAsync(app, ["--non-interactive"]);
        Assert.Equal((int)WrapExitCode.RangeStart + 2, validation.ExitCode);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args)
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
