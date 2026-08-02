using System.IO;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the help document's base-style contract: help is a TigerCli-rendered document, so the
/// selected theme's default Text ink over its default Background is the base of every help block's
/// style cascade. Plain text and the structural whitespace of the indent columns render on the
/// theme's background — a light theme is readable on a dark terminal — while semantic roles override
/// the foreground and inherit that background unless they define one of their own.
/// <para>What is painted is exactly what the block lays out. A document block has no painted
/// trailing edge, so a line stops at its content and the terminal beyond it stays terminal-owned;
/// blank separator lines have no content and paint nothing. Nothing pads a help line out to the
/// terminal width.</para>
/// </summary>
public sealed class TigerCliHelpThemeBaseTests
{
    // Built from the code point rather than written as a literal or an escape sequence: the source
    // stays pure ASCII, so nothing here can be mangled into a raw control character or an empty
    // string by an editor or tool round-trip.
    private static readonly string Esc = ((char)0x1B).ToString();

    private sealed class WideSettings : TigerCliSettings
    {
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
            .AddDescription("Copies a single existing file into a selected destination folder.")
            .SetDefaultCommand<WideCommand>()
            .Build();

    // Runs help through a segment-capturing sink of a fixed width, so both the styling and the
    // wrapping are deterministic instead of depending on the machine's terminal.
    private static async Task<List<List<CliTextSegment>>> RenderHelpAsync(string themeName, int width = 100)
    {
        var originalTheme = TigerConsole.CurrentTheme;
        try
        {
            var sink = new TextSegmentLinesSink { SoftMaxWidth = width };
            using var scope = TigerConsole.PushOutputSink(sink);
            await BuildApp().RunAsync(
                ["--help", "--theme", themeName],
                new TestShell(),
                ct: TestContext.Current.CancellationToken);
            return sink.Lines;
        }
        finally
        {
            TigerConsole.CurrentTheme = originalTheme;
        }
    }

    private static string LineText(List<CliTextSegment> line)
        => string.Concat(line.Select(segment => segment.Text));

    private static List<CliTextSegment> LineContaining(List<List<CliTextSegment>> lines, string needle)
    {
        var line = lines.FirstOrDefault(candidate => LineText(candidate).Contains(needle, StringComparison.Ordinal));
        Assert.True(line is not null, $"No rendered line contains '{needle}'.");
        return line!;
    }

    private static (CliColor? Foreground, CliColor? Background) DocumentBase(ITheme theme)
        => (theme.Resolve(ThemeStyle.Text).CharStyle?.Foreground,
            theme.Resolve(ThemeStyle.Background).CharStyle?.Background);

    // ---- Document base ink ----

    // The theme owns both halves of the document base. Unstyled help text must not fall back to the
    // terminal's current background, which is what made the light theme unusable on a dark terminal.
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("tiger-blue")]
    public async Task PlainHelpText_UsesThemeTextOverThemeBackground(string themeName)
    {
        var lines = await RenderHelpAsync(themeName);
        var (foreground, background) = DocumentBase(TigerConsole.GetTheme(themeName));

        var body = LineContaining(lines, "Show help")
            .First(segment => segment.Text.Contains("Show help", StringComparison.Ordinal));

        Assert.Equal(foreground, body.Style.Foreground);
        Assert.Equal(background, body.Style.Background);
    }

    // The light theme is the case the base style exists for: dark ink on a light surface, regardless
    // of what the terminal is painted with.
    [Fact]
    public void LightThemeDocumentBase_IsDarkInkOnALightSurface()
    {
        var theme = new LightTheme();

        var documentBase = TigerConsole.ResolveDocumentBaseCharStyle(theme);

        Assert.Equal(CliColor.Black, documentBase.Foreground);
        Assert.Equal(CliColor.White, documentBase.Background);
        // Colours only: a decoration in the base would apply to every glyph in the block, including
        // the structural whitespace of the indent columns.
        Assert.Equal(CliTextDecoration.None, documentBase.Decorations);
    }

    // Structural indentation is grid columns, not leading spaces in the text, so those spaces are
    // rendered whitespace — they belong to the document surface and carry its background.
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("tiger-blue")]
    public async Task IndentationSpaces_UseTheThemeBackground(string themeName)
    {
        var lines = await RenderHelpAsync(themeName);
        var (_, background) = DocumentBase(TigerConsole.GetTheme(themeName));

        var line = LineContaining(lines, "Show help");
        Assert.StartsWith("      ", LineText(line), StringComparison.Ordinal);

        var indentSpaces = line
            .Where(segment => segment.Text.Length > 0 && segment.Text.All(ch => ch == ' '))
            .ToList();
        Assert.NotEmpty(indentSpaces);
        Assert.All(indentSpaces, segment => Assert.Equal(background, segment.Style.Background));
    }

    // Continuation lines produced by wrapping are indented by the same structural columns, so their
    // indentation is painted exactly like the first line's.
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("tiger-blue")]
    public async Task WrappedContinuationIndentation_UsesTheThemeBackground(string themeName)
    {
        var lines = await RenderHelpAsync(themeName, width: 50);
        var (foreground, background) = DocumentBase(TigerConsole.GetTheme(themeName));

        // The connection-string description is long enough to wrap at 50 columns; take the line after
        // the one that starts it.
        var startIndex = lines.FindIndex(line =>
            LineText(line).Contains("Connection string used", StringComparison.Ordinal));
        Assert.True(startIndex >= 0, "The wrapping option description was not rendered.");

        var continuation = lines[startIndex + 1];
        var text = LineText(continuation);
        Assert.StartsWith("      ", text, StringComparison.Ordinal);
        Assert.NotEqual(' ', text[6]);

        Assert.All(
            continuation.Where(segment => segment.Text.Length > 0),
            segment =>
            {
                Assert.Equal(foreground, segment.Style.Foreground);
                Assert.Equal(background, segment.Style.Background);
            });
    }

    // ---- Semantic roles over the base ----

    // Foreground-only roles override the ink and keep the document surface underneath; a role that
    // defines its own background replaces it.
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("tiger-blue")]
    public async Task SemanticSpans_KeepTheirThemeForegroundOverTheDocumentBackground(string themeName)
    {
        var lines = await RenderHelpAsync(themeName);
        var theme = TigerConsole.GetTheme(themeName);
        var (_, background) = DocumentBase(theme);

        var segments = lines.SelectMany(line => line).ToList();

        var heading = segments.First(segment => segment.Text == "Options:");
        Assert.Equal(theme.Resolve(ThemeStyle.Accent).CharStyle?.Foreground, heading.Style.Foreground);
        Assert.Equal(background, heading.Style.Background);

        var key = segments.First(segment => segment.Text == "--connection-string");
        Assert.Equal(theme.Resolve(ThemeStyle.Key).CharStyle?.Foreground, key.Style.Foreground);
        Assert.Equal(background, key.Style.Background);
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("tiger-blue")]
    public void LinkRole_KeepsItsDecorationAndTheDocumentBackground(string themeName)
    {
        var theme = TigerConsole.GetTheme(themeName);
        var (_, background) = DocumentBase(theme);

        var segments = CaptureWithTheme(theme, () => TigerCliHelpRenderer.RenderMetadataFooter(
            null,
            [("[Key]Documentation[/]", "[Link]https://example.test/[/]")]));

        var link = segments.First(segment => segment.Text.Contains("example.test", StringComparison.Ordinal));
        Assert.Equal(theme.Resolve(ThemeStyle.Link).CharStyle?.Foreground, link.Style.Foreground);
        Assert.Equal(background, link.Style.Background);
        Assert.True(link.Style.Decorations.HasFlag(CliTextDecoration.Underline));
    }

    // A role that carries its own background wins over the document base — the base is a base, not a
    // ceiling.
    [Fact]
    public void RoleWithItsOwnBackground_OverridesTheDocumentBackground()
    {
        var theme = new LightTheme();
        var alert = theme.Resolve(ThemeStyle.Alert).CharStyle;

        var segments = CaptureWithTheme(theme, () => TigerCliHelpRenderer.RenderNoteSection(
            "[Accent]Notes:[/]",
            ["[Alert]exactly one of these is required[/]"]));

        var note = segments.First(segment => segment.Text.Contains("exactly one", StringComparison.Ordinal));
        Assert.Equal(alert?.Foreground, note.Style.Foreground);
        Assert.Equal(alert?.Background, note.Style.Background);
        Assert.NotEqual(theme.Resolve(ThemeStyle.Background).CharStyle?.Background, note.Style.Background);
    }

    // ---- ANSI emission ----

    // The console contract: a light-themed help document actually emits a background SGR, so the
    // rendered text sits on the theme's surface rather than the terminal's. Asserted on the colour's
    // own SGR parameters rather than a hand-written full sequence.
    [Fact]
    public async Task LightThemeHelp_EmitsTheThemeBackgroundSgr()
    {
        var ansi = await RenderHelpToAnsiAsync("light");

        var background = AnsiSgr.BackgroundParamsOrDefault(CliColor.White);
        var foreground = AnsiSgr.ForegroundParamsOrDefault(CliColor.Black);
        Assert.Contains($"{Esc}[{foreground};{background}m", ansi, StringComparison.Ordinal);

        // Never the terminal default background, which is what "unstyled" would have emitted.
        Assert.DoesNotContain(
            $";{AnsiSgr.BackgroundParamsOrDefault(null)}m",
            ansi,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DarkThemeHelp_EmitsTheThemeBackgroundSgr()
    {
        var ansi = await RenderHelpToAnsiAsync("dark");

        var background = AnsiSgr.BackgroundParamsOrDefault(CliColor.Black);
        var foreground = AnsiSgr.ForegroundParamsOrDefault(CliColor.Gray);
        Assert.Contains($"{Esc}[{foreground};{background}m", ansi, StringComparison.Ordinal);
    }

    // ---- Painted extent ----

    // The guarantee is content plus structural whitespace, not the terminal row: a help line stops at
    // its content, so nothing pads it out to the render width and the terminal keeps the rest of the
    // row. (This is also what keeps the terminal's own auto-wrap out of the picture.)
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task HelpLines_StopAtTheirContent_WithNoPaintedTrailingEdge(string themeName)
    {
        var lines = await RenderHelpAsync(themeName, width: 60);

        foreach (var line in lines)
        {
            var text = LineText(line);
            Assert.Equal(text.TrimEnd(), text);
            Assert.True(text.Length <= 60, $"Line of {text.Length} columns exceeds the render width: '{text}'");
        }
    }

    // A blank separator has no content, so it paints nothing at all: no styled segment, and in
    // particular no framework fallback ink and no terminal-derived ink.
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task BlankSeparatorLines_PaintNothing(string themeName)
    {
        var lines = await RenderHelpAsync(themeName);

        var blank = lines.Where(line => LineText(line).Length == 0).ToList();
        Assert.NotEmpty(blank);
        Assert.All(blank, line => Assert.All(line, segment =>
        {
            Assert.Null(segment.Style.Foreground);
            Assert.Null(segment.Style.Background);
        }));
    }

    // ---- TransparentDocument ----

    // TransparentDocument still means "no framework fallback repair ink and no painted trailing
    // edge". A document block that supplies no base of its own stays colourless — the grey-on-black
    // box fallback must never reach a document.
    [Fact]
    public void TransparentDocumentWithoutABase_StaysColourless()
    {
        var grid = new CliGrid(2, 1)
        {
            TransparentDocument = true,
            DefaultCellStyle = new CliCellStyle { FormattingMode = CliFormattingMode.Preformatted }
        };
        grid.SetColumn(0, new CliGridColumnDefinition(new CliCellStyle { Width = 2, MinWidth = 2 }));
        grid.SetColumn(1, new CliGridColumnDefinition(new CliCellStyle()) { Sizing = CliColumnSizing.Star });
        grid.Set(1, 0, "plain");

        var sink = new TextSegmentLinesSink { SoftMaxWidth = 40 };
        TigerConsole.RenderGrid(grid, sink);

        var segments = sink.Lines.SelectMany(line => line).ToList();
        Assert.All(segments, segment =>
        {
            Assert.Null(segment.Style.Foreground);
            Assert.Null(segment.Style.Background);
        });
        Assert.Equal("  plain", string.Concat(segments.Select(segment => segment.Text)));
    }

    // ---- Helpers ----

    private static List<CliTextSegment> CaptureWithTheme(ITheme theme, Action render)
    {
        var originalTheme = TigerConsole.CurrentTheme;
        try
        {
            TigerConsole.CurrentTheme = theme;
            var sink = new TextSegmentLinesSink { SoftMaxWidth = 80 };
            using var scope = TigerConsole.PushOutputSink(sink);
            render();
            return sink.Lines.SelectMany(line => line).ToList();
        }
        finally
        {
            TigerConsole.CurrentTheme = originalTheme;
        }
    }

    private static async Task<string> RenderHelpToAnsiAsync(string themeName)
    {
        var originalTheme = TigerConsole.CurrentTheme;
        try
        {
            var writer = new StringWriter();
            using var scope = TigerConsole.PushOutputSink(new AnsiSink(writer, target: CliSinkTarget.Buffer));
            await BuildApp().RunAsync(
                ["--help", "--theme", themeName],
                new TestShell(),
                ct: TestContext.Current.CancellationToken);
            return writer.ToString();
        }
        finally
        {
            TigerConsole.CurrentTheme = originalTheme;
        }
    }
}
