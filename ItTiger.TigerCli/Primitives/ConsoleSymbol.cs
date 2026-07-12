namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// Shared Unicode symbols used by frames, overlays, indicators, and inline controls.
/// </summary>
public static class ConsoleSymbol
{
    // Box drawing — single line
    /// <summary>Upper-left single-line box-drawing corner.</summary>
    public const char SingleTopLeft     = '\u250C'; // ┌
    /// <summary>Upper-right single-line box-drawing corner.</summary>
    public const char SingleTopRight    = '\u2510'; // ┐
    /// <summary>Lower-left single-line box-drawing corner.</summary>
    public const char SingleBottomLeft  = '\u2514'; // └
    /// <summary>Lower-right single-line box-drawing corner.</summary>
    public const char SingleBottomRight = '\u2518'; // ┘
    /// <summary>Horizontal single-line box-drawing line.</summary>
    public const char SingleH           = '\u2500'; // ─
    /// <summary>Vertical single-line box-drawing line.</summary>
    public const char SingleV           = '\u2502'; // │
    /// <summary>Single-line box-drawing intersection.</summary>
    public const char SingleCross       = '\u253C'; // ┼

    // Box drawing — single line tees
    /// <summary>Single-line T-junction with an upward stem.</summary>
    public const char SingleTUp         = '\u2534'; // ┴
    /// <summary>Single-line T-junction with a downward stem.</summary>
    public const char SingleTDown       = '\u252C'; // ┬
    /// <summary>Single-line T-junction with a leftward stem.</summary>
    public const char SingleTLeft       = '\u2524'; // ┤
    /// <summary>Single-line T-junction with a rightward stem.</summary>
    public const char SingleTRight      = '\u251C'; // ├

    // Box drawing — double line
    /// <summary>Upper-left double-line box-drawing corner.</summary>
    public const char DoubleTopLeft     = '\u2554'; // ╔
    /// <summary>Upper-right double-line box-drawing corner.</summary>
    public const char DoubleTopRight    = '\u2557'; // ╗
    /// <summary>Lower-left double-line box-drawing corner.</summary>
    public const char DoubleBottomLeft  = '\u255A'; // ╚
    /// <summary>Lower-right double-line box-drawing corner.</summary>
    public const char DoubleBottomRight = '\u255D'; // ╝
    /// <summary>Horizontal double-line box-drawing line.</summary>
    public const char DoubleH           = '\u2550'; // ═
    /// <summary>Vertical double-line box-drawing line.</summary>
    public const char DoubleV           = '\u2551'; // ║
    /// <summary>Double-line box-drawing intersection.</summary>
    public const char DoubleCross       = '\u256C'; // ╬

    // Box drawing — double line tees
    /// <summary>Double-line T-junction with an upward stem.</summary>
    public const char DoubleTUp         = '\u2569'; // ╩
    /// <summary>Double-line T-junction with a downward stem.</summary>
    public const char DoubleTDown       = '\u2566'; // ╦
    /// <summary>Double-line T-junction with a leftward stem.</summary>
    public const char DoubleTLeft       = '\u2563'; // ╣
    /// <summary>Double-line T-junction with a rightward stem.</summary>
    public const char DoubleTRight      = '\u2560'; // ╠

    // Arrows and triangles
    /// <summary>Up arrow.</summary>
    public const char ArrowUp           = '\u2191'; // ↑
    /// <summary>Down arrow.</summary>
    public const char ArrowDown         = '\u2193'; // ↓
    /// <summary>Left arrow.</summary>
    public const char ArrowLeft         = '\u2190'; // ←
    /// <summary>Right arrow.</summary>
    public const char ArrowRight        = '\u2192'; // →
    /// <summary>Up-pointing filled triangle.</summary>
    public const char TriangleUp        = '\u25B2'; // ▲
    /// <summary>Down-pointing filled triangle.</summary>
    public const char TriangleDown      = '\u25BC'; // ▼
    /// <summary>Left-pointing filled triangle.</summary>
    public const char TriangleLeft      = '\u25C4'; // ◄
    /// <summary>Right-pointing filled triangle.</summary>
    public const char TriangleRight     = '\u25BA'; // ►

    // Small pointing triangles used as focus markers (e.g. focused buttons: [▸ Yes ◂]).
    /// <summary>Small right-pointing focus marker.</summary>
    public const char MarkerRight       = '\u25B8'; // ▸
    /// <summary>Small left-pointing focus marker.</summary>
    public const char MarkerLeft        = '\u25C2'; // ◂

    // Block and shade
    /// <summary>Light shade block.</summary>
    public const char ShadeLight        = '\u2591'; // ░
    /// <summary>Medium shade block.</summary>
    public const char ShadeMedium       = '\u2592'; // ▒
    /// <summary>Dark shade block.</summary>
    public const char ShadeDark         = '\u2593'; // ▓
    /// <summary>Full block.</summary>
    public const char FullBlock         = '\u2588'; // █
    /// <summary>Lower half block.</summary>
    public const char LowerHalfBlock    = '\u2584'; // ▄

    // Progress-bar glyph pairs (filled / track). FullBlock + ShadeLight is the default bar.
    /// <summary>Heavy horizontal box-drawing line.</summary>
    public const char HeavyHorizontal        = '━'; // ━ U+2501 BOX DRAWINGS HEAVY HORIZONTAL (Line filled; SingleH ─ is its track)
    /// <summary>Unfilled square.</summary>
    public const char WhiteSquare            = '□'; // U+25A1 WHITE SQUARE (track for Square)
    /// <summary>Filled vertical rectangle.</summary>
    public const char BlackVerticalRectangle = '▮'; // U+25AE BLACK VERTICAL RECTANGLE (filled)
    /// <summary>Unfilled vertical rectangle.</summary>
    public const char WhiteVerticalRectangle = '▯'; // U+25AF WHITE VERTICAL RECTANGLE (track)
    /// <summary>Filled parallelogram.</summary>
    public const char BlackParallelogram     = '▰'; // U+25B0 BLACK PARALLELOGRAM (filled)
    /// <summary>Unfilled parallelogram.</summary>
    public const char WhiteParallelogram     = '▱'; // U+25B1 WHITE PARALLELOGRAM (track)

    // Symbols
    // Checkmark is not working on Windows with Console.OutputEncoding set to UTF8 and default fonts.
    // public const char Checkmark         = '\u2713'; // ✓
    /// <summary>Filled square.</summary>
    public const char Square            = '\u25A0'; // ■
    /// <summary>Centered middle dot.</summary>
    public const char MiddleDot         = '\u00B7'; // ·
    // Bullet needs Console.OutputEncoding set to UTF8
    // Console.OutputEncoding = Encoding.UTF8;
    /// <summary>Bullet.</summary>
    public const char Bullet            = '\u2022'; // •
    /// <summary>Horizontal ellipsis.</summary>
    public const char Ellipsis          = '\u2026'; // …
    /// <summary>Right-pointing chevron.</summary>
    public const char ChevronRight      = '›'; // ›

}
