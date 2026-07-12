using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Primitives;

/// <summary>
/// A delegate that produces the renderable content for a <see cref="CliOverlay"/>.
/// </summary>
/// <param name="grid">The fully measured grid.</param>
/// <param name="renderLength">
/// The number of screen cells occupied by the overlay: the sum of the measured widths (horizontal)
/// or heights (vertical) of the <see cref="CliOverlay.LogicalLength"/> covered grid cells. Equal to
/// <see cref="CliOverlay.LogicalLength"/> only when every covered cell measures one screen cell.
/// </param>
/// <returns>
/// <c>visible</c>: when <c>false</c> the overlay is skipped and underlying content is preserved.
/// <c>content</c>: characters to write; <c>content.Length</c> must be &lt;= <paramref name="renderLength"/>
/// when <c>visible</c> is <c>true</c>. Every character is written with the overlay's uniform
/// <see cref="CliOverlay.Style"/>.
/// </returns>
public delegate (bool visible, char[] content) CliOverlayRenderer(CliGrid grid, int renderLength);

/// <summary>
/// A single character emitted by a <see cref="CliStyledOverlayRenderer"/>, optionally carrying its own
/// per-character style. When <see cref="Style"/> is <c>null</c> the glyph falls back to the overlay's
/// base <see cref="CliOverlay.Style"/>, so a styled renderer only needs to specify a style where it
/// differs from the base.
/// </summary>
public readonly struct CliOverlayGlyph
{
    /// <summary>The character to write.</summary>
    public char Char { get; }

    /// <summary>The per-character style, or <c>null</c> to use the overlay's base style.</summary>
    public CliCharStyle? Style { get; }

    /// <summary>Creates an overlay glyph with an optional per-character style.</summary>
    /// <param name="character">The character to write.</param>
    /// <param name="style">The character style, or <c>null</c> to use the overlay's base style.</param>
    public CliOverlayGlyph(char character, CliCharStyle? style = null)
    {
        Char = character;
        Style = style;
    }
}

/// <summary>
/// A delegate that produces styled per-character content for a <see cref="CliOverlay"/>. This is the
/// richer counterpart to <see cref="CliOverlayRenderer"/>: it lets a single overlay emit characters with
/// different styles (for example a two- or three-colour progress bar) while still owning no placement or
/// measurement. A glyph whose <see cref="CliOverlayGlyph.Style"/> is <c>null</c> uses the overlay's base
/// <see cref="CliOverlay.Style"/>.
/// </summary>
/// <param name="grid">The fully measured grid.</param>
/// <param name="renderLength">
/// The number of screen cells occupied by the overlay (measured, like
/// <see cref="CliOverlayRenderer"/>'s — not the grid-cell <see cref="CliOverlay.LogicalLength"/>).
/// </param>
/// <returns>
/// <c>visible</c>: when <c>false</c> the overlay is skipped and underlying content is preserved.
/// <c>content</c>: glyphs to write; <c>content.Length</c> must be &lt;= <paramref name="renderLength"/>
/// when <c>visible</c> is <c>true</c>.
/// </returns>
public delegate (bool visible, CliOverlayGlyph[] content) CliStyledOverlayRenderer(CliGrid grid, int renderLength);

/// <summary>
/// A post-layout, one-dimensional strip that can overwrite measured cells in a
/// <see cref="CliGrid"/> after the standard measurement pass.
/// </summary>
public class CliOverlay
{
    /// <summary>Grid cell at which the overlay begins (column, row).</summary>
    public CliPoint Start { get; }

    /// <summary>
    /// Whether the overlay runs along rows (<c>Vertical</c>, shape 1 × m)
    /// or along columns (<c>Horizontal</c>, shape n × 1).
    /// </summary>
    public CliOrientation Orientation { get; }

    /// <summary>Number of grid cells covered by the overlay.</summary>
    public int LogicalLength { get; }

    /// <summary>
    /// Base character style applied to every character the overlay writes that does not carry its own
    /// per-character style. For a plain <see cref="CliOverlayRenderer"/> this is the uniform style of the
    /// whole overlay; for a <see cref="CliStyledOverlayRenderer"/> it is the fallback used wherever a
    /// glyph's <see cref="CliOverlayGlyph.Style"/> is <c>null</c>.
    /// </summary>
    public CliCharStyle Style { get; }

    /// <summary>
    /// Delegate that produces the actual characters to render, or <c>null</c> when the overlay was created
    /// from a <see cref="CliStyledOverlayRenderer"/>. Application always goes through the unified
    /// <see cref="StyledRenderer"/>; this property is retained for source compatibility.
    /// </summary>
    public CliOverlayRenderer? Renderer { get; }

    /// <summary>
    /// The unified styled renderer used to apply the overlay. A plain <see cref="CliOverlayRenderer"/> is
    /// adapted into this form, with each character producing a glyph that carries no style (so it falls
    /// back to <see cref="Style"/>). This keeps overlay application on a single code path.
    /// </summary>
    internal CliStyledOverlayRenderer StyledRenderer { get; }

    /// <summary>Creates an overlay whose renderer supplies uniformly styled characters.</summary>
    /// <param name="start">The starting grid coordinate in column, row order.</param>
    /// <param name="orientation">The direction in which the overlay extends.</param>
    /// <param name="logicalLength">The number of grid cells covered by the overlay.</param>
    /// <param name="style">The style applied to rendered characters.</param>
    /// <param name="renderer">The delegate that supplies the overlay characters.</param>
    public CliOverlay(
        CliPoint start,
        CliOrientation orientation,
        int logicalLength,
        CliCharStyle style,
        CliOverlayRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (logicalLength < 1)
            throw new ArgumentOutOfRangeException(nameof(logicalLength), "LogicalLength must be at least 1.");

        Start = start;
        Orientation = orientation;
        LogicalLength = logicalLength;
        Style = style;
        Renderer = renderer;
        StyledRenderer = Adapt(renderer);
    }

    /// <summary>Creates an overlay whose renderer can style individual characters.</summary>
    /// <param name="start">The starting grid coordinate in column, row order.</param>
    /// <param name="orientation">The direction in which the overlay extends.</param>
    /// <param name="logicalLength">The number of grid cells covered by the overlay.</param>
    /// <param name="style">The base style used by glyphs without a per-character style.</param>
    /// <param name="renderer">The delegate that supplies the styled overlay glyphs.</param>
    public CliOverlay(
        CliPoint start,
        CliOrientation orientation,
        int logicalLength,
        CliCharStyle style,
        CliStyledOverlayRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (logicalLength < 1)
            throw new ArgumentOutOfRangeException(nameof(logicalLength), "LogicalLength must be at least 1.");

        Start = start;
        Orientation = orientation;
        LogicalLength = logicalLength;
        Style = style;
        Renderer = null;
        StyledRenderer = renderer;
    }

    // Adapts a plain character renderer into the unified styled form: each character becomes a glyph with
    // no per-character style, so application resolves it to the overlay's base Style — preserving the
    // exact behaviour of single-style overlays.
    private static CliStyledOverlayRenderer Adapt(CliOverlayRenderer renderer) =>
        (grid, renderLength) =>
        {
            var (visible, content) = renderer(grid, renderLength);
            if (!visible || content is null || content.Length == 0)
                return (visible, Array.Empty<CliOverlayGlyph>());

            var glyphs = new CliOverlayGlyph[content.Length];
            for (int i = 0; i < content.Length; i++)
                glyphs[i] = new CliOverlayGlyph(content[i]);
            return (true, glyphs);
        };
}
