using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests
{
    public class TigerConsoleThemeRegistryTests
    {
        // Minimal custom theme with a configurable name/accent for registry tests.
        private sealed class NamedCustomTheme : ThemeBase
        {
            private readonly CliColor _accent;
            public override string Name { get; }

            public NamedCustomTheme(string name, CliColor accent = CliColor.Magenta)
            {
                Name = name;
                _accent = accent;
            }

            protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Gray));
            protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkGray));
            protected override CliCellStyle Accent => new(new CliCharStyle(_accent));
            protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Gray));
            protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Green));
            protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.Black));
        }

        // ---- Theme identity ----

        [Fact]
        public void FrameworkThemes_ExposeStableNames()
        {
            Assert.Equal("dark", new DarkTheme().Name);
            Assert.Equal("light", new LightTheme().Name);
            Assert.Equal("tiger-blue", new TigerBlueTheme().Name);
        }

        // ---- CurrentTheme ----

        [Fact]
        public void CurrentTheme_DefaultsToDarkTheme()
        {
            Assert.IsType<DarkTheme>(TigerConsole.CurrentTheme);
        }

        [Fact]
        public void CurrentTheme_CannotBeSetToNull()
        {
            Assert.Throws<ArgumentNullException>(() => TigerConsole.CurrentTheme = null!);
        }

        [Fact]
        public void CurrentTheme_CanBeSetToLightTheme()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                var light = new LightTheme();
                TigerConsole.CurrentTheme = light;
                Assert.Same(light, TigerConsole.CurrentTheme);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        [Fact]
        public void CurrentTheme_CanBeSetToCustomTheme()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                var custom = new NamedCustomTheme("test-custom-current");
                TigerConsole.CurrentTheme = custom;
                Assert.Same(custom, TigerConsole.CurrentTheme);
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        // ---- GetTheme / TryGetTheme ----

        [Fact]
        public void GetTheme_Default_ReturnsCurrentTheme()
        {
            var original = TigerConsole.CurrentTheme;
            try
            {
                var light = new LightTheme();
                TigerConsole.CurrentTheme = light;
                Assert.Same(light, TigerConsole.GetTheme("default"));
            }
            finally
            {
                TigerConsole.CurrentTheme = original;
            }
        }

        [Fact]
        public void GetTheme_FrameworkNames_ReturnBuiltInThemes()
        {
            Assert.IsType<DarkTheme>(TigerConsole.GetTheme("dark"));
            Assert.IsType<LightTheme>(TigerConsole.GetTheme("light"));
            Assert.IsType<TigerBlueTheme>(TigerConsole.GetTheme("tiger-blue"));
        }

        [Fact]
        public void GetTheme_IsCaseInsensitive()
        {
            Assert.IsType<DarkTheme>(TigerConsole.GetTheme("DARK"));
            Assert.IsType<TigerBlueTheme>(TigerConsole.GetTheme("Tiger-Blue"));
        }

        [Fact]
        public void GetTheme_UnknownName_Throws()
        {
            Assert.Throws<ArgumentException>(() => TigerConsole.GetTheme("no-such-theme"));
        }

        [Fact]
        public void TryGetTheme_UnknownName_ReturnsFalse()
        {
            Assert.False(TigerConsole.TryGetTheme("no-such-theme", out var theme));
            Assert.Null(theme);
        }

        [Fact]
        public void TryGetTheme_NullOrWhitespace_ReturnsFalse()
        {
            Assert.False(TigerConsole.TryGetTheme(null, out _));
            Assert.False(TigerConsole.TryGetTheme("   ", out _));
        }

        // ---- AddOrUpdateCustomTheme ----

        [Fact]
        public void AddOrUpdateCustomTheme_RegistersByName()
        {
            var custom = new NamedCustomTheme("test-custom-register");
            TigerConsole.AddOrUpdateCustomTheme(custom);

            Assert.Same(custom, TigerConsole.GetTheme("test-custom-register"));
            Assert.True(TigerConsole.TryGetTheme("TEST-CUSTOM-REGISTER", out var byCi));
            Assert.Same(custom, byCi);
        }

        [Fact]
        public void AddOrUpdateCustomTheme_UpdatesExistingByName()
        {
            var first = new NamedCustomTheme("test-custom-update", CliColor.Magenta);
            var second = new NamedCustomTheme("test-custom-update", CliColor.Red);

            TigerConsole.AddOrUpdateCustomTheme(first);
            TigerConsole.AddOrUpdateCustomTheme(second);

            Assert.Same(second, TigerConsole.GetTheme("test-custom-update"));
        }

        [Theory]
        [InlineData("default")]
        [InlineData("dark")]
        [InlineData("light")]
        [InlineData("tiger-blue")]
        [InlineData("DARK")]
        public void AddOrUpdateCustomTheme_RejectsReservedNames(string reserved)
        {
            Assert.Throws<ArgumentException>(() => TigerConsole.AddOrUpdateCustomTheme(new NamedCustomTheme(reserved)));
        }

        [Fact]
        public void AddOrUpdateCustomTheme_RejectsNullTheme()
        {
            Assert.Throws<ArgumentNullException>(() => TigerConsole.AddOrUpdateCustomTheme(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void AddOrUpdateCustomTheme_RejectsEmptyOrWhitespaceName(string name)
        {
            Assert.Throws<ArgumentException>(() => TigerConsole.AddOrUpdateCustomTheme(new NamedCustomTheme(name)));
        }

        [Fact]
        public void GetThemeNames_IncludesFrameworkAndCustomThemes()
        {
            TigerConsole.AddOrUpdateCustomTheme(new NamedCustomTheme("test-custom-names"));

            var names = TigerConsole.GetThemeNames();

            Assert.Contains("dark", names);
            Assert.Contains("light", names);
            Assert.Contains("tiger-blue", names);
            Assert.Contains("test-custom-names", names);
        }

        // ---- Table styles follow the resolved theme ----

        [Fact]
        public void SameRecipe_UnderFrameworkThemes_ProducesDistinctTableStyles()
        {
            var dark = CliTableStyleRecipe.Roma.Resolve(new DarkTheme());
            var light = CliTableStyleRecipe.Roma.Resolve(new LightTheme());
            var blue = CliTableStyleRecipe.Roma.Resolve(new TigerBlueTheme());

            // Same recipe => same structure.
            Assert.Equal(dark.Orientation, light.Orientation);
            Assert.Equal(dark.FrameConfig.OuterFrame.Style, blue.FrameConfig.OuterFrame.Style);

            // Theme-specific header colors, all distinct.
            var darkHeader = dark.HeaderStyle?.CharStyle?.Foreground;
            var lightHeader = light.HeaderStyle?.CharStyle?.Foreground;
            var blueHeader = blue.HeaderStyle?.CharStyle?.Foreground;

            Assert.Equal(CliColor.Cyan, darkHeader);
            Assert.Equal(CliColor.DarkBlue, lightHeader);
            Assert.Equal(CliColor.White, blueHeader);
            Assert.True(darkHeader != lightHeader && lightHeader != blueHeader && darkHeader != blueHeader);
        }

        [Fact]
        public void ApplyStyle_WithExplicitTheme_UsesThatTheme()
        {
            var table = new CliTable().ApplyStyle(CliTableStyles.Create(CliTableStylePreset.Milano, new LightTheme()));

            Assert.Equal(CliColor.DarkYellow, table.Header.HeaderStyle?.CharStyle?.Foreground);
        }
    }
}
