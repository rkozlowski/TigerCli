using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Testing;

namespace CommandParserTest.Tests;

/// <summary>
/// Locks the styled app-run capture contract (TigerCliAppTestHost.WithHtmlCapture): opt-in only,
/// deterministic HTML with no ANSI and no machine-dependent console colours, stdout/stderr
/// separation, and LF-only line endings — the properties the DocSamples artifacts rely on.
/// </summary>
public sealed class HtmlCaptureTests
{
    private const char Esc = (char)0x1B;

    private static TigerCliAppTestHost Host(params string[] args)
        => TigerCliAppTestHost.For(CommandParserTestApp.Create()).WithArgs(args);

    [Fact]
    public async Task DefaultRun_HtmlPropertiesAreNull_AndPlainCaptureUnchanged()
    {
        var result = await Host("echo", "-m", "plain text").RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Null(result.StdOutHtml);
        Assert.Null(result.StdErrHtml);
        // Plain capture is byte-for-byte the pre-capture behavior.
        Assert.Equal("plain text" + Environment.NewLine, result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Capture_UnstyledOutput_IsExactPlainFragment()
    {
        var result = await Host("echo", "-m", "plain text").WithHtmlCapture()
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        // Exact: no spans (no console colour leakage into unstyled text), LF-only, wrapped.
        Assert.Equal("<pre class=\"tigercli\">plain text\n</pre>", result.StdOutHtml);
        Assert.Equal("<pre class=\"tigercli\"></pre>", result.StdErrHtml);
        // TigerCli output went to the HTML capture, not the plain stream.
        Assert.Empty(result.StdOut);
    }

    [Fact]
    public async Task Capture_Help_ProducesStyledHtml_NoAnsi()
    {
        var result = await Host("--help").WithHtmlCapture().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        var html = Assert.IsType<string>(result.StdOutHtml);
        Assert.StartsWith("<pre class=\"tigercli\">", html);
        Assert.EndsWith("</pre>", html);
        Assert.Contains("parser-test", html);
        Assert.Contains("<span", html);          // help headings are styled
        Assert.False(html.Contains(Esc));        // never ANSI
        Assert.DoesNotContain("\r", html);       // LF-only
        Assert.Equal("<pre class=\"tigercli\"></pre>", result.StdErrHtml);
    }

    [Fact]
    public async Task Capture_HelpErrors_ProducesStyledHtml()
    {
        var result = await Host("--help-errors").WithHtmlCapture()
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Exit codes", result.StdOutHtml);
        Assert.False(result.StdOutHtml!.Contains(Esc));
    }

    [Fact]
    public async Task Capture_FrameworkError_GoesToStdErrHtml_NotStdOutHtml()
    {
        var result = await Host("raw", "--non-interactive").WithHtmlCapture()
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)ParserTestExitCode.ValidationError, result.ExitCode);
        Assert.Contains("Missing required option: --code", result.StdErrHtml);
        Assert.Contains("<span", result.StdErrHtml);   // the [Error] prefix is styled
        Assert.False(result.StdErrHtml!.Contains(Esc));
        Assert.Equal("<pre class=\"tigercli\"></pre>", result.StdOutHtml);
    }

    [Fact]
    public async Task Capture_RepeatedRuns_AreDeterministic()
    {
        var first = await Host("--help").WithHtmlCapture().RunAsync(TestContext.Current.CancellationToken);
        var second = await Host("--help").WithHtmlCapture().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(first.StdOutHtml, second.StdOutHtml);
        Assert.Equal(first.StdErrHtml, second.StdErrHtml);
    }

    [Fact]
    public async Task Capture_WrapInPreFalse_ReturnsInnerFragment()
    {
        var result = await Host("echo", "-m", "plain text")
            .WithHtmlCapture(new HtmlSinkOptions { WrapInPre = false })
            .RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal("plain text\n", result.StdOutHtml);
        Assert.Equal(string.Empty, result.StdErrHtml);
    }
}
