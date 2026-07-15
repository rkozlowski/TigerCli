using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Testing;

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

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n');

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
