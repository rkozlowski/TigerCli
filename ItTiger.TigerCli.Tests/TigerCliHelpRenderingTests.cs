using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Structured help rendering (first slice): the title block and Usage section render through
/// <see cref="TigerCliHelpRenderer"/> CliGrid blocks while keeping the legacy plain-text shape —
/// same lines, same two-space indent, same inner spacing, and no trailing whitespace.
/// </summary>
public sealed class TigerCliHelpRenderingTests
{
    private sealed class EmptySettings : TigerCliSettings
    {
    }

    private sealed class TargetSettings : TigerCliSettings
    {
        [TigerCliArgument(0, Name = "target", Description = "Target")]
        public string Target { get; set; } = "";
    }

    private sealed class NoopCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings) => Task.FromResult(0);
    }

    private sealed class RunCommand : TigerCliAsyncCommandHandler<TargetSettings>
    {
        public override Task<int> ExecuteAsync(TargetSettings settings) => Task.FromResult(0);
    }

    [Fact]
    public async Task RootHelp_TitleAndUsage_KeepLegacyPlainShape()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .AddDescription("Does things.")
            .SetDefaultCommand<NoopCommand>()
            .AddCommand<RunCommand>("run", "Runs the target.")
            .Build();

        var result = await RunAsync(app, ["--help"]);

        Assert.Equal(0, result.ExitCode);
        var lines = SplitLines(result.Stdout);

        // Title block and Usage section as consecutive lines, exactly as the legacy formatter
        // shaped them (structural two-space indent, single spaces between usage tokens).
        var title = Array.IndexOf(lines, "tool");
        Assert.True(title >= 0, $"Title line not found in: {result.Stdout}");
        Assert.Equal("  Does things.", lines[title + 1]);
        Assert.Equal("", lines[title + 2]);
        Assert.Equal("Usage:", lines[title + 3]);
        Assert.Equal("  tool [options]", lines[title + 4]);
        Assert.Equal("  tool <command> [options]", lines[title + 5]);
        Assert.Equal("", lines[title + 6]);

        // The grid path pads lines to the block width; the help renderer must trim that padding.
        foreach (var line in lines)
            Assert.Equal(line.TrimEnd(), line);
    }

    [Fact]
    public async Task CommandHelp_TitleAndUsage_KeepLegacyPlainShape()
    {
        var app = TigerCliApp.CreateBuilder()
            .SetApplicationName("tool")
            .SetDefaultCommand<NoopCommand>()
            .AddCommand<RunCommand>("run", "Runs the target.")
            .Build();

        var result = await RunAsync(app, ["run", "--help"]);

        Assert.Equal(0, result.ExitCode);
        var lines = SplitLines(result.Stdout);

        var title = Array.IndexOf(lines, "tool run");
        Assert.True(title >= 0, $"Title line not found in: {result.Stdout}");
        Assert.Equal("  Runs the target.", lines[title + 1]);
        Assert.Equal("", lines[title + 2]);
        Assert.Equal("Usage:", lines[title + 3]);
        Assert.Equal("  tool run <target> [options]", lines[title + 4]);

        foreach (var line in lines)
            Assert.Equal(line.TrimEnd(), line);
    }

    [Fact]
    public void RenderSection_GoesThroughGridPath_WithStructuralIndentAndSemanticSpans()
    {
        var sink = new TextSegmentLinesSink();
        using var scope = TigerConsole.PushOutputSink(sink);

        TigerCliHelpRenderer.RenderSection(
            "[Accent]Usage:[/]",
            ["[Key]tool[/] [Value][[options]][/]"]);

        var lines = sink.Lines
            .Select(line => string.Concat(line.Select(segment => segment.Text)))
            .ToList();

        Assert.Equal("Usage:", lines[0]);
        Assert.Equal("  tool [options]", lines[1]);

        // The semantic markup spans survive grid measurement as styled segments (segments with the
        // same resolved rendering may be merged; "tool" stays distinct because [Key] restyles it).
        Assert.Contains(sink.Lines[1], segment => segment.Text == "tool");
    }

    [Fact]
    public void RenderTitleBlock_WithoutDescription_RendersSingleUnpaddedLine()
    {
        var sink = new TextSegmentLinesSink();
        using var scope = TigerConsole.PushOutputSink(sink);

        TigerCliHelpRenderer.RenderTitleBlock("[Key]tool[/]", descriptionMarkup: null);

        var lines = sink.Lines
            .Select(line => string.Concat(line.Select(segment => segment.Text)))
            .ToList();

        Assert.Single(lines);
        Assert.Equal("tool", lines[0]);
    }

    [Fact]
    public void RenderExitCodeSection_UsesKeyCodesAccentNamesAndWrapsDescriptions()
    {
        var sink = new TextSegmentLinesSink { SoftMaxWidth = 60 };
        using var scope = TigerConsole.PushOutputSink(sink);

        TigerCliHelpRenderer.RenderExitCodeSection(
            "[Accent]Exit codes:[/]",
            "Toolkit response codes",
            [
                (0, "Ok", "Operation completed successfully."),
                (1003, "CliInteractiveNotAllowed", "Prompt attempted in non-interactive mode.")
            ]);

        var lines = sink.Lines
            .Select(line => string.Concat(line.Select(segment => segment.Text)))
            .ToList();

        Assert.Equal("Exit codes:", lines[0]);
        Assert.Equal("Toolkit response codes", lines[1]);
        Assert.Contains(lines, line => line.Contains("0", StringComparison.Ordinal) && line.Contains("Ok", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("1003", StringComparison.Ordinal) && line.Contains("CliInteractiveNotAllowed", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Prompt", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("non-interactive", StringComparison.Ordinal));
        Assert.True(lines.Count > 4, "The constrained description column should wrap onto additional lines.");
        Assert.DoesNotContain("", lines.Skip(2));

        var keyForeground = TigerConsole.CurrentTheme.Resolve(ThemeStyle.Key).CharStyle?.Foreground;
        var accentForeground = TigerConsole.CurrentTheme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground;
        foreach (var text in new[] { "0", "1003" })
        {
            var segment = sink.Lines.SelectMany(line => line).Single(candidate => candidate.Text == text);
            Assert.Equal(keyForeground, segment.Style.Foreground);
        }

        foreach (var text in new[] { "Ok", "CliInteractiveNotAllowed" })
        {
            var segment = sink.Lines.SelectMany(line => line).Single(candidate => candidate.Text == text);
            Assert.Equal(accentForeground, segment.Style.Foreground);
        }
    }

    [Fact]
    public void RenderDetailSection_UsesStructuralContinuationIndentAndSemanticSpans()
    {
        using var themeScope = new ThemeScope(new HelpTestTheme());
        var sink = new TextSegmentLinesSink { SoftMaxWidth = 40 };
        using var scope = TigerConsole.PushOutputSink(sink);

        TigerCliHelpRenderer.RenderDetailSection(
            "[Accent]Options:[/]",
            [
                (
                    "[Key]--theme[/] [Value]<theme>[/]",
                    (IReadOnlyList<string>)
                    [
                        "Select the UI theme by name; this description wraps in the content column.",
                        "[Value]dark | light | tiger-blue[/]"
                    ])
            ]);

        var lines = sink.Lines
            .Select(line => string.Concat(line.Select(segment => segment.Text)))
            .ToList();

        Assert.Equal("Options:", lines[0]);
        Assert.Equal("  --theme <theme>", lines[1]);
        Assert.StartsWith("      Select", lines[2], StringComparison.Ordinal);
        Assert.Contains(lines, line => line.StartsWith("      dark | light | tiger-blue", StringComparison.Ordinal));
        Assert.True(lines.Count > 4, "The detail cell should wrap independently of the signature row.");

        var accentForeground = TigerConsole.CurrentTheme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground;
        var keyForeground = TigerConsole.CurrentTheme.Resolve(ThemeStyle.Key).CharStyle?.Foreground;
        var valueForeground = TigerConsole.CurrentTheme.Resolve(ThemeStyle.Value).CharStyle?.Foreground;
        Assert.Equal(accentForeground, sink.Lines[0].Single(segment => segment.Text == "Options:").Style.Foreground);
        Assert.Equal(keyForeground, sink.Lines.SelectMany(line => line).Single(segment => segment.Text == "--theme").Style.Foreground);
        Assert.Equal(valueForeground, sink.Lines.SelectMany(line => line).Single(segment => segment.Text.Contains("<theme>", StringComparison.Ordinal)).Style.Foreground);
        Assert.Equal(valueForeground, sink.Lines.SelectMany(line => line).Single(segment => segment.Text.Contains("dark | light", StringComparison.Ordinal)).Style.Foreground);
    }

    [Fact]
    public void RenderNameDescriptionSection_UsesCompactKeyRowsAndWrapsDescriptions()
    {
        using var themeScope = new ThemeScope(new HelpTestTheme());
        var sink = new TextSegmentLinesSink { SoftMaxWidth = 42 };
        using var scope = TigerConsole.PushOutputSink(sink);

        TigerCliHelpRenderer.RenderNameDescriptionSection(
            "[Accent]Commands:[/]",
            [
                ("[Key]list[/]", "Lists items with a deliberately long description that wraps in the description column."),
                ("[Key]generate-code[/]", "Generates source code.")
            ]);

        var lines = sink.Lines
            .Select(line => string.Concat(line.Select(segment => segment.Text)))
            .ToList();

        Assert.Equal("Commands:", lines[0]);
        Assert.Contains(lines, line => line.StartsWith("  list ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("  generate-code ", StringComparison.Ordinal));
        Assert.DoesNotContain("", lines.Skip(1));
        Assert.True(lines.Count > 3, "The description column should wrap independently of command rows.");

        var accentForeground = TigerConsole.CurrentTheme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground;
        var keyForeground = TigerConsole.CurrentTheme.Resolve(ThemeStyle.Key).CharStyle?.Foreground;
        Assert.Equal(accentForeground, sink.Lines[0].Single(segment => segment.Text == "Commands:").Style.Foreground);
        Assert.Equal(keyForeground, sink.Lines.SelectMany(line => line).Single(segment => segment.Text == "list").Style.Foreground);
        Assert.Equal(keyForeground, sink.Lines.SelectMany(line => line).Single(segment => segment.Text == "generate-code").Style.Foreground);
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n');

    private sealed class ThemeScope : IDisposable
    {
        private readonly ITheme originalTheme = TigerConsole.CurrentTheme;

        public ThemeScope(ITheme theme)
        {
            TigerConsole.CurrentTheme = theme;
        }

        public void Dispose() => TigerConsole.CurrentTheme = originalTheme;
    }

    private sealed class HelpTestTheme : ITheme
    {
        private readonly DarkTheme inner = new();

        public string Name => "help-test";
        public TigerThemeFamily Family => inner.Family;

        public CliCellStyle Resolve(ThemeStyle style) => style == ThemeStyle.Value
            ? new CliCellStyle(new CliCharStyle(CliColor.Yellow))
            : inner.Resolve(style);

        public SurfaceColors ResolveSurface(SurfaceRole role) => inner.ResolveSurface(role);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        TigerCliApp app,
        string[] args)
    {
        var shell = new TestShell();
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
