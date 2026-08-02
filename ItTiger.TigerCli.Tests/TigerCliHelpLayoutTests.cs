using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the help document's layout contract: every section is a key line followed by its
/// description on the next lines, indented; wrapping happens in the layout at the width the
/// destination sink reports, never in the terminal; and every rendered row fills that width so the
/// grid owns the complete themed document surface.
/// </summary>
public sealed class TigerCliHelpLayoutTests
{
    private const int DescriptionIndent = 6;

    private sealed class WideSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "source",
            Description = "The absolute or relative path of the single existing file that will be copied into the destination folder.")]
        public string Source { get; set; } = "";

        [TigerCliOption("-c|--connection-string",
            Description = "Connection string used to reach the remote service. When omitted the value is read from configuration and then from the environment.")]
        public string? Connection { get; set; }
    }

    private sealed class WideCommand : TigerCliAsyncCommandHandler<WideSettings>
    {
        public override Task<int> ExecuteAsync(WideSettings settings) => Task.FromResult(0);
    }

    private static TigerCliApp BuildApp() =>
        TigerCliApp.CreateBuilder()
            .SetApplicationName("file-copy")
            .AddDescription("Copies a single existing file into a selected destination folder, prompting when required values are missing.")
            .SetDefaultCommand<WideCommand>()
            .AddCommand<WideCommand>("copy-to-folder", "Copies one existing file into a selected destination folder.")
            .Build();

    // Runs a help invocation against a sink of a fixed width, so wrapping is forced deterministically
    // instead of depending on the machine's terminal.
    private static async Task<List<string>> RenderHelpAsync(int width, params string[] args)
    {
        var app = BuildApp();
        var sink = new TextSegmentLinesSink { SoftMaxWidth = width };
        using var scope = TigerConsole.PushOutputSink(sink);
        var shell = new TestShell();
        await app.RunAsync(args, shell, ct: TestContext.Current.CancellationToken);

        return sink.Lines
            .Select(line => string.Concat(line.Select(segment => segment.Text)))
            .SelectMany(line => line.Replace("\r\n", "\n").Split('\n'))
            .ToList();
    }

    private static List<string> BlockAfter(List<string> lines, string keyLine)
    {
        var index = lines.FindIndex(line => line.TrimEnd() == keyLine);
        Assert.True(index >= 0, $"Key line '{keyLine}' not found in:\n{string.Join("\n", lines)}");
        return lines
            .Skip(index + 1)
            .TakeWhile(line => line.TrimEnd().Length > DescriptionIndent
                && line.StartsWith(new string(' ', DescriptionIndent), StringComparison.Ordinal))
            .ToList();
    }

    private static void AssertIndentedBlock(List<string> block, string context)
    {
        Assert.True(block.Count > 1, $"{context}: expected the description to wrap onto continuation lines.");
        foreach (var line in block)
        {
            Assert.StartsWith(new string(' ', DescriptionIndent), line, StringComparison.Ordinal);
            Assert.NotEqual(' ', line[DescriptionIndent]);
        }
    }

    [Theory]
    [InlineData(50)]
    [InlineData(70)]
    public async Task OptionDescription_WrapsWithIndentationPreserved(int width)
    {
        var lines = await RenderHelpAsync(width, "--help");

        AssertIndentedBlock(BlockAfter(lines, "  -c, --connection-string <value>"), "option description");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(70)]
    public async Task ArgumentDescription_WrapsWithIndentationPreserved(int width)
    {
        var lines = await RenderHelpAsync(width, "--help");

        AssertIndentedBlock(BlockAfter(lines, "  <source>"), "argument description");
    }

    // Commands use the key-line-plus-indented-description shape, not a same-row two-column table:
    // a command name is a key and keys can be long.
    [Theory]
    [InlineData(50)]
    [InlineData(70)]
    public async Task CommandDescription_WrapsWithIndentationPreserved(int width)
    {
        var lines = await RenderHelpAsync(width, "--help");

        Assert.Contains(lines, line => line.TrimEnd() == "  copy-to-folder");
        AssertIndentedBlock(BlockAfter(lines, "  copy-to-folder"), "command description");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(70)]
    public async Task EnvironmentVariableDescription_WrapsWithIndentationPreserved(int width)
    {
        var lines = await RenderHelpAsync(width, "--help-env");

        Assert.Contains(lines, line => line.TrimEnd() == "  TIGERCLI_THEME");
        AssertIndentedBlock(BlockAfter(lines, "  TIGERCLI_THEME"), "environment variable description");
    }

    // The allowed-values line of --theme is a detail line, so it sits at the description indent
    // together with the description it belongs to and never merges into it.
    [Fact]
    public async Task AllowedValuesDetailLine_StaysAtDescriptionIndent()
    {
        var lines = await RenderHelpAsync(60, "--help");

        // Theme registration is process-global, so other tests can add names to the list; only the
        // built-in prefix and the indentation of every line are asserted.
        var block = BlockAfter(lines, "  --theme <theme>");
        Assert.Equal("      Select the UI theme by name", block[0].TrimEnd());
        Assert.StartsWith("      dark | light | tiger-blue", block[1], StringComparison.Ordinal);
        foreach (var line in block)
        {
            Assert.StartsWith("      ", line, StringComparison.Ordinal);
            Assert.NotEqual(' ', line[DescriptionIndent]);
        }
    }

    [Theory]
    [InlineData(40)]
    [InlineData(50)]
    [InlineData(70)]
    [InlineData(120)]
    public async Task HelpRows_FillButNeverExceedTheRenderWidth(int width)
    {
        var lines = await RenderHelpAsync(width, "--help", "--help-errors", "--help-env");

        foreach (var line in lines)
            Assert.Equal(width, line.Length);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(70)]
    [InlineData(120)]
    public async Task HelpOutput_UsesFullWidthGridRows(int width)
    {
        var lines = await RenderHelpAsync(width, "--help", "--help-errors", "--help-env");

        foreach (var line in lines)
            Assert.Equal(width, line.Length);
    }

    // Composition of the three help flags still renders all three documents in order.
    [Fact]
    public async Task HelpFlagComposition_RendersAllThreeDocuments()
    {
        var lines = await RenderHelpAsync(100, "--help", "--help-errors", "--help-env");

        var usage = lines.FindIndex(line => line.TrimEnd() == "Usage:");
        var environment = lines.FindIndex(line => line.TrimEnd() == "Environment variables:");
        Assert.True(usage >= 0, "Usage section missing.");
        Assert.True(environment > usage, "Environment variables section missing or out of order.");

        // The hints are suppressed for the sections that were actually rendered.
        Assert.DoesNotContain(lines, line => line.Contains("use --help-env", StringComparison.Ordinal));
    }

    // Help is an ordinary themed grid: its base style overrides the framework fallback ink with the
    // selected theme's Text over Background, while semantic roles continue through the normal
    // cascade (see TigerCliHelpThemeBaseTests for the full base-style contract).
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("tiger-blue")]
    public async Task ThemeOption_AppliesSemanticInkToHelp_WithoutFrameworkFallbackInk(string themeName)
    {
        var originalTheme = TigerConsole.CurrentTheme;
        try
        {
            var app = BuildApp();
            var sink = new TextSegmentLinesSink { SoftMaxWidth = 100 };
            using var scope = TigerConsole.PushOutputSink(sink);
            var shell = new TestShell();
            await app.RunAsync(["--help", "--theme", themeName], shell, ct: TestContext.Current.CancellationToken);

            var theme = TigerConsole.CurrentTheme;
            Assert.Equal(themeName, theme.Name);

            var segments = sink.Lines.SelectMany(line => line).ToList();

            // Semantic spans carry the selected theme's ink.
            var accent = theme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground;
            var key = theme.Resolve(ThemeStyle.Key).CharStyle?.Foreground;
            var value = theme.Resolve(ThemeStyle.Value).CharStyle?.Foreground;
            Assert.Equal(accent, segments.First(segment => segment.Text == "Usage:").Style.Foreground);
            Assert.Equal(key, segments.First(segment => segment.Text == "--theme").Style.Foreground);
            Assert.Equal(value, segments.First(segment => segment.Text.Contains("<theme>", StringComparison.Ordinal)).Style.Foreground);

            // Plain body text carries the theme's document base, not the framework fallback and not
            // the terminal's own ink.
            var text = theme.Resolve(ThemeStyle.Text).CharStyle?.Foreground;
            var background = theme.Resolve(ThemeStyle.Background).CharStyle?.Background;
            var body = segments.First(segment => segment.Text.Contains("Show help", StringComparison.Ordinal));
            Assert.Equal(text, body.Style.Foreground);
            Assert.Equal(background, body.Style.Background);
        }
        finally
        {
            TigerConsole.CurrentTheme = originalTheme;
        }
    }

    // The light theme's Value ink is Black while the dark themes' is Grey — the same colour the
    // framework uses as its global fallback foreground. Both must reach the sink.
    [Fact]
    public void ThemeValueInk_SurvivesEvenWhenItMatchesTheFrameworkFallbackColour()
    {
        var fallbackForeground = CliColor.Gray;
        Assert.Equal(fallbackForeground, new DarkTheme().Resolve(ThemeStyle.Value).CharStyle?.Foreground);

        var originalTheme = TigerConsole.CurrentTheme;
        try
        {
            TigerConsole.CurrentTheme = new DarkTheme();
            var sink = new TextSegmentLinesSink { SoftMaxWidth = 60 };
            using var scope = TigerConsole.PushOutputSink(sink);

            TigerCliHelpRenderer.RenderDetailSection(
                "[Accent]Options:[/]",
                [("[Key]--theme[/] [Value]<theme>[/]", ["Select the UI theme by name"])]);

            var themeValue = sink.Lines
                .SelectMany(line => line)
                .First(segment => segment.Text.Contains("<theme>", StringComparison.Ordinal));
            Assert.Equal(fallbackForeground, themeValue.Style.Foreground);
        }
        finally
        {
            TigerConsole.CurrentTheme = originalTheme;
        }
    }

    // Layout width comes from the destination sink, which is what makes wrapping deterministic and
    // independent of the terminal's own auto-wrap.
    [Fact]
    public async Task HelpWrapsAtTheSinkWidth_NotAtTheTerminalWidth()
    {
        var narrow = await RenderHelpAsync(50, "--help-env");
        var wide = await RenderHelpAsync(120, "--help-env");

        var narrowBlock = BlockAfter(narrow, "  TIGERCLI_THEME");
        var wideBlock = BlockAfter(wide, "  TIGERCLI_THEME");

        Assert.True(narrowBlock.Count > wideBlock.Count,
            "A narrower sink must produce more wrapped lines than a wider one.");
        Assert.All(narrowBlock, line => Assert.Equal(50, line.Length));
        Assert.All(wideBlock, line => Assert.Equal(120, line.Length));
    }
}
