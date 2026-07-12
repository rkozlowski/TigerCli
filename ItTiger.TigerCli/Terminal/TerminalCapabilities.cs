using System;
using System.IO;
using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// Detects the ANSI capability of a console stream for <see cref="CliColorMode.Auto"/>. The decision
/// is conservative about <em>whether</em> ANSI is allowed at all (redirected streams, <c>NO_COLOR</c>,
/// <c>CLICOLOR=0</c>, and <c>TERM=dumb</c>/empty all disable it) but practical once it is: a normal
/// interactive non-Windows terminal resolves to <see cref="CliAnsiSupport.Ansi256"/> without requiring
/// <c>TERM</c> to advertise <c>256color</c>. The pure <see cref="Detect"/> core takes all environment
/// inputs as parameters so it can be unit-tested without touching the real process environment.
/// </summary>
public static class TerminalCapabilities
{
    /// <summary>
    /// Deterministic fallback width (in columns) used by <see cref="GetSafeOutputWidth"/> when the real
    /// terminal width cannot be read safely. The fallback affects layout/wrapping only; it does
    /// <em>not</em> imply ANSI/colour support (that is decided separately by <see cref="Detect"/>).
    /// </summary>
    public const int DefaultOutputWidth = 120;

    /// <summary>
    /// Deterministic fallback height (in rows) used when a concrete terminal height is required (the
    /// full-screen render buffer and interactive viewport) but cannot be read safely. Structured,
    /// non-interactive output does not clamp to this — it leaves height unbounded under redirection.
    /// </summary>
    public const int DefaultOutputHeight = 30;

    /// <summary>
    /// Returns the current terminal width when it can be read safely, otherwise
    /// <see cref="DefaultOutputWidth"/>. The result is always at least 1.
    /// <para>
    /// <see cref="Console.WindowWidth"/> is a terminal <em>capability</em>, not a guaranteed value:
    /// reading it when stdout is redirected, piped, captured, or there is no interactive console can
    /// throw (e.g. "The handle is invalid.") or report a nonsensical value. Rendering and layout code
    /// must call this instead of reading <see cref="Console.WindowWidth"/> directly.
    /// </para>
    /// </summary>
    /// <param name="forError">When <c>true</c>, gates on stderr redirection instead of stdout.</param>
    public static int GetSafeOutputWidth(bool forError = false)
        => ResolveWidth(
            forError ? Console.IsErrorRedirected : Console.IsOutputRedirected,
            static () => Console.WindowWidth);

    /// <summary>
    /// Returns the current terminal height when it can be read safely, otherwise <c>null</c>.
    /// A <c>null</c> result means "no soft height bound" — structured output then emits all rows
    /// rather than clamping to a window that may not exist under redirection.
    /// </summary>
    /// <param name="forError">When <c>true</c>, gates on stderr redirection instead of stdout.</param>
    public static int? GetSafeOutputHeight(bool forError = false)
        => ResolveHeight(
            forError ? Console.IsErrorRedirected : Console.IsOutputRedirected,
            static () => Console.WindowHeight);

    /// <summary>
    /// Pure width-resolution core, with redirection state and the raw width reader injected so it can
    /// be unit-tested without touching the real console. Returns <see cref="DefaultOutputWidth"/> when
    /// redirected, when <paramref name="readWidth"/> throws, or when it yields a non-positive value;
    /// otherwise the value read (always at least 1).
    /// </summary>
    public static int ResolveWidth(bool isRedirected, Func<int> readWidth)
    {
        ArgumentNullException.ThrowIfNull(readWidth);

        if (isRedirected)
            return DefaultOutputWidth;

        try
        {
            int width = readWidth();
            return width >= 1 ? width : DefaultOutputWidth;
        }
        catch (IOException) { return DefaultOutputWidth; }
        catch (PlatformNotSupportedException) { return DefaultOutputWidth; }
        catch (ArgumentOutOfRangeException) { return DefaultOutputWidth; }
    }

    /// <summary>
    /// Pure height-resolution core (see <see cref="ResolveWidth"/>). Returns <c>null</c> when
    /// redirected, when <paramref name="readHeight"/> throws, or when it yields a non-positive value.
    /// </summary>
    public static int? ResolveHeight(bool isRedirected, Func<int> readHeight)
    {
        ArgumentNullException.ThrowIfNull(readHeight);

        if (isRedirected)
            return null;

        try
        {
            int height = readHeight();
            return height >= 1 ? height : null;
        }
        catch (IOException) { return null; }
        catch (PlatformNotSupportedException) { return null; }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>Detects the stdout ANSI capability from the real process environment.</summary>
    public static CliAnsiSupport ForStdout()
    {
        var isWindows = OperatingSystem.IsWindows();
        var redirected = Console.IsOutputRedirected;
        return Detect(
            Environment.GetEnvironmentVariable,
            redirected,
            isWindows,
            windowsVtSupported: isWindows && !redirected && WindowsVt.TryEnableForStdout());
    }

    /// <summary>Detects the stderr ANSI capability from the real process environment.</summary>
    public static CliAnsiSupport ForStderr()
    {
        var isWindows = OperatingSystem.IsWindows();
        var redirected = Console.IsErrorRedirected;
        return Detect(
            Environment.GetEnvironmentVariable,
            redirected,
            isWindows,
            windowsVtSupported: isWindows && !redirected && WindowsVt.TryEnableForStderr());
    }

    /// <summary>
    /// Pure capability detection for <see cref="CliColorMode.Auto"/>.
    /// </summary>
    /// <param name="getEnvironmentVariable">Environment-variable lookup (returns <c>null</c> when unset).</param>
    /// <param name="isRedirected">Whether the target stream is redirected/captured.</param>
    /// <param name="isWindows">Whether the host OS is Windows.</param>
    /// <param name="windowsVtSupported">
    /// Result of the Windows VT probe for this stream. Ignored on non-Windows.
    /// </param>
    public static CliAnsiSupport Detect(
        Func<string, string?> getEnvironmentVariable,
        bool isRedirected,
        bool isWindows,
        bool windowsVtSupported)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var force = IsTruthy(getEnvironmentVariable("FORCE_COLOR"))
            || IsTruthy(getEnvironmentVariable("CLICOLOR_FORCE"));

        // NO_COLOR (https://no-color.org): presence with any value disables colour. Within Auto it
        // wins over FORCE_COLOR/CLICOLOR_FORCE; only an explicit CliColorMode (handled by the sink
        // factory) can still force ANSI.
        if (getEnvironmentVariable("NO_COLOR") is not null)
            return CliAnsiSupport.None;

        // CLICOLOR=0 disables colour unless forced.
        if (!force && getEnvironmentVariable("CLICOLOR") == "0")
            return CliAnsiSupport.None;

        // FORCE_COLOR/CLICOLOR_FORCE opt into faithful 256-colour, even when redirected.
        if (force)
            return CliAnsiSupport.Ansi256;

        // Auto never emits ANSI to a redirected/captured stream.
        if (isRedirected)
            return CliAnsiSupport.None;

        if (isWindows)
            return windowsVtSupported ? CliAnsiSupport.Ansi256 : CliAnsiSupport.None;

        // Non-Windows interactive terminal. TERM=dumb/empty disables ANSI; any other terminal is
        // treated as 256-colour capable — modern Linux/macOS terminals (xterm, screen, tmux, linux,
        // vt100, ansi, alacritty, kitty, …) handle 8-bit colour without advertising it via TERM.
        var term = getEnvironmentVariable("TERM");
        if (string.IsNullOrEmpty(term) || string.Equals(term, "dumb", StringComparison.OrdinalIgnoreCase))
            return CliAnsiSupport.None;

        return CliAnsiSupport.Ansi256;
    }

    // Truthy: any non-empty value other than "0". Matches the common FORCE_COLOR/CLICOLOR_FORCE convention.
    private static bool IsTruthy(string? value)
        => !string.IsNullOrEmpty(value) && value != "0";
}
