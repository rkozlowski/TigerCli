using System;
using System.IO;
using System.Text;
using System.Threading;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// TigerCli's standard-output encoding contract, applied once per process before the first console
/// write, never during type initialization:
/// <list type="bullet">
/// <item>Redirected output (files, pipes, captured processes) is UTF-8 on every platform.</item>
/// <item>Interactive Windows console output is UTF-16, which .NET writes with <c>WriteConsoleW</c>, so
/// every character renders whatever the console code page is.</item>
/// <item>The console code page, which every process attached to the console shares, is never
/// changed. On Windows, .NET calls <c>SetConsoleOutputCP</c> for any <see cref="Console.OutputEncoding"/>
/// except UTF-16, so TigerCli assigns only UTF-16 and reaches UTF-8 on redirected streams by giving
/// them UTF-8 writers instead.</item>
/// <item>A writer an app or test host installed with <see cref="Console.SetOut"/> or
/// <see cref="Console.SetError"/> is never replaced. Telling those apart from .NET's own writers needs
/// the compatibility mechanism in <see cref="PlatformStandardWriters"/>.</item>
/// </list>
/// An app that sets <see cref="TigerConsole.OutputEncoding"/> takes the choice over, as before.
/// </summary>
internal static class ConsoleOutputEncoding
{
    private static int _applied;

    // The UTF-8 writers TigerCli installed (as Console.Out/Error return them), so an encoding the app
    // chooses later can replace them while they are still the current writers.
    private static TextWriter? _installedOut;
    private static TextWriter? _installedError;

    /// <summary>
    /// Applies the contract once per process. Does nothing when the app chose an encoding through
    /// <see cref="TigerConsole.OutputEncoding"/>.
    /// </summary>
    public static void EnsureApplied()
    {
        if (Volatile.Read(ref _applied) != 0 || Interlocked.Exchange(ref _applied, 1) != 0)
            return;

        if (TigerConsole.IsOutputEncodingAppChosen)
            return;

        if (!OperatingSystem.IsWindows())
        {
            // Outside Windows assigning an encoding selects .NET's writer encoding and nothing else.
            Console.OutputEncoding = Encoding.UTF8;
            return;
        }

        ApplyOnWindows(Console.IsOutputRedirected, Console.IsErrorRedirected);
    }

    /// <summary>
    /// The Windows contract for the current process. Interactive streams keep the runtime's writer, now
    /// UTF-16. Redirected streams whose writer is the runtime's get a UTF-8 writer; installed writers
    /// stay as they are.
    /// </summary>
    internal static void ApplyOnWindows(bool isOutputRedirected, bool isErrorRedirected)
    {
        var ownership = PlatformStandardWriters.SwitchToUnicode();
        if (isOutputRedirected && ownership.OutIsRuntimeWriter)
        {
            Console.SetOut(PlatformStandardWriters.CreateWriter(Console.OpenStandardOutput(), Encoding.UTF8));
            _installedOut = Console.Out;
        }

        if (isErrorRedirected && ownership.ErrorIsRuntimeWriter)
        {
            Console.SetError(PlatformStandardWriters.CreateWriter(Console.OpenStandardError(), Encoding.UTF8));
            _installedError = Console.Error;
        }
    }

    /// <summary>
    /// Applies an encoding the app chose: assigned to <see cref="Console.OutputEncoding"/>, as it always
    /// was, and to any redirected-stream writer TigerCli installed that is still current.
    /// </summary>
    internal static void ApplyAppChoice(Encoding encoding)
    {
        Console.OutputEncoding = encoding;
        if (_installedOut != null && ReferenceEquals(Console.Out, _installedOut))
        {
            Console.SetOut(PlatformStandardWriters.CreateWriter(Console.OpenStandardOutput(), encoding));
            _installedOut = Console.Out;
        }

        if (_installedError != null && ReferenceEquals(Console.Error, _installedError))
        {
            Console.SetError(PlatformStandardWriters.CreateWriter(Console.OpenStandardError(), encoding));
            _installedError = Console.Error;
        }
    }
}
