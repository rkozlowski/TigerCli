using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TigerCliEditCommandTests
{
    private enum AuthMode
    {
        Integrated,
        Sql
    }

    [Flags]
    private enum Feature
    {
        None = 0,
        Read = 1,
        Write = 2,
        All = Read | Write
    }

    // ── Settings ─────────────────────────────────────────────────────

    // Selector + non-prompting editable/non-editable options. Used for merge tests.
    private sealed class MergeSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--server", Promptable = TigerCliPromptable.No, Description = "Server")]
        public string Server { get; set; } = string.Empty;

        [TigerCliOption("--note", Editable = false, Promptable = TigerCliPromptable.No, Description = "Note")]
        public string Note { get; set; } = string.Empty;
    }

    // Two arguments: a selector and a second (non-editable) argument.
    private sealed class TwoArgSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliArgument(1, Name = "region", Description = "Region")]
        public string Region { get; set; } = string.Empty;
    }

    // Non-promptable selector argument: cannot be prompted, so a missing value must be rejected.
    private sealed class NonPromptableSelectorSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Promptable = TigerCliPromptable.No, Description = "Profile")]
        public string Profile { get; set; } = string.Empty;
    }

    // Promptable selector argument: missing on the CLI, resolved before the loader.
    private sealed class PromptableSelectorSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Promptable = TigerCliPromptable.Normal, Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--server", Promptable = TigerCliPromptable.No, Description = "Server")]
        public string Server { get; set; } = string.Empty;
    }

    // Promptable, provider-backed selector argument resolved before the loader.
    private sealed class ProviderSelectorSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Provider = "profiles",
            Promptable = TigerCliPromptable.Normal, Description = "Profile")]
        public string Profile { get; set; } = string.Empty;
    }

    // Selector + a single editable string option (prompts in edit mode).
    private sealed class EditableOptionSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--server", Promptable = TigerCliPromptable.Normal, Description = "Server")]
        public string Server { get; set; } = string.Empty;
    }

    // Selector + a single non-editable option that is promptable.
    private sealed class NonEditableOptionSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--note", Editable = false, Promptable = TigerCliPromptable.Normal, Description = "Note")]
        public string Note { get; set; } = string.Empty;
    }

    // Selector + all promptable editable kinds for current-value preselect.
    private sealed class SeedSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--server", Promptable = TigerCliPromptable.Normal, Description = "Server")]
        public string Server { get; set; } = string.Empty;

        [TigerCliOption("--auth", Promptable = TigerCliPromptable.Normal, Description = "Auth")]
        public AuthMode Auth { get; set; }

        [TigerCliOption("--trusted", Promptable = TigerCliPromptable.Normal, Description = "Trusted")]
        public bool? Trusted { get; set; }

        [TigerCliOption("--features", Promptable = TigerCliPromptable.Normal, Description = "Features")]
        public Feature Features { get; set; }
    }

    // Selector + provider-backed editable option.
    private sealed class ProviderEditSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string Database { get; set; } = string.Empty;
    }

    // Selector + provider-backed editable option that opts out of provider validation.
    private sealed class ProviderOptOutSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--database", Provider = "databases", ValidateAgainstProvider = false,
            Promptable = TigerCliPromptable.No, Description = "Database")]
        public string Database { get; set; } = string.Empty;
    }

    // Selector + provider-backed NON-editable option (selector-like, not validated).
    private sealed class ProviderNonEditableSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--legacy", Provider = "databases", Editable = false,
            Promptable = TigerCliPromptable.No, Description = "Legacy")]
        public string Legacy { get; set; } = string.Empty;
    }

    // Provider-backed option for add-mode validation (default empty).
    private sealed class AddProviderSettings : TigerCliSettings
    {
        [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string Database { get; set; } = string.Empty;
    }

    // Provider-backed option for add-mode validation with an invalid initializer default.
    private sealed class AddProviderDefaultSettings : TigerCliSettings
    {
        [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.No, Description = "Database")]
        public string Database { get; set; } = "bogus";
    }

    // Provider-backed promptable option with an initializer default that matches a provider choice.
    private sealed class AddProviderValidDefaultSettings : TigerCliSettings
    {
        [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string Database { get; set; } = "Reports";
    }

    // Provider-backed promptable option with an initializer default that is NOT a provider choice.
    private sealed class AddProviderStaleDefaultSettings : TigerCliSettings
    {
        [TigerCliOption("--database", Provider = "databases", Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string Database { get; set; } = "bogus";
    }

    // ── EditProvider settings (shared add/edit) ──────────────────────

    // Shared add/edit name. Only EditProvider is set: in add/normal mode it is ignored
    // (a new name is typed); in edit mode it selects an existing name via the provider.
    private sealed class SharedNameSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "name", EditProvider = "profiles",
            Promptable = TigerCliPromptable.Normal, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    // Argument with BOTH Provider and EditProvider: edit mode must use EditProvider.
    private sealed class BothProvidersSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "name", Provider = "addprofiles", EditProvider = "editprofiles",
            Promptable = TigerCliPromptable.Normal, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    // Argument with only a normal Provider (no EditProvider): the provider applies in both
    // normal and edit mode (edit falls back to Provider when EditProvider is absent).
    private sealed class ProviderOnlySharedSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "name", Provider = "names",
            Promptable = TigerCliPromptable.Normal, Description = "Name")]
        public string Name { get; set; } = string.Empty;
    }

    // Selector + an editable option carrying both Provider and EditProvider (option-level symmetry).
    private sealed class OptionEditProviderSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--database", Provider = "add-db", EditProvider = "edit-db",
            Promptable = TigerCliPromptable.Normal, Description = "Database")]
        public string Database { get; set; } = string.Empty;
    }

    // Selector + a secret editable option (seeded in edit mode; argv secret forbidden).
    private sealed class SecretEditSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--password", Secret = true, AllowCommandLineValue = false,
            Promptable = TigerCliPromptable.Normal, Description = "Password")]
        public string Password { get; set; } = string.Empty;
    }

    // Selector + seeded (non-prompted) secret + a provider-backed database that depends on the
    // effective secret. Mirrors the SQL Server case: the database provider needs the current
    // password to connect, and edit mode must seed it so the user is not forced to re-enter it.
    private sealed class SecretDependentSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "profile", Description = "Profile")]
        public string Profile { get; set; } = string.Empty;

        [TigerCliOption("--password", Secret = true, AllowCommandLineValue = false,
            Promptable = TigerCliPromptable.No, Description = "Password")]
        public string Password { get; set; } = string.Empty;

        [TigerCliOption("--database", Provider = "databases",
            Promptable = TigerCliPromptable.Normal, DependsOnOptions = new[] { "--password" },
            Description = "Database")]
        public string Database { get; set; } = string.Empty;
    }

    // ── Commands ─────────────────────────────────────────────────────

    private sealed class MergeCommand : TigerCliAsyncCommandHandler<MergeSettings>
    {
        public override Task<int> ExecuteAsync(MergeSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(
                $"profile={settings.Profile};server={settings.Server};note={settings.Note}"));
            return Task.FromResult(0);
        }
    }

    private sealed class TwoArgCommand : TigerCliAsyncCommandHandler<TwoArgSettings>
    {
        public override Task<int> ExecuteAsync(TwoArgSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(
                $"profile={settings.Profile};region={settings.Region}"));
            return Task.FromResult(0);
        }
    }

    private sealed class NonPromptableSelectorCommand : TigerCliAsyncCommandHandler<NonPromptableSelectorSettings>
    {
        public override Task<int> ExecuteAsync(NonPromptableSelectorSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"profile={settings.Profile}"));
            return Task.FromResult(0);
        }
    }

    private sealed class PromptableSelectorCommand : TigerCliAsyncCommandHandler<PromptableSelectorSettings>
    {
        public override Task<int> ExecuteAsync(PromptableSelectorSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(
                $"profile={settings.Profile};server={settings.Server}"));
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderSelectorCommand : TigerCliAsyncCommandHandler<ProviderSelectorSettings>
    {
        public override Task<int> ExecuteAsync(ProviderSelectorSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"profile={settings.Profile}"));
            return Task.FromResult(0);
        }
    }

    private sealed class EditableOptionCommand : TigerCliAsyncCommandHandler<EditableOptionSettings>
    {
        public override Task<int> ExecuteAsync(EditableOptionSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"server={settings.Server}"));
            return Task.FromResult(0);
        }
    }

    private sealed class NonEditableOptionCommand : TigerCliAsyncCommandHandler<NonEditableOptionSettings>
    {
        public override Task<int> ExecuteAsync(NonEditableOptionSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"note={settings.Note}"));
            return Task.FromResult(0);
        }
    }

    private sealed class SeedCommand : TigerCliAsyncCommandHandler<SeedSettings>
    {
        public override Task<int> ExecuteAsync(SeedSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape(
                $"server={settings.Server};auth={settings.Auth};trusted={settings.Trusted};features={settings.Features}"));
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderEditCommand : TigerCliAsyncCommandHandler<ProviderEditSettings>
    {
        public override Task<int> ExecuteAsync(ProviderEditSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={settings.Database}"));
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderOptOutCommand : TigerCliAsyncCommandHandler<ProviderOptOutSettings>
    {
        public override Task<int> ExecuteAsync(ProviderOptOutSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={settings.Database}"));
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderNonEditableCommand : TigerCliAsyncCommandHandler<ProviderNonEditableSettings>
    {
        public override Task<int> ExecuteAsync(ProviderNonEditableSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"legacy={settings.Legacy}"));
            return Task.FromResult(0);
        }
    }

    private sealed class AddProviderCommand : TigerCliAsyncCommandHandler<AddProviderSettings>
    {
        public override Task<int> ExecuteAsync(AddProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={settings.Database}"));
            return Task.FromResult(0);
        }
    }

    private sealed class AddProviderDefaultCommand : TigerCliAsyncCommandHandler<AddProviderDefaultSettings>
    {
        public override Task<int> ExecuteAsync(AddProviderDefaultSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={settings.Database}"));
            return Task.FromResult(0);
        }
    }

    private sealed class AddProviderValidDefaultCommand : TigerCliAsyncCommandHandler<AddProviderValidDefaultSettings>
    {
        public override Task<int> ExecuteAsync(AddProviderValidDefaultSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={settings.Database}"));
            return Task.FromResult(0);
        }
    }

    private sealed class AddProviderStaleDefaultCommand : TigerCliAsyncCommandHandler<AddProviderStaleDefaultSettings>
    {
        public override Task<int> ExecuteAsync(AddProviderStaleDefaultSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={settings.Database}"));
            return Task.FromResult(0);
        }
    }

    private sealed class SharedNameCommand : TigerCliAsyncCommandHandler<SharedNameSettings>
    {
        public override Task<int> ExecuteAsync(SharedNameSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"name={settings.Name}"));
            return Task.FromResult(0);
        }
    }

    private sealed class BothProvidersCommand : TigerCliAsyncCommandHandler<BothProvidersSettings>
    {
        public override Task<int> ExecuteAsync(BothProvidersSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"name={settings.Name}"));
            return Task.FromResult(0);
        }
    }

    private sealed class ProviderOnlySharedCommand : TigerCliAsyncCommandHandler<ProviderOnlySharedSettings>
    {
        public override Task<int> ExecuteAsync(ProviderOnlySharedSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"name={settings.Name}"));
            return Task.FromResult(0);
        }
    }

    private sealed class OptionEditProviderCommand : TigerCliAsyncCommandHandler<OptionEditProviderSettings>
    {
        public override Task<int> ExecuteAsync(OptionEditProviderSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={settings.Database}"));
            return Task.FromResult(0);
        }
    }

    private sealed class SecretEditCommand : TigerCliAsyncCommandHandler<SecretEditSettings>
    {
        public override Task<int> ExecuteAsync(SecretEditSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"password={settings.Password}"));
            return Task.FromResult(0);
        }
    }

    private sealed class SecretDependentCommand : TigerCliAsyncCommandHandler<SecretDependentSettings>
    {
        public override Task<int> ExecuteAsync(SecretDependentSettings settings)
        {
            TigerConsole.MarkupLine(CliMarkupParser.Escape($"database={settings.Database}"));
            return Task.FromResult(0);
        }
    }

    // ── Part 3/4: edit registration and merge ────────────────────────

    [Fact]
    public async Task NormalCommand_WithoutAsEdit_DoesNotMergeOrChangeBehavior()
    {
        // Non-editable promptable option still prompts in ADD mode (Editable is inert in add).
        var shell = ShellWithText("typed-note");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("edit-test")
            .SetDefaultCommand<NonEditableOptionCommand>()
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .Build();

        var result = await RunCapturedAsync(app, ["reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("note=typed-note", result.Stdout);
    }

    [Fact]
    public async Task Loader_ReceivesBoundSelectorArgument()
    {
        string? observed = null;
        var app = BuildEditApp<MergeCommand, MergeSettings>(settings =>
        {
            observed = settings.Profile;
            return TigerCliEditLoad<MergeSettings>.Found(new MergeSettings { Server = "sql-old" });
        });

        var result = await RunCapturedAsync(app, ["edit", "reporting", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("reporting", observed);
    }

    [Fact]
    public async Task CliValue_WinsOverExistingValue()
    {
        var app = BuildEditApp<MergeCommand, MergeSettings>(_ =>
            TigerCliEditLoad<MergeSettings>.Found(new MergeSettings { Server = "sql-old", Note = "keep" }));

        var result = await RunCapturedAsync(
            app, ["edit", "reporting", "--server", "sql-new", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("server=sql-new", result.Stdout);
    }

    [Fact]
    public async Task ExistingValues_FillUnspecifiedFields()
    {
        var app = BuildEditApp<MergeCommand, MergeSettings>(_ =>
            TigerCliEditLoad<MergeSettings>.Found(new MergeSettings { Server = "sql-old", Note = "keep" }));

        var result = await RunCapturedAsync(app, ["edit", "reporting", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("server=sql-old", result.Stdout);
        Assert.Contains("note=keep", result.Stdout);
    }

    [Fact]
    public async Task SelectorArgument_IsNotOverwrittenByExisting()
    {
        var app = BuildEditApp<MergeCommand, MergeSettings>(_ =>
            TigerCliEditLoad<MergeSettings>.Found(new MergeSettings { Profile = "different", Server = "sql-old" }));

        var result = await RunCapturedAsync(app, ["edit", "reporting", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("profile=reporting", result.Stdout);
    }

    [Fact]
    public async Task NonInteractiveEdit_KeepsExistingUnspecifiedFields_WithOverride()
    {
        var app = BuildEditApp<MergeCommand, MergeSettings>(_ =>
            TigerCliEditLoad<MergeSettings>.Found(new MergeSettings { Server = "sql-old", Note = "keep" }));

        var result = await RunCapturedAsync(
            app, ["edit", "reporting", "--server", "sql-new", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("server=sql-new", result.Stdout);
        Assert.Contains("note=keep", result.Stdout);
    }

    [Fact]
    public async Task SecondaryArgument_Missing_IsPromptedBeforeLoader_NotSeededFromExisting()
    {
        // Every positional argument is a selector resolved before the loader. The second
        // argument is missing on the CLI and promptable, so it is prompted before the loader
        // (it is NOT seeded from the existing object — that object has not been loaded yet).
        string? observedProfile = null;
        string? observedRegion = null;
        var shell = ShellWithText("west");
        var app = BuildEditApp<TwoArgCommand, TwoArgSettings>(settings =>
            {
                observedProfile = settings.Profile;
                observedRegion = settings.Region;
                return TigerCliEditLoad<TwoArgSettings>.Found(new TwoArgSettings { Region = "east" });
            },
            builder => builder.SetDefaultPromptMode(TigerCliPromptMode.Yes));

        var result = await RunCapturedAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("reporting", observedProfile);   // selector from CLI
        Assert.Equal("west", observedRegion);         // loader sees the prompted value, not ""
        Assert.Contains("region=west", result.Stdout); // prompted value wins over existing "east"
    }

    [Fact]
    public async Task NotFound_ReturnsFrameworkError_AndHandlerDoesNotRun()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("edit-test")
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.InvalidArguments, 77)
            .AddCommand<MergeCommand>("edit", b =>
                b.AsEdit<MergeSettings>(_ => TigerCliEditLoad<MergeSettings>.NotFound()))
            .Build();

        var result = await RunCapturedAsync(app, ["edit", "reporting", "--non-interactive"], new TestShell());

        Assert.Equal(77, result.ExitCode);
        Assert.Contains("Cannot find 'reporting' to edit.", result.Stderr);
        Assert.Equal(string.Empty, result.Stdout);
    }

    // ── Pre-load selector argument resolution ────────────────────────

    [Fact]
    public async Task MissingPromptableSelector_IsPromptedBeforeLoader_AndLoaderReceivesIt()
    {
        string? observed = null;
        var shell = ShellWithText("reporting");
        var app = BuildEditApp<PromptableSelectorCommand, PromptableSelectorSettings>(settings =>
        {
            observed = settings.Profile;
            return TigerCliEditLoad<PromptableSelectorSettings>.Found(
                new PromptableSelectorSettings { Server = "sql-old" });
        });

        var result = await RunCapturedAsync(app, ["edit"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("reporting", observed);              // loader saw the prompted selector
        Assert.Contains("profile=reporting", result.Stdout); // handler executed with it
        Assert.Contains("server=sql-old", result.Stdout);    // existing value merged after load
    }

    [Fact]
    public async Task SuppliedSelector_IsNotPromptedBeforeLoader()
    {
        var shell = new TestShell();
        var app = BuildEditApp<PromptableSelectorCommand, PromptableSelectorSettings>(_ =>
            TigerCliEditLoad<PromptableSelectorSettings>.Found(new PromptableSelectorSettings { Server = "sql-old" }));

        var result = await RunCapturedAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("profile=reporting", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task MissingSelector_NonInteractive_FailsBeforeLoader_AndLoaderNotCalled()
    {
        var loaderCalled = false;
        var app = BuildEditApp<PromptableSelectorCommand, PromptableSelectorSettings>(_ =>
            {
                loaderCalled = true;
                return TigerCliEditLoad<PromptableSelectorSettings>.Found(new PromptableSelectorSettings());
            },
            builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.MissingRequiredArgument, 66));

        var result = await RunCapturedAsync(app, ["edit", "--non-interactive"], new TestShell());

        Assert.Equal(66, result.ExitCode);
        Assert.False(loaderCalled);
        Assert.Equal(string.Empty, result.Stdout);
    }

    [Fact]
    public async Task PromptedSelector_IsNotOverwrittenByExisting()
    {
        var shell = ShellWithText("reporting");
        var app = BuildEditApp<PromptableSelectorCommand, PromptableSelectorSettings>(_ =>
            TigerCliEditLoad<PromptableSelectorSettings>.Found(
                new PromptableSelectorSettings { Profile = "different", Server = "sql-old" }));

        var result = await RunCapturedAsync(app, ["edit"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("profile=reporting", result.Stdout);
    }

    [Fact]
    public async Task ProviderBackedSelector_IsPromptedBeforeLoader()
    {
        string? observed = null;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the first provider choice
        var app = BuildEditApp<ProviderSelectorCommand, ProviderSelectorSettings>(
            settings =>
            {
                observed = settings.Profile;
                return TigerCliEditLoad<ProviderSelectorSettings>.Found(new ProviderSelectorSettings());
            },
            builder => builder.ConfigureProviders(providers =>
                providers.Add("profiles", _ => Strings("reporting", "sales"))));

        var result = await RunCapturedAsync(app, ["edit"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("reporting", observed);
        Assert.Contains("profile=reporting", result.Stdout);
    }

    [Fact]
    public async Task CancelingSelectorPrompt_CancelsCommand_AndLoaderNotCalled()
    {
        var loaderCalled = false;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Escape); // cancel the selector prompt
        var app = BuildEditApp<PromptableSelectorCommand, PromptableSelectorSettings>(_ =>
            {
                loaderCalled = true;
                return TigerCliEditLoad<PromptableSelectorSettings>.Found(new PromptableSelectorSettings());
            },
            builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.Cancelled, 88));

        var result = await RunCapturedAsync(app, ["edit"], shell);

        Assert.Equal(88, result.ExitCode);
        Assert.False(loaderCalled);
        Assert.Equal(string.Empty, result.Stdout);
    }

    [Fact]
    public async Task NonPromptableMissingSelector_FailsBeforeLoader_AndLoaderNotCalled()
    {
        // Semi-interactive, but the selector is Promptable.No: it cannot be prompted, so a
        // missing value must be rejected before the loader rather than reaching it empty.
        var loaderCalled = false;
        var shell = new TestShell();
        var app = BuildEditApp<NonPromptableSelectorCommand, NonPromptableSelectorSettings>(_ =>
            {
                loaderCalled = true;
                return TigerCliEditLoad<NonPromptableSelectorSettings>.Found(new NonPromptableSelectorSettings());
            },
            builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.MissingRequiredArgument, 66));

        var result = await RunCapturedAsync(app, ["edit"], shell);

        Assert.Equal(66, result.ExitCode);
        Assert.False(loaderCalled);
        Assert.Equal(0, shell.Terminal.ReadCount);
        Assert.Equal(string.Empty, result.Stdout);
    }

    [Fact]
    public async Task MissingDefaultPromptableSelector_NonInteractive_DoesNotCallLoaderWithEmpty()
    {
        // Dogfood-equivalent: `edit` with the <name> selector omitted, non-interactive. The
        // loader mimics the real app store, which throws on an empty name. The fix must prevent
        // the loader call entirely (clean missing-argument error), not catch the exception.
        var loaderCalled = false;
        var app = BuildEditApp<MergeCommand, MergeSettings>(settings =>
            {
                loaderCalled = true;
                if (string.IsNullOrWhiteSpace(settings.Profile))
                    throw new ArgumentException(
                        "The value cannot be an empty string or composed entirely of whitespace.", "name");
                return TigerCliEditLoad<MergeSettings>.Found(new MergeSettings());
            },
            builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.MissingRequiredArgument, 66));

        var result = await RunCapturedAsync(app, ["edit", "--non-interactive"], new TestShell());

        Assert.Equal(66, result.ExitCode); // missing-argument error, not an unhandled exception
        Assert.False(loaderCalled);        // loader was never called with an empty selector
        Assert.Equal(string.Empty, result.Stdout);
        Assert.DoesNotContain("ArgumentException", result.Stderr);
    }

    [Fact]
    public async Task MissingDefaultPromptableSelector_SemiInteractive_IsPromptedBeforeLoader()
    {
        // Dogfood-equivalent: `edit` with the <name> selector omitted, semi-interactive. The
        // selector uses the default prompt rules (required, so promptable) and is prompted
        // before the loader; the loader sees the prompted value.
        string? observed = null;
        var shell = ShellWithText("reporting");
        var app = BuildEditApp<MergeCommand, MergeSettings>(settings =>
        {
            observed = settings.Profile;
            return TigerCliEditLoad<MergeSettings>.Found(new MergeSettings { Server = "sql-old" });
        });

        var result = await RunCapturedAsync(app, ["edit"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("reporting", observed);
        Assert.Contains("profile=reporting", result.Stdout);
        Assert.Contains("server=sql-old", result.Stdout);
    }

    // ── Part 5: edit-only current-value prompting ────────────────────

    [Fact]
    public async Task NonEditableOption_IsNotPromptedInEditMode()
    {
        var shell = new TestShell();
        var app = BuildEditApp<NonEditableOptionCommand, NonEditableOptionSettings>(_ =>
            TigerCliEditLoad<NonEditableOptionSettings>.Found(new NonEditableOptionSettings { Note = "keep" }),
            builder => builder.SetDefaultPromptMode(TigerCliPromptMode.Yes));

        var result = await RunCapturedAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("note=keep", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    [Fact]
    public async Task EditStringPrompt_UsesExistingValueAsDefault()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept prefilled existing value
        var app = BuildEditApp<EditableOptionCommand, EditableOptionSettings>(_ =>
            TigerCliEditLoad<EditableOptionSettings>.Found(new EditableOptionSettings { Server = "sql-old" }));

        var result = await RunCapturedAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("server=sql-old", result.Stdout);
    }

    [Fact]
    public async Task EditPrompts_PreselectExistingStringEnumBoolAndFlags()
    {
        var shell = new TestShell();
        // Accept the preselected existing value at each prompt: text, enum, confirm, flags.
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = BuildEditApp<SeedCommand, SeedSettings>(_ =>
            TigerCliEditLoad<SeedSettings>.Found(new SeedSettings
            {
                Server = "sql-old",
                Auth = AuthMode.Sql,
                Trusted = false,
                Features = Feature.Write
            }));

        var result = await RunCapturedAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("server=sql-old;auth=Sql;trusted=False;features=Write", result.Stdout);
    }

    [Fact]
    public async Task CliSuppliedEditableValue_IsNotPromptedInEditMode()
    {
        var shell = new TestShell();
        var app = BuildEditApp<EditableOptionCommand, EditableOptionSettings>(_ =>
            TigerCliEditLoad<EditableOptionSettings>.Found(new EditableOptionSettings { Server = "sql-old" }));

        var result = await RunCapturedAsync(app, ["edit", "reporting", "--server", "sql-new"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("server=sql-new", result.Stdout);
        Assert.Equal(0, shell.Terminal.ReadCount);
    }

    // ── Part 2/6: provider validation and context ────────────────────

    [Fact]
    public async Task ProviderContext_IncludesSelectorArguments()
    {
        string? observedProfile = null;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // select first valid choice
        var app = BuildEditApp<ProviderEditCommand, ProviderEditSettings>(
            _ => TigerCliEditLoad<ProviderEditSettings>.Found(new ProviderEditSettings { Database = "master" }),
            builder => builder.ConfigureProviders(providers => providers.Add("databases", ctx =>
            {
                observedProfile = ctx.GetOptionValue<string>("profile");
                return Strings("master", "Reports");
            })));

        var result = await RunCapturedAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("reporting", observedProfile);
    }

    [Fact]
    public async Task ProviderBacked_CliInvalidValue_FailsInAddMode()
    {
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("edit-test")
            .SetDefaultCommand<AddProviderCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers => providers.Add("databases", _ => Databases()))
            .Build();

        var result = await RunCapturedAsync(app, ["--database", "bogus", "--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Invalid value for --database: bogus is not an available choice.", result.Stderr);
    }

    [Fact]
    public async Task ProviderBacked_CliInvalidValue_FailsInEditMode()
    {
        var app = BuildEditApp<ProviderEditCommand, ProviderEditSettings>(
            _ => TigerCliEditLoad<ProviderEditSettings>.Found(new ProviderEditSettings { Database = "master" }),
            builder => builder
                .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
                .ConfigureProviders(providers => providers.Add("databases", _ => Databases())));

        var result = await RunCapturedAsync(
            app, ["edit", "reporting", "--database", "bogus", "--non-interactive"], new TestShell());

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Invalid value for --database: bogus is not an available choice.", result.Stderr);
    }

    [Fact]
    public async Task ProviderBacked_InvalidInitializerDefault_FailsWhenValidationApplies()
    {
        var shell = new TestShell();
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("edit-test")
            .SetDefaultCommand<AddProviderDefaultCommand>()
            .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
            .ConfigureProviders(providers => providers.Add("databases", _ => Databases()))
            .Build();

        var result = await RunCapturedAsync(app, ["--non-interactive"], shell);

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Invalid value for --database: bogus is not an available choice.", result.Stderr);
    }

    [Fact]
    public async Task ProviderBacked_StaleExistingValue_FailsInNonInteractiveEdit()
    {
        var app = BuildEditApp<ProviderEditCommand, ProviderEditSettings>(
            _ => TigerCliEditLoad<ProviderEditSettings>.Found(new ProviderEditSettings { Database = "OldReporting" }),
            builder => builder
                .UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45)
                .ConfigureProviders(providers => providers.Add("databases", _ => Databases())));

        var result = await RunCapturedAsync(app, ["edit", "reporting", "--non-interactive"], new TestShell());

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Invalid value for --database: OldReporting is not an available choice.", result.Stderr);
    }

    [Fact]
    public async Task ProviderBacked_StaleExistingValue_PromptsForReplacementInSemiInteractiveEdit()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.DownArrow); // move off first choice
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);     // select a valid replacement
        var app = BuildEditApp<ProviderEditCommand, ProviderEditSettings>(
            _ => TigerCliEditLoad<ProviderEditSettings>.Found(new ProviderEditSettings { Database = "OldReporting" }),
            builder => builder.ConfigureProviders(providers => providers.Add("databases", _ => Strings("master", "Reports"))));

        var result = await RunCapturedAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=Reports", result.Stdout);
        Assert.DoesNotContain("OldReporting", shell.Terminal.LastRenderedText);
    }

    [Fact]
    public async Task ProviderValidation_OptOut_AllowsStaleValue()
    {
        var app = BuildEditApp<ProviderOptOutCommand, ProviderOptOutSettings>(
            _ => TigerCliEditLoad<ProviderOptOutSettings>.Found(new ProviderOptOutSettings { Database = "OldReporting" }),
            builder => builder.ConfigureProviders(providers => providers.Add("databases", _ => Databases())));

        var result = await RunCapturedAsync(app, ["edit", "reporting", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=OldReporting", result.Stdout);
    }

    [Fact]
    public async Task NonEditableProviderBackedField_IsNotProviderValidated()
    {
        var app = BuildEditApp<ProviderNonEditableCommand, ProviderNonEditableSettings>(
            _ => TigerCliEditLoad<ProviderNonEditableSettings>.Found(new ProviderNonEditableSettings { Legacy = "OldLegacy" }),
            builder => builder.ConfigureProviders(providers => providers.Add("databases", _ => Databases())));

        var result = await RunCapturedAsync(app, ["edit", "reporting", "--non-interactive"], new TestShell());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("legacy=OldLegacy", result.Stdout);
    }

    [Fact]
    public async Task ProviderBackedValue_FromProviderChoice_PassesValidationInNormalCommand()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // select first provider choice
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("edit-test")
            .SetDefaultCommand<AddProviderCommand>()
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .ConfigureProviders(providers => providers.Add("databases", _ => Databases()))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("database=master", result.Stdout);
    }

    [Fact]
    public async Task AddMode_ProviderBacked_PreselectsInitializerDefaultWhenMatchesChoice()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the preselected default choice
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("edit-test")
            .SetDefaultCommand<AddProviderValidDefaultCommand>()
            .ConfigureProviders(providers => providers.Add("databases", _ => Databases()))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        // "Reports" is the initializer default and a valid choice; it must be preselected,
        // not the first choice ("master").
        Assert.Contains("database=Reports", result.Stdout);
    }

    [Fact]
    public async Task AddMode_ProviderBacked_StaleInitializerDefault_IsNotInjected()
    {
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept whatever is preselected
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("edit-test")
            .SetDefaultCommand<AddProviderStaleDefaultCommand>()
            .ConfigureProviders(providers => providers.Add("databases", _ => Strings("master", "Reports")))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        // "bogus" is not a provider choice, so it is never injected; the prompt falls back to
        // the first valid choice.
        Assert.Contains("database=master", result.Stdout);
        Assert.DoesNotContain("bogus", shell.Terminal.LastRenderedText);
    }

    // ── Part 7: EditProvider (edit-only provider override) ───────────

    [Fact]
    public async Task EditProvider_OnArgument_IsIgnoredInAddMode_UsesTextInput()
    {
        // Add/normal command: EditProvider is ignored and there is no normal Provider, so the
        // missing <name> is prompted as plain text input — the provider is never invoked.
        var providerInvoked = false;
        var shell = ShellWithText("newname");
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("edit-test")
            .SetDefaultCommand<SharedNameCommand>()
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .ConfigureProviders(providers => providers.Add("profiles", _ =>
            {
                providerInvoked = true;
                return Strings("reporting", "sales");
            }))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.False(providerInvoked);           // EditProvider ignored in add mode
        Assert.Contains("name=newname", result.Stdout);
    }

    [Fact]
    public async Task EditProvider_OnSelector_IsUsedBeforeLoader_AndSelectedValuePassedToLoader()
    {
        // Edit command: the shared <name> uses EditProvider, so a missing selector renders the
        // provider-backed select before the loader, and the chosen value reaches the loader.
        string? observed = null;
        var providerInvoked = false;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the first provider choice
        var app = BuildEditApp<SharedNameCommand, SharedNameSettings>(
            settings =>
            {
                observed = settings.Name;
                return TigerCliEditLoad<SharedNameSettings>.Found(new SharedNameSettings());
            },
            builder => builder.ConfigureProviders(providers => providers.Add("profiles", _ =>
            {
                providerInvoked = true;
                return Strings("reporting", "sales");
            })));

        var result = await RunCapturedAsync(app, ["edit"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.True(providerInvoked);            // EditProvider drives the pre-load prompt
        Assert.Equal("reporting", observed);     // selected value passed to the loader
        Assert.Contains("name=reporting", result.Stdout);
    }

    [Fact]
    public async Task EditProvider_OverridesProvider_OnArgument_InEditMode()
    {
        // Both Provider and EditProvider are set; edit mode must use EditProvider only.
        string? observed = null;
        var addInvoked = false;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = BuildEditApp<BothProvidersCommand, BothProvidersSettings>(
            settings =>
            {
                observed = settings.Name;
                return TigerCliEditLoad<BothProvidersSettings>.Found(new BothProvidersSettings());
            },
            builder => builder.ConfigureProviders(providers =>
            {
                providers.Add("addprofiles", _ => { addInvoked = true; return Strings("add-one"); });
                providers.Add("editprofiles", _ => Strings("edit-one"));
            }));

        var result = await RunCapturedAsync(app, ["edit"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.False(addInvoked);                // normal Provider not used in edit mode
        Assert.Equal("edit-one", observed);      // EditProvider overrode Provider
    }

    [Fact]
    public async Task Provider_OnArgument_IsUsedInNormalMode_WhenEditProviderAbsent()
    {
        // Normal command: the argument's normal Provider still drives the missing-value prompt.
        var providerInvoked = false;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // select first provider choice
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("edit-test")
            .SetDefaultCommand<ProviderOnlySharedCommand>()
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .ConfigureProviders(providers => providers.Add("names", _ =>
            {
                providerInvoked = true;
                return Strings("alpha", "beta");
            }))
            .Build();

        var result = await RunCapturedAsync(app, [], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.True(providerInvoked);
        Assert.Contains("name=alpha", result.Stdout);
    }

    [Fact]
    public async Task EditMode_FallsBackToProvider_WhenEditProviderAbsent()
    {
        // No EditProvider configured; edit mode falls back to the normal Provider before the loader.
        string? observed = null;
        var providerInvoked = false;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        var app = BuildEditApp<ProviderOnlySharedCommand, ProviderOnlySharedSettings>(
            settings =>
            {
                observed = settings.Name;
                return TigerCliEditLoad<ProviderOnlySharedSettings>.Found(new ProviderOnlySharedSettings());
            },
            builder => builder.ConfigureProviders(providers => providers.Add("names", _ =>
            {
                providerInvoked = true;
                return Strings("alpha", "beta");
            })));

        var result = await RunCapturedAsync(app, ["edit"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.True(providerInvoked);            // fell back to Provider in edit mode
        Assert.Equal("alpha", observed);
    }

    [Fact]
    public async Task EditSelector_WithExplicitProvider_SuppliedUnknownValue_IsLoaderAuthoritative()
    {
        // A supplied edit selector is resolved by the edit loader, not by provider validation:
        // "bogus" is not among the provider's choices, but the failure must be the loader's
        // not-found error. The provider decides prompt choices; the loader decides existence.
        var app = BuildEditApp<ProviderOnlySharedCommand, ProviderOnlySharedSettings>(
            _ => TigerCliEditLoad<ProviderOnlySharedSettings>.NotFound(),
            builder => builder.ConfigureProviders(providers =>
                providers.Add("names", _ => Strings("alpha", "beta"))));

        var result = await RunCapturedAsync(app, ["edit", "bogus", "--non-interactive"], new TestShell());

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Cannot find 'bogus' to edit.", result.Stderr);
        Assert.DoesNotContain("is not an available choice", result.Stderr);
    }

    [Fact]
    public async Task EditProvider_OnOption_OverridesProvider_InEditMode()
    {
        // Option-level symmetry: an editable option with both providers uses EditProvider in edit mode.
        var addInvoked = false;
        var editInvoked = false;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the preselected existing value
        var app = BuildEditApp<OptionEditProviderCommand, OptionEditProviderSettings>(
            _ => TigerCliEditLoad<OptionEditProviderSettings>.Found(
                new OptionEditProviderSettings { Database = "edit-master" }),
            builder => builder.ConfigureProviders(providers =>
            {
                providers.Add("add-db", _ => { addInvoked = true; return Strings("add-master"); });
                providers.Add("edit-db", _ => { editInvoked = true; return Strings("edit-master", "edit-reports"); });
            }));

        var result = await RunCapturedAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.True(editInvoked);                // EditProvider drives the edit-mode prompt
        Assert.False(addInvoked);                // normal Provider not used in edit mode
        Assert.Contains("database=edit-master", result.Stdout);
    }

    [Fact]
    public async Task EditProvider_OnOption_IsIgnoredInAddMode_UsesProvider()
    {
        // Option-level symmetry: in add/normal mode the option uses Provider, not EditProvider.
        var addInvoked = false;
        var editInvoked = false;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept first add-db choice
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("edit-test")
            .SetDefaultCommand<OptionEditProviderCommand>()
            .SetDefaultPromptMode(TigerCliPromptMode.Yes)
            .ConfigureProviders(providers =>
            {
                providers.Add("add-db", _ => { addInvoked = true; return Strings("add-master"); });
                providers.Add("edit-db", _ => { editInvoked = true; return Strings("edit-master"); });
            })
            .Build();

        var result = await RunCapturedAsync(app, ["reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.True(addInvoked);                 // normal Provider used in add mode
        Assert.False(editInvoked);               // EditProvider ignored in add mode
        Assert.Contains("database=add-master", result.Stdout);
    }

    // ── Part 4: secret fields in edit mode ───────────────────────────

    [Fact]
    public async Task SecretEditableOption_IsSeeded_AndEnterKeepsValue_MaskingPlaintext()
    {
        // The existing (decrypted) password is seeded into settings; the secret prompt preselects
        // it, pressing Enter keeps it, and the plaintext is never rendered to the terminal.
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the seeded secret
        var app = BuildEditApp<SecretEditCommand, SecretEditSettings>(_ =>
            TigerCliEditLoad<SecretEditSettings>.Found(new SecretEditSettings { Password = "s3cret-old" }));

        var result = await RunCapturedAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("password=s3cret-old", result.Stdout);          // Enter kept the seeded value
        Assert.DoesNotContain("s3cret-old", shell.Terminal.LastRenderedText); // prompt stays masked
    }

    [Fact]
    public async Task SecretEdit_AllowCommandLineValueFalse_RejectsArgvSecret()
    {
        // AllowCommandLineValue=false still forbids an argv secret in edit mode, before the handler runs.
        var app = BuildEditApp<SecretEditCommand, SecretEditSettings>(
            _ => TigerCliEditLoad<SecretEditSettings>.Found(new SecretEditSettings { Password = "s3cret-old" }),
            builder => builder.UseExitCodes(0, -1).ExitKind(TigerCliExitKind.ValidationError, 45));

        var result = await RunCapturedAsync(
            app, ["edit", "reporting", "--password", "fromargv", "--non-interactive"], new TestShell());

        Assert.Equal(45, result.ExitCode);
        Assert.Contains("Option '--password' cannot be supplied on the command line.", result.Stderr);
    }

    [Fact]
    public async Task SecretEdit_DependentProvider_SeesEffectiveSeededSecret()
    {
        // The database provider depends on the current password. In edit mode the seeded (effective)
        // password must be visible to the provider so it can list databases without re-prompting it.
        string? observedPassword = null;
        var shell = new TestShell();
        shell.Terminal.EnqueueKey(ConsoleKey.Enter); // accept the preselected existing database
        var app = BuildEditApp<SecretDependentCommand, SecretDependentSettings>(
            _ => TigerCliEditLoad<SecretDependentSettings>.Found(
                new SecretDependentSettings { Password = "s3cret-old", Database = "master" }),
            builder => builder.ConfigureProviders(providers => providers.Add("databases", ctx =>
            {
                observedPassword = ctx.GetOptionValue<string>("--password");
                return Strings("master", "Reports");
            })));

        var result = await RunCapturedAsync(app, ["edit", "reporting"], shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("s3cret-old", observedPassword); // provider saw the effective seeded secret
        Assert.Contains("database=master", result.Stdout);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static TigerCliApp BuildEditApp<TCommand, TSettings>(
        Func<TSettings, TigerCliEditLoad<TSettings>> loader,
        Action<TigerCliAppBuilder>? configure = null)
        where TCommand : class, new()
        where TSettings : TigerCliSettings
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("edit-test")
            .AddCommand<TCommand>("edit", b => b.AsEdit(loader));

        configure?.Invoke(builder);
        return builder.Build();
    }

    private static IReadOnlyList<string> Databases() => Strings("master", "Reports", "ReportingArchive");

    private static IReadOnlyList<string> Strings(params string[] values) => values;

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
