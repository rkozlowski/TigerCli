using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Pins the above-frame path-input width contract for the composite <see cref="InlineFolderSelect"/>:
/// the path input is a horizontally-scrolling editing viewport that <b>fills the available width</b>
/// (wider than the narrow, content-sized folder-list frame), while the frame stays driven by the
/// folder list. Both are measured from the dialog grid's column geometry under a sink that carries a
/// soft-max width (a real console always has one); a null sink supplies no ceiling, so a fill viewport
/// legitimately has no width to fill toward and is not exercised here.
///
/// Geometry recap (7-column dialog): the frame spans columns 0..4; the path area
/// (<see cref="InlineDialogArea.AboveFrameWithIndicators"/>) spans columns 1..5, so column 5 sits
/// inside the path span but OUTSIDE the frame and is where the path absorbs its extra width without
/// widening the frame.
/// </summary>
public sealed class InlineFolderSelectPathWidthTests
{
    private const int Viewport = 80;
    private static readonly string[] Folders = ["A", "BB", "CCC"]; // longest label is short → narrow frame

    private sealed class SoftMaxSink : ICliRenderSink
    {
        public int? SoftMaxWidth { get; init; }
        public int? SoftMaxHeight => null;
        public int? MaxWidth => null;
        public int? MaxHeight => null;
        public void Write(CliTextSegment segment) { }
        public void NewLine() { }
        public void Flush() { }
        public void Reset() { }
    }

    private static CliGrid MeasuredDialog(int softMax = Viewport, string[]? folders = null)
    {
        var shell = new TestShell(viewportWidth: Viewport);
        folders ??= Folders;
        var control = new InlineFolderSelect(shell, new FakeBrowser(folders), $@"Z:\proj\{folders[0]}");
        var grid = new InlineDialog(shell, "Select a folder", control).ToGrid();
        grid.Measure(new SoftMaxSink { SoftMaxWidth = softMax });
        return grid;
    }

    // Left-edge X of column c (column-major origin).
    private static int X(CliGrid g, int c) => g.GetMeasuredCellOrigin(c, 0)!.Value.Column;

    // Sum of column widths over [from, toExclusive).
    private static int Span(CliGrid g, int from, int toExclusive)
        => (toExclusive < g.ColumnCount ? X(g, toExclusive) : g.MeasuredWidth ?? X(g, from)) - X(g, from);

    private static int FrameWidth(CliGrid g) => Span(g, 0, 5);     // cols 0..4
    private static int PathSpanWidth(CliGrid g) => Span(g, 1, 6);  // cols 1..5
    private static int Col5Width(CliGrid g) => Span(g, 5, 6);

    [Fact]
    public void PathSpan_IsWiderThanFrame()
    {
        var g = MeasuredDialog();
        Assert.True(PathSpanWidth(g) > FrameWidth(g),
            $"path span {PathSpanWidth(g)} should exceed frame {FrameWidth(g)}");
    }

    [Fact]
    public void PathSpan_FillsTowardTheViewport()
    {
        var g = MeasuredDialog();
        // The path is an editing viewport: it should consume most of the available width, far beyond
        // the narrow frame. (Not exactly the viewport: column 6 keeps a 1-cell minimum.)
        Assert.True(PathSpanWidth(g) >= Viewport - 4,
            $"path span {PathSpanWidth(g)} should fill toward viewport {Viewport}");
    }

    [Fact]
    public void Col5_AbsorbsPathWidth_OutsideTheFrame()
    {
        var g = MeasuredDialog();
        // The extra path width lands in column 5 (in the path span, outside the frame), not in the
        // frame columns.
        Assert.True(Col5Width(g) > 1, $"col5 {Col5Width(g)} should absorb path width");
    }

    [Fact]
    public void FrameWidth_TracksLongestFolderName_NotThePath()
    {
        // A longer longest-folder-name widens the frame by exactly that delta (the frame is driven by
        // the list content), while the path span stays far wider than either frame. No padding/marker
        // constants are hard-coded — only the difference is asserted. Both names are kept above the
        // control's list min-width floor so the floor does not absorb the delta.
        var shortG = MeasuredDialog(folders: ["A", "BB", "CCCCCCCC"]);       // longest 8
        var longG = MeasuredDialog(folders: ["A", "BB", "CCCCCCCCCCCCC"]);   // longest 13 (+5)

        Assert.Equal(5, FrameWidth(longG) - FrameWidth(shortG));
        Assert.True(FrameWidth(longG) < PathSpanWidth(longG));             // path still wider than frame
    }

    [Fact]
    public void FrameWidth_IsStable_RegardlessOfViewportWidth()
    {
        // Doubling the viewport widens the path viewport but must not change the list-driven frame.
        int frameNarrow = FrameWidth(MeasuredDialog(softMax: 60));
        int frameWide = FrameWidth(MeasuredDialog(softMax: 120));
        Assert.Equal(frameNarrow, frameWide);
    }

    // Minimal in-memory browser: one parent with the given leaf folders.
    private sealed class FakeBrowser : IFolderBrowser
    {
        private readonly string[] _paths;
        public FakeBrowser(string[] folders) => _paths = folders.Select(f => $@"Z:\proj\{f}").ToArray();
        public string? RootLocation => null;

        public IReadOnlyList<FolderEntry> GetEntries(string? location)
            => string.Equals(location, @"Z:\proj", StringComparison.OrdinalIgnoreCase)
                ? _paths.Select(p => new FolderEntry(p[(p.LastIndexOf('\\') + 1)..], p, false)).ToList()
                : Array.Empty<FolderEntry>();

        public bool TryGetParent(string? location, out string? parent)
        {
            parent = @"Z:\proj";
            return location is not null;
        }

        public (string?, string?) ResolveInitial(string? init)
            => init is null ? (null, null) : (@"Z:\proj", init);
    }
}
