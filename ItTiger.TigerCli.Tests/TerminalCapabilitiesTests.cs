using System.Collections.Generic;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the pure <see cref="TerminalCapabilities.Detect"/> policy for <see cref="CliColorMode.Auto"/>.
/// All environment inputs are injected, so these never depend on the host terminal.
/// </summary>
public sealed class TerminalCapabilitiesTests
{
    private static CliAnsiSupport Detect(
        IDictionary<string, string?> env,
        bool isRedirected = false,
        bool isWindows = false,
        bool windowsVtSupported = false)
        => TerminalCapabilities.Detect(
            name => env.TryGetValue(name, out var v) ? v : null,
            isRedirected,
            isWindows,
            windowsVtSupported);

    [Fact]
    public void NoColor_DisablesAnsi_EvenWith256Term()
    {
        var env = new Dictionary<string, string?> { ["NO_COLOR"] = "1", ["TERM"] = "xterm-256color" };
        Assert.Equal(CliAnsiSupport.None, Detect(env));
    }

    [Fact]
    public void NoColor_WinsOver_ForceColor()
    {
        var env = new Dictionary<string, string?> { ["NO_COLOR"] = "", ["FORCE_COLOR"] = "1", ["TERM"] = "xterm-256color" };
        Assert.Equal(CliAnsiSupport.None, Detect(env));
    }

    [Fact]
    public void ClicolorZero_DisablesAnsi()
    {
        var env = new Dictionary<string, string?> { ["CLICOLOR"] = "0", ["TERM"] = "xterm-256color" };
        Assert.Equal(CliAnsiSupport.None, Detect(env));
    }

    [Fact]
    public void TermDumb_DisablesAnsi()
    {
        var env = new Dictionary<string, string?> { ["TERM"] = "dumb" };
        Assert.Equal(CliAnsiSupport.None, Detect(env));
    }

    [Fact]
    public void EmptyTerm_NonWindows_DisablesAnsi()
    {
        var env = new Dictionary<string, string?>();
        Assert.Equal(CliAnsiSupport.None, Detect(env));
    }

    [Fact]
    public void Redirected_DisablesAnsi()
    {
        var env = new Dictionary<string, string?> { ["TERM"] = "xterm-256color" };
        Assert.Equal(CliAnsiSupport.None, Detect(env, isRedirected: true));
    }

    [Fact]
    public void ForceColor_ForcesAnsi256_EvenWhenRedirected()
    {
        var env = new Dictionary<string, string?> { ["FORCE_COLOR"] = "1" };
        // Forcing opts into faithful 256-colour and ignores redirection.
        Assert.Equal(CliAnsiSupport.Ansi256, Detect(env, isRedirected: true));
    }

    [Fact]
    public void ClicolorForce_ForcesColor()
    {
        var env = new Dictionary<string, string?> { ["CLICOLOR_FORCE"] = "1", ["TERM"] = "xterm-256color" };
        Assert.Equal(CliAnsiSupport.Ansi256, Detect(env, isRedirected: true));
    }

    [Fact]
    public void Term256Color_NotRedirected_DetectsAnsi256()
    {
        var env = new Dictionary<string, string?> { ["TERM"] = "xterm-256color" };
        Assert.Equal(CliAnsiSupport.Ansi256, Detect(env));
    }

    [Theory]
    [InlineData("truecolor")]
    [InlineData("24bit")]
    [InlineData("256")]
    public void ColorTerm_DetectsAnsi256(string colorTerm)
    {
        var env = new Dictionary<string, string?> { ["TERM"] = "xterm", ["COLORTERM"] = colorTerm };
        Assert.Equal(CliAnsiSupport.Ansi256, Detect(env));
    }

    [Theory]
    [InlineData("xterm")]
    [InlineData("linux")]
    [InlineData("screen")]
    [InlineData("tmux")]
    [InlineData("vt100")]
    [InlineData("ansi")]
    [InlineData("rxvt")]
    [InlineData("alacritty")]
    public void NonWindows_InteractiveTerm_DetectsAnsi256(string term)
    {
        // Modern non-Windows terminals are assumed 256-colour capable even without a 256color/COLORTERM signal.
        var env = new Dictionary<string, string?> { ["TERM"] = term };
        Assert.Equal(CliAnsiSupport.Ansi256, Detect(env));
    }

    [Theory]
    [InlineData("xterm")]
    [InlineData("linux")]
    [InlineData("screen")]
    [InlineData("tmux")]
    [InlineData("vt100")]
    [InlineData("ansi")]
    public void NonWindows_InteractiveTerm_Redirected_DisablesAnsi(string term)
    {
        // Redirected output never gets ANSI under Auto, regardless of TERM.
        var env = new Dictionary<string, string?> { ["TERM"] = term };
        Assert.Equal(CliAnsiSupport.None, Detect(env, isRedirected: true));
    }

    [Fact]
    public void Windows_VtSupported_DetectsAnsi256()
    {
        var env = new Dictionary<string, string?>();
        Assert.Equal(CliAnsiSupport.Ansi256, Detect(env, isWindows: true, windowsVtSupported: true));
    }

    [Fact]
    public void Windows_VtNotSupported_FallsBackToNone()
    {
        var env = new Dictionary<string, string?>();
        Assert.Equal(CliAnsiSupport.None, Detect(env, isWindows: true, windowsVtSupported: false));
    }

    [Fact]
    public void Windows_Redirected_DisablesAnsi()
    {
        var env = new Dictionary<string, string?>();
        Assert.Equal(CliAnsiSupport.None, Detect(env, isRedirected: true, isWindows: true, windowsVtSupported: false));
    }

    [Fact]
    public void Stdout_And_Stderr_CanDiffer()
    {
        var env = new Dictionary<string, string?> { ["TERM"] = "xterm-256color" };

        // Same environment, different per-stream redirection -> different result.
        var stdout = Detect(env, isRedirected: true);   // e.g. piped data
        var stderr = Detect(env, isRedirected: false);  // still a terminal

        Assert.Equal(CliAnsiSupport.None, stdout);
        Assert.Equal(CliAnsiSupport.Ansi256, stderr);
    }
}
