using ItTiger.TigerCli.Enums;

namespace ItTiger.TigerCli.Primitives;


/// <summary>
/// Describes the visual style of one frame segment, with optional custom content.
/// </summary>
/// <param name="Style">The built-in segment style.</param>
/// <param name="Custom">Optional custom segment content when <paramref name="Style"/> is <see cref="CliFrameSegmentStyle.Custom"/>.</param>
public record CliFrameSegment
(
    CliFrameSegmentStyle Style,
    string? Custom = null
);
