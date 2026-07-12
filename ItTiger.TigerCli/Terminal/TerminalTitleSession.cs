namespace ItTiger.TigerCli.Terminal;

internal sealed class TerminalTitleSession
{
    private readonly ICliRenderSink _sink;

    public TerminalTitleSession(ICliRenderSink sink, bool enabled, bool spinnerPrefixEnabled)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        Enabled = enabled;
        SpinnerPrefixEnabled = spinnerPrefixEnabled;
    }

    public string? BaseTitle { get; private set; }
    public string? SpinnerPrefix { get; private set; }
    public string? LastWrittenTitle { get; private set; }
    public bool Enabled { get; }
    public bool SpinnerPrefixEnabled { get; }

    public void SetBaseTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        BaseTitle = title;
        WriteIfChanged();
    }

    public void SetSpinnerPrefix(string? prefix)
    {
        if (!SpinnerPrefixEnabled)
            prefix = null;

        if (string.Equals(SpinnerPrefix, prefix, StringComparison.Ordinal))
            return;

        SpinnerPrefix = prefix;
        WriteIfChanged();
    }

    private void WriteIfChanged()
    {
        if (!Enabled || string.IsNullOrWhiteSpace(BaseTitle))
            return;

        var composed = string.IsNullOrEmpty(SpinnerPrefix)
            ? BaseTitle
            : $"{SpinnerPrefix} {BaseTitle}";

        if (string.Equals(composed, LastWrittenTitle, StringComparison.Ordinal))
            return;

        _sink.SetWindowTitle(composed);
        LastWrittenTitle = composed;
    }
}
