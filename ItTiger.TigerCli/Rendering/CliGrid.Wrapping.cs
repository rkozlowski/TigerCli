using System.Text;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Rendering;

public partial class CliGrid
{

    // Render-relevant style identity (foreground, background, decorations, hyperlink target). Used to
    // decide whether adjacent token segments may share one run; comparing a subset would drop a
    // difference such as bold/underline or a link target when segments are coalesced.
    private static bool StyleEquals(in CliCharStyle a, in CliCharStyle b)
        => a.HasSameRenderingAs(b);
    // ---- Entry from Measure -----------------------------------------------------------
    // SOFT-MAX semantics: softMaxWidth/softMaxHeight are CEILINGS (try not to exceed).
    // We never "grow to fill" a soft max; we only try to shrink when we exceed it.
    internal void ApplyWrappingAndResizing(
        MeasuredCell[,] output,
        int[] columnWidths,
        int[] rowHeights,
        int? softMaxWidth,
        int? softMaxHeight,
        int? maxWidth = null,
        int? maxHeight = null)
    {
        // Strict caps from component Width/Height (hard maximums)
        int? strictW = this.Width;
        int? strictH = this.Height;

        // Incorporate hard max limits (must not be exceeded if provided)
        if (maxWidth.HasValue)
            strictW = strictW.HasValue ? Math.Min(strictW.Value, maxWidth.Value) : maxWidth;
        if (maxHeight.HasValue)
            strictH = strictH.HasValue ? Math.Min(strictH.Value, maxHeight.Value) : maxHeight;

        // Attempts (A ladder): keep width higher priority by relaxing height first.
        var attempts = new (int? WCeil, int? HCeil)[]
        {
            (softMaxWidth, softMaxHeight),   // A1: both ceilings in play
            (softMaxWidth, null),            // A2: relax height ceiling
            (null, null)                     // A3: fully content-driven (no soft ceilings)
        };

        // Clamp ceilings to strict caps if present
        for (int ai = 0; ai < attempts.Length; ai++)
        {
            var (w, h) = attempts[ai];
            if (strictW.HasValue) w = Math.Min(w ?? strictW.Value, strictW.Value);
            if (strictH.HasValue) h = Math.Min(h ?? strictH.Value, strictH.Value);
            attempts[ai] = (w, h);
        }

        // Column hard bounds from styles/locks
        var colMin = new int[ColumnCount];
        var colMax = new int[ColumnCount];
        for (int c = 0; c < ColumnCount; c++)
        {
            var s = columns[c]?.Style;
            colMin[c] = Math.Max(1, s?.EffectiveMinWidth ?? 0);
            colMax[c] = s?.EffectiveMaxWidth ?? int.MaxValue;
            if (columns[c]?.IsWidthLocked == true && s?.Width is int wLock)
            {
                colMin[c] = Math.Max(colMin[c], wLock);
                colMax[c] = Math.Min(colMax[c], wLock);
            }
        }

        // Row hard bounds from styles/locks
        var rowMin = new int[RowCount];
        var rowMax = new int[RowCount];
        for (int r = 0; r < RowCount; r++)
        {
            var s = rows[r]?.Style;
            rowMin[r] = Math.Max(1, s?.EffectiveMinHeight ?? 0);
            rowMax[r] = s?.EffectiveMaxHeight ?? int.MaxValue;
            if (rows[r]?.IsHeightLocked == true && s?.Height is int hLock)
            {
                rowMin[r] = Math.Max(rowMin[r], hLock);
                rowMax[r] = Math.Min(rowMax[r], hLock);
            }
        }

        // Wrapping ladder (B)
        var wrapModes = new (CliWrapMode Mode, bool Truncate)[]
        {
            (CliWrapMode.WordWrap,   false),
            (CliWrapMode.SymbolWrap, false),
            (CliWrapMode.CharWrap,   false),
            (CliWrapMode.CharWrap,   true)
        };

        // Attempt loop
        for (int ai = 0; ai < attempts.Length; ai++)
        {
            var (wCeil, hCeil) = attempts[ai];

            // Fresh start from initial lines each attempt
            ResetAllLinesToInitial(output);

            // Working copies of widths/heights (start from what InitializeMeasuredCells produced)
            var workColW = (int[])columnWidths.Clone();
            var workRowH = (int[])rowHeights.Clone();

            bool success = false;

            for (int bi = 0; bi < wrapModes.Length && !success; bi++)
            {
                var (mode, truncAllowedAtB) = wrapModes[bi];

                // Reset widths and lines for each B-ladder step to avoid carrying over
                // distorted widths from a failed prior step.
                Array.Copy(columnWidths, workColW, ColumnCount);
                ResetAllLinesToInitial(output);

                // 1) Columns: bring each col up to its min, clamp to max; DO NOT grow to meet any ceiling.
                for (int c = 0; c < ColumnCount; c++)
                {
                    workColW[c] = Math.Max(workColW[c], colMin[c]);
                    workColW[c] = Math.Min(workColW[c], colMax[c]);
                }

                // Spanned cells whose natural width exceeds the sum of spanned columns widen
                // those columns (Auto first, Star as fallback). Runs before shrink so ceilings
                // still get the final say.
                EnsureSpanWidthContribution(output, workColW, colMin, colMax);

                // If we exceed a ceiling (soft or strict), try to shrink; otherwise leave as-is.
                ShrinkColumnsToCeilingIfNeeded(output, workColW, colMin, colMax, wCeil, mode, truncAllowedAtB);

                // 2) Rewrap into current column widths (width-first). Apply truncation only if (a) B4 or (b) hard caps force it.
                bool widthOk = RewrapAllCellsToCurrentWidths(output, workColW, workRowH, mode, allowTruncation: truncAllowedAtB);
                if (!widthOk)
                {
                    // Could not satisfy width for this B step
                    continue;
                }

                // Verify the total width is within the ceiling after shrink + rewrap.
                // ShrinkColumnsToCeilingIfNeeded may not have been able to reduce enough
                // at this B-level (e.g., SingleLine cells with high floors when truncation
                // is not yet available). Escalate to the next B step in that case.
                if (wCeil.HasValue && workColW.Sum() > wCeil.Value)
                {
                    continue;
                }

                // 3) Rows: recompute heights from wrapped content, clamp to row bounds; DO NOT grow to fill a ceiling.
                RecomputeRowHeights(output, workRowH);
                for (int r = 0; r < RowCount; r++)
                {
                    workRowH[r] = Math.Max(workRowH[r], rowMin[r]);
                    workRowH[r] = Math.Min(workRowH[r], rowMax[r]);
                }

                // If we exceed the height ceiling, try to reduce (future: trunc). For now we just signal failure and escalate.
                if (hCeil.HasValue)
                {
                    int sumH = 0; for (int r = 0; r < RowCount; r++) sumH += workRowH[r];
                    if (sumH > hCeil.Value)
                    {
                        continue;
                    }
                }

                success = true;
            }

            if (success)
            {
                // Scroll-aware grow: rows/columns that host scrolling subgrid cells expand
                // to fill remaining space within the original soft ceiling. Capped by colMax/rowMax.
                GrowScrollingColumns(workColW, colMin, colMax, softMaxWidth);
                GrowStarColumns(workColW, colMax, softMaxWidth);
                GrowScrollingRows(workRowH, rowMax, softMaxHeight, output);

                Array.Copy(workColW, columnWidths, ColumnCount);
                Array.Copy(workRowH, rowHeights, RowCount);
                return;
            }
        }

        // If none succeed, keep the initial sizes but still enforce hard limits.
        if (strictW.HasValue || strictH.HasValue)
        {
            if (strictW.HasValue)
            {
                int sumW = columnWidths.Sum();
                if (sumW > strictW.Value)
                {
                    // Best-effort: shrink proportionally to fit the hard width limit
                    var stats = ComputeColumnStats(output, CliWrapMode.CharWrap, true);
                    var colMinFallback = new int[ColumnCount];
                    var colMaxFallback = new int[ColumnCount];
                    for (int c = 0; c < ColumnCount; c++)
                    {
                        var s = columns[c]?.Style;
                        colMinFallback[c] = Math.Max(1, s?.EffectiveMinWidth ?? 0);
                        colMaxFallback[c] = s?.EffectiveMaxWidth ?? int.MaxValue;
                        if (columns[c]?.IsWidthLocked == true && s?.Width is int wLock)
                        {
                            colMinFallback[c] = Math.Max(colMinFallback[c], wLock);
                            colMaxFallback[c] = Math.Min(colMaxFallback[c], wLock);
                        }
                    }
                    ShrinkColumnsToCeilingIfNeeded(output, columnWidths, colMinFallback, colMaxFallback, strictW, CliWrapMode.CharWrap, true);
                    RewrapAllCellsToCurrentWidths(output, columnWidths, rowHeights, CliWrapMode.CharWrap, allowTruncation: true);
                }
            }
            if (strictH.HasValue)
            {
                RecomputeRowHeights(output, rowHeights);
                for (int r = 0; r < RowCount; r++)
                {
                    var s = rows[r]?.Style;
                    int rMin = s?.EffectiveMinHeight ?? 0;
                    int rMax = s?.EffectiveMaxHeight ?? int.MaxValue;
                    if (rows[r]?.IsHeightLocked == true && s?.Height is int hLock)
                    {
                        rMin = Math.Max(rMin, hLock);
                        rMax = Math.Min(rMax, hLock);
                    }
                    rowHeights[r] = Math.Max(rowHeights[r], Math.Max(1, rMin));
                    rowHeights[r] = Math.Min(rowHeights[r], rMax);
                }
            }
        }
    }

    // ---- Helpers ----------------------------------------------------------------------

    private static void ResetAllLinesToInitial(MeasuredCell[,] output)
    {
        int rows = output.GetLength(0);
        int cols = output.GetLength(1);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var mc = output[r, c];
                if (mc == null) continue;
                if (mc.IsCovered) continue;
                // Subgrid cells are re-measured later in RewrapAllCellsToCurrentWidths, but their
                // width is read *before* that — by EnsureSpanWidthContribution — to size the spanned
                // columns. Reset them to InitialLines (the natural, content-driven measure) too, so
                // every ladder attempt/step sizes spans from the true natural width rather than from
                // a width left over from a previous (possibly shrunk) remeasure.
                mc.UpdateLines(MeasuredCell.CloneLines(mc.InitialLines));
            }
        }
    }

    private struct ColumnStats
    {
        public int TotalChars;
        public int Breakpoints;
        public int LongestToken;
        public int MinTruncationWidth; // indicatorLen + 1 for truncatable cells
    }

    private ColumnStats[] ComputeColumnStats(MeasuredCell[,] output, CliWrapMode ladderMode, bool ladderTruncation)
    {
        var stats = new ColumnStats[ColumnCount];
        for (int r = 0; r < RowCount; r++)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                var cell = output[r, c];
                if (cell == null || cell.IsCovered) continue;
                if (cell.Sink != null) continue; // subgrid: content is opaque, not wrappable text
                if (cell.ColSpan != 1) continue; // spans ignored in stats for now

                var cellMode = cell.Style.Wrapping?.Mode ?? CliWrapMode.WordWrap;

                // For wrappable cells, use the more permissive (higher enum → more
                // breakpoints → shorter tokens → lower floor) of the B-ladder mode
                // and the cell's own mode.
                // SingleLine/Multiline can't wrap — use their actual mode so that
                // the floor reflects the true content length.
                // Exception: when truncation is available at this B-level AND the cell
                // opts in, treat it as CharWrap for tokenization so the floor drops.
                CliWrapMode tokenMode;
                if (cellMode <= CliWrapMode.Multiline)
                    tokenMode = (ladderTruncation && cell.Style.Wrapping?.AllowTruncation == true)
                        ? CliWrapMode.CharWrap
                        : cellMode;
                else
                    tokenMode = (CliWrapMode)Math.Max((int)ladderMode, (int)cellMode);

                foreach (var line in cell.InitialLines)
                {
                    stats[c].TotalChars += CliTextSegment.Length(line);
                    Tokenize(line, tokenMode, out int lt, out int bps);
                    stats[c].LongestToken = Math.Max(stats[c].LongestToken, lt);
                    stats[c].Breakpoints += bps;
                }

                // Safety floor for truncatable cells: ensure at least 1 char + indicator
                // survives if truncation is eventually applied (at B3).
                if (cell.Style.Wrapping?.AllowTruncation == true)
                {
                    int indicatorLen = cell.Style.Wrapping?.TruncationIndicator?.Length ?? 0;
                    stats[c].MinTruncationWidth = Math.Max(
                        stats[c].MinTruncationWidth,
                        indicatorLen + 1);
                }
            }
        }
        return stats;
    }

    private static void Tokenize(
        List<CliTextSegment> line,
        CliWrapMode mode,
        out int longestToken,
        out int breakpoints)
    {
        longestToken = 0;
        breakpoints = 0;
        int currentLen = 0;

        bool IsBreakRune(System.Text.Rune r)
        {
            if (mode == CliWrapMode.WordWrap)   return System.Text.Rune.IsWhiteSpace(r);
            if (mode == CliWrapMode.SymbolWrap) return System.Text.Rune.IsWhiteSpace(r) || !System.Text.Rune.IsLetterOrDigit(r);
            if (mode == CliWrapMode.CharWrap)   return true;
            return false; // SingleLine/Multiline: no auto breakpoints
        }

        foreach (var seg in line)
        {
            foreach (var rune in seg.Text.EnumerateRunes())
            {
                if (IsBreakRune(rune))
                {
                    breakpoints++;
                    if (currentLen > 0)
                    {
                        if (currentLen > longestToken) longestToken = currentLen;
                        currentLen = 0;
                    }
                }
                else
                {
                    currentLen++;
                }
            }
        }
        if (currentLen > longestToken) longestToken = currentLen;
    }

    private void ShrinkColumnsToCeilingIfNeeded(
        MeasuredCell[,] output,
        int[] workColW,
        int[] colMin,
        int[] colMax,
        int? wCeil,
        CliWrapMode mode,
        bool truncAllowed)
    {
        if (!wCeil.HasValue) return;
        int sum = workColW.Sum();
        if (sum <= wCeil.Value) return;

        var stats = ComputeColumnStats(output, mode, truncAllowed);
        int excess = sum - wCeil.Value;

        var floorPerCol = new int[ColumnCount];
        for (int c = 0; c < ColumnCount; c++)
            floorPerCol[c] = Math.Max(colMin[c],
                Math.Max(stats[c].LongestToken, stats[c].MinTruncationWidth));

        // Proportional headroom distribution: each column gives up a share of
        // the excess proportional to its available headroom (width − floor).
        int totalHeadroom = 0;
        for (int c = 0; c < ColumnCount; c++)
            totalHeadroom += Math.Max(0, workColW[c] - floorPerCol[c]);

        if (totalHeadroom <= 0) return;

        int remaining = Math.Min(excess, totalHeadroom);

        // First pass: proportional reduction (truncation rounds down)
        for (int c = 0; c < ColumnCount && remaining > 0; c++)
        {
            int headroom = Math.Max(0, workColW[c] - floorPerCol[c]);
            if (headroom == 0) continue;
            int reduction = (int)((double)headroom / totalHeadroom * excess);
            reduction = Math.Min(reduction, headroom);
            reduction = Math.Min(reduction, remaining);
            workColW[c] -= reduction;
            remaining -= reduction;
        }

        // Second pass: distribute rounding remainder to columns with most headroom
        while (remaining > 0)
        {
            int bestCol = -1;
            int bestHeadroom = 0;
            for (int c = 0; c < ColumnCount; c++)
            {
                int h = workColW[c] - floorPerCol[c];
                if (h > bestHeadroom)
                {
                    bestHeadroom = h;
                    bestCol = c;
                }
            }
            if (bestCol < 0) break;
            workColW[bestCol]--;
            remaining--;
        }
    }

    // Existing methods: RewrapAllCellsToCurrentWidths, JoinSegmentsIntoSingleLine, 
    // TruncateIfNeeded, WrapSegments, RecomputeRowHeights are assumed to be present
    // from the previous version of this partial class.

    private bool RewrapAllCellsToCurrentWidths(
        MeasuredCell[,] output,
        int[] workColW,
        int[] workRowH,
        CliWrapMode mode,
        bool allowTruncation)
    {
        for (int r = 0; r < RowCount; r++)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                var cell = output[r, c];
                if (cell == null || cell.IsCovered) continue;

                var scrollMode = GetScrollMode(c, r);

                // Subgrid cells (scrollable or not): re-measure with current working dimensions
                if (cell.Sink != null)
                {
                    RemeasureSubgridCell(cell, c, r, workColW, workRowH, scrollMode);
                    continue;
                }

                int span = Math.Max(1, cell.ColSpan);
                int width = 0;
                for (int i = 0; i < span; i++) width += workColW[c + i];

                // Padding reserves column space that content cannot occupy. Wrap into the
                // content area (column width minus padding), matching how ApplyAlignmentAndFill
                // and InitializeMeasuredCells account for padding. Without this, content wraps
                // to the full column width and padding spills over into the next column.
                int paddingW = GetCellPaddingWidth(c, r, cell.Style);
                if (paddingW > 0)
                    width = Math.Max(1, width - paddingW);

                // Scrollable non-subgrid cell: content can overflow in the scroll direction,
                // treat the corresponding dimension as 1 so wrapping/truncation is not needed.
                if (scrollMode.HasFlag(CliScrollMode.Horizontal))
                    width = 1;

                // Determine per-cell effective mode
                var effMode = cell.Style.Wrapping?.Mode ?? CliWrapMode.WordWrap;
                var trunc = allowTruncation && (cell.Style.Wrapping?.AllowTruncation == true);

                // SingleLine is special: no wrapping; only truncate if allowed at B4 (or forced by hard caps)
                if (effMode == CliWrapMode.SingleLine)
                {
                    var single = JoinSegmentsIntoSingleLine(cell.InitialLines, ShouldTrimLineEnds(cell.Style));
                    var adjusted = TruncateIfNeeded(single, width, trunc, cell.Style.Wrapping?.TruncationIndicator);
                    if (adjusted == null) return false; // illegal overflow
                    cell.UpdateLines([adjusted]);
                    continue;
                }

                if (effMode == CliWrapMode.Multiline)
                {
                    // Enforce explicit lines; apply per-line truncation if needed/allowed
                    var newLines = new List<List<CliTextSegment>>(cell.InitialLines.Count);
                    foreach (var line in cell.InitialLines)
                    {
                        var adjusted = TruncateIfNeeded(line, width, trunc, cell.Style.Wrapping?.TruncationIndicator);
                        if (adjusted == null) return false;
                        newLines.Add(adjusted);
                    }
                    cell.UpdateLines(newLines);
                    continue;
                }

                // Word/Symbol/Char with progressive preference
                
                CliWrapMode[] pref = effMode switch
                {
                    CliWrapMode.WordWrap => new[] { CliWrapMode.WordWrap, CliWrapMode.CharWrap },
                    CliWrapMode.SymbolWrap => new[] { CliWrapMode.WordWrap, CliWrapMode.SymbolWrap, CliWrapMode.CharWrap },
                    CliWrapMode.CharWrap => new[] { CliWrapMode.WordWrap, CliWrapMode.SymbolWrap, CliWrapMode.CharWrap },
                    _ => Array.Empty<CliWrapMode>()
                };
                /*
                CliWrapMode[] pref = mode switch
                {
                    CliWrapMode.WordWrap => new[] { CliWrapMode.WordWrap },
                    CliWrapMode.SymbolWrap => new[] { CliWrapMode.WordWrap, CliWrapMode.SymbolWrap },
                    CliWrapMode.CharWrap => new[] { CliWrapMode.WordWrap, CliWrapMode.SymbolWrap, CliWrapMode.CharWrap },
                    _ => Array.Empty<CliWrapMode>()
                };
                */
                var wrapped = WrapWithPreference(cell.InitialLines, width, pref, trunc, cell.Style.Wrapping?.TruncationIndicator);
                if (wrapped == null) return false;
                cell.UpdateLines(wrapped);
            }
        }
        return true;
    }

    private static List<CliTextSegment>? TruncateIfNeeded(
        List<CliTextSegment> line,
        int width,
        bool allowTruncation,
        string? indicator)
    {
        int len = CliTextSegment.Length(line);
        if (len <= width) return MeasuredCell.CloneLine(line);
        if (!allowTruncation) return null;
        var copy = MeasuredCell.CloneLine(line);
        // Trim from the end until fits; then append indicator if provided
        while (CliTextSegment.Length(copy) > Math.Max(0, width - (indicator?.Length ?? 0)))
        {
            if (copy.Count == 0) break;
            var last = copy[^1];
            if (last.Text.Length <= 1)
            {
                copy.RemoveAt(copy.Count - 1);
                continue;
            }
            copy[^1] = new CliTextSegment(last.Text[..^1], last.Style);
        }
        if (!string.IsNullOrEmpty(indicator))
        {
            // Append indicator with last style (or default)
            var style = copy.Count > 0 ? copy[^1].Style : new CliCharStyle(CliColor.Gray, CliColor.Black);
            copy.Add(new CliTextSegment(indicator!, style));
        }
        return copy;
    }


    private static List<List<CliTextSegment>>? WrapSegments(
List<List<CliTextSegment>> initialLines,
int width,
CliWrapMode mode,
bool allowTruncation,
string? indicator)
    {
        // Tokenization helpers
        static bool IsBreakRune(System.Text.Rune r, CliWrapMode m) =>
            m switch
            {
                CliWrapMode.WordWrap => System.Text.Rune.IsWhiteSpace(r),
                CliWrapMode.SymbolWrap => System.Text.Rune.IsWhiteSpace(r) || !System.Text.Rune.IsLetterOrDigit(r),
                CliWrapMode.CharWrap => false, // handled by forced-split
                _ => false
            };

        // Split a single styled line into tokens: word tokens and break tokens.
        static List<(List<CliTextSegment> segs, int len, bool isBreak)> TokenizeLine(
            List<CliTextSegment> line, CliWrapMode m)
        {
            var tokens = new List<(List<CliTextSegment>, int, bool)>();

            var cur = new List<CliTextSegment>();
            var sb = new StringBuilder();
            int curLen = 0;
            bool curIsBreak = false;

            void FlushToken()
            {
                if (sb.Length == 0) return;
                cur.Add(new CliTextSegment(sb.ToString(), cur.Count > 0 ? cur[^1].Style : new CliCharStyle()));
                tokens.Add((cur, curLen, curIsBreak));
                cur = new List<CliTextSegment>();
                sb.Clear();
                curLen = 0;
            }

            foreach (var seg in line)
            {
                foreach (var rune in seg.Text.EnumerateRunes())
                {
                    bool br = IsBreakRune(rune, m);
                    if (curLen == 0)
                    {
                        // start a new token with current rune type
                        curIsBreak = br;
                        sb.Append(rune.ToString());
                        curLen++;
                        // seed first segment style
                        if (cur.Count == 0) cur.Add(new CliTextSegment(string.Empty, seg.Style));
                    }
                    else if (br == curIsBreak)
                    {
                        sb.Append(rune.ToString());
                        curLen++;
                    }
                    else
                    {
                        // type changed: flush previous token
                        FlushToken();
                        curIsBreak = br;
                        // seed style
                        cur.Add(new CliTextSegment(string.Empty, seg.Style));
                        sb.Append(rune.ToString());
                        curLen = 1;
                    }
                }

                // when segment changes style, ensure we carry style into current token
                if (sb.Length > 0 && (cur.Count == 0 || !StyleEquals(cur[^1].Style, seg.Style)))
                    cur.Add(new CliTextSegment(string.Empty, seg.Style));
            }

            FlushToken();
            return tokens;
        }

        // Append a token's segments into a line buffer
        static void AppendToken(List<CliTextSegment> target, (List<CliTextSegment> segs, int len, bool isBreak) tok)
        {
            foreach (var s in tok.segs)
            {
                if (s.Text.Length == 0) continue;
                if (target.Count > 0 && target[^1].Style.HasSameRenderingAs(s.Style))
                    target[^1] = new CliTextSegment(target[^1].Text + s.Text, s.Style);
                else
                    target.Add(new CliTextSegment(s.Text, s.Style));
            }
        }

        // Split a "word" token across lines (CharWrap fallback)
        static void AppendForcedSplit(
            List<List<CliTextSegment>> result,
            List<CliTextSegment> current,
            ref int curLen,
            (List<CliTextSegment> segs, int len, bool isBreak) tok,
            int width)
        {
            // Flatten token text preserving style boundaries
            foreach (var s in tok.segs)
            {
                var text = s.Text.AsSpan();
                int idx = 0;
                while (idx < text.Length)
                {
                    int room = Math.Max(1, width - curLen);
                    int take = Math.Min(room, text.Length - idx);
                    var slice = text.Slice(idx, take).ToString();
                    if (slice.Length > 0)
                    {
                        if (current.Count > 0 && current[^1].Style.HasSameRenderingAs(s.Style))
                            current[^1] = new CliTextSegment(current[^1].Text + slice, s.Style);
                        else
                            current.Add(new CliTextSegment(slice, s.Style));
                        curLen += slice.Length;
                    }
                    idx += take;
                    if (curLen >= width)
                    {
                        result.Add(MeasuredCell.CloneLine(current));
                        current.Clear();
                        curLen = 0;
                    }
                }
            }
        }

        var resultLines = new List<List<CliTextSegment>>();

        foreach (var line in initialLines)
        {
            // Short-circuit fast path
            if (CliTextSegment.Length(line) <= width)
            {
                resultLines.Add(MeasuredCell.CloneLine(line));
                continue;
            }

            var tokens = TokenizeLine(line, mode);
            var current = new List<CliTextSegment>();
            int curLen = 0;
            (List<CliTextSegment> segs, int len, bool isBreak)? pendingBreak = null;

            void Flush()
            {
                resultLines.Add(MeasuredCell.CloneLine(current));
                current.Clear();
                curLen = 0;
                pendingBreak = null;
            }

            foreach (var tok in tokens)
            {
                if (mode == CliWrapMode.CharWrap)
                {
                    // CharWrap ignores break semantics — just flow and split
                    if (!tok.isBreak)
                        AppendForcedSplit(resultLines, current, ref curLen, tok, width);
                    else
                    {
                        // breaks are optional spaces — only add if room remains on line
                        if (curLen > 0 && curLen + tok.len <= width)
                        {
                            AppendToken(current, tok);
                            curLen += tok.len;
                        }
                        else
                        {
                            // drop leading break at line start
                        }
                    }
                    continue;
                }

                if (tok.isBreak)
                {
                    if (mode == CliWrapMode.WordWrap)
                    {
                        // whitespace only: can be dropped
                        pendingBreak = tok;
                        continue;
                    }
                    if (mode == CliWrapMode.SymbolWrap)
                    {
                        // punctuation: treat like a short "word" that must be rendered
                        // Try to fit with the next word; if overflow, place it alone at end of line.
                        if (curLen + tok.len <= width)
                        {
                            AppendToken(current, tok);
                            curLen += tok.len;
                        }
                        else
                        {
                            Flush(); // break line here
                            AppendToken(current, tok);
                            curLen += tok.len;
                        }
                        continue;
                    }
                }

                // tok is a word
                int needed = tok.len + (pendingBreak?.len ?? 0);

                if (curLen == 0)
                {
                    // don't carry leading separators; drop pendingBreak
                    pendingBreak = null;

                    if (tok.len <= width)
                    {
                        AppendToken(current, tok);
                        curLen += tok.len;
                        continue;
                    }
                    // For Word/Symbol: signal failure so the ladder can escalate
                    if (mode == CliWrapMode.WordWrap || mode == CliWrapMode.SymbolWrap)
                        return null;
                    // CharWrap: allowed to split
                    AppendForcedSplit(resultLines, current, ref curLen, tok, width);
                    continue;
                }

                // we already have content on the line
                if (curLen + needed <= width)
                {
                    if (pendingBreak is { } br)
                    {
                        AppendToken(current, br);
                        curLen += br.len;
                        pendingBreak = null;
                    }
                    AppendToken(current, tok);
                    curLen += tok.len;
                }
                else
                {
                    // wrap at the previous breakpoint
                    Flush();

                    if (tok.len <= width)
                    {
                        AppendToken(current, tok);
                        curLen += tok.len;
                    }
                    else
                    {
                        // For Word/Symbol: no legal break → escalate
                        if (mode == CliWrapMode.WordWrap || mode == CliWrapMode.SymbolWrap)
                            return null;
                        // CharWrap: split
                        AppendForcedSplit(resultLines, current, ref curLen, tok, width);
                    }
                }
            }

            if (current.Count > 0) Flush();

            // As a last resort: if nothing produced and truncation allowed
            if (resultLines.Count == 0 && allowTruncation)
            {
                var t = TruncateIfNeeded(line, width, true, indicator);
                if (t == null) return null;
                resultLines.Add(t);
            }
            else if (resultLines.Count == 0)
            {
                return null;
            }
        }

        return resultLines;
    }


    private void RemeasureSubgridCell(
        MeasuredCell cell, int col, int row,
        int[] workColW, int[] workRowH,
        CliScrollMode scrollMode)
    {
        var gridCell = cells[row, col];
        if (gridCell == null || !gridCell.HasSubgrid) return;

        var sink = cell.Sink!;
        var subgrid = gridCell.Subgrid!;

        // Compute span width from current working column widths
        int colSpan = Math.Max(1, cell.ColSpan);
        int spanWidth = 0;
        for (int i = 0; i < colSpan; i++) spanWidth += workColW[col + i];

        // Compute span height from current working row heights
        int rowSpan = Math.Max(1, cell.RowSpan);
        int spanHeight = 0;
        for (int i = 0; i < rowSpan; i++) spanHeight += workRowH[row + i];

        // Set sink constraints based on working dimensions.
        // Scrollable axes get no constraints (content can overflow and scroll).
        if (scrollMode.HasFlag(CliScrollMode.Horizontal))
        {
            sink.SoftMaxWidth = null;
            sink.MaxWidth = null;
        }
        else
        {
            sink.SoftMaxWidth = spanWidth > 0 ? spanWidth : null;
            sink.MaxWidth = spanWidth > 0 ? spanWidth : null;
        }

        if (scrollMode.HasFlag(CliScrollMode.Vertical))
        {
            sink.SoftMaxHeight = null;
            sink.MaxHeight = null;
        }
        else
        {
            sink.SoftMaxHeight = spanHeight > 0 ? spanHeight : null;
            sink.MaxHeight = spanHeight > 0 ? spanHeight : null;
        }

        // Re-measure the subgrid with updated constraints
        subgrid.InvalidateLayout();
        subgrid.Measure(sink);
        var lines = TigerConsole.RenderGridToSegmentedLines(subgrid, sink);
        cell.UpdateLines(lines);
    }

    private void RecomputeRowHeights(MeasuredCell[,] output, int[] workRowH)
    {
        Array.Fill(workRowH, 1);
        for (int r = 0; r < RowCount; r++)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                var cell = output[r, c];
                if (cell == null || cell.IsCovered) continue;
                if (cell.RowSpan == 1)
                {
                    // Vertically scrollable cells can overflow; treat their height
                    // contribution as 1 so the row is sized by other cells.
                    var scrollMode = GetScrollMode(c, r);
                    int h = scrollMode.HasFlag(CliScrollMode.Vertical)
                        ? 1
                        : cell.Height;
                    workRowH[r] = Math.Max(workRowH[r], h);
                }
                // Note: for RowSpan > 1 we keep simple first-pass behavior; can improve later.
            }
        }
    }

    private void GrowScrollingColumns(int[] workColW, int[] colMin, int[] colMax, int? wCeil)
    {
        if (!wCeil.HasValue) return;

        // A horizontally-scrolling cell is an editing/scrolling viewport that should fill the available
        // width. Each such cell contributes one absorb column: the trailing growable column inside its
        // span. For a single-column scroll cell that is the scroll column itself (the existing
        // behaviour). For a spanned scroll cell whose anchor is pinned (e.g. a frame border locked to
        // Width=1), the fill lands on the outermost free column of the span — leaving the inner
        // frame/content columns untouched so the frame is not widened — which is exactly where a
        // left-anchored viewport should extend.
        var absorbCols = new SortedSet<int>();
        var effectiveColMax = (int[])colMax.Clone();
        var scrollSpanCols = new HashSet<int>();
        foreach (var kvp in _scrollCells)
        {
            if (!kvp.Value.Mode.HasFlag(CliScrollMode.Horizontal)) continue;

            int col = kvp.Key.Column;
            int row = kvp.Key.Row;
            var gridCell = cells[row, col];
            int span = Math.Max(1, gridCell?.ColSpan ?? 1);

            for (int i = 0; i < span; i++)
                scrollSpanCols.Add(col + i);

            int? spanMaxWidth = gridCell?.Style?.EffectiveMaxWidth is int max and < int.MaxValue
                ? max
                : null;

            for (int i = span - 1; i >= 0; i--)
            {
                int ci = col + i;
                if (spanMaxWidth is int maxWidth)
                {
                    int otherWidth = 0;
                    for (int j = 0; j < span; j++)
                    {
                        if (j != i)
                            otherWidth += workColW[col + j];
                    }

                    effectiveColMax[ci] = Math.Min(effectiveColMax[ci], Math.Max(workColW[ci], maxWidth - otherWidth));
                }

                if (effectiveColMax[ci] - workColW[ci] > 0)
                {
                    absorbCols.Add(ci);
                    break;
                }
            }
        }
        if (absorbCols.Count == 0) return;

        // The scroll viewport outranks Star "remaining space" columns for the shared leftover budget.
        // A Star column that sits OUTSIDE every scroll span may have picked up elastic width during
        // span-fit (EnsureSpanWidthContribution distributes a wide full-width row's deficit into Star
        // first); that width belongs to the viewport, not to a column beyond it. Release such Star
        // columns back to their floor so the viewport can claim the width; GrowStarColumns then refills
        // Star from whatever the viewport leaves. Columns inside a scroll span are never released.
        for (int c = 0; c < ColumnCount; c++)
        {
            if (columns[c]?.Sizing != CliColumnSizing.Star) continue;
            if (scrollSpanCols.Contains(c)) continue;
            if (workColW[c] > colMin[c])
                workColW[c] = colMin[c];
        }

        int sumW = 0;
        for (int c = 0; c < ColumnCount; c++) sumW += workColW[c];
        int extra = wCeil.Value - sumW;
        if (extra <= 0) return;

        DistributeExtraToScrollAxes(workColW, effectiveColMax, absorbCols.ToList(), extra);
    }

    // Spanned cells whose natural width exceeds the sum of their spanned column widths
    // widen those columns to fit. Star columns absorb first (they exist precisely to soak
    // up extra width); Auto columns are widened only as a fallback when Star can't cover.
    // Horizontally-scrolling subgrid cells are skipped (handled by GrowScrollingColumns).
    private void EnsureSpanWidthContribution(MeasuredCell[,] output, int[] workColW, int[] colMin, int[] colMax)
    {
        for (int r = 0; r < RowCount; r++)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                var cell = output[r, c];
                if (cell == null || cell.IsCovered) continue;
                if (cell.ColSpan <= 1) continue;

                var scrollMode = GetScrollMode(c, r);
                if (scrollMode.HasFlag(CliScrollMode.Horizontal)) continue;

                int span = cell.ColSpan;
                int paddingW = GetCellPaddingWidth(c, r, cell.Style);
                int natural = Math.Max(cell.Width + paddingW, cell.Style.EffectiveMinWidth);
                int currentSum = 0;
                for (int i = 0; i < span; i++) currentSum += workColW[c + i];
                int deficit = natural - currentSum;
                if (deficit <= 0) continue;

                // Partition spanned columns by Sizing.
                var autoCols = new List<int>();
                var starCols = new List<int>();
                for (int i = 0; i < span; i++)
                {
                    int ci = c + i;
                    if (columns[ci]?.Sizing == CliColumnSizing.Star)
                        starCols.Add(ci);
                    else
                        autoCols.Add(ci);
                }

                // Phase 1: Star cols absorb first.
                deficit = DistributeSpanDeficit(workColW, colMax, starCols, deficit);
                // Phase 2: Auto cols widen only if Star couldn't cover the deficit.
                if (deficit > 0)
                    DistributeSpanDeficit(workColW, colMax, autoCols, deficit);
            }
        }
    }

    private static int DistributeSpanDeficit(int[] sizes, int[] maxes, List<int> indices, int deficit)
    {
        if (indices.Count == 0 || deficit <= 0) return deficit;

        // Only cols with headroom participate. Cols that cap mid-pass spill into the
        // next pass over still-eligible cols, so locked/capped cols don't waste shares.
        var active = new List<int>(indices.Count);
        foreach (var i in indices)
            if (maxes[i] - sizes[i] > 0) active.Add(i);

        while (active.Count > 0 && deficit > 0)
        {
            int per = deficit / active.Count;
            int remainder = deficit - per * active.Count;
            int distributed = 0;
            var next = new List<int>(active.Count);
            foreach (var i in active)
            {
                int delta = per + (remainder > 0 ? 1 : 0);
                if (remainder > 0) remainder--;
                int avail = maxes[i] - sizes[i];
                int take = Math.Min(delta, avail);
                sizes[i] += take;
                distributed += take;
                if (maxes[i] - sizes[i] > 0) next.Add(i);
            }
            if (distributed == 0) break;
            deficit -= distributed;
            active = next;
        }
        return Math.Max(0, deficit);
    }

    private void GrowStarColumns(int[] workColW, int[] colMax, int? wCeil)
    {
        if (!wCeil.HasValue) return;

        int sumW = 0;
        for (int c = 0; c < ColumnCount; c++) sumW += workColW[c];
        int extra = wCeil.Value - sumW;
        if (extra <= 0) return;

        var starCols = new List<int>();
        for (int c = 0; c < ColumnCount; c++)
        {
            if (columns[c]?.Sizing == CliColumnSizing.Star)
                starCols.Add(c);
        }
        if (starCols.Count == 0) return;

        DistributeExtraToScrollAxes(workColW, colMax, starCols, extra);
    }

    private void GrowScrollingRows(int[] workRowH, int[] rowMax, int? hCeil, MeasuredCell[,] output)
    {
        if (!hCeil.HasValue) return;

        int sumH = 0;
        for (int r = 0; r < RowCount; r++) sumH += workRowH[r];
        int extra = hCeil.Value - sumH;
        if (extra <= 0) return;

        var scrollRows = new List<int>();
        for (int r = 0; r < RowCount; r++)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                if (GetScrollMode(c, r).HasFlag(CliScrollMode.Vertical))
                {
                    scrollRows.Add(r);
                    break;
                }
            }
        }
        if (scrollRows.Count == 0) return;

        // Subgrid cells: soft max is a ceiling, not a grow target. Cap growth at the
        // subgrid's natural measured height rather than the full soft-max ceiling.
        var effectiveRowMax = (int[])rowMax.Clone();
        foreach (int r in scrollRows)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                var cell = output[r, c];
                if (cell == null || cell.IsCovered || cell.Sink == null) continue;
                if (!GetScrollMode(c, r).HasFlag(CliScrollMode.Vertical)) continue;
                if (cells[r, c]?.Subgrid?.MeasuredHeight is int subH)
                    effectiveRowMax[r] = Math.Min(effectiveRowMax[r], subH);
            }
        }

        DistributeExtraToScrollAxes(workRowH, effectiveRowMax, scrollRows, extra);
    }

    private static void DistributeExtraToScrollAxes(int[] sizes, int[] maxes, List<int> indices, int extra)
    {
        int per = extra / indices.Count;
        int remainder = extra - per * indices.Count;
        foreach (var i in indices)
        {
            int delta = per + (remainder > 0 ? 1 : 0);
            if (remainder > 0) remainder--;
            int avail = maxes[i] - sizes[i];
            sizes[i] += Math.Min(delta, Math.Max(0, avail));
        }
    }

    private static List<List<CliTextSegment>>? WrapWithPreference(
    List<List<CliTextSegment>> initialLines,
    int width,
    CliWrapMode[] preference,
    bool allowTruncation,
    string? indicator)
    {
        // Try each mode WITHOUT truncation first
        foreach (var mode in preference)
        {
            var lines = WrapSegments(initialLines, width, mode, allowTruncation: false, indicator: indicator);
            if (lines != null) return lines;
        }
        // Last resort: use the LAST preferred mode with truncation (if allowed)
        var last = preference[^1];
        return WrapSegments(initialLines, width, last, allowTruncation, indicator);
    }

}
