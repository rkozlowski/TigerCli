using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliPromptingTests
{
    private enum PromptColor
    {
        Red,
        Green,
        Blue
    }

    [Flags]
    private enum PromptFeatures
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4,
        All = Read | Write | Execute
    }

    private sealed class RequiredStringSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class RequiredArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "name", Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class RequiredIntArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "count", Description = "Count")]
        public int Count { get; set; }
    }

    private sealed class ProviderKeyAndIntSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "key", Provider = "keys", Description = "Key")]
        public string Key { get; set; } = string.Empty;

        [TigerCliArgument(1, Name = "count", Description = "Count")]
        public int Count { get; set; }
    }

    private sealed class MinIntArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "count", Description = "Count", MinValue = 5)]
        public int Count { get; set; }
    }

    private sealed class MaxIntArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "count", Description = "Count", MaxValue = 4)]
        public int Count { get; set; }
    }

    private sealed class RangeIntArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "count", Description = "Count", MinValue = 2, MaxValue = 4)]
        public int Count { get; set; }
    }

    private sealed class ProviderMinIntArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "count", Description = "Count", MinValueProvider = "min-count")]
        public int Count { get; set; }
    }

    private sealed class ProviderMaxIntArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "count", Description = "Count", MaxValueProvider = "max-count")]
        public int Count { get; set; }
    }

    private sealed class ProviderRangeIntArgumentSettings : TigerCliSettings
    {
        [TigerCliArgument(
            0,
            Name = "count",
            Description = "Count",
            MinValueProvider = "min-count",
            MaxValueProvider = "max-count")]
        public int Count { get; set; }
    }

    private sealed class RequiredServerSettings : TigerCliSettings
    {
        [TigerCliOption("--server", Required = true, Description = "Server")]
        public string Server { get; set; } = string.Empty;
    }

    private sealed class MinLengthStringSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, MinLength = 3, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class MaxLengthStringSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, MaxLength = 3, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class SecretMinLengthSettings : TigerCliSettings
    {
        [TigerCliOption("--secret", Required = true, Secret = true, MinLength = 5, Description = "Secret")]
        public string Secret { get; set; } = string.Empty;
    }

    private sealed class OptionalStringSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Description = "Name")]
        public string Name { get; set; } = "default";
    }

    private sealed class OptionalPromptableSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Promptable = TigerCliPromptable.Normal, Description = "Name")]
        public string Name { get; set; } = "default";
    }

    private sealed class RequiredNotPromptableSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Promptable = TigerCliPromptable.No, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ArgumentAndOptionSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "item", Description = "Item")]
        public string Item { get; set; } = string.Empty;

        [TigerCliOption("--name", Required = true, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class EnumSettings : TigerCliSettings
    {
        [TigerCliOption("--color", Required = true, Description = "Color")]
        public PromptColor Color { get; set; }
    }

    private sealed class FlagsSettings : TigerCliSettings
    {
        [TigerCliOption("--features", Required = true, Description = "Features")]
        public PromptFeatures Features { get; set; }
    }

    private sealed class DefaultFlagsSettings : TigerCliSettings
    {
        [TigerCliOption("--features", Promptable = TigerCliPromptable.Normal, Description = "Features")]
        public PromptFeatures Features { get; set; } = PromptFeatures.All;
    }

    private sealed class NullableBoolSettings : TigerCliSettings
    {
        [TigerCliOption("--enabled", Required = true, Description = "Enabled")]
        public bool? Enabled { get; set; }
    }

    private sealed class DefaultEnumSettings : TigerCliSettings
    {
        [TigerCliOption("--color", Promptable = TigerCliPromptable.Normal, Description = "Color")]
        public PromptColor Color { get; set; } = PromptColor.Blue;
    }

    private sealed class DefaultBoolSettings : TigerCliSettings
    {
        [TigerCliOption("--enabled", Promptable = TigerCliPromptable.Normal, Description = "Enabled")]
        public bool? Enabled { get; set; } = false;
    }

    private sealed class ValidatingSettings : TigerCliSettings
    {
        [TigerCliOption("--name", Required = true, Description = "Name")]
        public string Name { get; set; } = string.Empty;

        public override TigerCliValidationResult Validate()
        {
            return Name == "good"
                ? TigerCliValidationResult.Success()
                : TigerCliValidationResult.Error("Name must be good.");
        }
    }

    private sealed class RequiredStringCommand : TigerCliAsyncCommandHandler<RequiredStringSettings>
    {
        public override Task<int> ExecuteAsync(RequiredStringSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class RequiredArgumentCommand : TigerCliAsyncCommandHandler<RequiredArgumentSettings>
    {
        public override Task<int> ExecuteAsync(RequiredArgumentSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class RequiredIntArgumentCommand : TigerCliAsyncCommandHandler<RequiredIntArgumentSettings>
    {
        public override Task<int> ExecuteAsync(RequiredIntArgumentSettings settings)
        {
            TigerConsole.MarkupLine(settings.Count.ToString());
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderKeyAndIntCommand : TigerCliAsyncCommandHandler<ProviderKeyAndIntSettings>
    {
        public override Task<int> ExecuteAsync(ProviderKeyAndIntSettings settings)
        {
            TigerConsole.MarkupLine($"{CliMarkupParser.Escape(settings.Key)}:{settings.Count}");
            return Task.FromResult(0);
        }
    }

    private sealed class MinIntArgumentCommand : TigerCliAsyncCommandHandler<MinIntArgumentSettings>
    {
        public override Task<int> ExecuteAsync(MinIntArgumentSettings settings)
        {
            TigerConsole.MarkupLine(settings.Count.ToString());
            return Task.FromResult(0);
        }
    }

    private sealed class MaxIntArgumentCommand : TigerCliAsyncCommandHandler<MaxIntArgumentSettings>
    {
        public override Task<int> ExecuteAsync(MaxIntArgumentSettings settings)
        {
            TigerConsole.MarkupLine(settings.Count.ToString());
            return Task.FromResult(0);
        }
    }

    private sealed class RangeIntArgumentCommand : TigerCliAsyncCommandHandler<RangeIntArgumentSettings>
    {
        public override Task<int> ExecuteAsync(RangeIntArgumentSettings settings)
        {
            TigerConsole.MarkupLine(settings.Count.ToString());
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderMinIntArgumentCommand :
        TigerCliAsyncCommandHandler<ProviderMinIntArgumentSettings>
    {
        public override Task<int> ExecuteAsync(ProviderMinIntArgumentSettings settings)
        {
            TigerConsole.MarkupLine(settings.Count.ToString());
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderMaxIntArgumentCommand :
        TigerCliAsyncCommandHandler<ProviderMaxIntArgumentSettings>
    {
        public override Task<int> ExecuteAsync(ProviderMaxIntArgumentSettings settings)
        {
            TigerConsole.MarkupLine(settings.Count.ToString());
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderRangeIntArgumentCommand :
        TigerCliAsyncCommandHandler<ProviderRangeIntArgumentSettings>
    {
        public override Task<int> ExecuteAsync(ProviderRangeIntArgumentSettings settings)
        {
            TigerConsole.MarkupLine(settings.Count.ToString());
            return Task.FromResult(0);
        }
    }

    private sealed class RequiredServerCommand : TigerCliAsyncCommandHandler<RequiredServerSettings>
    {
        public override Task<int> ExecuteAsync(RequiredServerSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Server));
            return Task.FromResult(0);
        }
    }

    private sealed class MinLengthStringCommand : TigerCliAsyncCommandHandler<MinLengthStringSettings>
    {
        public override Task<int> ExecuteAsync(MinLengthStringSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class MaxLengthStringCommand : TigerCliAsyncCommandHandler<MaxLengthStringSettings>
    {
        public override Task<int> ExecuteAsync(MaxLengthStringSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class SecretMinLengthCommand : TigerCliAsyncCommandHandler<SecretMinLengthSettings>
    {
        public override Task<int> ExecuteAsync(SecretMinLengthSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Secret));
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

    private sealed class OptionalPromptableCommand : TigerCliAsyncCommandHandler<OptionalPromptableSettings>
    {
        public override Task<int> ExecuteAsync(OptionalPromptableSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class RequiredNotPromptableCommand : TigerCliAsyncCommandHandler<RequiredNotPromptableSettings>
    {
        public override Task<int> ExecuteAsync(RequiredNotPromptableSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    private sealed class ArgumentAndOptionCommand : TigerCliAsyncCommandHandler<ArgumentAndOptionSettings>
    {
        public override Task<int> ExecuteAsync(ArgumentAndOptionSettings settings)
        {
            TigerConsole.MarkupLine(
                $"item={CliMarkupParser.Escape(settings.Item)};name={CliMarkupParser.Escape(settings.Name)}");
            return Task.FromResult(0);
        }
    }

    private sealed class EnumCommand : TigerCliAsyncCommandHandler<EnumSettings>
    {
        public override Task<int> ExecuteAsync(EnumSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Color.ToString()));
            return Task.FromResult(0);
        }
    }

    private sealed class FlagsCommand : TigerCliAsyncCommandHandler<FlagsSettings>
    {
        public override Task<int> ExecuteAsync(FlagsSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Features.ToString()));
            return Task.FromResult(0);
        }
    }

    private sealed class DefaultFlagsCommand : TigerCliAsyncCommandHandler<DefaultFlagsSettings>
    {
        public override Task<int> ExecuteAsync(DefaultFlagsSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Features.ToString()));
            return Task.FromResult(0);
        }
    }

    private sealed class NullableBoolCommand : TigerCliAsyncCommandHandler<NullableBoolSettings>
    {
        public override Task<int> ExecuteAsync(NullableBoolSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Enabled.ToString() ?? string.Empty));
            return Task.FromResult(0);
        }
    }

    private sealed class DefaultEnumCommand : TigerCliAsyncCommandHandler<DefaultEnumSettings>
    {
        public override Task<int> ExecuteAsync(DefaultEnumSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Color.ToString()));
            return Task.FromResult(0);
        }
    }

    private sealed class DefaultBoolCommand : TigerCliAsyncCommandHandler<DefaultBoolSettings>
    {
        public override Task<int> ExecuteAsync(DefaultBoolSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Enabled.ToString() ?? string.Empty));
            return Task.FromResult(0);
        }
    }

    private sealed class ValidatingCommand : TigerCliAsyncCommandHandler<ValidatingSettings>
    {
        public override Task<int> ExecuteAsync(ValidatingSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(settings.Name));
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task FrameworkDefaultRequiredOnly_PromptsMissingRequiredOption()
    {
        var shell = ShellWithText("riley");
        var app = App<RequiredStringCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("riley", result.Stdout);
        Assert.True(shell.Terminal.ReadCount > 0);
    }

    [Fact]
    public async Task AppLevelPromptModeNo_DoesNotPromptRequiredOption()
    {
        var shell = ShellWithText("riley");
        var app = App<RequiredStringCommand>(builder => builder
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --name", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task CommandLevelPromptMode_OverridesAppLevelPromptMode()
    {
        var shell = ShellWithText("riley");
        var app = App<RequiredStringCommand>(builder => builder
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .SetCommandPromptMode<RequiredStringCommand>(TigerCliPromptMode.RequiredOnly));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("riley", result.Stdout);
    }

    [Fact]
    public async Task PromptableTrue_PromptsOptionalOptionUnderRequiredOnly()
    {
        var shell = ShellWithText("prompted");
        var app = App<OptionalPromptableCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("prompted", result.Stdout);
    }

    [Fact]
    public async Task PromptableFalse_DoesNotPromptRequiredOption()
    {
        var shell = ShellWithText("riley");
        var app = App<RequiredNotPromptableCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --name", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task MissingPositionalArgument_IsPromptedBeforeRequiredOptions()
    {
        var shell = new TestShell();
        EnqueueText(shell, "positional");
        EnqueueText(shell, "option");
        var app = App<ArgumentAndOptionCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("item=positional;name=option", result.Stdout);
    }

    [Fact]
    public async Task OptionalOption_IsNotPromptedUnderRequiredOnly()
    {
        var shell = ShellWithText("prompted");
        var app = App<OptionalStringCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("default", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task OptionalOption_IsPromptedUnderPromptModeYes()
    {
        var shell = ShellWithText("prompted");
        var app = App<OptionalStringCommand>(builder => builder.SetDefaultPromptMode(TigerCliPromptMode.Yes));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("prompted", result.Stdout);
    }

    [Fact]
    public async Task StringPrompt_BindsPromptValue()
    {
        var shell = ShellWithText("bound");
        var app = App<RequiredStringCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("bound", result.Stdout);
    }

    [Fact]
    public async Task MissingRequiredIntArgument_PromptModeYes_Prompts()
    {
        var shell = ShellWithText("12");
        var app = App<RequiredIntArgumentCommand>(builder =>
            builder.SetDefaultPromptMode(TigerCliPromptMode.Yes));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("12", result.Stdout);
        Assert.True(shell.Terminal.ReadCount > 0);
    }

    [Fact]
    public async Task ProviderBackedStringArgument_FollowedByIntArgument_PromptsBothAndExecutes()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        EnqueueText(shell, "7", pressEnter: true);
        var app = App<ProviderKeyAndIntCommand>(builder => builder
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .ConfigureProviders(providers => providers.Add("keys", _ => new[] { "alpha" })));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("alpha:7", result.Stdout);
    }

    [Fact]
    public async Task IntPrompt_BindsValidInput()
    {
        var shell = ShellWithText("42");
        var app = App<RequiredIntArgumentCommand>(builder =>
            builder.SetDefaultPromptMode(TigerCliPromptMode.Yes));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("42", result.Stdout);
    }

    [Fact]
    public async Task IntPrompt_NonIntegerInput_ShowsValidationAndStaysOpen()
    {
        var app = App<RequiredIntArgumentCommand>(builder =>
            builder.SetDefaultPromptMode(TigerCliPromptMode.Yes));
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                EnqueueText(shell, "abc", pressEnter: true);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("Enter a valid integer.", shell.Terminal.LastRenderedText);

                shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
                shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
                shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
                EnqueueText(shell, "12", pressEnter: true);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("12", stdout);
    }

    [Fact]
    public async Task IntPrompt_MinValue_RejectsValuesBelowMinimum()
    {
        var app = App<MinIntArgumentCommand>(builder =>
            builder.SetDefaultPromptMode(TigerCliPromptMode.Yes));
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                EnqueueText(shell, "4", pressEnter: true);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("Value must be at least 5.", shell.Terminal.LastRenderedText);

                shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
                EnqueueText(shell, "5", pressEnter: true);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("5", stdout);
    }

    [Fact]
    public async Task IntPrompt_MaxValue_RejectsValuesAboveMaximum()
    {
        var app = App<MaxIntArgumentCommand>(builder =>
            builder.SetDefaultPromptMode(TigerCliPromptMode.Yes));
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                EnqueueText(shell, "5", pressEnter: true);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("Value must be at most 4.", shell.Terminal.LastRenderedText);

                shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
                EnqueueText(shell, "4", pressEnter: true);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("4", stdout);
    }

    [Fact]
    public async Task IntPrompt_MinValueAndMaxValue_AcceptsOnlyRange()
    {
        var app = App<RangeIntArgumentCommand>(builder =>
            builder.SetDefaultPromptMode(TigerCliPromptMode.Yes));
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                EnqueueText(shell, "1", pressEnter: true);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("Value must be between 2 and 4.", shell.Terminal.LastRenderedText);

                shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
                EnqueueText(shell, "3", pressEnter: true);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("3", stdout);
    }

    [Fact]
    public async Task IntPrompt_MinValueProvider_RejectsValuesBelowProviderMinimum()
    {
        var app = App<ProviderMinIntArgumentCommand>(builder => builder
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .ConfigureProviders(providers => providers.Add("min-count", _ => new[] { "3" })));
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                EnqueueText(shell, "2", pressEnter: true);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("Value must be at least 3.", shell.Terminal.LastRenderedText);

                shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
                EnqueueText(shell, "3", pressEnter: true);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("3", stdout);
    }

    [Fact]
    public async Task IntPrompt_MaxValueProvider_RejectsValuesAboveProviderMaximum()
    {
        var app = App<ProviderMaxIntArgumentCommand>(builder => builder
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .ConfigureProviders(providers => providers.Add<int>("max-count", _ =>
                [new OptionItem<int>(6, "Six")])));
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                EnqueueText(shell, "7", pressEnter: true);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("Value must be at most 6.", shell.Terminal.LastRenderedText);

                shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
                EnqueueText(shell, "6", pressEnter: true);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("6", stdout);
    }

    [Fact]
    public async Task IntPrompt_MinAndMaxValueProviders_AcceptsOnlyProviderRange()
    {
        var app = App<ProviderRangeIntArgumentCommand>(builder => builder
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .ConfigureProviders(providers =>
            {
                providers.Add("min-count", _ => new[] { "2" });
                providers.Add("max-count", _ => new[] { "5" });
            }));
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                EnqueueText(shell, "6", pressEnter: true);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("Value must be between 2 and 5.", shell.Terminal.LastRenderedText);

                shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
                EnqueueText(shell, "5", pressEnter: true);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("5", stdout);
    }

    [Fact]
    public async Task IntPrompt_InvalidProviderValue_ReportsConfigurationError()
    {
        var shell = new TestShell();
        var app = App<ProviderMinIntArgumentCommand>(builder => builder
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers => providers.Add("min-count", _ => new[] { "not-an-int" })));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("provider 'min-count' returned 'not-an-int', which is not a valid integer", result.Stderr);
    }

    [Fact]
    public async Task MissingRequiredIntArgument_PromptModeNo_ReportsMissingArgument()
    {
        var shell = ShellWithText("12");
        var app = App<RequiredIntArgumentCommand>(builder => builder
            .SetDefaultPromptMode(TigerCliPromptMode.No)
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.MissingRequiredArgument, 45));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required argument: <count>", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task EnumPrompt_BindsSelectedEnumValue()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<EnumCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Green", result.Stdout);
    }

    [Fact]
    public async Task FlagsEnumPrompt_BindsSelectedFlags()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<FlagsCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Read, Write", result.Stdout);
        Assert.Contains("+ All; - None; * Invert", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task FlagsEnumCommandLineParsing_AcceptsCommaSeparatedValues()
    {
        var shell = new TestShell();
        var app = App<FlagsCommand>();

        var result = await RunCapturedAsync(app, ["--features", "Read,Write"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Read, Write", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task PromptableFlagsEnum_NonInteractiveKeepsDefaultValue()
    {
        var shell = new TestShell();
        var app = App<DefaultFlagsCommand>();

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("All", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task AddModeEnumPrompt_PreselectsInitializerDefault()
    {
        // Mirrors the tiger-sqlcmd dogfood case: an enum option with an initializer default
        // (Blue) must preselect that default, not the first enum member (Red).
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the preselected default
        var app = App<DefaultEnumCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Blue", result.Stdout);
    }

    [Fact]
    public async Task AddModeBoolPrompt_PreselectsInitializerDefault()
    {
        // Initializer default is false; accepting the preselected choice must yield False,
        // not the first ("yes") option that an un-seeded confirm would highlight.
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the preselected default
        var app = App<DefaultBoolCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("False", result.Stdout);
    }

    [Fact]
    public async Task AddModeFlagsPrompt_PreselectsInitializerDefault()
    {
        // Initializer default is All; accepting the preselected flags must yield All,
        // not the empty (None) selection of an un-seeded flags prompt.
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the preselected flags
        var app = App<DefaultFlagsCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("All", result.Stdout);
    }

    [Fact]
    public async Task AddModeStringPrompt_UsesInitializerDefaultAsInitialValue()
    {
        // Initializer default is "default"; accepting the prompt without typing must keep it,
        // proving the input is seeded with the default rather than starting empty.
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the prefilled default
        var app = App<OptionalPromptableCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("default", result.Stdout);
    }

    [Fact]
    public async Task AddModeEnumPrompt_CliValueWinsAndIsNotPrompted()
    {
        var shell = new TestShell();
        var app = App<DefaultEnumCommand>();

        var result = await RunCapturedAsync(app, ["--color", "Green"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Green", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task NullableBoolPrompt_BindsConfirmValue()
    {
        var shell = new TestShell();
        // The confirm prompt is a Yes/No message box; Right moves Yes -> No.
        shell.Terminal.EnqueueKey(ConsoleKey.RightArrow);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<NullableBoolCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("False", result.Stdout);
    }

    [Fact]
    public async Task TextPrompt_MinLengthPreventsEnterUntilValid()
    {
        var shell = new TestShell();
        EnqueueText(shell, "ab", pressEnter: true);
        EnqueueText(shell, "c", pressEnter: true);
        var app = App<MinLengthStringCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("abc", result.Stdout);
    }

    [Fact]
    public async Task TextPrompt_MinLengthHint_IsFieldNeutral()
    {
        var app = App<MinLengthStringCommand>();
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                EnqueueText(shell, "ab", pressEnter: true);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("Enter at least 3 characters.", shell.Terminal.LastRenderedText);
                Assert.DoesNotContain("--name must be at least", shell.Terminal.LastRenderedText);
                Assert.DoesNotContain("<name> must be at least", shell.Terminal.LastRenderedText);

                EnqueueText(shell, "c", pressEnter: true);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("abc", stdout);
    }

    [Fact]
    public async Task TextPrompt_MaxLengthPreventsEnterUntilFixed()
    {
        var shell = new TestShell();
        EnqueueText(shell, "abcd", pressEnter: true);
        shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<MaxLengthStringCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("abc", result.Stdout);
    }

    [Fact]
    public async Task TextPrompt_MaxLengthHint_IsFieldNeutral()
    {
        var app = App<MaxLengthStringCommand>();
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                EnqueueText(shell, "abcd", pressEnter: true);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("Enter no more than 3 characters.", shell.Terminal.LastRenderedText);
                Assert.DoesNotContain("--name must be at most", shell.Terminal.LastRenderedText);
                Assert.DoesNotContain("<name> must be at most", shell.Terminal.LastRenderedText);

                shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
                shell.Terminal.EnqueueKey(ConsoleKey.Enter);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("abc", stdout);
    }

    [Fact]
    public async Task TextPrompt_RequiredPreventsEmptyConfirmation()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        EnqueueText(shell, "r", pressEnter: true);
        var app = App<RequiredStringCommand>();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("r", result.Stdout);
    }

    [Fact]
    public async Task TextPrompt_RequiredOptionHint_IsFieldNeutral()
    {
        var app = App<RequiredServerCommand>();
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                shell.Terminal.EnqueueKey(ConsoleKey.Enter);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("A value is required.", shell.Terminal.LastRenderedText);
                Assert.DoesNotContain("--server", shell.Terminal.LastRenderedText);

                EnqueueText(shell, "local", pressEnter: true);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("local", stdout);
    }

    [Fact]
    public async Task TextPrompt_RequiredArgumentHint_IsFieldNeutral()
    {
        var app = App<RequiredArgumentCommand>();
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                shell.Terminal.EnqueueKey(ConsoleKey.Enter);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("A value is required.", shell.Terminal.LastRenderedText);
                Assert.DoesNotContain("<name>", shell.Terminal.LastRenderedText);

                EnqueueText(shell, "riley", pressEnter: true);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("riley", stdout);
    }

    [Fact]
    public async Task SecretPrompt_MinLengthHint_IsFieldNeutralAndDoesNotRevealValue()
    {
        var app = App<SecretMinLengthCommand>();
        var shell = new TestShell();

        var (exitCode, stdout, _) = await RunCapturedWithLivePromptAsync(
            app,
            [],
            shell,
            async runTask =>
            {
                EnqueueText(shell, "hide", pressEnter: true);
                await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

                Assert.False(runTask.IsCompleted);
                Assert.Contains("Enter at least 5 characters.", shell.Terminal.LastRenderedText);
                Assert.DoesNotContain("--secret", shell.Terminal.LastRenderedText);
                Assert.DoesNotContain("hide", shell.Terminal.LastRenderedText);

                EnqueueText(shell, "n", pressEnter: true);
            });

        Assert.Equal(0, exitCode);
        Assert.Contains("hiden", stdout);
    }

    [Fact]
    public async Task NonInteractiveFlag_PreventsPrompting()
    {
        var shell = ShellWithText("riley");
        var app = App<RequiredStringCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --name", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task PromptCancel_MapsToCancelledKind_AndPrintsGentleNotice()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var app = App<RequiredStringCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 46));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("Cancelled.", result.Stderr);
        // Cancellation is a normal flow: no error prefix / error styling.
        Assert.DoesNotContain("Error:", result.Stderr);
    }

    [Fact]
    public async Task PromptCancel_CanBeMappedThroughCancelledCategory()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        var app = App<RequiredStringCommand>(builder => builder
            .UseExitCodes(0, -1).ExitCategory(TigerCliExitCategory.Cancelled, 12));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(12, result.ExitCode);
        Assert.Contains("Cancelled.", result.Stderr);
    }

    [Fact]
    public async Task PromptTimeout_MapsToCancelledKind()
    {
        var shell = new TestShell();
        var app = App<RequiredStringCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 46));

        var result = await RunCapturedAsync(app, [], shell, TimeSpan.FromMilliseconds(20));

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("Cancelled.", result.Stderr);
    }

    [Fact]
    public async Task PromptTokenCancellation_MapsToCancelledKind()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));
        var shell = new TestShell();
        var app = App<RequiredStringCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 46));

        var result = await RunCapturedWithCancellationAsync(app, [], shell, TimeSpan.FromSeconds(10), cts.Token);

        Assert.Equal(46, result.ExitCode);
        Assert.Contains("Cancelled.", result.Stderr);
    }

    [Fact]
    public async Task UserValidation_RunsAfterPrompting()
    {
        var shell = ShellWithText("bad");
        var app = App<ValidatingCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 47));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(47, result.ExitCode);
        Assert.Contains("Validation error: Name must be good.", result.Stderr);
    }

    [Fact]
    public async Task FrameworkValidation_StillRunsAfterPrompting()
    {
        var shell = new TestShell();
        var app = App<MinLengthStringCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await RunCapturedAsync(app, ["--name", "ab"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("--name must be at least 3 characters.", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    private static TigerCliApp App<TCommand>(Action<TigerCliAppBuilder>? configure = null)
        where TCommand : class, new()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("prompt-test")
            .SetDefaultCommand<TCommand>();

        configure?.Invoke(builder);
        return builder.Build();
    }

    private static TestShell ShellWithText(string value)
    {
        var shell = new TestShell();
        EnqueueText(shell, value, pressEnter: true);
        return shell;
    }

    private static void EnqueueText(TestShell shell, string value, bool pressEnter = true)
    {
        foreach (var ch in value)
        {
            var key = char.IsLetter(ch)
                ? Enum.Parse<ConsoleKey>(char.ToUpperInvariant(ch).ToString())
                : ConsoleKey.Spacebar;
            shell.Terminal.EnqueueKey(key, keyChar: ch);
        }
        if (pressEnter)
            shell.Terminal.EnqueueKey(ConsoleKey.Enter);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedWithLivePromptAsync(
        TigerCliApp app,
        string[] args,
        TestShell shell,
        Func<Task<int>, Task> interact)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var runTask = app.RunAsync(args, shell, ct: TestContext.Current.CancellationToken);
            await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            await interact(runTask);
            var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app,
        string[] args,
        ICliAppShell shell,
        TimeSpan? promptTimeout = null)
    {
        return await RunCapturedWithCancellationAsync(
            app,
            args,
            shell,
            promptTimeout,
            TestContext.Current.CancellationToken);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedWithCancellationAsync(
        TigerCliApp app,
        string[] args,
        ICliAppShell shell,
        TimeSpan? promptTimeout,
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
            var exitCode = await app.RunAsync(args, shell, promptTimeout, ct);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
