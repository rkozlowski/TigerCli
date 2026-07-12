using System;
using System.Runtime.InteropServices;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// Defensive, cached Windows virtual-terminal (VT) probe. Attempts to enable
/// <c>ENABLE_VIRTUAL_TERMINAL_PROCESSING</c> on the stdout/stderr console handle so ANSI SGR
/// sequences are interpreted. Any failure (legacy conhost, restricted environment, redirected
/// handle) results in <c>false</c>, letting callers fall back to the legacy console sink. The probe
/// runs at most once per handle and is a no-op on non-Windows platforms.
/// </summary>
internal static class WindowsVt
{
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    private static bool? _stdout;
    private static bool? _stderr;
    private static readonly object Sync = new();

    /// <summary>Probes (once) whether VT processing is enabled for the stdout console handle.</summary>
    public static bool TryEnableForStdout()
    {
        lock (Sync)
            return _stdout ??= TryEnable(STD_OUTPUT_HANDLE);
    }

    /// <summary>Probes (once) whether VT processing is enabled for the stderr console handle.</summary>
    public static bool TryEnableForStderr()
    {
        lock (Sync)
            return _stderr ??= TryEnable(STD_ERROR_HANDLE);
    }

    private static bool TryEnable(int handleId)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            var handle = GetStdHandle(handleId);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                return false;

            if (!GetConsoleMode(handle, out var mode))
                return false; // Not a real console (e.g. redirected to a pipe/file).

            if ((mode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) != 0)
                return true; // Already enabled.

            return SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
