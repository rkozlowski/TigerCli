using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Terminal
{
    /// <summary>
    /// Terminal abstraction used by interactive rendering and test hosts.
    /// </summary>
    public interface ICliTerminal
    {
        /// <summary>Captures the terminal state needed for later restoration.</summary>
        ITerminalState State { get; }
        /// <summary>Whether a key press is available without blocking.</summary>
        bool KeyAvailable { get; }
        /// <summary>Reads a key from the terminal.</summary>
        ConsoleKeyInfo ReadKey(bool intercept);

        /// <summary>Current foreground colour.</summary>
        CliColor ForegroundColor { get; set; }
        /// <summary>Current background colour.</summary>
        CliColor BackgroundColor { get; set; }

        /// <summary>Cursor visibility.</summary>
        bool CursorVisible { get; set; }

        /// <summary>Current cursor column.</summary>
        int CursorLeft { get; }
        /// <summary>Current cursor row.</summary>
        int CursorTop { get; }

        /// <summary>Current terminal window width.</summary>
        int WindowWidth { get; }
        /// <summary>Current terminal window height.</summary>
        int WindowHeight { get; }

        /// <summary>Render sink used by this terminal.</summary>
        ICliRenderSink Sink { get; }

        /// <summary>Moves the cursor.</summary>
        void SetCursorPosition(int left, int top);

        /// <summary>Renders a grid at a terminal coordinate.</summary>
        void RenderGrid(int x, int y, CliGrid grid);
        
        /// <summary>Clears a range of terminal rows using the supplied background colour.</summary>
        void ClearLines(int fromRow, int count, CliColor bgColor);

        /// <summary>Restores a captured terminal state and clears the rendered region.</summary>
        void RestoreState(ITerminalState terminalState, int startRow, int height);

        
    }
}
