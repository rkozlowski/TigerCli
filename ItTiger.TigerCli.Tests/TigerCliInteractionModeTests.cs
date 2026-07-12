using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliInteractionModeTests
{
    private sealed class ModeSettings : TigerCliSettings
    {
        [TigerCliOption("--value")]
        public string Value { get; set; } = "default";

        [TigerCliOption("--non-interactive")]
        public bool NonInteractiveOption { get; set; }
    }

    private sealed class ModeCommand : TigerCliAsyncCommandHandler<ModeSettings>
    {
        public override Task<int> ExecuteAsync(ModeSettings settings)
        {
            TigerConsole.MarkupLine(
                $"{CliMarkupParser.Escape(settings.InteractionMode.ToString())}:{settings.NonInteractiveOption}:{CliMarkupParser.Escape(settings.Value)}");
            return Task.FromResult(0);
        }
    }

    private sealed class OtherModeCommand : TigerCliAsyncCommandHandler<ModeSettings>
    {
        public override Task<int> ExecuteAsync(ModeSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.InteractionMode.ToString()));
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task DefaultInteractionMode_IsSemiInteractive()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .AddCommand<ModeCommand>("mode")
            .Build();

        var result = await RunCapturedAsync(app, ["mode"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SemiInteractive:False:default", result.Stdout);
    }

    [Fact]
    public async Task AppLevelNonInteractiveMode_IsApplied()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetInteractionMode(TigerCliInteractionMode.NonInteractive)
            .AddCommand<ModeCommand>("mode")
            .Build();

        var result = await RunCapturedAsync(app, ["mode"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NonInteractive:False:default", result.Stdout);
    }

    [Fact]
    public async Task CommandLevelInteractionMode_OverridesAppLevelMode()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetInteractionMode(TigerCliInteractionMode.NonInteractive)
            .SetCommandInteractionMode<ModeCommand>(TigerCliInteractionMode.SemiInteractive)
            .AddCommand<ModeCommand>("mode")
            .Build();

        var result = await RunCapturedAsync(app, ["mode"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SemiInteractive:False:default", result.Stdout);
    }

    [Fact]
    public async Task NonInteractiveFlag_DowngradesSemiInteractiveToNonInteractive()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .AddCommand<ModeCommand>("mode")
            .Build();

        var result = await RunCapturedAsync(app, ["mode", "--non-interactive"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NonInteractive:False:default", result.Stdout);
    }

    [Fact]
    public async Task NonInteractiveFlag_OnAlreadyNonInteractive_StaysNonInteractive()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetInteractionMode(TigerCliInteractionMode.NonInteractive)
            .AddCommand<ModeCommand>("mode")
            .Build();

        var result = await RunCapturedAsync(app, ["mode", "--non-interactive"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NonInteractive:False:default", result.Stdout);
    }

    [Fact]
    public async Task NonInteractiveFlag_AgainstFullInteractive_FailsWithInteractiveNotAllowed()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetInteractionMode(TigerCliInteractionMode.FullInteractive)
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.InteractiveNotAllowed, 45)
            .AddCommand<ModeCommand>("mode")
            .Build();

        var result = await RunCapturedAsync(app, ["mode", "--non-interactive"]);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("--non-interactive cannot be used with FullInteractive commands.", result.Stderr);
        Assert.Equal(string.Empty, result.Stdout);
    }

    [Fact]
    public async Task NonInteractiveFlag_IsNotPassedToCommandSettingsOrUnknownOption()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .AddCommand<ModeCommand>("mode")
            .Build();

        var result = await RunCapturedAsync(app, ["mode", "--non-interactive", "--value", "x"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NonInteractive:False:x", result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public async Task Help_WorksWithNonInteractiveFlag()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetInteractionMode(TigerCliInteractionMode.FullInteractive)
            .AddCommand<ModeCommand>("mode")
            .Build();

        var result = await RunCapturedAsync(app, ["mode", "--help", "--non-interactive"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.Stdout);
        Assert.Contains("--non-interactive", result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public async Task ExitCodePolicy_MapsInteractiveNotAllowed()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("test")
            .SetCommandInteractionMode<ModeCommand>(TigerCliInteractionMode.FullInteractive)
            .SetCommandInteractionMode<OtherModeCommand>(TigerCliInteractionMode.SemiInteractive)
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.InteractiveNotAllowed, 123)
            .AddCommand<ModeCommand>("mode")
            .AddCommand<OtherModeCommand>("other")
            .Build();

        var result = await RunCapturedAsync(app, ["mode", "--non-interactive"]);

        Assert.Equal(123, result.ExitCode);
    }

    [Fact]
    public async Task InlineShell_ReturnsInteractionNotAllowedWithoutRenderingOrReading()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(interactionMode: TigerCliInteractionMode.NonInteractive);
        var select = new InlineSelect(shell, ["Yes", "No"]);
        var dialog = new InlineDialog(shell, "Pick one", select);

        var result = await shell.RunModalAsync(dialog, ct);

        Assert.Equal(DialogResultKind.InteractionNotAllowed, result.Kind);
        Assert.Null(result.Payload);
        Assert.Equal(0, shell.Terminal.RenderCount);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task TigerTuiPromptHelpers_ReturnNullOnInteractionNotAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        var shell = new TestShell(interactionMode: TigerCliInteractionMode.NonInteractive);

        var selectResult = await TigerTui.SelectIndexAsync(shell, "Pick one", ["Yes", "No"], ct: ct);
        var confirmResult = await TigerTui.ConfirmAsync(shell, "Continue?", ct: ct);
        var inputResult = await TigerTui.InputAsync(shell, "Name", ct: ct);

        Assert.Null(selectResult);
        Assert.Null(confirmResult);
        Assert.Null(inputResult);
        Assert.Equal(0, shell.Terminal.RenderCount);
        Assert.Equal(0, shell.Terminal.ReadCount);
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
