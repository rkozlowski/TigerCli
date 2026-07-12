using System;
using System.Collections.Generic;
using System.Text;

namespace ItTiger.TigerCli.Terminal
{
    /// <summary>
    /// Snapshot of real console state captured by <see cref="ConsoleTerminal"/> so the terminal can
    /// restore colors after an interactive render.
    /// </summary>
    /// <remarks>
    /// This type is public only because <see cref="ICliTerminal.State"/> exposes the
    /// <see cref="ITerminalState"/> boundary. It has no public members and is not intended to be
    /// constructed or inspected by application code.
    /// </remarks>
    public class ConsoleTerminalState : ITerminalState
    {
        internal ConsoleColor ForegroundColor { get; set; }
        internal ConsoleColor BackgroundColor { get; set; }

        internal ConsoleTerminalState()
        {
            ForegroundColor =Console.ForegroundColor; 
            BackgroundColor = Console.BackgroundColor;
        }

    }
}
