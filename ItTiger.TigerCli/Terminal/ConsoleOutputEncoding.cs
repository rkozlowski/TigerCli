using System;
using System.Text;
using System.Threading;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// Chooses how text written to the process's standard output and error streams is encoded, without
/// changing state the process shares with others. On Windows, assigning any encoding other than
/// UTF-16 to <see cref="Console.OutputEncoding"/> calls <c>SetConsoleOutputCP</c>, which changes the
/// console's code page for every process attached to it and outlives this one. So TigerCli never does
/// that unless the app explicitly asked for it through <see cref="TigerConsole.OutputEncoding"/>:
/// <list type="bullet">
/// <item>Windows, both streams on the interactive console: UTF-16, which .NET writes with
/// <c>WriteConsoleW</c>. Every character renders whatever the console code page is, and the code page
/// is left alone.</item>
/// <item>Windows, any stream redirected: the console's output code page, untouched. It is the
/// encoding the invoking shell or parent process decodes redirected output with; a consumer that
/// wants UTF-8 selects code page 65001 before starting the app. UTF-16 is not an option here: one
/// encoding serves both streams, and a redirected stream would receive UTF-16 bytes.</item>
/// <item>Other platforms: UTF-8. Assigning it only selects .NET's writer encoding; it changes nothing
/// in the terminal.</item>
/// </list>
/// The policy is applied explicitly by the paths that write to the console, never by type
/// initialization, so code that only reads TigerCli metadata (such as a shell completion request)
/// gets no console setup at all.
/// </summary>
internal static class ConsoleOutputEncoding
{
    private static int _applied;

    /// <summary>The encoding decisions the policy can make.</summary>
    internal enum Choice
    {
        /// <summary>Leave <see cref="Console.OutputEncoding"/> as the platform chose it.</summary>
        KeepPlatformDefault,

        /// <summary>UTF-16, written to the interactive Windows console with <c>WriteConsoleW</c>.</summary>
        ConsoleUnicode,

        /// <summary>UTF-8, on platforms where assigning it changes no shared terminal state.</summary>
        Utf8
    }

    /// <summary>
    /// Applies the policy once per process, before the first console write. Does nothing when the app
    /// chose an encoding through <see cref="TigerConsole.OutputEncoding"/>.
    /// </summary>
    public static void EnsureApplied()
    {
        if (Volatile.Read(ref _applied) != 0 || Interlocked.Exchange(ref _applied, 1) != 0)
            return;

        if (TigerConsole.OutputEncoding != null)
            return;

        switch (Select(OperatingSystem.IsWindows(), Console.IsOutputRedirected, Console.IsErrorRedirected))
        {
            case Choice.ConsoleUnicode:
                Console.OutputEncoding = Encoding.Unicode;
                break;
            case Choice.Utf8:
                Console.OutputEncoding = Encoding.UTF8;
                break;
        }
    }

    /// <summary>The pure decision behind <see cref="EnsureApplied"/>.</summary>
    internal static Choice Select(bool isWindows, bool isOutputRedirected, bool isErrorRedirected)
    {
        if (!isWindows)
            return Choice.Utf8;

        return isOutputRedirected || isErrorRedirected ? Choice.KeepPlatformDefault : Choice.ConsoleUnicode;
    }
}
