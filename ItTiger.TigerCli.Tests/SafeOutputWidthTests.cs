using System;
using System.IO;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the redirection-safe terminal-dimension policy in <see cref="TerminalCapabilities"/>.
/// <see cref="Console.WindowWidth"/>/<see cref="Console.WindowHeight"/> are a terminal capability,
/// not a guaranteed value: under redirected/captured/headless output they can throw ("The handle is
/// invalid.") or report nonsense. The pure resolve cores take redirection state and the raw reader as
/// parameters, so these never depend on the host console.
/// </summary>
public sealed class SafeOutputWidthTests
{
    // ---- width ----

    [Fact]
    public void ResolveWidth_Redirected_ReturnsDefault()
    {
        // Reader is never consulted when redirected.
        Assert.Equal(
            TerminalCapabilities.DefaultOutputWidth,
            TerminalCapabilities.ResolveWidth(isRedirected: true, () => 200));
    }

    [Fact]
    public void ResolveWidth_ReaderThrowsHandleInvalid_ReturnsDefault()
    {
        // "The handle is invalid." surfaces as an IOException from Console.WindowWidth.
        Assert.Equal(
            TerminalCapabilities.DefaultOutputWidth,
            TerminalCapabilities.ResolveWidth(isRedirected: false,
                () => throw new IOException("The handle is invalid.")));
    }

    [Fact]
    public void ResolveWidth_ReaderThrowsPlatformNotSupported_ReturnsDefault()
    {
        Assert.Equal(
            TerminalCapabilities.DefaultOutputWidth,
            TerminalCapabilities.ResolveWidth(isRedirected: false,
                () => throw new PlatformNotSupportedException()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ResolveWidth_NonPositive_ReturnsDefault(int reported)
    {
        Assert.Equal(
            TerminalCapabilities.DefaultOutputWidth,
            TerminalCapabilities.ResolveWidth(isRedirected: false, () => reported));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    [InlineData(200)]
    public void ResolveWidth_RealPositiveWidth_ReturnsIt(int reported)
    {
        Assert.Equal(reported, TerminalCapabilities.ResolveWidth(isRedirected: false, () => reported));
    }

    [Fact]
    public void DefaultOutputWidth_Is120()
    {
        Assert.Equal(120, TerminalCapabilities.DefaultOutputWidth);
    }

    [Fact]
    public void GetSafeOutputWidth_NeverThrows_AndIsAtLeastOne()
    {
        // Exercises the real console path on the test host (commonly redirected under a test runner).
        var width = TerminalCapabilities.GetSafeOutputWidth();
        Assert.True(width >= 1);
    }

    // ---- height (null == "no soft bound", so output is not clamped under redirection) ----

    [Fact]
    public void ResolveHeight_Redirected_ReturnsNull()
    {
        Assert.Null(TerminalCapabilities.ResolveHeight(isRedirected: true, () => 50));
    }

    [Fact]
    public void ResolveHeight_ReaderThrows_ReturnsNull()
    {
        Assert.Null(TerminalCapabilities.ResolveHeight(isRedirected: false,
            () => throw new IOException("The handle is invalid.")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ResolveHeight_NonPositive_ReturnsNull(int reported)
    {
        Assert.Null(TerminalCapabilities.ResolveHeight(isRedirected: false, () => reported));
    }

    [Fact]
    public void ResolveHeight_RealPositiveHeight_ReturnsIt()
    {
        Assert.Equal(42, TerminalCapabilities.ResolveHeight(isRedirected: false, () => 42));
    }
}
