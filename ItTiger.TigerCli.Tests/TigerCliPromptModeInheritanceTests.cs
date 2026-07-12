using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliPromptModeInheritanceTests
{
    private sealed class RequiredStringSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class OptionalStringSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Description = "Name")]
        public string Name { get; set; } = "default";
    }

    private sealed class RequiredNotPromptableSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Promptable = TigerCliPromptable.No, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ProviderSettings : TigerCliSettings
    {
        [TigerCliOption("--connection", Required = true, Description = "Connection")]
        public string ConnectionName { get; set; } = string.Empty;
    }

    private sealed class RequiredStringCommand : TigerCliAsyncCommandHandler<RequiredStringSettings>
    {
        public override Task<int> ExecuteAsync(RequiredStringSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class OptionalStringCommand : TigerCliAsyncCommandHandler<OptionalStringSettings>
    {
        public override Task<int> ExecuteAsync(OptionalStringSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class RequiredNotPromptableCommand :
        TigerCliAsyncCommandHandler<RequiredNotPromptableSettings>
    {
        public override Task<int> ExecuteAsync(RequiredNotPromptableSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderCommand : TigerCliAsyncCommandHandler<ProviderSettings>
    {
        public override Task<int> ExecuteAsync(ProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.ConnectionName));
            return Task.FromResult(0);
        }
    }

    private sealed class FactoryRequiredCommand : TigerCliAsyncCommandHandler<RequiredStringSettings>
    {
        private readonly string _prefix;

        public FactoryRequiredCommand(string prefix)
        {
            _prefix = prefix;
        }

        public override Task<int> ExecuteAsync(RequiredStringSettings settings)
        {
            TigerConsole.MarkupLine($"{CliMarkupParser.Escape(_prefix)}:{CliMarkupParser.Escape(settings.Name)}");
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task AppLevelPromptMode_StillPromptsFromDefaultMode()
    {
        var shell = ShellWithText("app");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("prompt-inheritance-test")
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .SetDefaultCommand<OptionalStringCommand>()
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("app", result.Stdout);
        Assert.True(shell.Terminal.ReadCount > 0);
    }

    [Fact]
    public async Task GroupLevelPromptMode_OverridesAppLevelPromptMode()
    {
        var shell = ShellWithText("group");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("prompt-inheritance-test")
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .AddCommandGroup("tools", group =>
            {
                group.SetPromptMode(TigerCliPromptMode.RequiredOnly);
                group.AddCommand<RequiredStringCommand>("run");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["tools", "run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("group", result.Stdout);
    }

    [Fact]
    public async Task CommandLevelPromptMode_OverridesGroupLevelPromptMode()
    {
        var shell = ShellWithText("group");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("prompt-inheritance-test")
            .AddCommandGroup("tools", group =>
            {
                group.SetPromptMode(TigerCliPromptMode.Yes);
                group.AddCommand<OptionalStringCommand>(
                    "run",
                    command => command.SetPromptMode(TigerCliPromptMode.RequiredOnly));
            })
            .Build();

        var result = await RunCapturedAsync(app, ["tools", "run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("default", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task CommandLevelPromptMode_OverridesAppLevelPromptModeForUngroupedCommand()
    {
        var shell = ShellWithText("command");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("prompt-inheritance-test")
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .AddCommand<RequiredStringCommand>(
                "run",
                command => command.SetPromptMode(TigerCliPromptMode.RequiredOnly))
            .Build();

        var result = await RunCapturedAsync(app, ["run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("command", result.Stdout);
    }

    [Fact]
    public async Task PromptableFalse_PreventsPromptingRegardlessOfGroupPromptMode()
    {
        var shell = ShellWithText("blocked");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("prompt-inheritance-test")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .AddCommandGroup("tools", group =>
            {
                group.SetPromptMode(TigerCliPromptMode.Yes);
                group.AddCommand<RequiredNotPromptableCommand>("run");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["tools", "run"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --name", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task NonInteractive_PreventsPromptingRegardlessOfGroupOrCommandPromptMode()
    {
        var shell = ShellWithText("blocked");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("prompt-inheritance-test")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .AddCommandGroup("tools", group =>
            {
                group.SetPromptMode(TigerCliPromptMode.Yes);
                group.AddCommand<RequiredStringCommand>(
                    "run",
                    command => command.SetPromptMode(TigerCliPromptMode.RequiredOnly));
            })
            .Build();

        var result = await RunCapturedAsync(app, ["tools", "run", "--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --name", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task ProviderBackedPrompt_UsesEffectiveGroupPromptMode()
    {
        var called = false;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("prompt-inheritance-test")
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .ConfigurePrompts<ProviderSettings>(prompts =>
                prompts.For(s => s.ConnectionName).SelectFrom((_, _) =>
                {
                    called = true;
                    return [new OptionItem<string>("local", "Local connection")];
                }))
            .AddCommandGroup("tools", group =>
            {
                group.SetPromptMode(TigerCliPromptMode.RequiredOnly);
                group.AddCommand<ProviderCommand>("run");
            })
            .Build();

        var result = await RunCapturedAsync(app, ["tools", "run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.True(called);
        Assert.Contains("local", result.Stdout);
    }

    [Fact]
    public async Task CommandFactoryRegistration_ReceivesEffectiveCommandPromptMode()
    {
        var shell = ShellWithText("factory");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("prompt-inheritance-test")
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .AddCommandGroup("tools", group =>
            {
                group.SetPromptMode(TigerCliPromptMode.No);
                group.AddCommand(
                    "run",
                    () => new FactoryRequiredCommand("made"),
                    command => command.SetPromptMode(TigerCliPromptMode.RequiredOnly));
            })
            .Build();

        var result = await RunCapturedAsync(app, ["tools", "run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("made:factory", result.Stdout);
    }

    [Fact]
    public async Task CommandFactoryRegistration_InheritsGroupPromptMode()
    {
        var shell = ShellWithText("group-factory");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("prompt-inheritance-test")
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .AddCommandGroup("tools", group =>
            {
                group.SetPromptMode(TigerCliPromptMode.RequiredOnly);
                group.AddCommand(
                    "run",
                    () => new FactoryRequiredCommand("made"));
            })
            .Build();

        var result = await RunCapturedAsync(app, ["tools", "run"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("made:group-factory", result.Stdout);
    }

    [Fact]
    public async Task GroupHelpAndParsing_RemainUnchangedWhenPromptModeIsConfigured()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("prompt-inheritance-test")
            .AddCommandGroup("tools", group =>
            {
                group.SetDescription("Tool commands");
                group.SetPromptMode(TigerCliPromptMode.Yes);
                group.AddCommand<RequiredStringCommand>("run", "Runs the tool.");
            })
            .Build();

        var help = await RunCapturedAsync(app, ["--help"], new TestShell());
        var leafHelp = await RunCapturedAsync(app, ["tools", "run", "--help"], new TestShell());

        Assert.Equal(0, help.ExitCode);
        Assert.Contains("tools", help.Stdout);
        Assert.Contains("Tool commands", help.Stdout);
        Assert.DoesNotContain("tools run", help.Stdout);
        Assert.Equal(0, leafHelp.ExitCode);
        Assert.Contains("prompt-inheritance-test tools run [options]", leafHelp.Stdout);
    }

    private static TestShell ShellWithText(string value)
    {
        var shell = new TestShell();
        foreach (var ch in value)
        {
            var key = char.IsLetter(ch)
                ? Enum.Parse<ConsoleKey>(char.ToUpperInvariant(ch).ToString())
                : ConsoleKey.Spacebar;
            shell.Terminal.EnqueueKey(key, keyChar: ch);
        }
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        return shell;
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
