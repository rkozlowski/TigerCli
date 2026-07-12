namespace ItTiger.TigerCli.Commands;

/// <summary>
/// A help-footer link configured through the <see cref="TigerCliAppBuilder"/> link methods
/// (<c>AddLink</c>, <c>AddWebsite</c>, <c>AddRepository</c>, <c>AddDocumentation</c>) or derived
/// from assembly metadata. Instances are created by the builder and exposed on
/// <see cref="TigerCliApplicationMetadata.Links"/>.
/// </summary>
public sealed class TigerCliApplicationLink
{
    internal TigerCliApplicationLink(
        string label,
        string url,
        string? labelResourceKey = null,
        string? standardKind = null)
    {
        Label = label;
        Url = url;
        LabelResourceKey = labelResourceKey;
        StandardKind = standardKind;
    }

    /// <summary>
    /// The visible help-footer label. Custom links carry app-owned text; the convenience links
    /// (website/repository/documentation) use localized framework labels.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// The visible/copyable URL, rendered with TigerCli link styling in help output.
    /// </summary>
    public string Url { get; }

    internal string? LabelResourceKey { get; }

    internal string? StandardKind { get; }
}
