using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests
{
    public sealed class TigerConsoleThemeOptionTests
    {
        private const string ThemeEnvironmentVariable = "TIGERCLI_THEME";

        private sealed class NamedTheme : ThemeBase
        {
            public override string Name { get; }
            public NamedTheme(string name) => Name = name;

            protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Gray));
            protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
            protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.Cyan));
            protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
            protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
            protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
        }

        private sealed class ProbeSettings : TigerCliSettings { }

        private sealed class ProbeCommand : TigerCliAsyncCommandHandler<ProbeSettings>
        {
            public static bool Executed;
            public static string? ThemeAtExecution;
            public static Type? ThemeTypeAtExecution;

            public override Task<int> ExecuteAsync(ProbeSettings settings)
            {
                Executed = true;
                ThemeAtExecution = TigerConsole.CurrentTheme.Name;
                ThemeTypeAtExecution = TigerConsole.CurrentTheme.GetType();
                return Task.FromResult(0);
            }
        }

        private static TigerCliApp CreateApp() =>
            TigerCliApp.CreateBuilder()
                .SetApplicationName("themed")
                .SetInteractionMode(TigerCliInteractionMode.NonInteractive)
                .SetDefaultCommand<ProbeCommand>()
                .Build();

        // ---- Registry: names come from instances; "default" is not selectable ----

        [Fact]
        public void FrameworkLookup_UsesThemeNameFromInstance_NotConstants()
        {
            // Looking up by the instance's Name and by the literal resolve to the same framework theme,
            // proving the registry is keyed by ITheme.Name rather than duplicated constants.
            Assert.Same(TigerConsole.GetTheme(new DarkTheme().Name), TigerConsole.GetTheme("dark"));
            Assert.Same(TigerConsole.GetTheme(new LightTheme().Name), TigerConsole.GetTheme("light"));
            Assert.Same(TigerConsole.GetTheme(new TigerBlueTheme().Name), TigerConsole.GetTheme("tiger-blue"));
        }

        [Fact]
        public void GetThemeNames_ExcludesDefaultAlias()
        {
            Assert.DoesNotContain(
                TigerConsole.GetThemeNames(),
                name => string.Equals(name, "default", StringComparison.OrdinalIgnoreCase));
        }

        // ---- --theme help ----

        [Fact]
        public async Task ThemeOption_AppearsInHelp_WithSelectableThemes()
        {
            var (_, stdout, _) = await RunCapturedWithThemeEnvironmentAsync(CreateApp(), ["--help"], null);

            Assert.Contains("--theme", stdout);
            Assert.Contains("dark", stdout);
            Assert.Contains("light", stdout);
            Assert.Contains("tiger-blue", stdout);
        }

        [Fact]
        public async Task ThemeOption_Help_IncludesCustomThemeRegisteredBeforeRun()
        {
            TigerConsole.AddOrUpdateCustomTheme(new NamedTheme("opt-help-custom"));

            var (_, stdout, _) = await RunCapturedWithThemeEnvironmentAsync(CreateApp(), ["--help"], null);

            Assert.Contains("opt-help-custom", stdout);
        }

        [Theory]
        [InlineData("dark")]
        [InlineData("light")]
        [InlineData("tiger-blue")]
        public async Task Help_RendersSemanticHeadings_UnderEachFrameworkTheme(string themeName)
        {
            // Help headings were migrated from raw [cyan] to the semantic [Accent] token (and the
            // default marker to [Muted]). Those must resolve through every framework theme without an
            // unknown-tag throw. Output capture strips styling, so the heading text is theme-invariant.
            var original = TigerConsole.CurrentTheme;
            try
            {
                TigerConsole.CurrentTheme = TigerConsole.GetTheme(themeName);

                var (exit, stdout, stderr) = await RunCapturedWithThemeEnvironmentAsync(CreateApp(), ["--help"], null);

                Assert.Equal(0, exit);
                Assert.Equal(string.Empty, stderr);
                Assert.Contains("--theme", stdout);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        // ---- --theme resolution ----

        [Fact]
        public async Task ValidTheme_SetsCurrentThemeForTheRun()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                ResetProbe();

                var (exit, _, _) = await RunCapturedAsync(CreateApp(), ["--theme", "light"]);

                Assert.Equal(0, exit);
                Assert.True(ProbeCommand.Executed);
                Assert.Equal("light", ProbeCommand.ThemeAtExecution);
                Assert.Equal(typeof(LightTheme), ProbeCommand.ThemeTypeAtExecution);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        [Fact]
        public async Task ValidTheme_AcceptsCustomThemeRegisteredBeforeRun()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                TigerConsole.AddOrUpdateCustomTheme(new NamedTheme("opt-run-custom"));
                ResetProbe();

                var (exit, _, _) = await RunCapturedAsync(CreateApp(), ["--theme", "opt-run-custom"]);

                Assert.Equal(0, exit);
                Assert.Equal("opt-run-custom", ProbeCommand.ThemeAtExecution);
                Assert.Equal(typeof(NamedTheme), ProbeCommand.ThemeTypeAtExecution);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        [Fact]
        public async Task InvalidTheme_FailsBeforeHandlerExecution()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                ResetProbe();

                var (exit, _, stderr) = await RunCapturedAsync(CreateApp(), ["--theme", "no-such-theme"]);

                Assert.NotEqual(0, exit);
                Assert.False(ProbeCommand.Executed);
                Assert.Contains("Unsupported --theme value 'no-such-theme'", stderr);
                Assert.Contains("no-such-theme", stderr);
                Assert.DoesNotContain(ThemeEnvironmentVariable, stderr);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        [Fact]
        public async Task ThemeDefault_IsRejected()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                ResetProbe();

                var (exit, _, stderr) = await RunCapturedAsync(CreateApp(), ["--theme", "default"]);

                Assert.NotEqual(0, exit);
                Assert.False(ProbeCommand.Executed);
                Assert.Contains("Unsupported --theme value 'default'", stderr);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        // ---- TIGERCLI_THEME resolution ----

        [Theory]
        [InlineData("dark", "DarkTheme")]
        [InlineData("light", "LightTheme")]
        [InlineData("tiger-blue", "TigerBlueTheme")]
        public async Task ThemeEnvironment_SelectsFrameworkTheme_WhenThemeOptionIsAbsent(
            string themeName,
            string expectedTypeName)
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                TigerConsole.CurrentTheme = TigerConsole.GetTheme("dark");
                ResetProbe();

                var (exit, _, _) = await RunCapturedWithThemeEnvironmentAsync(CreateApp(), [], themeName);

                Assert.Equal(0, exit);
                Assert.True(ProbeCommand.Executed);
                Assert.Equal(themeName, ProbeCommand.ThemeAtExecution);
                Assert.Equal(expectedTypeName, ProbeCommand.ThemeTypeAtExecution?.Name);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        [Fact]
        public async Task ThemeEnvironment_AcceptsCustomThemeRegisteredBeforeRun()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                TigerConsole.AddOrUpdateCustomTheme(new NamedTheme("env-run-custom"));
                ResetProbe();

                var (exit, _, _) = await RunCapturedWithThemeEnvironmentAsync(CreateApp(), [], "env-run-custom");

                Assert.Equal(0, exit);
                Assert.True(ProbeCommand.Executed);
                Assert.Equal("env-run-custom", ProbeCommand.ThemeAtExecution);
                Assert.Equal(typeof(NamedTheme), ProbeCommand.ThemeTypeAtExecution);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        [Fact]
        public async Task EmptyThemeEnvironment_IsIgnored()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                TigerConsole.CurrentTheme = TigerConsole.GetTheme("tiger-blue");
                ResetProbe();

                var (exit, _, _) = await RunCapturedWithThemeEnvironmentAsync(CreateApp(), [], string.Empty);

                Assert.Equal(0, exit);
                Assert.True(ProbeCommand.Executed);
                Assert.Equal("tiger-blue", ProbeCommand.ThemeAtExecution);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        [Fact]
        public async Task WhitespaceThemeEnvironment_IsIgnored()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                TigerConsole.CurrentTheme = TigerConsole.GetTheme("light");
                ResetProbe();

                var (exit, _, _) = await RunCapturedWithThemeEnvironmentAsync(CreateApp(), [], "   ");

                Assert.Equal(0, exit);
                Assert.True(ProbeCommand.Executed);
                Assert.Equal("light", ProbeCommand.ThemeAtExecution);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        [Fact]
        public async Task InvalidThemeEnvironment_FailsBeforeHandlerExecution()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                ResetProbe();

                var (exit, _, stderr) = await RunCapturedWithThemeEnvironmentAsync(
                    CreateApp(),
                    [],
                    "no-such-theme");

                Assert.NotEqual(0, exit);
                Assert.False(ProbeCommand.Executed);
                Assert.Contains("Invalid TIGERCLI_THEME value 'no-such-theme'", stderr);
                Assert.Contains("Available themes:", stderr);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        [Fact]
        public async Task ThemeOption_OverridesThemeEnvironment()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                TigerConsole.CurrentTheme = TigerConsole.GetTheme("tiger-blue");
                ResetProbe();

                var (exit, _, _) = await RunCapturedWithThemeEnvironmentAsync(
                    CreateApp(),
                    ["--theme", "light"],
                    "dark");

                Assert.Equal(0, exit);
                Assert.True(ProbeCommand.Executed);
                Assert.Equal("light", ProbeCommand.ThemeAtExecution);
                Assert.Equal(typeof(LightTheme), ProbeCommand.ThemeTypeAtExecution);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        [Fact]
        public async Task ThemeEnvironmentDefault_IsRejected()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                ResetProbe();

                var (exit, _, stderr) = await RunCapturedWithThemeEnvironmentAsync(CreateApp(), [], "default");

                Assert.NotEqual(0, exit);
                Assert.False(ProbeCommand.Executed);
                Assert.Contains("Invalid TIGERCLI_THEME value 'default'", stderr);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
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
            TigerCliApp app,
            string[] args,
            string? value)
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

        private static void ResetProbe()
        {
            ProbeCommand.Executed = false;
            ProbeCommand.ThemeAtExecution = null;
            ProbeCommand.ThemeTypeAtExecution = null;
        }
    }
}
