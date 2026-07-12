using System;
using System.Collections.Generic;
using System.Linq;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Pins the focus/width invariant for the composite <see cref="InlineFolderSelect"/>:
/// <para><b>Focus changes may change active styling, cursor, hint, and scroll position. They must not
/// change the dialog/frame width.</b></para>
/// The dialog is rendered in four focus states (list → path → buttons → list) and the frame border,
/// in-frame list row, and overall dialog width are compared. Each widget exposes a different
/// focus-aware hint, which previously made the dialog grow/shrink purely because focus moved.
/// </summary>
public sealed class InlineFolderSelectFocusWidthTests
{
    private const string LongParent = @"Z:\Projects\Alpha\Quarterly\SampleData";
    private const string LongInitialPath = LongParent + @"\Tiny";
    private static readonly string[] LongPathFolderNames = ["Tiny", "Reports", "MediumFolder"];
    private const string LongestLongPathFolderName = "MediumFolder";

    // ----- Deterministic, cross-platform in-memory browser -----

    private sealed class FakeFolderBrowser : IFolderBrowser
    {
        private readonly Dictionary<string, List<string>> _tree;
        private readonly List<string> _drives;
        private readonly Dictionary<string, string?> _parentOf = new(StringComparer.OrdinalIgnoreCase);

        public FakeFolderBrowser(Dictionary<string, List<string>> tree, List<string> drives)
        {
            _tree = new(tree, StringComparer.OrdinalIgnoreCase);
            _drives = drives;
            foreach (var (loc, kids) in _tree)
                foreach (var k in kids)
                    _parentOf[k] = loc;
            foreach (var d in drives)
                _parentOf.TryAdd(d, null);
        }

        public string? RootLocation => null;

        public IReadOnlyList<FolderEntry> GetEntries(string? location)
        {
            var kids = location is null ? _drives : (_tree.TryGetValue(location, out var c) ? c : new());
            return kids.Select(p => new FolderEntry(Label(p, location), p, _tree.TryGetValue(p, out var cc) && cc.Count > 0)).ToList();
        }

        public bool TryGetParent(string? location, out string? parent)
        {
            parent = null;
            if (location is null) return false;
            if (_parentOf.TryGetValue(location, out var p)) { parent = p; return true; }
            return false;
        }

        public (string?, string?) ResolveInitial(string? init)
        {
            if (string.IsNullOrWhiteSpace(init)) return (null, null);
            var probe = init;
            while (probe is not null && !_parentOf.ContainsKey(probe) && !_tree.ContainsKey(probe))
                probe = StringParent(probe);
            if (probe is null) return (null, null);
            return TryGetParent(probe, out var par) ? (par, probe) : (probe, null);
        }

        private static string Label(string p, string? loc)
        {
            if (loc is null) return p;
            var t = p.TrimEnd('\\'); int i = t.LastIndexOf('\\'); return i < 0 ? t : t[(i + 1)..];
        }

        private static string? StringParent(string p)
        {
            var t = p.TrimEnd('\\'); int i = t.LastIndexOf('\\'); if (i < 0) return null;
            return i == 2 ? t[..3] : t[..i];
        }
    }

    private static FakeFolderBrowser Tree() => new(
        new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [@"Z:\"] = new()
            {
                @"Z:\Media", @"Z:\Projects", @"Z:\Docs", @"Z:\Samples",
            },
            [@"Z:\Media"] = new() { @"Z:\Media\Incoming", @"Z:\Media\Archive" },
            [@"Z:\Media\Incoming"] = new() { @"Z:\Media\Incoming\Audio", @"Z:\Media\Incoming\Images" },
            [@"Z:\Media\Incoming\Audio"] = new(),
            [@"Z:\Media\Incoming\Images"] = new(),
            [@"Z:\Media\Archive"] = new() { @"Z:\Media\Archive\2025", @"Z:\Media\Archive\2026" },
            [@"Z:\Media\Archive\2025"] = new(),
            [@"Z:\Media\Archive\2026"] = new(),
            [@"Z:\Projects"] = new() { @"Z:\Projects\Alpha", @"Z:\Projects\Beta" },
            [@"Z:\Projects\Alpha"] = new()
            {
                @"Z:\Projects\Alpha\Builds", @"Z:\Projects\Alpha\DesignDocs", @"Z:\Projects\Alpha\ReleaseNotes",
            },
            [@"Z:\Projects\Alpha\Builds"] = new(),
            [@"Z:\Projects\Alpha\DesignDocs"] = new(),
            [@"Z:\Projects\Alpha\ReleaseNotes"] = new(),
            [@"Z:\Projects\Beta"] = new() { @"Z:\Projects\Beta\Exports", @"Z:\Projects\Beta\Fixtures" },
            [@"Z:\Projects\Beta\Exports"] = new(),
            [@"Z:\Projects\Beta\Fixtures"] = new(),
            [@"Z:\Docs"] = new() { @"Z:\Docs\Reports", @"Z:\Docs\Exports" },
            [@"Z:\Docs\Reports"] = new() { @"Z:\Docs\Reports\Monthly", @"Z:\Docs\Reports\Quarterly" },
            [@"Z:\Docs\Reports\Monthly"] = new(),
            [@"Z:\Docs\Reports\Quarterly"] = new(),
            [@"Z:\Docs\Exports"] = new(),
            [@"Z:\Samples"] = new() { @"Z:\Samples\North", @"Z:\Samples\South" },
            [@"Z:\Samples\North"] = new() { @"Z:\Samples\North\ScenarioOne", @"Z:\Samples\North\ScenarioTwo" },
            [@"Z:\Samples\North\ScenarioOne"] = new(),
            [@"Z:\Samples\North\ScenarioTwo"] = new(),
            [@"Z:\Samples\South"] = new(),
        },
        new List<string> { @"C:\", @"D:\", @"M:\", @"P:\", @"R:\", @"V:\", @"Z:\" });

    private static FakeFolderBrowser LongPathTree() => new(
        new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [@"Z:\"] = new() { @"Z:\Projects" },
            [@"Z:\Projects"] = new() { @"Z:\Projects\Alpha" },
            [@"Z:\Projects\Alpha"] = new() { @"Z:\Projects\Alpha\Quarterly" },
            [@"Z:\Projects\Alpha\Quarterly"] = new() { LongParent },
            [LongParent] = LongPathFolderNames.Select(name => LongParent + @"\" + name).ToList(),
            [LongParent + @"\Tiny"] = new(),
            [LongParent + @"\Reports"] = new(),
            [LongParent + @"\MediumFolder"] = new(),
        },
        new List<string> { @"Z:\" });

    // ----- Focus-state capture helpers -----

    private enum Focus { List, Path, Buttons }

    private static (InlineFolderSelect control, TestShell shell) New()
    {
        var shell = new TestShell();
        var control = new InlineFolderSelect(shell, Tree(), @"Z:\Projects\Alpha\Builds");
        return (control, shell);
    }

    private static (InlineFolderSelect control, TestShell shell) NewLongPath()
    {
        var shell = new TestShell(viewportWidth: 120);
        var control = new InlineFolderSelect(shell, LongPathTree(), LongInitialPath);
        return (control, shell);
    }

    private static void SetFocus(InlineFolderSelect control, Focus focus)
    {
        int target = focus switch { Focus.Path => 0, Focus.List => 1, Focus.Buttons => 2, _ => 1 };
        // Bounded traversal: at most one full cycle.
        for (int i = 0; i < 4 && !control.GetWidgets()[target].IsFocused; i++)
            control.HandleKey(new KeyEvent(ConsoleKey.Tab, ConsoleModifiers.None));
    }

    private static List<string> RenderLines(InlineFolderSelect control, Focus focus)
    {
        SetFocus(control, focus);
        var grid = new InlineDialog(control.Shell, "Select a folder", control).ToGrid();
        return TigerConsole.RenderGridToLines(grid);
    }

    private static List<string> RenderLines(InlineDialog dialog, InlineFolderSelect control, Focus focus)
    {
        SetFocus(control, focus);
        return TigerConsole.RenderGridToLines(dialog.ToGrid());
    }

    private static int OverallWidth(List<string> lines) => lines.Count == 0 ? 0 : lines.Max(l => l.Length);

    // The frame border lines use the double box-drawing characters.
    private static int FrameWidth(List<string> lines)
    {
        var frameLine = lines.First(l => l.Contains('╔') || l.Contains('╚'));
        return frameLine.TrimEnd().Length;
    }

    // The in-frame list row is the vertical-border line between the top and bottom frame borders.
    private static int ListRowWidth(List<string> lines)
    {
        var listLine = lines.First(l => l.Contains('║'));
        return listLine.TrimEnd().Length;
    }

    private static int PathRowWidth(List<string> lines)
    {
        var pathLine = lines.First(l => l.Contains(LongParent));
        return pathLine.TrimEnd().Length;
    }

    private static int ExpectedFrameWidthFromFolderNames()
    {
        // Folder rows render a two-character open/no-open marker before the folder label, and the
        // reusable select widget adds left/right cell padding.
        int listItemWidth = LongPathFolderNames.Max(name => name.Length) + 2;
        return listItemWidth + 2 + 2; // select padding plus left/right frame borders.
    }

    // ----- Frame and list-row width are already stable (regression guard) -----

    [Fact]
    public void FrameWidth_IsStable_AcrossFocusChanges()
    {
        var (control, _) = New();

        int list = FrameWidth(RenderLines(control, Focus.List));
        int path = FrameWidth(RenderLines(control, Focus.Path));
        int buttons = FrameWidth(RenderLines(control, Focus.Buttons));
        int listAgain = FrameWidth(RenderLines(control, Focus.List));

        Assert.Equal(list, path);
        Assert.Equal(list, buttons);
        Assert.Equal(list, listAgain);
    }

    [Fact]
    public void ListRowWidth_IsStable_AcrossFocusChanges()
    {
        var (control, _) = New();

        int list = ListRowWidth(RenderLines(control, Focus.List));
        int path = ListRowWidth(RenderLines(control, Focus.Path));
        int buttons = ListRowWidth(RenderLines(control, Focus.Buttons));

        Assert.Equal(list, path);
        Assert.Equal(list, buttons);
    }

    // ----- The actual bug: overall dialog width must not change with focus -----

    [Fact]
    public void OverallDialogWidth_IsStable_AcrossFocusChanges()
    {
        var (control, shell) = New();
        var dialog = new InlineDialog(shell, "Select a folder", control);

        int list = OverallWidth(RenderLines(dialog, control, Focus.List));
        int path = OverallWidth(RenderLines(dialog, control, Focus.Path));
        int buttons = OverallWidth(RenderLines(dialog, control, Focus.Buttons));
        int listAgain = OverallWidth(RenderLines(dialog, control, Focus.List));

        Assert.Equal(list, path);
        Assert.Equal(list, buttons);
        Assert.Equal(list, listAgain);
    }

    // Every rendered line shares one width (a clean rectangular dialog), in every focus state.
    [Fact]
    public void AllRows_ShareOneWidth_InEveryFocusState()
    {
        var (control, _) = New();

        foreach (var focus in new[] { Focus.List, Focus.Path, Focus.Buttons })
        {
            var lines = RenderLines(control, focus);
            int width = OverallWidth(lines);
            Assert.All(lines, l => Assert.Equal(width, l.Length));
        }
    }

    // ----- Frame width comes only from in-frame folder-list content -----

    [Fact]
    public void LongPathFixture_PathTextIsLongerThanEveryFolderName()
    {
        Assert.True(LongInitialPath.Length > LongPathFolderNames.Max(name => name.Length));
    }

    [Fact]
    public void FrameWidth_IsBasedOnLongestFolderName_NotPathText()
    {
        var (control, _) = NewLongPath();

        var lines = RenderLines(control, Focus.List);

        Assert.Equal(ExpectedFrameWidthFromFolderNames(), FrameWidth(lines));
        Assert.True(LongInitialPath.Length + 2 > FrameWidth(lines));
    }

    [Fact]
    public void PathInput_RendersHorizontalViewport_ForLongPath()
    {
        var (control, _) = NewLongPath();

        var lines = RenderLines(control, Focus.Path);
        var viewport = PathInputViewport(lines);

        Assert.Contains(@"SampleData\Tiny", viewport);
        Assert.True(viewport.Length < LongInitialPath.Length);
    }

    [Fact]
    public void SelectedFolderRowWidth_MatchesFolderListFrameWidth()
    {
        var (control, _) = NewLongPath();

        var lines = RenderLines(control, Focus.List);

        Assert.Equal(FrameWidth(lines), ListRowWidth(lines));
    }

    [Fact]
    public void LongPathFrameWidth_IsStable_AcrossFocusChanges()
    {
        var (control, _) = NewLongPath();

        int list = FrameWidth(RenderLines(control, Focus.List));
        int path = FrameWidth(RenderLines(control, Focus.Path));
        int buttons = FrameWidth(RenderLines(control, Focus.Buttons));
        int listAgain = FrameWidth(RenderLines(control, Focus.List));

        Assert.Equal(list, path);
        Assert.Equal(list, buttons);
        Assert.Equal(list, listAgain);
    }

    [Fact]
    public void PathInputViewport_IsStable_AcrossFocusChanges()
    {
        var (control, shell) = NewLongPath();
        var dialog = new InlineDialog(shell, "Select a folder", control);

        string Render(Focus focus)
        {
            SetFocus(control, focus);
            return PathInputViewport(TigerConsole.RenderGridToLines(dialog.ToGrid()));
        }

        string focused = Render(Focus.Path);
        string listFocused = Render(Focus.List);
        string buttonsFocused = Render(Focus.Buttons);
        string focusedAgain = Render(Focus.Path);

        Assert.Equal(focused, listFocused);
        Assert.Equal(focused, buttonsFocused);
        Assert.Equal(focused, focusedAgain);
    }

    [Fact]
    public void ChangingPathText_DoesNotChangeFrameWidth()
    {
        var (control, _) = NewLongPath();

        int before = FrameWidth(RenderLines(control, Focus.Path));
        Assert.True(control.HandleKey(new KeyEvent(ConsoleKey.X, ConsoleModifiers.None, 'X')).IsHandled);
        int after = FrameWidth(RenderLines(control, Focus.Path));

        Assert.Equal(before, after);
    }

    [Fact]
    public void ChangingHintText_DoesNotChangeFrameWidth()
    {
        var (control, _) = NewLongPath();

        int before = FrameWidth(RenderLines(control, Focus.Path));
        Assert.True(control.HandleKey(new KeyEvent(ConsoleKey.X, ConsoleModifiers.None, 'X')).IsHandled);
        Assert.True(control.HandleKey(new KeyEvent(ConsoleKey.Enter, ConsoleModifiers.None)).IsHandled);
        Assert.Equal("Invalid path entered", control.Hint);
        int after = FrameWidth(RenderLines(control, Focus.Path));

        Assert.Equal(before, after);
    }

    // ----- HtmlSink-based structural checks (spans / background segments) -----

    private static string[] HtmlLines(InlineFolderSelect control, Focus focus)
    {
        SetFocus(control, focus);
        var grid = new InlineDialog(control.Shell, "Select a folder", control).ToGrid();
        var html = TigerConsole.RenderGridToHtml(grid);

        // No ANSI is ever emitted by the HTML sink.
        Assert.DoesNotContain((char)0x1B, html);

        const string open = "<pre class=\"tigercli\">";
        const string close = "</pre>";
        int s = html.IndexOf(open, StringComparison.Ordinal) + open.Length;
        int e = html.IndexOf(close, StringComparison.Ordinal);
        return html[s..e].Split('\n');
    }

    private static string[] HtmlLines(InlineDialog dialog, InlineFolderSelect control, Focus focus)
    {
        SetFocus(control, focus);
        var html = TigerConsole.RenderGridToHtml(dialog.ToGrid());

        Assert.DoesNotContain((char)0x1B, html);

        const string open = "<pre class=\"tigercli\">";
        const string close = "</pre>";
        int s = html.IndexOf(open, StringComparison.Ordinal) + open.Length;
        int e = html.IndexOf(close, StringComparison.Ordinal);
        return html[s..e].Split('\n');
    }

    private static string FrameTopHtml(string[] htmlLines) => htmlLines.First(l => l.Contains('╔'));

    private static string PathInputViewport(List<string> lines)
    {
        int topFrame = lines.FindIndex(l => l.Contains('╔'));
        Assert.True(topFrame > 0);
        return NormalizePathInputRow(lines[topFrame - 1]);
    }

    private static string NormalizePathInputRow(string line)
    {
        var chars = line.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] is ConsoleSymbol.TriangleLeft or ConsoleSymbol.TriangleRight)
                chars[i] = ' ';
        }

        return new string(chars).TrimEnd();
    }

    // The frame border row is rendered identically — same spans, same background, same width —
    // regardless of which widget is focused.
        [Fact]
    public void Html_FrameRow_IsByteIdentical_AcrossFocusChanges()
    {
        var (control, shell) = New();
        var dialog = new InlineDialog(shell, "Select a folder", control);

        string list = FrameTopHtml(HtmlLines(dialog, control, Focus.List));
        string path = FrameTopHtml(HtmlLines(dialog, control, Focus.Path));
        string buttons = FrameTopHtml(HtmlLines(dialog, control, Focus.Buttons));

        Assert.Equal(list, path);
        Assert.Equal(list, buttons);
    }

    // The HTML sink preserves every cell (including background-fill) verbatim inside <pre>, so the
    // stripped visible width of each focus state agrees with the styled-segment view and is stable.
    [Fact]
    public void Html_OverallWidth_IsStable_AcrossFocusChanges()
    {
        var (control, shell) = New();
        var dialog = new InlineDialog(shell, "Select a folder", control);

        int VisibleWidth(Focus f)
        {
            var lines = HtmlLines(dialog, control, f);
            return lines.Select(StripTags).Max(t => t.Length);
        }

        int list = VisibleWidth(Focus.List);
        Assert.Equal(list, VisibleWidth(Focus.Path));
        Assert.Equal(list, VisibleWidth(Focus.Buttons));
    }

    private static string StripTags(string html)
    {
        var sb = new System.Text.StringBuilder(html.Length);
        bool inTag = false;
        foreach (var ch in html)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(ch);
        }
        // Undo HTML entity escaping for the few characters the sink escapes.
        return sb.ToString().Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
    }
}
