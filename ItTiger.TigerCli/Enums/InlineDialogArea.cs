namespace ItTiger.TigerCli.Enums;


/// <summary>
/// Fixed layout areas where an inline dialog can place title, frame, status, widgets, and activity
/// overlays.
/// </summary>
/// <remarks>
/// Widget areas determine whether content is outside the frame, inside the frame, scrollable, or
/// indicator-aware. The hosting dialog translates these logical areas into grid placement and
/// reserves indicator or scrollbar columns where appropriate.
/// </remarks>
public enum InlineDialogArea
{
    /// <summary>Title row above the dialog content.</summary>
    Title,
    /// <summary>Above the frame, using the normal content span (no indicator columns reserved).</summary>
    AboveFrame,

    /// <summary>Above the frame, placed in the indicator-aware content column with indicator columns reserved.</summary>
    AboveFrameWithIndicators,

    /// <summary>The top frame row, including any frame-level overlays such as activity spinners.</summary>
    TopFrame,

    /// <summary>Label row inside the frame, above in-frame widget content.</summary>
    Label,

    /// <summary>Inside the frame, placed in the indicator-aware content column with indicator columns reserved.</summary>
    InFrameWithIndicators,

    /// <summary>Inside the frame, scrollable, with a vertical scrollbar over the frame border.</summary>
    InFrameScrollable,

    /// <summary>Inside the frame, using the normal content span (no indicator columns reserved).</summary>
    InFrame,

    /// <summary>The bottom frame row.</summary>
    BottomFrame,
    /// <summary>Below the frame, placed in the indicator-aware content column with indicator columns reserved.</summary>
    BelowFrameWithIndicators,

    /// <summary>Below the frame, using the normal content span (no indicator columns reserved).</summary>
    BelowFrame,

    /// <summary>Status or hint row below the dialog content.</summary>
    Status
}
