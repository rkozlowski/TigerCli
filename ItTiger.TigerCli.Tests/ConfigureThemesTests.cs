using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// App-builder <c>ConfigureThemes</c> policy: app-scoped colour aliases and custom styles applied to
/// the run, disabling framework themes, and the opt-in (no implicit registration) rule for packages.
/// </summary>
public sealed class ConfigureThemesTests
{
    private const string ThemeEnvironmentVariable = "TIGERCLI_THEME";

    private sealed class ProbeSettings : TigerCliSettings { }

    private sealed class ProbeCommand : TigerCliAsyncCommandHandler<ProbeSettings>
    {
        public static bool BrandBlueAliasActive;
        public static bool ConnectionNameStyleActive;

        public override Task<int> ExecuteAsync(ProbeSettings settings)
        {
            BrandBlueAliasActive = TigerConsole.ColorAliases.Contains("BrandBlue");
            ConnectionNameStyleActive = TigerConsole.CustomStyles.Contains("ConnectionName");
            return Task.FromResult(0);
        }
    }

    // ---- Builder applies app-scoped aliases and custom styles to the run ----

    [Fact]
    public async Task ConfigureThemes_AppliesColorAliasesAndCustomStyles_ToTheRun()
    {
        await WithAppearanceIsolationAsync(async () =>
        {
            ProbeCommand.BrandBlueAliasActive = false;
            ProbeCommand.ConnectionNameStyleActive = false;

            var app = TigerCliApp.CreateBuilder()
                .SetApplicationName("themed")
                .SetInteractionMode(TigerCliInteractionMode.NonInteractive)
                .ConfigureThemes(themes =>
                {
                    themes.RegisterColorAlias("BrandBlue", CliColor.Blue1);
                    themes.RegisterCustomStyle("ConnectionName", ThemeStyle.Accent);
                })
                .SetDefaultCommand<ProbeCommand>()
                .Build();

            var (exit, _, _) = await RunCapturedAsync(app, []);

            Assert.Equal(0, exit);
            Assert.True(ProbeCommand.BrandBlueAliasActive);
            Assert.True(ProbeCommand.ConnectionNameStyleActive);
        });
    }

    // ---- Disabling a framework theme makes it unavailable ----

    private static TigerCliApp CreateAppWithDisabledTigerBlue() =>
        TigerCliApp.CreateBuilder()
            .SetApplicationName("themed")
            .SetInteractionMode(TigerCliInteractionMode.NonInteractive)
            .ConfigureThemes(themes => themes.DisableTheme("tiger-blue"))
            .SetDefaultCommand<ProbeCommand>()
            .Build();

    [Fact]
    public async Task DisabledTheme_IsRejectedBy_ThemeOption()
    {
        await WithAppearanceIsolationAsync(async () =>
        {
            var (exit, _, stderr) = await RunCapturedAsync(CreateAppWithDisabledTigerBlue(), ["--theme", "tiger-blue"]);

            Assert.NotEqual(0, exit);
            Assert.Contains("Unsupported --theme value 'tiger-blue'", stderr);
            Assert.DoesNotContain("tiger-blue", stderr["Unsupported --theme value 'tiger-blue'".Length..]);
        });
    }

    [Fact]
    public async Task EnabledTheme_StillResolves_WhenAnotherThemeIsDisabled()
    {
        await WithAppearanceIsolationAsync(async () =>
        {
            var (exit, _, stderr) = await RunCapturedAsync(CreateAppWithDisabledTigerBlue(), ["--theme", "dark"]);

            Assert.Equal(0, exit);
            Assert.Equal(string.Empty, stderr);
        });
    }

    [Fact]
    public async Task DisabledTheme_IsRejectedBy_ThemeEnvironment()
    {
        await WithAppearanceIsolationAsync(async () =>
        {
            var (exit, _, stderr) = await RunCapturedWithThemeEnvironmentAsync(
                CreateAppWithDisabledTigerBlue(), [], "tiger-blue");

            Assert.NotEqual(0, exit);
            Assert.Contains("Invalid TIGERCLI_THEME value 'tiger-blue'", stderr);
        });
    }

    [Fact]
    public async Task DisabledTheme_IsOmittedFromHelp()
    {
        await WithAppearanceIsolationAsync(async () =>
        {
            var (exit, stdout, _) = await RunCapturedAsync(CreateAppWithDisabledTigerBlue(), ["--help"]);

            Assert.Equal(0, exit);
            Assert.Contains("dark", stdout);
            Assert.Contains("light", stdout);
            Assert.DoesNotContain("tiger-blue", stdout);
        });
    }

    // ---- Opt-in: a package extension only registers when invoked ----

    [Fact]
    public async Task PackageExtension_RegistersOnlyWhenInvoked()
    {
        await WithAppearanceIsolationAsync(async () =>
        {
            // App that references the package but does NOT call its extension: nothing is registered.
            ProbeCommand.BrandBlueAliasActive = true;
            var notInvoked = TigerCliApp.CreateBuilder()
                .SetApplicationName("themed")
                .SetInteractionMode(TigerCliInteractionMode.NonInteractive)
                .ConfigureThemes(_ => { })
                .SetDefaultCommand<ProbeCommand>()
                .Build();
            await RunCapturedAsync(notInvoked, []);
            Assert.False(ProbeCommand.BrandBlueAliasActive);

            // App that explicitly opts in by calling the extension: alias is registered.
            ProbeCommand.BrandBlueAliasActive = false;
            var invoked = TigerCliApp.CreateBuilder()
                .SetApplicationName("themed")
                .SetInteractionMode(TigerCliInteractionMode.NonInteractive)
                .ConfigureThemes(themes => themes.AddSampleCompatibilityAliases())
                .SetDefaultCommand<ProbeCommand>()
                .Build();
            await RunCapturedAsync(invoked, []);
            Assert.True(ProbeCommand.BrandBlueAliasActive);
        });
    }

    // ---- Helpers ----

    private static async Task WithAppearanceIsolationAsync(Func<Task> body)
    {
        var originalAliases = TigerConsole.ColorAliases;
        var originalStyles = TigerConsole.CustomStyles;
        var originalTheme = TigerConsole.CurrentTheme;
        try
        {
            await body();
        }
        finally
        {
            TigerConsole.ColorAliases = originalAliases;
            TigerConsole.CustomStyles = originalStyles;
            TigerConsole.CurrentTheme = originalTheme;
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedAsync(
        TigerCliApp app, string[] args)
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

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCapturedWithThemeEnvironmentAsync(
        TigerCliApp app, string[] args, string? value)
    {
        var originalValue = Environment.GetEnvironmentVariable(ThemeEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(ThemeEnvironmentVariable, value);
            return await RunCapturedAsync(app, args);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ThemeEnvironmentVariable, originalValue);
        }
    }
}

/// <summary>
/// Sample opt-in theme/style "package" extension. A real library ships methods like this; nothing is
/// registered unless the app explicitly calls them from its <c>ConfigureThemes</c> block.
/// </summary>
internal static class SampleThemePackageExtensions
{
    public static TigerThemeConfiguration AddSampleCompatibilityAliases(this TigerThemeConfiguration themes)
    {
        themes.RegisterColorAlias("BrandBlue", CliColor.Blue1);
        return themes;
    }
}
