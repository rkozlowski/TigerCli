namespace ItTiger.TigerCli.Enums;


/// <summary>
/// Selects which axes of a hosted subgrid cell are scrollable.
/// </summary>
[Flags]
public enum CliScrollMode
{
    /// <summary>No scrolling.</summary>
    None       = 0,
    /// <summary>Horizontal scrolling.</summary>
    Horizontal = 1,                      // H
    /// <summary>Vertical scrolling.</summary>
    Vertical   = 2,                      // V
    /// <summary>Horizontal and vertical scrolling.</summary>
    Both       = Horizontal | Vertical   // B
}
