using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineShellThemeTests
{
    private sealed class CustomTheme : ThemeBase
    {
        public override string Name => "inline-shell-test-custom";

        protected override CliCellStyle Text => new(new CliCharStyle(CliColor.Yellow));
        protected override CliCellStyle MutedText => new(new CliCharStyle(CliColor.DarkYellow));
        protected override CliCellStyle Accent => new(new CliCharStyle(CliColor.Magenta));
        protected override CliCellStyle Frame => new(new CliCharStyle(CliColor.Red));
        protected override CliCellStyle Selected => new(new CliCharStyle(CliColor.Black, CliColor.Yellow));
        protected override CliCellStyle Background => new(new CliCharStyle(null, CliColor.DarkMagenta));
    }

    [Fact]
    public void Theme_UsesCurrentThemeInsteadOfHardcodedDarkTheme()
    {
        var original = TigerConsole.CurrentTheme;
        try
        {
            var light = new LightTheme();
            TigerConsole.CurrentTheme = light;

            var shell = new TestShell();

            Assert.Same(light, shell.Theme);
            Assert.IsNotType<DarkTheme>(shell.Theme);
        }
        finally
        {
            TigerConsole.CurrentTheme = original;
        }
    }

    [Fact]
    public void Theme_UsesCustomCurrentTheme()
    {
        var original = TigerConsole.CurrentTheme;
        try
        {
            var custom = new CustomTheme();
            TigerConsole.CurrentTheme = custom;

            var shell = new TestShell();

            Assert.Same(custom, shell.Theme);
            Assert.Equal(CliColor.DarkMagenta, shell.Theme.Resolve(ThemeStyle.DialogSurface).CharStyle?.Background);
        }
        finally
        {
            TigerConsole.CurrentTheme = original;
        }
    }

    [Fact]
    public void Dialog_ResolvesTigerBlueStylingWhenTigerBlueIsCurrentTheme()
    {
        var original = TigerConsole.CurrentTheme;
        try
        {
            TigerConsole.CurrentTheme = new TigerBlueTheme();
            var shell = new TestShell();
            var select = new InlineSelect(shell, ["Red", "Green"]);
            var dialog = new InlineDialog(shell, "Pick one", select);

            var grid = dialog.ToGrid();

            Assert.Equal(CliColor.Navy, grid.DefaultCellStyle?.CharStyle?.Background);
            Assert.Equal(CliColor.Navy, grid.GetCellStyle(1, 1).CharStyle?.Background);
        }
        finally
        {
            TigerConsole.CurrentTheme = original;
        }
    }

    [Fact]
    public async Task ModalFlow_StillRendersAndConfirmsUnderLightTheme()
    {
        var original = TigerConsole.CurrentTheme;
        try
        {
            TigerConsole.CurrentTheme = new LightTheme();
            var ct = TestContext.Current.CancellationToken;
            var shell = new TestShell();
            var select = new InlineSelect(shell, ["Red", "Green"], preselectIndex: 1);
            var dialog = new InlineDialog(shell, "Pick one", select);

            shell.Terminal.EnqueueKey(ConsoleKey.Enter);
            var result = await shell.RunModalAsync(dialog, ct);

            Assert.Equal(DialogResultKind.Ok, result.Kind);
            Assert.Equal(1, result.Payload);
            Assert.Contains("Green", shell.Terminal.LastRenderedText);
        }
        finally
        {
            TigerConsole.CurrentTheme = original;
        }
    }
}
