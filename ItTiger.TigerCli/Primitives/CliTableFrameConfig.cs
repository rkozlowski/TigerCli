using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Frame and separator configuration used by <see cref="Rendering.CliTable"/> when building its grid.
/// </summary>
public class CliTableFrameConfig
{
    /// <summary>How crossing frame segments are joined.</summary>
    public CliFrameJoinStyle JoinStyle { get; set; } = CliFrameJoinStyle.SimplifiedCompatible;

    /// <summary>Outer table frame segment style.</summary>
    public CliFrameSegment OuterFrame { get; set; } = new(CliFrameSegmentStyle.DoubleFrame);

    /// <summary>Separator after the header band.</summary>
    public CliFrameSegment AfterHeader { get; set; } = new(CliFrameSegmentStyle.SingleFrame);

    /// <summary>Reserved footer separator configuration.</summary>
    public CliFrameSegment BeforeFooter { get; set; } = new(CliFrameSegmentStyle.SingleFrame);

    /// <summary>Separator between records.</summary>
    public CliFrameSegment BetweenRecords { get; set; } = new(CliFrameSegmentStyle.None);

    /// <summary>Separator between elements: columns in vertical tables, rows in horizontal tables.</summary>
    public CliFrameSegment BetweenElements { get; set; } = new(CliFrameSegmentStyle.SingleFrame);

    /// <summary>Optional character style applied to frame glyphs.</summary>
    public CliCharStyle? CharStyle { get; set; }
}
