namespace ItTiger.TigerCli.Enums;

/// <summary>
/// Which orientations a predefined city table style (see <see cref="Rendering.CliTableStyles"/>)
/// is intended for. Most city styles work in either orientation (<see cref="Both"/>); a few are
/// designed for a single orientation and clamp to it regardless of the requested orientation.
/// </summary>
public enum CliTableStyleOrientationSupport
{
    /// <summary>Usable vertically (record list) or horizontally (detail view).</summary>
    Both,

    /// <summary>Designed for vertical (record-list) layout only; always resolves to vertical.</summary>
    VerticalOnly,

    /// <summary>Designed for horizontal (detail-view) layout only; always resolves to horizontal.</summary>
    HorizontalOnly
}
