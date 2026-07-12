namespace ItTiger.TigerCli.Testing;

/// <summary>
/// Result of a <see cref="TigerCliAppTestHost"/> run: the process exit code the app would have
/// returned, plus the text captured from <see cref="Console.Out"/> and <see cref="Console.Error"/>
/// during the run. Captured text is colour-free (the host pins a no-colour mode for the run).
/// </summary>
/// <param name="ExitCode">The app exit code of the run.</param>
/// <param name="StdOut">The text captured from <see cref="Console.Out"/>.</param>
/// <param name="StdErr">The text captured from <see cref="Console.Error"/>.</param>
public sealed record TigerCliAppRunResult(
    int ExitCode,
    string StdOut,
    string StdErr)
{
    /// <summary>
    /// Standard output captured as deterministic HTML (via <c>HtmlSink</c>), or <c>null</c> when the
    /// run was not started with <c>TigerCliAppTestHost.WithHtmlCapture</c>. When capture is enabled,
    /// TigerCli-rendered output goes to the HTML capture instead of <see cref="StdOut"/>.
    /// Line endings are normalized to <c>\n</c>.
    /// </summary>
    public string? StdOutHtml { get; init; }

    /// <summary>
    /// Standard error captured as deterministic HTML, or <c>null</c> when HTML capture is off.
    /// See <see cref="StdOutHtml"/> for the capture semantics.
    /// </summary>
    public string? StdErrHtml { get; init; }
}
