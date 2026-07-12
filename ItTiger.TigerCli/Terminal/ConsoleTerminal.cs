using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Terminal
{
    /// <summary>
    /// Real console-backed terminal implementation.
    /// </summary>
    public class ConsoleTerminal : ICliTerminal
    {
        private ICliRenderSink? sink;
        private CliColorMode sinkColorMode;

        // Resolves the render sink through the shared ConsoleSinkFactory stdout policy so live TUI
        // rendering uses the same effective colour path (ANSI 256 / 16-colour / plain) as normal
        // TigerConsole output, instead of a hardcoded 16-colour ConsoleSink. Cached per colour
        // mode: Auto-detection probes the terminal (env vars + Windows VT), which must not run on
        // every frame of the interactive render loop.
        private ICliRenderSink ResolveSink()
        {
            var mode = TigerConsole.ColorMode;
            if (sink is null || mode != sinkColorMode)
            {
                sink = ConsoleSinkFactory.CreateTerminalSink();
                sinkColorMode = mode;
            }
            return sink;
        }

        // Whether the resolved sink writes ANSI escape sequences; gates the ANSI clear/restore
        // paths so escape sequences are never written when the 16-colour or plain sink is active.
        private bool IsAnsiActive => ResolveSink() is AnsiSink;
        /// <inheritdoc/>
        public bool KeyAvailable 
        {
            get
            {
                return Console.KeyAvailable;
            }
        }         

        /// <inheritdoc/>
        public int CursorLeft => Console.CursorLeft;

        /// <inheritdoc/>
        public int CursorTop => Console.CursorTop;

        // Safe even without an interactive console: falls back to deterministic dimensions rather
        // than throwing when the terminal width/height cannot be read.
        /// <summary>Gets the terminal width, using a deterministic fallback when it cannot be read.</summary>
        public int WindowWidth => TerminalCapabilities.GetSafeOutputWidth();

        /// <summary>Gets the terminal height, using a deterministic fallback when it cannot be read.</summary>
        public int WindowHeight => TerminalCapabilities.GetSafeOutputHeight() ?? TerminalCapabilities.DefaultOutputHeight;

        /// <inheritdoc/>
        public ITerminalState State 
        { 
            get
            {
                return new ConsoleTerminalState();
            }
        }

        /// <inheritdoc/>
        public CliColor ForegroundColor
        {
            get
            {
                // A redirected/captured console can report an invalid color; fall back to the
                // conventional default rather than surfacing an invalid CliColor.
                return CliColorMapper.FromConsoleColorOrNull(Console.ForegroundColor) ?? CliColor.Gray;
            }
            set
            {
                Console.ForegroundColor = CliColorMapper.ToConsoleColor(value);
            }
        }
        /// <inheritdoc/>
        public CliColor BackgroundColor
        {
            get
            {
                return CliColorMapper.FromConsoleColorOrNull(Console.BackgroundColor) ?? CliColor.Black;
            }
            set
            {
                Console.BackgroundColor = CliColorMapper.ToConsoleColor(value);
            }
        }

        /// <inheritdoc/>
        public bool CursorVisible 
        {
            get
            {
                
                if (!OperatingSystem.IsWindows())
                {
                    return true;
                }
                return Console.CursorVisible;                
            }
            set 
            { 
                Console.CursorVisible = value;  
            } 
        }

        /// <summary>
        /// Gets the render sink for this terminal, after resetting its tracked style state. The sink
        /// is chosen by <see cref="TigerConsole.ColorMode"/> and, under <see cref="CliColorMode.Auto"/>,
        /// detected terminal capability — the same policy as normal <see cref="TigerConsole"/> output.
        /// </summary>
        public ICliRenderSink Sink
        {
            get
            {
                var s = ResolveSink();
                s.Reset();
                return s;
            }
        }

        /// <inheritdoc/>
        public ConsoleKeyInfo ReadKey(bool intercept)
        {
            return Console.ReadKey(intercept);
        }

        /// <summary>Attempts to move the console cursor to the specified coordinates.</summary>
        /// <param name="left">The zero-based column.</param>
        /// <param name="top">The zero-based row.</param>
        public void SetCursorPosition(int left, int top)
        {
            TrySetCursorPosition(left, top);
        }

        // Resilient to terminal resize/reflow races: validates the cursor
        // position against the current buffer before rendering, and swallows
        // terminal-race exceptions raised mid-frame. A skipped or partial frame
        // is acceptable — the outer loop will remeasure and re-render.
        /// <summary>
        /// Attempts to render a grid at the specified console coordinate. A frame may be skipped if
        /// the terminal is unavailable or changes size during rendering.
        /// </summary>
        /// <param name="x">The zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        /// <param name="grid">The grid to render.</param>
        public void RenderGrid(int x, int y, CliGrid grid)
        {
            if (!TrySetCursorPosition(x, y))
                return;

            try
            {
                TigerConsole.RenderGrid(grid, ResolveSink());
            }
            catch (ArgumentOutOfRangeException)
            {
            }
            catch (IOException)
            {
            }
        }

        /// <inheritdoc/>
        public void ClearLines(int fromRow, int count, CliColor bgColor)
        {
            if (IsAnsiActive)
                ClearLinesAnsi(fromRow, count, bgColor);
            else
                ClearLines(fromRow, count, CliColorMapper.ToConsoleColor(bgColor));
        }

        private void ClearLines(int fromRow, int count, ConsoleColor bgColor)
        {
            if (!TryClampClearRegion(ref fromRow, ref count, out int windowWidth))
                return;

            Console.BackgroundColor = bgColor;
            WriteClearLines(fromRow, count, new string(' ', windowWidth));
        }

        // ANSI counterpart of ClearLines: paints each row with the faithful 0–255 background via SGR
        // sequences instead of degrading it to a ConsoleColor. A null background clears to the
        // terminal default (after a reset), used when restoring state at modal exit.
        private void ClearLinesAnsi(int fromRow, int count, CliColor? bgColor)
        {
            if (!TryClampClearRegion(ref fromRow, ref count, out int windowWidth))
                return;

            WriteClearLines(fromRow, count, BuildAnsiClearLine(bgColor, windowWidth));
        }

        // The blank line written by the ANSI clear path. Ends at the terminal default (trailing
        // reset / leading reset for the default-background case) so the fill colour never bleeds
        // into subsequent output.
        internal static string BuildAnsiClearLine(CliColor? bgColor, int width)
        {
            var spaces = new string(' ', width);
            return bgColor.HasValue
                ? AnsiSgr.BuildSgr(new[] { AnsiSgr.BackgroundParams(bgColor.Value) }) + spaces + AnsiSgr.Reset
                : AnsiSgr.Reset + spaces;
        }

        private static void WriteClearLines(int fromRow, int count, string blank)
        {
            for (int i = 0; i < count; i++)
            {
                try
                {
                    Console.SetCursorPosition(0, fromRow + i);
                    Console.Write(blank);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return;
                }
                catch (IOException)
                {
                    return;
                }
            }
        }

        // Re-reads buffer dimensions at call time and clamps the clear region so a
        // terminal resize between layout and cleanup cannot make SetCursorPosition throw.
        // Returns false when nothing remains to clear or dimensions are unavailable.
        private static bool TryClampClearRegion(ref int fromRow, ref int count, out int windowWidth)
        {
            windowWidth = 0;
            if (count <= 0) return false;

            int bufferHeight;
            try
            {
                bufferHeight = Console.BufferHeight;
                windowWidth = Console.WindowWidth;
            }
            catch (IOException)
            {
                return false;
            }

            if (bufferHeight <= 0 || windowWidth <= 0) return false;

            if (fromRow < 0)
            {
                count += fromRow;
                fromRow = 0;
            }
            if (fromRow >= bufferHeight) return false;

            if (fromRow + count > bufferHeight)
                count = bufferHeight - fromRow;

            return count > 0;
        }

        /// <summary>Restores a captured console state and clears the specified rendered region.</summary>
        /// <param name="terminalState">A state previously captured from this terminal.</param>
        /// <param name="startRow">The first row of the rendered region.</param>
        /// <param name="height">The number of rows in the rendered region.</param>
        public void RestoreState(ITerminalState terminalState, int startRow, int height)
        {
            var state = terminalState as ConsoleTerminalState;
            if (state == null)
            {
                throw new ArgumentException("Bad or missing terminalState", "terminalState");
            }
            TrySetCursorPosition(0, startRow);
            if (IsAnsiActive)
            {
                // The ANSI sink never touched Console.Fore/BackgroundColor, so the captured colours
                // are already in effect; clear the region to the terminal default background (which
                // a ConsoleColor approximation could repaint incorrectly).
                ClearLinesAnsi(startRow, height, bgColor: null);
            }
            else
            {
                ClearLines(startRow, height, state.BackgroundColor);
            }
            TrySetCursorPosition(0, startRow);
            Console.ForegroundColor = state.ForegroundColor;
            Console.BackgroundColor = state.BackgroundColor;
            Console.CursorVisible = true;
        }

        // Guards against stale coordinates from before a terminal resize. Returns
        // true only when the cursor was successfully placed at (left, top); false
        // if dimensions were unavailable, the coordinates were out of range, or
        // a terminal-race exception was swallowed. Callers that need to gate
        // further output on a valid cursor position should check the return.
        private static bool TrySetCursorPosition(int left, int top)
        {
            int bufferWidth, bufferHeight;
            try
            {
                bufferWidth = Console.BufferWidth;
                bufferHeight = Console.BufferHeight;
            }
            catch (IOException)
            {
                return false;
            }

            if (bufferWidth <= 0 || bufferHeight <= 0) return false;
            if (left < 0 || top < 0) return false;
            if (left >= bufferWidth || top >= bufferHeight) return false;

            try
            {
                Console.SetCursorPosition(left, top);
                return true;
            }
            catch (ArgumentOutOfRangeException) { return false; }
            catch (IOException) { return false; }
        }
    }
}
