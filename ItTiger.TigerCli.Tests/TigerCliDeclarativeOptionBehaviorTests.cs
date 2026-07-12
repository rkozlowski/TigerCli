using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliDeclarativeOptionBehaviorTests
{
    private enum AuthenticationType
    {
        Integrated,
        SqlPassword,
        EntraPassword
    }

    private sealed class ConditionalUsernameSettings : TigerCliSettings
    {
        [TigerCliOption("--authentication", Description = "Authentication mode.")]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

        [TigerCliOption("--username",
            Description = "SQL login username.",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword",
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword",
            MinLength = 1,
            MaxLength = 128)]
        public string? Username { get; set; }
    }

    private sealed class PromptWhenFalseSettings : TigerCliSettings
    {
        [TigerCliOption("--authentication")]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

        [TigerCliOption("--username",
            Required = true,
            Promptable = TigerCliPromptable.Normal,
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword")]
        public string? Username { get; set; }
    }

    private sealed class NotPromptableConditionalSettings : TigerCliSettings
    {
        [TigerCliOption("--authentication")]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

        [TigerCliOption("--username",
            Promptable = TigerCliPromptable.No,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword",
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword")]
        public string? Username { get; set; }
    }

    private sealed class BoolConditionSettings : TigerCliSettings
    {
        [TigerCliOption("--enabled")]
        public bool Enabled { get; set; }

        [TigerCliOption("--name",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--enabled",
            RequiredWhenValue = "true",
            PromptWhenOption = "--enabled",
            PromptWhenValue = "true")]
        public string? Name { get; set; }
    }

    private sealed class StringConditionSettings : TigerCliSettings
    {
        [TigerCliOption("--mode")]
        public string Mode { get; set; } = "integrated";

        [TigerCliOption("--name",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--mode",
            RequiredWhenValue = "sql",
            PromptWhenOption = "--mode",
            PromptWhenValue = "sql")]
        public string? Name { get; set; }
    }

    private sealed class ValueInConditionSettings : TigerCliSettings
    {
        [TigerCliOption("--authentication")]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

        [TigerCliOption("--username",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--authentication",
            RequiredWhenValueIn = new[] { "SqlPassword", "EntraPassword" },
            PromptWhenOption = "--authentication",
            PromptWhenValueIn = new[] { "SqlPassword", "EntraPassword" })]
        public string? Username { get; set; }
    }

    private sealed class ValueNotInConditionSettings : TigerCliSettings
    {
        [TigerCliOption("--authentication")]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

        [TigerCliOption("--username",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--authentication",
            RequiredWhenValueNotIn = new[] { "Integrated" },
            PromptWhenOption = "--authentication",
            PromptWhenValueNotIn = new[] { "Integrated" })]
        public string? Username { get; set; }
    }

    private sealed class EmptyCollectionConditionSettings : TigerCliSettings
    {
        [TigerCliOption("--authentication")]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

        [TigerCliOption("--username",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--authentication",
            RequiredWhenValueIn = new string[] { },
            RequiredWhenValueNotIn = new string[] { },
            PromptWhenOption = "--authentication",
            PromptWhenValueIn = new string[] { },
            PromptWhenValueNotIn = new string[] { })]
        public string? Username { get; set; }
    }

    private sealed class SecretPasswordSettings : TigerCliSettings
    {
        [TigerCliOption("--authentication")]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;

        [TigerCliOption("--password",
            Description = "SQL login password.",
            Promptable = TigerCliPromptable.Normal,
            Secret = true,
            AllowCommandLineValue = false,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword",
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword",
            MinLength = 1)]
        public string? Password { get; set; }
    }

    private sealed class ConditionalMinSettings : TigerCliSettings
    {
        [TigerCliOption("--authentication")]
        public AuthenticationType Authentication { get; set; }

        [TigerCliOption("--username",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword",
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword",
            MinLength = 3)]
        public string? Username { get; set; }
    }

    private sealed class ConditionalMaxSettings : TigerCliSettings
    {
        [TigerCliOption("--authentication")]
        public AuthenticationType Authentication { get; set; }

        [TigerCliOption("--username",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword",
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword",
            MaxLength = 3)]
        public string? Username { get; set; }
    }

    private sealed class ProviderConditionalSettings : TigerCliSettings
    {
        [TigerCliOption("--authentication")]
        public AuthenticationType Authentication { get; set; }

        [TigerCliOption("--username",
            Provider = "user",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword",
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword")]
        public string? Username { get; set; }
    }

    private sealed class DependentFirstConditionalSettings : TigerCliSettings
    {
        [TigerCliOption("--username",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword",
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword")]
        public string? Username { get; set; }

        [TigerCliOption("--authentication", Promptable = TigerCliPromptable.Normal)]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;
    }

    private sealed class DependentFirstCredentialsSettings : TigerCliSettings
    {
        [TigerCliOption("--username",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword",
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword",
            MinLength = 1)]
        public string? Username { get; set; }

        [TigerCliOption("--password",
            Promptable = TigerCliPromptable.Normal,
            Secret = true,
            AllowCommandLineValue = false,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword",
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword",
            MinLength = 1)]
        public string? Password { get; set; }

        [TigerCliOption("--authentication", Promptable = TigerCliPromptable.Normal)]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;
    }

    private sealed class DependentFirstProviderSettings : TigerCliSettings
    {
        [TigerCliOption("--username",
            Provider = "user",
            Promptable = TigerCliPromptable.Normal,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword",
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword")]
        public string? Username { get; set; }

        [TigerCliOption("--authentication", Promptable = TigerCliPromptable.Normal)]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;
    }

    private sealed class DependentFirstNotPromptableSettings : TigerCliSettings
    {
        [TigerCliOption("--username",
            Promptable = TigerCliPromptable.No,
            RequiredWhenOption = "--authentication",
            RequiredWhenValue = "SqlPassword",
            PromptWhenOption = "--authentication",
            PromptWhenValue = "SqlPassword")]
        public string? Username { get; set; }

        [TigerCliOption("--authentication", Promptable = TigerCliPromptable.Normal)]
        public AuthenticationType Authentication { get; set; } = AuthenticationType.Integrated;
    }

    private sealed class ConditionalUsernameCommand : TigerCliAsyncCommandHandler<ConditionalUsernameSettings>
    {
        public override Task<int> ExecuteAsync(ConditionalUsernameSettings settings)
        {
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class PromptWhenFalseCommand : TigerCliAsyncCommandHandler<PromptWhenFalseSettings>
    {
        public override Task<int> ExecuteAsync(PromptWhenFalseSettings settings)
        {
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class NotPromptableConditionalCommand : TigerCliAsyncCommandHandler<NotPromptableConditionalSettings>
    {
        public override Task<int> ExecuteAsync(NotPromptableConditionalSettings settings)
        {
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class BoolConditionCommand : TigerCliAsyncCommandHandler<BoolConditionSettings>
    {
        public override Task<int> ExecuteAsync(BoolConditionSettings settings)
        {
            TigerConsole.MarkupLine($"name={CliMarkupParser.Escape(settings.Name ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class StringConditionCommand : TigerCliAsyncCommandHandler<StringConditionSettings>
    {
        public override Task<int> ExecuteAsync(StringConditionSettings settings)
        {
            TigerConsole.MarkupLine($"name={CliMarkupParser.Escape(settings.Name ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class ValueInConditionCommand : TigerCliAsyncCommandHandler<ValueInConditionSettings>
    {
        public override Task<int> ExecuteAsync(ValueInConditionSettings settings)
        {
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class ValueNotInConditionCommand : TigerCliAsyncCommandHandler<ValueNotInConditionSettings>
    {
        public override Task<int> ExecuteAsync(ValueNotInConditionSettings settings)
        {
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class EmptyCollectionConditionCommand :
        TigerCliAsyncCommandHandler<EmptyCollectionConditionSettings>
    {
        public override Task<int> ExecuteAsync(EmptyCollectionConditionSettings settings)
        {
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class SecretPasswordCommand : TigerCliAsyncCommandHandler<SecretPasswordSettings>
    {
        public override Task<int> ExecuteAsync(SecretPasswordSettings settings)
        {
            TigerConsole.MarkupLine($"password={CliMarkupParser.Escape(settings.Password ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class ConditionalMinCommand : TigerCliAsyncCommandHandler<ConditionalMinSettings>
    {
        public override Task<int> ExecuteAsync(ConditionalMinSettings settings)
        {
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class ConditionalMaxCommand : TigerCliAsyncCommandHandler<ConditionalMaxSettings>
    {
        public override Task<int> ExecuteAsync(ConditionalMaxSettings settings)
        {
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderConditionalCommand : TigerCliAsyncCommandHandler<ProviderConditionalSettings>
    {
        public override Task<int> ExecuteAsync(ProviderConditionalSettings settings)
        {
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class DependentFirstConditionalCommand :
        TigerCliAsyncCommandHandler<DependentFirstConditionalSettings>
    {
        public override Task<int> ExecuteAsync(DependentFirstConditionalSettings settings)
        {
            TigerConsole.MarkupLine($"authentication={settings.Authentication}");
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class DependentFirstCredentialsCommand :
        TigerCliAsyncCommandHandler<DependentFirstCredentialsSettings>
    {
        public override Task<int> ExecuteAsync(DependentFirstCredentialsSettings settings)
        {
            TigerConsole.MarkupLine($"authentication={settings.Authentication}");
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            TigerConsole.MarkupLine($"password={CliMarkupParser.Escape(settings.Password ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class DependentFirstProviderCommand :
        TigerCliAsyncCommandHandler<DependentFirstProviderSettings>
    {
        public override Task<int> ExecuteAsync(DependentFirstProviderSettings settings)
        {
            TigerConsole.MarkupLine($"authentication={settings.Authentication}");
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    private sealed class DependentFirstNotPromptableCommand :
        TigerCliAsyncCommandHandler<DependentFirstNotPromptableSettings>
    {
        public override Task<int> ExecuteAsync(DependentFirstNotPromptableSettings settings)
        {
            TigerConsole.MarkupLine($"authentication={settings.Authentication}");
            TigerConsole.MarkupLine($"username={CliMarkupParser.Escape(settings.Username ?? "<null>")}");
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task RequiredWhenTrue_MissingValueFailsInNonInteractiveMode()
    {
        var app = App<ConditionalUsernameCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var shell = ShellWithText("ignored");

        var result = await RunCapturedAsync(
            app,
            ["--authentication", "SqlPassword", "--non-interactive"],
            shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --username", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task RequiredWhenFalse_MissingValueIsAllowed()
    {
        var app = App<ConditionalUsernameCommand>();
        var shell = ShellWithText("ignored");

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("username=<null>", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task RequiredWhenTrueAndPromptWhenTrue_PromptsWhenAllowed()
    {
        var app = App<ConditionalUsernameCommand>();
        var shell = ShellWithText("alice");

        var result = await RunCapturedAsync(app, ["--authentication", "SqlPassword"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("username=alice", result.Stdout);
    }

    [Fact]
    public async Task PromptWhenFalse_PreventsPrompt()
    {
        var app = App<PromptWhenFalseCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var shell = ShellWithText("alice");

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --username", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task PromptableFalse_PreventsPromptEvenWhenPromptWhenTrue()
    {
        var app = App<NotPromptableConditionalCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var shell = ShellWithText("alice");

        var result = await RunCapturedAsync(app, ["--authentication", "SqlPassword"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --username", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task NonInteractive_PreventsPromptEvenWhenPromptWhenTrue()
    {
        var app = App<ConditionalUsernameCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var shell = ShellWithText("alice");

        var result = await RunCapturedAsync(
            app,
            ["--authentication", "SqlPassword", "--non-interactive"],
            shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --username", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task EnumConditionMatching_WorksByEnumName()
    {
        var app = App<ConditionalUsernameCommand>();
        var shell = ShellWithText("enum-user");

        var result = await RunCapturedAsync(app, ["--authentication", "SqlPassword"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("username=enum-user", result.Stdout);
    }

    [Fact]
    public async Task BoolConditionMatching_Works()
    {
        var app = App<BoolConditionCommand>();
        var shell = ShellWithText("bool-user");

        var result = await RunCapturedAsync(app, ["--enabled"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("name=bool-user", result.Stdout);
    }

    [Fact]
    public async Task StringConditionMatching_Works()
    {
        var app = App<StringConditionCommand>();
        var shell = ShellWithText("string-user");

        var result = await RunCapturedAsync(app, ["--mode", "sql"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("name=string-user", result.Stdout);
    }

    [Fact]
    public async Task RequiredWhenValueIn_MatchingValueRequiresOption()
    {
        var app = App<ValueInConditionCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var shell = new TestShell();

        var result = await RunCapturedAsync(
            app,
            ["--authentication", "SqlPassword", "--non-interactive"],
            shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --username", result.Stderr);
    }

    [Fact]
    public async Task RequiredWhenValueIn_NonMatchingValueDoesNotRequireOption()
    {
        var app = App<ValueInConditionCommand>();
        var shell = new TestShell();

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("username=<null>", result.Stdout);
    }

    [Fact]
    public async Task RequiredWhenValueNotIn_NonListedValueRequiresOption()
    {
        var app = App<ValueNotInConditionCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var shell = new TestShell();

        var result = await RunCapturedAsync(
            app,
            ["--authentication", "SqlPassword", "--non-interactive"],
            shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --username", result.Stderr);
    }

    [Fact]
    public async Task RequiredWhenValueNotIn_ListedValueDoesNotRequireOption()
    {
        var app = App<ValueNotInConditionCommand>();
        var shell = new TestShell();

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("username=<null>", result.Stdout);
    }

    [Fact]
    public async Task PromptWhenValueIn_PromptsOnlyForListedValues()
    {
        var app = App<ValueInConditionCommand>();
        var listedShell = ShellWithText("entra-user");
        var nonListedShell = ShellWithText("ignored");

        var listedResult = await RunCapturedAsync(
            app,
            ["--authentication", "EntraPassword"],
            listedShell);
        var nonListedResult = await RunCapturedAsync(app, [], nonListedShell);

        Assert.Equal(0, listedResult.ExitCode);
        Assert.Contains("username=entra-user", listedResult.Stdout);
        Assert.Equal(0, nonListedResult.ExitCode);
        Assert.Contains("username=<null>", nonListedResult.Stdout);
        Assert.Equal(0, nonListedShell.Terminal.ReadCount);
    }

    [Fact]
    public async Task PromptWhenValueNotIn_PromptsOnlyForNonListedValues()
    {
        var app = App<ValueNotInConditionCommand>();
        var nonListedShell = ShellWithText("sql-user");
        var listedShell = ShellWithText("ignored");

        var nonListedResult = await RunCapturedAsync(
            app,
            ["--authentication", "SqlPassword"],
            nonListedShell);
        var listedResult = await RunCapturedAsync(app, [], listedShell);

        Assert.Equal(0, nonListedResult.ExitCode);
        Assert.Contains("username=sql-user", nonListedResult.Stdout);
        Assert.Equal(0, listedResult.ExitCode);
        Assert.Contains("username=<null>", listedResult.Stdout);
        Assert.Equal(0, listedShell.Terminal.ReadCount);
    }

    [Fact]
    public async Task EmptyConditionValueCollections_DoNotMatch()
    {
        var app = App<EmptyCollectionConditionCommand>();
        var shell = ShellWithText("ignored");

        var result = await RunCapturedAsync(
            app,
            ["--authentication", "SqlPassword"],
            shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("username=<null>", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task SecretPrompt_MasksRenderedValueAndStoresActualValue()
    {
        var app = App<SecretPasswordCommand>();
        var shell = ShellWithText("secret");

        var result = await RunCapturedAsync(app, ["--authentication", "SqlPassword"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("password=secret", result.Stdout);
        Assert.DoesNotContain("secret", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task AllowCommandLineValueFalse_RejectsArgvValue()
    {
        var app = App<SecretPasswordCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var shell = new TestShell();

        var result = await RunCapturedAsync(
            app,
            ["--authentication", "SqlPassword", "--password", "secret"],
            shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Option '--password' cannot be supplied on the command line.", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task AllowCommandLineValueFalse_AllowsPromptValue()
    {
        var app = App<SecretPasswordCommand>();
        var shell = ShellWithText("secret");

        var result = await RunCapturedAsync(app, ["--authentication", "SqlPassword"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("password=secret", result.Stdout);
    }

    [Fact]
    public async Task Help_ShowsPromptOnlySecretSafely()
    {
        var app = App<SecretPasswordCommand>();
        var shell = new TestShell();

        var result = await RunCapturedAsync(app, ["--help"], shell);

        Assert.Equal(0, result.ExitCode);
        var optionsIndex = result.Stdout.IndexOf("Options:", StringComparison.Ordinal);
        var promptedValuesIndex = result.Stdout.IndexOf("Prompted values:", StringComparison.Ordinal);
        Assert.True(optionsIndex >= 0);
        Assert.True(promptedValuesIndex > optionsIndex);

        var optionsSection = result.Stdout[optionsIndex..promptedValuesIndex];
        var promptedValuesSection = result.Stdout[promptedValuesIndex..];

        Assert.Contains("--authentication", optionsSection);
        Assert.DoesNotContain("password", optionsSection);
        Assert.DoesNotContain("--password", result.Stdout);
        Assert.DoesNotContain("--password <value>", result.Stdout);
        Assert.DoesNotContain("password <value>", result.Stdout);
        Assert.Contains("  password", promptedValuesSection);
        Assert.Contains("SQL login password.", promptedValuesSection);
        Assert.Contains("Secret value; prompted when required.", promptedValuesSection);
        Assert.Contains("Cannot be supplied on the command line.", promptedValuesSection);
    }

    [Fact]
    public async Task MinLength_AppliesToConditionallyRequiredPromptedValue()
    {
        var app = App<ConditionalMinCommand>();
        var shell = new TestShell();
        EnqueueText(shell, "ab", pressEnter: true);
        EnqueueText(shell, "c", pressEnter: true);

        var result = await RunCapturedAsync(app, ["--authentication", "SqlPassword"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("username=abc", result.Stdout);
    }

    [Fact]
    public async Task MaxLength_AppliesToConditionallyRequiredPromptedValue()
    {
        var app = App<ConditionalMaxCommand>();
        var shell = new TestShell();
        EnqueueText(shell, "abcd", pressEnter: true);
        shell.Terminal.EnqueueKey(ConsoleKey.Backspace);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);

        var result = await RunCapturedAsync(app, ["--authentication", "SqlPassword"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("username=abc", result.Stdout);
    }

    [Fact]
    public async Task ProviderBackedPrompt_WorksWithPromptWhenTrue()
    {
        var calls = 0;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = App<ProviderConditionalCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("user", _ =>
            {
                calls++;
                return (IReadOnlyList<string>)["provider-user"];
            })));

        var result = await RunCapturedAsync(app, ["--authentication", "SqlPassword"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, calls);
        Assert.Contains("username=provider-user", result.Stdout);
    }

    [Fact]
    public async Task Provider_IsNotCalledWhenPromptWhenFalse()
    {
        var calls = 0;
        var shell = new TestShell();
        var app = App<ProviderConditionalCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("user", _ =>
            {
                calls++;
                return (IReadOnlyList<string>)["provider-user"];
            })));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, calls);
        Assert.Contains("username=<null>", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task DependentOption_IsPromptedAfterPromptedControllerMatches()
    {
        var app = App<DependentFirstConditionalCommand>();
        var shell = new TestShell();
        EnqueueSelect(shell, index: 1);
        EnqueueText(shell, "alice");

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("authentication=SqlPassword", result.Stdout);
        Assert.Contains("username=alice", result.Stdout);
    }

    [Fact]
    public async Task DependentOption_IsNotPromptedAfterPromptedControllerDoesNotMatch()
    {
        var app = App<DependentFirstConditionalCommand>();
        var shell = new TestShell();
        EnqueueSelect(shell, index: 0);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("authentication=Integrated", result.Stdout);
        Assert.Contains("username=<null>", result.Stdout);
        Assert.Equal(1, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task MultipleDependentOptions_ArePromptedAfterControllerBecomesMatching()
    {
        var app = App<DependentFirstCredentialsCommand>();
        var shell = new TestShell();
        EnqueueSelect(shell, index: 1);
        EnqueueText(shell, "alice");
        EnqueueText(shell, "secret");

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("authentication=SqlPassword", result.Stdout);
        Assert.Contains("username=alice", result.Stdout);
        Assert.Contains("password=secret", result.Stdout);
        Assert.DoesNotContain("secret", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task ProviderBackedDependentOption_ProviderIsCalledAfterPromptedControllerMatches()
    {
        var calls = 0;
        var shell = new TestShell();
        EnqueueSelect(shell, index: 1);
        EnqueueSelect(shell, index: 0);
        var app = App<DependentFirstProviderCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("user", _ =>
            {
                calls++;
                return (IReadOnlyList<string>)["provider-user"];
            })));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, calls);
        Assert.Contains("authentication=SqlPassword", result.Stdout);
        Assert.Contains("username=provider-user", result.Stdout);
    }

    [Fact]
    public async Task ProviderBackedDependentOption_ProviderIsNotCalledAfterPromptedControllerDoesNotMatch()
    {
        var calls = 0;
        var shell = new TestShell();
        EnqueueSelect(shell, index: 0);
        var app = App<DependentFirstProviderCommand>(builder => builder
            .ConfigureProviders(providers => providers.Add("user", _ =>
            {
                calls++;
                return (IReadOnlyList<string>)["provider-user"];
            })));

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, calls);
        Assert.Contains("authentication=Integrated", result.Stdout);
        Assert.Contains("username=<null>", result.Stdout);
    }

    [Fact]
    public async Task NonInteractive_MatchingConditionReportsMissingDependentRequiredValue()
    {
        var app = App<DependentFirstConditionalCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var shell = new TestShell();

        var result = await RunCapturedAsync(app, ["--authentication", "SqlPassword", "--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --username", result.Stderr);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task NonInteractive_NonMatchingConditionDoesNotRequireDependentValue()
    {
        var app = App<DependentFirstConditionalCommand>();
        var shell = new TestShell();

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("authentication=Integrated", result.Stdout);
        Assert.Contains("username=<null>", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task PromptableFalse_StillPreventsDependentPromptAfterControllerMatches()
    {
        var app = App<DependentFirstNotPromptableCommand>(builder => builder
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));
        var shell = new TestShell();
        EnqueueSelect(shell, index: 1);

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Missing required option: --username", result.Stderr);
        Assert.Equal(2, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task GroupFactoryCommand_UsesDeclarativeMetadata()
    {
        var shell = ShellWithText("factory-user");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("declarative-test")
            .AddCommandGroup("connections", group =>
            {
                group.AddCommand(
                    "test",
                    () => new ConditionalUsernameCommand());
            })
            .Build();

        var result = await RunCapturedAsync(
            app,
            ["connections", "test", "--authentication", "SqlPassword"],
            shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("username=factory-user", result.Stdout);
    }

    private static TigerCliApp App<TCommand>(Action<TigerCliAppBuilder>? configure = null)
        where TCommand : class, new()
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("declarative-test")
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
