namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Decoration flags requested by inline controls, such as scroll indicators and scroll bars.
/// </summary>
[Flags]
public enum CliControlDecoration
{
    /// <summary>No decoration.</summary>
    None = 0,

    /// <summary>Low-profile left/right indicators for horizontally scrollable content.</summary>
    // Low-profile indicators (usually for single-line text or restricted space)
    HorizontalIndicators = 1 << 0, // ◄ ► (Overlays in reserved width-1 columns)
    //VerticalIndicators = 1 << 1, // ▲ ▼ (Overlays on frame or inside)

    /// <summary>Full horizontal scroll bar decoration.</summary>
    // Full scroll bars (Standard for list/grid views)
    HorizontalScrollBar = 1 << 2, // ◄█─► (Overwrites bottom frame or reserved row)

    /// <summary>Full vertical scroll bar decoration.</summary>
    VerticalScrollBar = 1 << 3, // ▲█│▼ (Overwrites right frame border)

    // Explicit UI Helpers
    //BottomHint = 1 << 4  // [ Hint Text ] (Overwrites bottom frame border)
}
