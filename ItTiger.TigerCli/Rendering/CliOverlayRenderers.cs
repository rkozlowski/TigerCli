using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using Microsoft.Extensions.Logging;

namespace ItTiger.TigerCli.Rendering;

/// <summary>
/// Which edge a horizontal scroll indicator sits on.
/// </summary>
public enum CliOverlayEdge
{
    /// <summary>The left edge.</summary>
    Left,
    /// <summary>The right edge.</summary>
    Right,
}

/// <summary>
/// Reusable <see cref="CliOverlayRenderer"/> factories for the common one-dimensional overlays
/// (vertical scrollbar, horizontal scroll indicators, time-/state-driven text such as a spinner or
/// clock). The renderers read only the already-measured <see cref="CliGrid"/> (scroll info) or a
/// caller-supplied content provider; they own no placement, measurement, or style. Callers (e.g.
/// <c>InlineDialog</c>) keep ownership of where each overlay is placed and what style it uses, and add
/// the resulting <see cref="CliOverlay"/> through <see cref="CliGrid.AddOverlay"/> as before.
/// </summary>
public static class CliOverlayRenderers
{
    /// <summary>
    /// A vertical scrollbar renderer: up/down arrows at the ends and a proportional thumb on the track,
    /// driven by the grid's active vertical scroll info. Renders nothing when there is no visible
    /// vertical scroll region or the strip is too short to hold both arrows.
    /// </summary>
    public static CliOverlayRenderer VerticalScrollBar() => RenderVerticalScrollBar;

    /// <summary>
    /// A single-cell horizontal scroll indicator for the given <paramref name="edge"/>, driven by the
    /// grid's active horizontal scroll info. The left indicator shows only when scrolled away from the
    /// start; the right indicator shows only when more content remains to the right.
    /// </summary>
    public static CliOverlayRenderer HorizontalIndicator(CliOverlayEdge edge) =>
        edge == CliOverlayEdge.Left ? RenderLeftIndicator : RenderRightIndicator;

    /// <summary>
    /// A generic text overlay driven by a content provider, suitable for spinners, clocks, or any small
    /// dynamic indicator. The provider is read on every render: a <c>null</c>/empty result, or content
    /// that would overflow the reserved length, renders nothing (leaving the underlying cells intact);
    /// otherwise the content is written. This is the shared mechanism behind activity/spinner overlays.
    /// </summary>
    public static CliOverlayRenderer DynamicText(Func<string?> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return (_, renderLength) =>
        {
            var content = provider();
            if (string.IsNullOrEmpty(content) || content.Length > renderLength)
                return (false, Array.Empty<char>());

            return (true, content.ToCharArray());
        };
    }

    /// <summary>
    /// A horizontal progress-bar renderer. The leading <c>fraction</c> of the bar uses
    /// <paramref name="filled"/>, the remainder <paramref name="track"/>. When <paramref name="leftCap"/>
    /// and/or <paramref name="rightCap"/> are supplied (e.g. <c>'['</c>/<c>']'</c>), those glyphs occupy
    /// the end cell(s) and the bar fills the interior; caps are dropped when the strip is too short to also
    /// hold at least one interior cell, so a tiny bar still fills. The fraction is read on every render from
    /// <paramref name="fractionProvider"/> and clamped to [0, 1]. Because overlays receive the post-layout
    /// <c>renderLength</c>, placing the bar's column under <see cref="Enums.CliColumnSizing.Star"/> lets
    /// <see cref="CliGrid"/> own the width — no local measurement is needed. The progress <em>value</em>
    /// calculation (current/max, clamping) belongs to the caller and is supplied through the provider.
    /// </summary>
    public static CliOverlayRenderer ProgressBar(
        Func<double> fractionProvider,
        char filled = ConsoleSymbol.FullBlock,
        char track = ConsoleSymbol.ShadeLight,
        char? leftCap = null,
        char? rightCap = null)
    {
        ArgumentNullException.ThrowIfNull(fractionProvider);

        return (_, renderLength) =>
        {
            if (renderLength < 1)
                return (false, []);

            double fraction = fractionProvider();
            if (double.IsNaN(fraction) || fraction < 0) fraction = 0;
            if (fraction > 1) fraction = 1;

            // End caps reserve the outer cell(s); the bar fills the interior. If the strip cannot hold the
            // caps plus at least one interior cell, drop the caps so the whole width still fills.
            int left = leftCap.HasValue ? 1 : 0;
            int right = rightCap.HasValue ? 1 : 0;
            if (renderLength < left + right + 1)
                left = right = 0;

            int inner = renderLength - left - right;
            int fill = (int)Math.Round(fraction * inner, MidpointRounding.AwayFromZero);
            if (fill < 0) fill = 0;
            if (fill > inner) fill = inner;

            var chars = new char[renderLength];
            if (left == 1) chars[0] = leftCap!.Value;
            if (right == 1) chars[renderLength - 1] = rightCap!.Value;
            for (int i = 0; i < inner; i++)
                chars[left + i] = i < fill ? filled : track;

            return (true, chars);
        };
    }

    /// <summary>
    /// A horizontal multi-style progress-bar renderer, the styled counterpart to
    /// <see cref="ProgressBar(Func{double},char,char,char?,char?)"/>. The leading <c>fraction</c> of the
    /// interior is drawn with <paramref name="done"/> and the remainder with <paramref name="track"/>; when
    /// the fraction reaches <c>1.0</c> (100%) and <paramref name="completed"/> is supplied, the whole filled
    /// interior is drawn with <paramref name="completed"/> instead of <paramref name="done"/> — a
    /// "completed state". Below 100% <paramref name="completed"/> is never used, so a bar that merely rounds
    /// up to a visually full interior (e.g. 0.99 on a tiny strip) is <em>not</em> treated as complete.
    /// <para>
    /// Each segment is a <see cref="CliOverlayGlyph"/> (glyph + optional style); a segment whose
    /// <see cref="CliOverlayGlyph.Style"/> is <c>null</c> falls back to the overlay's base
    /// <see cref="CliOverlay.Style"/>, exactly like any styled overlay. End caps
    /// (<paramref name="leftCap"/>/<paramref name="rightCap"/>) behave as in the single-style factory: they
    /// occupy the outer cell(s) and are dropped when the strip cannot also hold at least one interior cell;
    /// caps carry no per-glyph style, so they use the overlay base style. The fraction is read on every
    /// render and clamped to [0, 1]; the progress <em>value</em> calculation belongs to the caller's
    /// provider. Styles are pre-resolved <see cref="CliCharStyle"/> values — this renderer stays
    /// theme-agnostic.
    /// </para>
    /// </summary>
    public static CliStyledOverlayRenderer ProgressBar(
        Func<double> fractionProvider,
        CliOverlayGlyph done,
        CliOverlayGlyph track,
        CliOverlayGlyph? completed = null,
        char? leftCap = null,
        char? rightCap = null)
    {
        ArgumentNullException.ThrowIfNull(fractionProvider);

        return (_, renderLength) =>
        {
            if (renderLength < 1)
                return (false, Array.Empty<CliOverlayGlyph>());

            double fraction = fractionProvider();
            if (double.IsNaN(fraction) || fraction < 0) fraction = 0;
            if (fraction > 1) fraction = 1;

            // Completed state is keyed on the value reaching exactly 100%, not on the rounded fill — so a
            // bar that only rounds up to a full interior is still rendered with `done`, not `completed`.
            bool isComplete = fraction >= 1.0;

            // End caps reserve the outer cell(s); the bar fills the interior. If the strip cannot hold the
            // caps plus at least one interior cell, drop the caps so the whole width still fills.
            int left = leftCap.HasValue ? 1 : 0;
            int right = rightCap.HasValue ? 1 : 0;
            if (renderLength < left + right + 1)
                left = right = 0;

            int inner = renderLength - left - right;
            int fill = (int)Math.Round(fraction * inner, MidpointRounding.AwayFromZero);
            if (fill < 0) fill = 0;
            if (fill > inner) fill = inner;

            // The filled segment is `completed` only at 100% (and only when supplied); otherwise `done`.
            CliOverlayGlyph filledSegment = isComplete && completed.HasValue ? completed.Value : done;

            var glyphs = new CliOverlayGlyph[renderLength];
            if (left == 1) glyphs[0] = new CliOverlayGlyph(leftCap!.Value);
            if (right == 1) glyphs[renderLength - 1] = new CliOverlayGlyph(rightCap!.Value);
            for (int i = 0; i < inner; i++)
                glyphs[left + i] = i < fill ? filledSegment : track;

            return (true, glyphs);
        };
    }

    private static (bool visible, char[] content) RenderLeftIndicator(CliGrid grid, int length)
    {
        var info = grid.GetHorizontalScrollInfo();
        if (info == null || !info.Value.visible || length < 1)
            return (false, []);

        var (_, offset, _, _, _) = info.Value;
        if (offset <= 0)
            return (false, []);

        char[] chars = new char[1];
        chars[0] = ConsoleSymbol.TriangleLeft;

        return (true, chars);
    }

    private static (bool visible, char[] content) RenderRightIndicator(CliGrid grid, int length)
    {
        var info = grid.GetHorizontalScrollInfo();
        if (info == null || !info.Value.visible || length < 1)
            return (false, []);

        var (_, offset, _, _, maxOffset) = info.Value;
        if (offset >= maxOffset)
            return (false, []);

        char[] chars = new char[1];
        chars[0] = ConsoleSymbol.TriangleRight;

        return (true, chars);
    }

    private static (bool visible, char[] content) RenderVerticalScrollBar(CliGrid grid, int length)
    {
        var info = grid.GetVerticalScrollInfo();
        if (info == null || !info.Value.visible || length <= 2)
            return (false, []);

        var (_, offset, viewport, total, maxOffset) = info.Value;

        char[] chars = new char[length];
        chars[0] = ConsoleSymbol.TriangleUp;
        chars[length - 1] = ConsoleSymbol.TriangleDown;

        int trackLength = length - 2;
        if (trackLength > 0)
        {
            // Thumb size always reflects the visible fraction (viewport / total).
            int thumbSize = trackLength * viewport / total;
            if (thumbSize < 1) thumbSize = 1;
            if (thumbSize > trackLength) thumbSize = trackLength;

            int maxThumbPos = trackLength - thumbSize;
            int thumbPos = maxOffset <= 0
                ? 0
                : (int)Math.Round((double)offset * maxThumbPos / maxOffset);
            if (thumbPos < 0) thumbPos = 0;
            if (thumbPos > maxThumbPos) thumbPos = maxThumbPos;

            TigerConsole.Logger?.LogTrace("[RenderScrollBar] TrackLength: {TrackLength}, ThumbSize: {ThumbSize}, ThumbPos: {ThumbPos}",
                trackLength, thumbSize, thumbPos);

            for (int i = 0; i < trackLength; i++)
            {
                if (i >= thumbPos && i < thumbPos + thumbSize)
                    chars[i + 1] = ConsoleSymbol.FullBlock;
                else
                    chars[i + 1] = ConsoleSymbol.SingleV;
            }
        }

        return (true, chars);
    }
}
