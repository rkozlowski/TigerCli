using System.Text.RegularExpressions;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Integration coverage for the real placement the dialogs use: a vertically-scrolling list placed as
/// a <b>colSpan&gt;1</b> subgrid anchored in a frame-border column locked to Width=1. This exercises
/// the span/subgrid sizing that pure-CliGrid tests can't reach (the public API only places colSpan-1
/// subgrids):
/// <list type="bullet">
/// <item>the list fills the frame interior to the widest in-frame content (its longest item),</item>
/// <item>the spanned subgrid is not collapsed to its anchor column's locked Width,</item>
/// <item>a title/status wider than the frame does not drag the frame/list wider, and</item>
/// <item>the selected row's background fills the whole frame interior (HtmlSink ownership).</item>
/// </list>
/// </summary>
public sealed class CliGridSelectDialogFillTests
{
    private const string Longest = "LongestItemHere"; // 15
    private static readonly string[] Items = ["A", "BB", Longest];
    private static int ExpectedInterior => Longest.Length + 2; // SingleLine items + Both padding

    private static List<string> RenderSelectDialog(string title, int? softMaxWidth = null, int viewport = 120)
    {
        var shell = new TestShell(viewportWidth: viewport);
        var select = new InlineSelect(shell, Items, preselectIndex: 0);
        var grid = new InlineDialog(shell, title, select).ToGrid();
        if (softMaxWidth is int w)
            grid.SoftMaxWidth = w;
        return TigerConsole.RenderGridToLines(grid);
    }

    private static string FrameRow(List<string> lines) => lines.First(l => l.Contains('╔'));
    private static string ListRow(List<string> lines) => lines.First(l => l.Contains('║'));
    private static int InteriorWidth(string framedLine)
    {
        int first = framedLine.IndexOfAny(['╔', '║', '╚']);
        // width between the two double-vertical/corner borders
        int last = framedLine.LastIndexOfAny(['╗', '║', '╝']);
        return last - first - 1;
    }

    [Fact]
    public void SelectDialog_FrameInterior_FillsToLongestItem()
    {
        var lines = RenderSelectDialog("Pick");

        Assert.Equal(ExpectedInterior, InteriorWidth(FrameRow(lines)));
        Assert.Equal(ExpectedInterior, InteriorWidth(ListRow(lines)));
    }

    [Fact]
    public void SelectDialog_WideTitle_DoesNotWidenFrame()
    {
        // The title spans the full dialog and may exceed the frame; it must not force the frame/list
        // wider — the fill target is the widest in-frame content only.
        var wideTitle = new string('T', ExpectedInterior + 40);
        var lines = RenderSelectDialog(wideTitle);

        Assert.Equal(ExpectedInterior, InteriorWidth(FrameRow(lines)));
        Assert.True(lines.Max(l => l.TrimEnd().Length) > InteriorWidth(FrameRow(lines)) + 2);
    }

    [Fact]
    public void SelectDialog_FrameWidth_IsDrivenByLongestItem_NotTheSelectedRow()
    {
        // The shortest item ("A") is selected and is the only row rendered through ToGrid (height is
        // owned by the modal flow). The frame width must still reflect the LONGEST item's natural
        // width, proving the span was sized from natural content, not from the collapsed anchor
        // column (Width=1) it is anchored in.
        var lines = RenderSelectDialog("Pick");
        Assert.Equal(ExpectedInterior, InteriorWidth(FrameRow(lines)));
        Assert.True(ExpectedInterior > 1);
    }

    // ---- selected-row background fills the interior (HtmlSink ownership) ----

    // (visibleChar, backgroundHex) for each rendered character on an HTML line.
    private static List<(char ch, string bg)> PerChar(string htmlLine)
    {
        var result = new List<(char, string)>();
        string current = "none";
        foreach (Match tok in Regex.Matches(htmlLine, "<span[^>]*>|</span>|<[^>]+>|[^<]+"))
        {
            var s = tok.Value;
            if (s.StartsWith("<span", StringComparison.Ordinal))
            {
                var bg = Regex.Match(s, "background-color:(#[0-9A-Fa-f]{6})");
                current = bg.Success ? bg.Groups[1].Value : "none";
            }
            else if (s == "</span>") current = "none";
            else if (!s.StartsWith('<'))
                foreach (var c in System.Net.WebUtility.HtmlDecode(s)) result.Add((c, current));
        }
        return result;
    }

    [Fact]
    public void Html_SelectedRowBackground_FillsFrameInterior()
    {
        var shell = new TestShell(viewportWidth: 120);
        // Selected item is the SHORT "A": its highlight must still fill the full interior.
        var select = new InlineSelect(shell, Items, preselectIndex: 0);
        var html = TigerConsole.RenderGridToHtml(new InlineDialog(shell, "Pick", select).ToGrid());

        const string open = "<pre class=\"tigercli\">";
        const string close = "</pre>";
        int s = html.IndexOf(open, StringComparison.Ordinal) + open.Length;
        int e = html.IndexOf(close, StringComparison.Ordinal);
        var rowLine = html[s..e].Split('\n').First(l => l.Contains('║'));

        var cells = PerChar(rowLine);
        int left = cells.FindIndex(c => c.ch == '║');
        int right = cells.FindLastIndex(c => c.ch == '║');
        var interior = cells.Skip(left + 1).Take(right - left - 1).ToList();

        Assert.Equal(ExpectedInterior, interior.Count);                 // interior fills to longest item
        Assert.Single(interior.Select(c => c.bg).Distinct());           // one uniform selection background
        Assert.DoesNotContain(interior, c => c.bg == "none");           // every interior cell is styled
    }
}
