using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tui.Testing;

/// <summary>
/// In-memory terminal implementation intended for tests of semi-interactive TUI flows.
/// </summary>
public sealed class TestTerminal : ICliTerminal
{
    private readonly object _sync = new();
    private readonly Queue<ConsoleKeyInfo> _keys = new();
    private readonly TestRenderSink _sink;
    private TaskCompletionSource _changed = NewSignal();

    private int _cursorLeft;
    private int _cursorTop;
    private int _renderCount;
    private int _readCount;
    private int _clearCount;
    private long _inputGeneration;
    private long _drainedGeneration;
    private CliGrid? _lastRenderedGrid;
    private List<string> _lastRenderedLines = [];
    private string _lastRenderedText = string.Empty;
    private bool _cursorVisibleAtLastRender;
    private string? _windowTitle;
    private int _windowTitleWriteCount;

    /// <summary>Creates an in-memory terminal with the specified simulated window size.</summary>
    /// <param name="windowWidth">The window width in cells.</param>
    /// <param name="windowHeight">The window height in cells.</param>
    public TestTerminal(int windowWidth = 80, int windowHeight = 24)
    {
        WindowWidth = windowWidth;
        WindowHeight = windowHeight;
        _sink = new TestRenderSink(this);
    }

    /// <inheritdoc/>
    public ITerminalState State => new TestTerminalState(ForegroundColor, BackgroundColor, CursorVisible, CursorLeft, CursorTop);

    /// <inheritdoc/>
    public bool KeyAvailable
    {
        get
        {
            TaskCompletionSource? signal = null;
            bool available;
            lock (_sync)
            {
                available = _keys.Count > 0;
                if (!available && _drainedGeneration < _inputGeneration)
                {
                    _drainedGeneration = _inputGeneration;
                    signal = ResetSignal();
                }
            }

            signal?.TrySetResult();
            return available;
        }
    }

    /// <inheritdoc/>
    public CliColor ForegroundColor { get; set; } = CliColor.Gray;
    /// <inheritdoc/>
    public CliColor BackgroundColor { get; set; } = CliColor.Black;
    /// <inheritdoc/>
    public bool CursorVisible { get; set; } = true;

    /// <inheritdoc/>
    public int CursorLeft
    {
        get
        {
            lock (_sync)
                return _cursorLeft;
        }
    }

    /// <inheritdoc/>
    public int CursorTop
    {
        get
        {
            lock (_sync)
                return _cursorTop;
        }
    }

    /// <summary>Gets or sets the simulated window width in cells.</summary>
    public int WindowWidth { get; set; }
    /// <summary>Gets or sets the simulated window height in cells.</summary>
    public int WindowHeight { get; set; }

    /// <summary>Gets the render sink associated with this terminal.</summary>
    public ICliRenderSink Sink
    {
        get
        {
            _sink.Reset();
            return _sink;
        }
    }

    /// <summary>The number of grids captured by <see cref="RenderGrid"/>.</summary>
    public int RenderCount
    {
        get
        {
            lock (_sync)
                return _renderCount;
        }
    }

    /// <summary>The number of simulated keys read from the input queue.</summary>
    public int ReadCount
    {
        get
        {
            lock (_sync)
                return _readCount;
        }
    }

    /// <summary>The number of line-clear requests received by the terminal.</summary>
    public int ClearCount
    {
        get
        {
            lock (_sync)
                return _clearCount;
        }
    }

    /// <summary>The most recently captured grid, or <c>null</c> before the first render.</summary>
    public CliGrid? LastRenderedGrid
    {
        get
        {
            lock (_sync)
                return _lastRenderedGrid;
        }
    }

    /// <summary>A snapshot of the most recently rendered text lines.</summary>
    public IReadOnlyList<string> LastRenderedLines
    {
        get
        {
            lock (_sync)
                return _lastRenderedLines.ToArray();
        }
    }

    /// <summary>The most recently rendered lines joined with line-feed characters.</summary>
    public string LastRenderedText
    {
        get
        {
            lock (_sync)
                return _lastRenderedText;
        }
    }

    /// <summary>The most recently captured sanitized window title.</summary>
    public string? WindowTitle
    {
        get
        {
            lock (_sync)
                return _windowTitle;
        }
    }

    /// <summary>The number of window-title writes captured by the terminal.</summary>
    public int WindowTitleWriteCount
    {
        get
        {
            lock (_sync)
                return _windowTitleWriteCount;
        }
    }

    /// <summary>
    /// The value of <see cref="CursorVisible"/> captured at the moment the most recent
    /// <see cref="RenderGrid"/> call began drawing. Lets tests assert the cursor was hidden while the
    /// grid was being written (it should only be made visible again after a render completes).
    /// </summary>
    public bool CursorVisibleAtLastRender
    {
        get
        {
            lock (_sync)
                return _cursorVisibleAtLastRender;
        }
    }

    /// <summary>Queues a simulated key press for the modal loop to read.</summary>
    /// <param name="key">The console key.</param>
    /// <param name="modifiers">The modifier keys held during the key press.</param>
    /// <param name="keyChar">The character produced by the key press.</param>
    public void EnqueueKey(ConsoleKey key, ConsoleModifiers modifiers = ConsoleModifiers.None, char keyChar = '\0')
    {
        EnqueueKey(CreateKeyInfo(key, modifiers, keyChar));
    }

    /// <summary>Queues a sequence of simulated key presses without modifiers or character values.</summary>
    /// <param name="keys">The keys to queue in read order.</param>
    public void EnqueueKeys(params ConsoleKey[] keys)
    {
        if (keys.Length == 0)
            return;

        TaskCompletionSource signal;
        lock (_sync)
        {
            foreach (var key in keys)
                _keys.Enqueue(CreateKeyInfo(key, ConsoleModifiers.None, '\0'));
            _inputGeneration++;
            signal = ResetSignal();
        }
        signal.TrySetResult();
    }

    /// <summary>Queues a fully specified simulated key press.</summary>
    /// <param name="key">The key information to queue.</param>
    public void EnqueueKey(ConsoleKeyInfo key)
    {
        TaskCompletionSource signal;
        lock (_sync)
        {
            _keys.Enqueue(key);
            _inputGeneration++;
            signal = ResetSignal();
        }
        signal.TrySetResult();
    }

    /// <summary>Dequeues the next simulated key and increments <see cref="ReadCount"/>.</summary>
    /// <param name="intercept">Accepted for terminal compatibility; simulated input is not echoed.</param>
    /// <returns>The next queued key.</returns>
    public ConsoleKeyInfo ReadKey(bool intercept)
    {
        TaskCompletionSource signal;
        lock (_sync)
        {
            if (_keys.Count == 0)
                throw new InvalidOperationException("No test key input is available.");

            var key = _keys.Dequeue();
            _readCount++;
            signal = ResetSignal();
            signal.TrySetResult();
            return key;
        }
    }

    /// <inheritdoc/>
    public void SetCursorPosition(int left, int top)
    {
        lock (_sync)
        {
            _cursorLeft = left;
            _cursorTop = top;
        }
    }

    /// <summary>Captures a grid as plain text and increments <see cref="RenderCount"/>.</summary>
    /// <param name="x">The requested starting column.</param>
    /// <param name="y">The requested starting row.</param>
    /// <param name="grid">The grid to capture.</param>
    public void RenderGrid(int x, int y, CliGrid grid)
    {
        var lines = ItTiger.TigerCli.Terminal.TigerConsole.RenderGridToLines(grid);
        TaskCompletionSource signal;

        lock (_sync)
        {
            // Capture cursor visibility as drawing begins so tests can verify it was hidden mid-render.
            _cursorVisibleAtLastRender = CursorVisible;
            _lastRenderedGrid = grid;
            _lastRenderedLines = lines;
            _lastRenderedText = string.Join("\n", lines);
            _renderCount++;
            _cursorLeft = 0;
            _cursorTop = y + lines.Count;
            signal = ResetSignal();
        }

        signal.TrySetResult();
    }

    /// <summary>Records a line-clear request and increments <see cref="ClearCount"/>.</summary>
    /// <param name="fromRow">The first row requested for clearing.</param>
    /// <param name="count">The number of rows requested for clearing.</param>
    /// <param name="bgColor">The requested background colour.</param>
    public void ClearLines(int fromRow, int count, CliColor bgColor)
    {
        TaskCompletionSource signal;
        lock (_sync)
        {
            _clearCount++;
            signal = ResetSignal();
        }
        signal.TrySetResult();
    }

    /// <inheritdoc/>
    public void RestoreState(ITerminalState terminalState, int startRow, int height)
    {
        if (terminalState is TestTerminalState state)
        {
            ForegroundColor = state.ForegroundColor;
            BackgroundColor = state.BackgroundColor;
            CursorVisible = state.CursorVisible;
            SetCursorPosition(state.CursorLeft, state.CursorTop);
        }
        else
        {
            CursorVisible = true;
            SetCursorPosition(0, startRow);
        }
    }

    /// <summary>Waits until at least the specified number of renders has been captured.</summary>
    /// <param name="renderCount">The minimum render count.</param>
    /// <param name="timeout">The optional maximum time to wait.</param>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    /// <returns>A task that completes when the count is reached.</returns>
    public Task WaitForRenderCountAsync(int renderCount, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        return WaitUntilAsync(() => _renderCount >= renderCount, timeout, cancellationToken);
    }

    /// <summary>Waits until at least the specified number of simulated keys has been read.</summary>
    /// <param name="readCount">The minimum read count.</param>
    /// <param name="timeout">The optional maximum time to wait.</param>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    /// <returns>A task that completes when the count is reached.</returns>
    public Task WaitForReadCountAsync(int readCount, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        return WaitUntilAsync(() => _readCount >= readCount, timeout, cancellationToken);
    }

    /// <summary>Waits until the modal loop has observed the queued input as drained.</summary>
    /// <param name="timeout">The optional maximum time to wait.</param>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    /// <returns>A task that completes when the current input batch has drained.</returns>
    public Task WaitForInputDrainedAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        long targetGeneration;
        lock (_sync)
            targetGeneration = _inputGeneration;

        return WaitUntilAsync(() => _drainedGeneration >= targetGeneration, timeout, cancellationToken);
    }

    private async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        CancellationTokenSource? timeoutCts = null;
        CancellationTokenSource? linkedCts = null;
        var token = cancellationToken;

        if (timeout.HasValue)
        {
            timeoutCts = new CancellationTokenSource(timeout.Value);
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            token = linkedCts.Token;
        }

        try
        {
            while (true)
            {
                Task waitTask;
                lock (_sync)
                {
                    if (condition())
                        return;
                    waitTask = _changed.Task;
                }

                await waitTask.WaitAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for the test terminal condition.");
        }
        finally
        {
            linkedCts?.Dispose();
            timeoutCts?.Dispose();
        }
    }

    private TaskCompletionSource ResetSignal()
    {
        var signal = _changed;
        _changed = NewSignal();
        return signal;
    }

    private static TaskCompletionSource NewSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static ConsoleKeyInfo CreateKeyInfo(ConsoleKey key, ConsoleModifiers modifiers, char keyChar)
    {
        return new ConsoleKeyInfo(
            keyChar,
            key,
            modifiers.HasFlag(ConsoleModifiers.Shift),
            modifiers.HasFlag(ConsoleModifiers.Alt),
            modifiers.HasFlag(ConsoleModifiers.Control));
    }

    private sealed record TestTerminalState(
        CliColor ForegroundColor,
        CliColor BackgroundColor,
        bool CursorVisible,
        int CursorLeft,
        int CursorTop) : ITerminalState;

    private sealed class TestRenderSink(TestTerminal terminal) : ICliRenderSink
    {
        public int? SoftMaxWidth => terminal.WindowWidth;
        public int? SoftMaxHeight => terminal.WindowHeight;
        public int? MaxWidth => terminal.WindowWidth;
        public int? MaxHeight => terminal.WindowHeight;

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
            TaskCompletionSource signal;
            lock (terminal._sync)
            {
                terminal._windowTitle = AnsiSgr.SanitizeControlString(title);
                terminal._windowTitleWriteCount++;
                signal = terminal.ResetSignal();
            }
            signal.TrySetResult();
        }
    }
}
