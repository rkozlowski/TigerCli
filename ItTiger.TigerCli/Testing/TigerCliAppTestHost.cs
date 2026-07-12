using System.Globalization;
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Testing;

/// <summary>
/// App-level test host for running a <see cref="TigerCliApp"/> without the real console input path.
/// The host injects a test shell, queues planned prompt answers (<see cref="WithTextInput"/>,
/// <see cref="WithSelectIndex"/>, <see cref="WithConfirm"/>, <see cref="WithMultiSelectIndexes"/>),
/// redirects <see cref="Console.Out"/> and <see cref="Console.Error"/> for the duration of the run
/// (restoring both in <c>finally</c>), pins a deterministic colour mode so captured output carries
/// no ANSI escapes, and returns the captured output plus the exit code as a
/// <see cref="TigerCliAppRunResult"/>.
/// </summary>
/// <remarks>
/// A host is single-use: calling <see cref="RunAsync"/> twice on the same instance throws
/// <see cref="InvalidOperationException"/>. Create a new host for each run.
/// Configuration methods are fluent and may be chained in any order before <see cref="RunAsync"/>.
/// </remarks>
public sealed class TigerCliAppTestHost
{
    private readonly TigerCliApp _app;
    private readonly List<PlannedAnswer> _answers = [];
    private string[] _args = [];
    private TimeSpan? _promptTimeout;
    private int _viewportWidth = 80;
    private int _viewportHeight = 24;
    private int _hasRun;
    private bool _htmlCaptureEnabled;
    private HtmlSinkOptions? _htmlCaptureOptions;

    private TigerCliAppTestHost(TigerCliApp app)
    {
        _app = app;
    }

    /// <summary>
    /// Creates a test host for <paramref name="app"/>. This is the only way to obtain a host —
    /// the constructor is private.
    /// </summary>
    /// <param name="app">The built app to run; must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <c>null</c>.</exception>
    public static TigerCliAppTestHost For(TigerCliApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return new TigerCliAppTestHost(app);
    }

    /// <summary>
    /// Replaces the command-line arguments passed to the run. When not called, the app runs with
    /// no arguments.
    /// </summary>
    /// <param name="args">The argument tokens; the array and its elements must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="args"/> contains a <c>null</c> element.</exception>
    public TigerCliAppTestHost WithArgs(params string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Any(arg => arg is null))
            throw new ArgumentException("Arguments cannot contain null values.", nameof(args));

        _args = args.ToArray();
        return this;
    }

    /// <summary>
    /// Queues a planned answer for a text prompt: each character of <paramref name="value"/> is
    /// typed using its key character, followed by Enter. Answers are consumed by prompts in the
    /// order they were queued.
    /// </summary>
    /// <param name="value">The text to type; must not be <c>null</c> (empty submits an empty answer).</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    public TigerCliAppTestHost WithTextInput(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _answers.Add(PlannedAnswer.Text(value));
        return this;
    }

    /// <summary>
    /// Queues a planned answer for a select prompt: DownArrow is pressed <paramref name="index"/>
    /// times, then Enter — selecting the zero-based row <paramref name="index"/> in the choice list.
    /// </summary>
    /// <param name="index">The zero-based choice index to select; must not be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    public TigerCliAppTestHost WithSelectIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        _answers.Add(PlannedAnswer.Select(index));
        return this;
    }

    /// <summary>
    /// Queues a planned answer for a Yes/No confirm prompt. <c>true</c> accepts the default Yes
    /// button with Enter; <c>false</c> navigates to the No button and confirms.
    /// </summary>
    /// <param name="value"><c>true</c> to answer Yes, <c>false</c> to answer No.</param>
    public TigerCliAppTestHost WithConfirm(bool value)
    {
        _answers.Add(PlannedAnswer.Confirm(value));
        return this;
    }

    /// <summary>
    /// Queues a planned answer for a multi-select (checklist) prompt. Indexes are normalized by
    /// sorting and removing duplicates; the answer walks the rows top-to-bottom, toggles each
    /// selected row with Spacebar, then confirms with Enter. An empty <paramref name="indexes"/>
    /// confirms with nothing selected.
    /// </summary>
    /// <param name="indexes">The zero-based choice indexes to toggle; none may be negative.</param>
    /// <exception cref="ArgumentNullException"><paramref name="indexes"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Any index is negative.</exception>
    public TigerCliAppTestHost WithMultiSelectIndexes(params int[] indexes)
    {
        ArgumentNullException.ThrowIfNull(indexes);
        if (indexes.Any(index => index < 0))
            throw new ArgumentOutOfRangeException(nameof(indexes), "Multi-select indexes cannot be negative.");

        _answers.Add(PlannedAnswer.MultiSelect(indexes.Distinct().Order().ToArray()));
        return this;
    }

    /// <summary>
    /// Sets the prompt timeout passed through to
    /// <see cref="TigerCliApp.RunAsync(string[], Tui.Abstractions.ICliAppShell?, TimeSpan?, CancellationToken)"/>.
    /// When not called, no prompt timeout applies.
    /// </summary>
    /// <param name="timeout">The prompt timeout; must not be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    public TigerCliAppTestHost WithPromptTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Prompt timeout cannot be negative.");

        _promptTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Additionally captures the run's TigerCli output as deterministic HTML (via <see cref="HtmlSink"/>),
    /// exposed on <see cref="TigerCliAppRunResult.StdOutHtml"/> / <see cref="TigerCliAppRunResult.StdErrHtml"/> —
    /// for documentation artifacts and styled-output assertions. Opt-in: without this call both
    /// properties stay <c>null</c> and the run behaves exactly as before.
    /// <para>Semantics: TigerCli-rendered output (markup, help, framework errors, structured output)
    /// goes to the HTML capture instead of the plain <c>StdOut</c>/<c>StdErr</c> strings; unstyled
    /// text is captured without machine-dependent console colours; line endings are normalized to
    /// <c>\n</c>. <paramref name="options"/> controls hyperlink mode, layout width
    /// (<see cref="HtmlSinkOptions.SoftMaxWidth"/>), and whether the returned fragments are wrapped
    /// in <c>&lt;pre class="tigercli"&gt;</c> (<see cref="HtmlSinkOptions.WrapInPre"/>, default true).
    /// No ANSI is ever emitted.</para>
    /// </summary>
    public TigerCliAppTestHost WithHtmlCapture(HtmlSinkOptions? options = null)
    {
        _htmlCaptureEnabled = true;
        _htmlCaptureOptions = options;
        return this;
    }

    /// <summary>
    /// Configures the injected test shell's viewport size. The default is 80×24.
    /// </summary>
    /// <param name="width">Viewport width in columns; must be positive.</param>
    /// <param name="height">Viewport height in rows; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is zero or negative.</exception>
    public TigerCliAppTestHost WithViewport(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _viewportWidth = width;
        _viewportHeight = height;
        return this;
    }

    /// <summary>
    /// Runs the app once with the configured arguments, planned prompt answers, viewport, and
    /// prompt timeout, and returns the exit code plus the output captured from
    /// <see cref="Console.Out"/> and <see cref="Console.Error"/>. The run is forced to a
    /// colour-free output mode so captured text never contains ANSI escape sequences; the original
    /// console writers and colour mode are restored even when the run throws.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token passed through to the app run.</param>
    /// <returns>The exit code and captured output of the completed run.</returns>
    /// <exception cref="InvalidOperationException">The host has already run once — hosts are single-use.</exception>
    public async Task<TigerCliAppRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _hasRun, 1) == 1)
            throw new InvalidOperationException("TigerCliAppTestHost is single-use. Create a new host for another run.");

        var shell = new TestShell(
            _viewportWidth,
            _viewportHeight,
            culture: ResolveShellCulture());
        EnqueuePlannedAnswers(shell);

        var originalOut = Console.Out;
        var originalError = Console.Error;
        // Pin a deterministic colour mode so captured output never depends on the host terminal's
        // ANSI capability (Auto could otherwise upgrade to AnsiSink and leak escape sequences).
        var originalColorMode = TigerConsole.ColorMode;
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        var synchronizedStdout = TextWriter.Synchronized(stdout);
        var synchronizedStderr = TextWriter.Synchronized(stderr);

        // Styled capture: push HtmlSink scopes for stdout and stderr so the whole run renders into
        // them (TigerCliApp.RunAsync keeps an already-pushed output scope). The live sinks run with
        // WrapInPre = false — HtmlSink's <pre> wrapper closes permanently on the first Flush, and a
        // run flushes once per markup call — so the requested wrapping is applied at completion.
        // The scopes pin a plain base style so unstyled output carries no machine-dependent console
        // colours. --no-color pinning below stays: it keeps anything outside the scopes plain.
        StringWriter? htmlStdout = null;
        StringWriter? htmlStderr = null;
        IDisposable? htmlOutputScope = null;
        IDisposable? htmlErrorScope = null;
        if (_htmlCaptureEnabled)
        {
            var liveOptions = new HtmlSinkOptions
            {
                WrapInPre = false,
                HyperlinkMode = _htmlCaptureOptions?.HyperlinkMode ?? HtmlHyperlinkMode.Text,
                SoftMaxWidth = _htmlCaptureOptions?.SoftMaxWidth,
            };
            htmlStdout = new StringWriter(CultureInfo.InvariantCulture);
            htmlStderr = new StringWriter(CultureInfo.InvariantCulture);
            htmlOutputScope = TigerConsole.PushOutputSink(new HtmlSink(htmlStdout, liveOptions), plainBaseStyle: true);
            htmlErrorScope = TigerConsole.PushErrorSink(new HtmlSink(htmlStderr, liveOptions), plainBaseStyle: true);
        }

        try
        {
            TigerConsole.ColorMode = CliColorMode.Never;
            Console.SetOut(synchronizedStdout);
            Console.SetError(synchronizedStderr);
            // Force Never via the framework option too: RunAsync applies the app's configured colour
            // mode, which would otherwise overwrite the global set above. --no-color is stripped
            // before command parsing, so the command never sees it.
            var effectiveArgs = _args.Append("--no-color").ToArray();
            var exitCode = await _app.RunAsync(effectiveArgs, shell, _promptTimeout, cancellationToken).ConfigureAwait(false);
            synchronizedStdout.Flush();
            synchronizedStderr.Flush();
            return new TigerCliAppRunResult(exitCode, stdout.ToString(), stderr.ToString())
            {
                StdOutHtml = _htmlCaptureEnabled ? FinalizeCapturedHtml(htmlStdout!.ToString()) : null,
                StdErrHtml = _htmlCaptureEnabled ? FinalizeCapturedHtml(htmlStderr!.ToString()) : null,
            };
        }
        finally
        {
            htmlErrorScope?.Dispose();
            htmlOutputScope?.Dispose();
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            TigerConsole.ColorMode = originalColorMode;
        }
    }

    // Normalizes to LF (markup writes Environment.NewLine as segment text, so raw capture is
    // platform-dependent) and applies the caller's WrapInPre choice (default true).
    private string FinalizeCapturedHtml(string innerHtml)
    {
        var normalized = innerHtml.Replace("\r\n", "\n");
        return _htmlCaptureOptions?.WrapInPre ?? true
            ? $"<pre class=\"tigercli\">{normalized}</pre>"
            : normalized;
    }

    private CultureInfo ResolveShellCulture()
    {
        if (!_app.CultureOptionEnabled)
            return _app.DefaultCulture;

        string? requested = null;
        for (var i = 0; i < _args.Length; i++)
        {
            var arg = _args[i];
            if (arg == "--culture")
            {
                requested = i + 1 < _args.Length ? _args[i + 1] : string.Empty;
                break;
            }

            if (arg.StartsWith("--culture=", StringComparison.Ordinal))
            {
                requested = arg["--culture=".Length..];
                break;
            }
        }

        if (requested is null)
            return _app.DefaultCulture;

        return _app.SupportedCultures.FirstOrDefault(
            culture => string.Equals(culture.Name, requested, StringComparison.OrdinalIgnoreCase))
            ?? _app.DefaultCulture;
    }

    private void EnqueuePlannedAnswers(TestShell shell)
    {
        foreach (var answer in _answers)
        {
            switch (answer.Kind)
            {
                case AnswerKind.Text:
                    EnqueueText(shell, answer.TextValue!);
                    break;
                case AnswerKind.Select:
                    EnqueueSelect(shell, answer.Index);
                    break;
                case AnswerKind.Confirm:
                    EnqueueConfirm(shell, answer.BoolValue);
                    break;
                case AnswerKind.MultiSelect:
                    EnqueueMultiSelect(shell, answer.Indexes!);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown answer kind '{answer.Kind}'.");
            }
        }
    }

    private static void EnqueueText(TestShell shell, string value)
    {
        foreach (var ch in value)
            shell.Terminal.EnqueueKey(ToConsoleKey(ch), keyChar: ch);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
    }

    private static ConsoleKey ToConsoleKey(char value)
    {
        if (value >= 'a' && value <= 'z')
            return (ConsoleKey)((int)ConsoleKey.A + (value - 'a'));

        if (value >= 'A' && value <= 'Z')
            return (ConsoleKey)((int)ConsoleKey.A + (value - 'A'));

        if (value >= '0' && value <= '9')
            return (ConsoleKey)((int)ConsoleKey.D0 + (value - '0'));

        return ConsoleKey.Spacebar;
    }

    private static void EnqueueSelect(TestShell shell, int index)
    {
        for (var i = 0; i < index; i++)
            shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
    }

    private static void EnqueueConfirm(TestShell shell, bool value)
    {
        // Confirm prompts use a Yes/No message box: Yes is the default (left) button, and the row
        // navigates horizontally, so Right moves to the No button.
        if (!value)
            shell.Terminal.EnqueueKey(ConsoleKey.RightArrow);

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
    }

    private static void EnqueueMultiSelect(TestShell shell, int[] indexes)
    {
        var currentRow = 0;
        foreach (var index in indexes)
        {
            for (var i = currentRow; i < index; i++)
                shell.Terminal.EnqueueKey(ConsoleKey.DownArrow);

            shell.Terminal.EnqueueKey(ConsoleKey.Spacebar, keyChar: ' ');
            currentRow = index;
        }

        shell.Terminal.EnqueueKey(ConsoleKey.Enter);
    }

    private sealed record PlannedAnswer(
        AnswerKind Kind,
        string? TextValue = null,
        int Index = 0,
        bool BoolValue = false,
        int[]? Indexes = null)
    {
        public static PlannedAnswer Text(string value) => new(AnswerKind.Text, TextValue: value);
        public static PlannedAnswer Select(int index) => new(AnswerKind.Select, Index: index);
        public static PlannedAnswer Confirm(bool value) => new(AnswerKind.Confirm, BoolValue: value);
        public static PlannedAnswer MultiSelect(int[] indexes) => new(AnswerKind.MultiSelect, Indexes: indexes);
    }

    private enum AnswerKind
    {
        Text,
        Select,
        Confirm,
        MultiSelect
    }
}
