using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

public sealed class TerminalTitleTests
{
    private const string Esc = "\u001b";
    private const string Bel = "\u0007";

    [Fact]
    public void AnsiSgr_TitleSequence_UsesUnicodeEscapeConstants()
    {
        Assert.Equal(Esc, AnsiSgr.Esc);
        Assert.Equal(Bel, AnsiSgr.Bel);
        Assert.Equal($"{Esc}]0;App{Bel}", AnsiSgr.SetWindowTitle("App"));
    }

    [Fact]
    public void AnsiSgr_TitleSequence_SanitizesControlCharacters()
    {
        var title = "A" + Esc + "B" + Bel + "C\nD" + '\u007f' + '\u0085' + "E";

        Assert.Equal($"{Esc}]0;ABCDE{Bel}", AnsiSgr.SetWindowTitle(title));
    }

    [Fact]
    public void AnsiSgr_Source_DoesNotContainLiteralEsc()
    {
        var path = FindSourceFile("ItTiger.TigerCli", "Terminal", "AnsiSgr.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain('\u001b', source);
    }

    [Fact]
    public void AnsiSink_SetWindowTitle_EmitsOscTitle_WhenControlsEnabled()
    {
        using var writer = new StringWriter();
        var sink = new AnsiSink(writer, emitTerminalControls: true);

        sink.SetWindowTitle("App");

        Assert.Equal($"{Esc}]0;App{Bel}", writer.ToString());
    }

    [Fact]
    public void AnsiSink_SetWindowTitle_EmitsNothing_WhenControlsDisabled()
    {
        using var writer = new StringWriter();
        var sink = new AnsiSink(writer, emitTerminalControls: false);

        sink.SetWindowTitle("App");

        Assert.Equal(string.Empty, writer.ToString());
    }

    [Fact]
    public void HtmlSink_SetWindowTitle_StoresTitleOnly()
    {
        using var writer = new StringWriter();
        var sink = new HtmlSink(writer);

        sink.SetWindowTitle("App");
        sink.Write(new CliTextSegment("body", new CliCharStyle()));
        sink.Flush();

        Assert.Equal("App", sink.WindowTitle);
        Assert.DoesNotContain("App", writer.ToString());
        Assert.DoesNotContain("]0;", writer.ToString());
    }

    [Fact]
    public void StringLinesSink_SetWindowTitle_StoresTitleWithoutAddingLines()
    {
        var sink = new StringLinesSink();

        sink.SetWindowTitle("App");
        sink.Write(new CliTextSegment("body", new CliCharStyle()));
        sink.Flush();

        Assert.Equal("App", sink.WindowTitle);
        Assert.Equal(["body"], sink.Lines);
    }

    [Fact]
    public async Task AppRun_WritesDisplayNameAsDefaultTitle()
    {
        var app = App(builder => builder
            .SetDisplayName("Tiger Media Flow")
            .SetDefaultCommand<NoopCommand>());
        var sink = new CountingSink();

        await RunWithSinkAsync(app, sink);

        Assert.Contains("Tiger Media Flow", sink.WindowTitles);
        Assert.Equal("Tiger Media Flow", sink.WindowTitle);
    }

    [Fact]
    public async Task AppRun_DisableTerminalTitleManagement_WritesNoTitle()
    {
        var app = App(builder => builder
            .SetDisplayName("Tiger Media Flow")
            .DisableTerminalTitleManagement()
            .SetDefaultCommand<NoopCommand>());
        var sink = new CountingSink();

        await RunWithSinkAsync(app, sink);

        Assert.Empty(sink.WindowTitles);
    }

    [Fact]
    public async Task AppRun_CommandAppendTitle_AppendsToAppTitle()
    {
        var app = App(builder => builder
            .SetDisplayName("Tiger Media Flow")
            .AddCommand<NoopCommand>("scan", command => command.AppendTitle("Scanning")));
        var sink = new CountingSink();

        await RunWithSinkAsync(app, sink, "scan");

        Assert.Equal("Tiger Media Flow - Scanning", sink.WindowTitle);
    }

    [Fact]
    public async Task AppRun_CommandSetTitle_ReplacesAppTitle()
    {
        var app = App(builder => builder
            .SetDisplayName("Tiger Media Flow")
            .AddCommand<NoopCommand>("scan", command => command.SetTitle("TMF Scan")));
        var sink = new CountingSink();

        await RunWithSinkAsync(app, sink, "scan");

        Assert.Equal("TMF Scan", sink.WindowTitle);
    }

    [Fact]
    public void CommandTitle_BothAppendAndSet_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            App(builder => builder
                .AddCommand<NoopCommand>("scan", command => command
                    .AppendTitle("Scanning")
                    .SetTitle("TMF Scan")));
        });

        Assert.Contains("TitleAppend", ex.Message);
        Assert.Contains("TitleSet", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CommandTitle_NullOrWhitespace_Throws(string? title)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            App(builder => builder
                .AddCommand<NoopCommand>("scan", command => command.AppendTitle(title!)));
        });

        Assert.ThrowsAny<ArgumentException>(() =>
        {
            App(builder => builder
                .AddCommand<NoopCommand>("scan", command => command.SetTitle(title!)));
        });
    }

    [Fact]
    public async Task AppRun_TitleWritesUsePushedCurrentSink()
    {
        var app = App(builder => builder
            .SetDisplayName("Tiger Media Flow")
            .SetDefaultCommand<NoopCommand>());
        var sink = new CountingSink();

        await RunWithSinkAsync(app, sink);

        Assert.Equal(["Tiger Media Flow"], sink.WindowTitles);
    }

    [Fact]
    public void TerminalTitleSession_SuppressesDuplicateWrites()
    {
        var sink = new CountingSink();
        var session = new TerminalTitleSession(sink, enabled: true, spinnerPrefixEnabled: true);

        session.SetBaseTitle("App");
        session.SetBaseTitle("App");
        session.SetSpinnerPrefix("A");
        session.SetSpinnerPrefix("A");
        session.SetSpinnerPrefix(null);
        session.SetSpinnerPrefix(null);

        Assert.Equal(["App", "A App", "App"], sink.WindowTitles);
    }

    [Fact]
    public void SpinnerTicker_CurrentContent_IsRawBrailleFrame()
    {
        var spinner = new SpinnerTicker(TimeSpan.FromMilliseconds(500), ["\u2816", "\u2832"]);

        Assert.Equal("\u2816", spinner.CurrentContent);
        Assert.DoesNotContain("[", spinner.CurrentContent);
        Assert.DoesNotContain("]", spinner.CurrentContent);
    }

    [Fact]
    public async Task ModalSpinner_TitleUsesRawFrameWhileOverlayKeepsBracketsAndAdvances()
    {
        var shell = new TestShell(useManualClock: true);
        var control = new SpinnerTitleControl(shell,
        [
            new SpinnerTicker(TimeSpan.FromMilliseconds(500), ["\u2816", "\u2832"])
        ], decorateOverlay: true);
        var dialog = new InlineDialog(shell, "Title", control);
        var session = new TerminalTitleSession(shell.Terminal.Sink, enabled: true, spinnerPrefixEnabled: true);
        session.SetBaseTitle("App");

        using var titleScope = TerminalTitleScope.Push(session);
        var modalTask = shell.RunModalAsync(dialog, TestContext.Current.CancellationToken);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal("\u2816 App", shell.Terminal.WindowTitle);
        Assert.DoesNotContain("[", shell.Terminal.WindowTitle);
        Assert.DoesNotContain("]", shell.Terminal.WindowTitle);
        Assert.Contains(shell.Terminal.LastRenderedLines, line => line.Contains("[\u2816]", StringComparison.Ordinal));

        shell.AdvanceTime(TimeSpan.FromMilliseconds(500));
        await shell.Terminal.WaitForRenderCountAsync(2, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal("\u2832 App", shell.Terminal.WindowTitle);
        Assert.DoesNotContain("[", shell.Terminal.WindowTitle);
        Assert.DoesNotContain("]", shell.Terminal.WindowTitle);
        Assert.Contains(shell.Terminal.LastRenderedLines, line => line.Contains("[\u2832]", StringComparison.Ordinal));

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await modalTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ModalSpinner_StopClearsPrefixBackToBaseTitle()
    {
        var shell = new TestShell(useManualClock: true);
        var spinner = new SpinnerTicker(TimeSpan.FromMilliseconds(500), ["A", "B"]);
        var control = new SpinnerTitleControl(shell, [spinner]);
        var dialog = new InlineDialog(shell, "Title", control);
        var session = new TerminalTitleSession(shell.Terminal.Sink, enabled: true, spinnerPrefixEnabled: true);
        session.SetBaseTitle("App");

        using var titleScope = TerminalTitleScope.Push(session);
        var modalTask = shell.RunModalAsync(dialog, TestContext.Current.CancellationToken);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
        await shell.Terminal.WaitForInputDrainedAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal("App", shell.Terminal.WindowTitle);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await modalTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ModalSpinner_DisableSpinnerTitlePrefix_KeepsBaseTitle()
    {
        var shell = new TestShell(useManualClock: true);
        var control = new SpinnerTitleControl(shell,
        [
            new SpinnerTicker(TimeSpan.FromMilliseconds(500), ["\u2816", "\u2832"])
        ], decorateOverlay: true);
        var dialog = new InlineDialog(shell, "Title", control);
        var session = new TerminalTitleSession(shell.Terminal.Sink, enabled: true, spinnerPrefixEnabled: false);
        session.SetBaseTitle("App");

        using var titleScope = TerminalTitleScope.Push(session);
        var modalTask = shell.RunModalAsync(dialog, TestContext.Current.CancellationToken);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal("App", shell.Terminal.WindowTitle);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await modalTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ModalSpinner_MultipleActiveSpinners_UsesFirstActive()
    {
        var shell = new TestShell(useManualClock: true);
        var control = new SpinnerTitleControl(shell,
        [
            new SpinnerTicker(TimeSpan.FromMilliseconds(500), ["A"]),
            new SpinnerTicker(TimeSpan.FromMilliseconds(500), ["B"])
        ]);
        var dialog = new InlineDialog(shell, "Title", control);
        var session = new TerminalTitleSession(shell.Terminal.Sink, enabled: true, spinnerPrefixEnabled: true);
        session.SetBaseTitle("App");

        using var titleScope = TerminalTitleScope.Push(session);
        var modalTask = shell.RunModalAsync(dialog, TestContext.Current.CancellationToken);
        await shell.Terminal.WaitForRenderCountAsync(1, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal("A App", shell.Terminal.WindowTitle);

        shell.Terminal.EnqueueKey(ConsoleKey.Escape);
        await modalTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ModalSpinner_TitlePath_HasNoDecorationStrippingHelper()
    {
        var path = FindSourceFile("ItTiger.TigerCli", "Tui", "Controls", "InlineDialog.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("GetTitleSpinnerFrame", source);
        Assert.DoesNotContain("[1..^1]", source);
    }

    private static TigerCliApp App(Action<TigerCliAppBuilder> configure)
    {
        var builder = TigerCliApp.CreateBuilder()
            .SetApplicationName("tmf");

        configure(builder);
        return builder.Build();
    }

    private static async Task RunWithSinkAsync(TigerCliApp app, ICliRenderSink sink, params string[] args)
    {
        using var sinkScope = TigerConsole.PushOutputSink(sink);
        var exit = await app.RunAsync(args);
        Assert.Equal(0, exit);
    }

    private static string FindSourceFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathParts).ToArray());
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate source file.", Path.Combine(pathParts));
    }

    private sealed class EmptySettings : TigerCliSettings
    {
    }

    private sealed class NoopCommand : TigerCliAsyncCommandHandler<EmptySettings>
    {
        public override Task<int> ExecuteAsync(EmptySettings settings) => Task.FromResult(0);
    }

    private sealed class SpinnerTitleControl : InlineControlBase
    {
        private readonly IReadOnlyList<SpinnerTicker> _spinners;
        private readonly InlineActivityOverlay[] _overlays;

        public SpinnerTitleControl(ICliAppShell shell, IReadOnlyList<SpinnerTicker> spinners, bool decorateOverlay = false)
            : base(shell)
        {
            _spinners = spinners;
            _overlays = spinners.Select((spinner, index) => new InlineActivityOverlay
            {
                Area = InlineDialogArea.TopFrame,
                ColumnOffset = 1 + index,
                MaxLength = decorateOverlay ? spinner.CurrentContent.Length + 2 : spinner.CurrentContent.Length,
                Ticker = spinner,
                ContentFormatter = decorateOverlay ? static frame => $"[{frame}]" : null,
            }).ToArray();
        }

        public override object? Payload => null;

        public override IReadOnlyList<InlineActivityOverlay> GetActivityOverlays() => _overlays;

        public override InlineKeyResult HandleKey(KeyEvent key)
        {
            if (key.Key == ConsoleKey.Enter)
            {
                foreach (var spinner in _spinners)
                    spinner.Stop();

                return InlineKeyResult.Handled;
            }

            return InlineKeyResult.NotHandled;
        }

        public override CliGrid ToGrid()
        {
            var grid = new CliGrid(1, 1);
            grid.Set(0, 0, "body");
            return grid;
        }
    }

    private sealed class CountingSink : ICliRenderSink
    {
        private readonly List<string> _windowTitles = [];

        public IReadOnlyList<string> WindowTitles => _windowTitles;
        public string? WindowTitle => _windowTitles.LastOrDefault();
        public int? SoftMaxWidth => null;
        public int? SoftMaxHeight => null;
        public int? MaxWidth => null;
        public int? MaxHeight => null;

        public void Write(CliTextSegment segment)
        {
        }

        public void NewLine()
        {
        }

        public void Flush()
        {
        }

        public void Reset()
        {
        }

        public void SetWindowTitle(string title)
        {
            _windowTitles.Add(AnsiSgr.SanitizeControlString(title));
        }
    }
}
