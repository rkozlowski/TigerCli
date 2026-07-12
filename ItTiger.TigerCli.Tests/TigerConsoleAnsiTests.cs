using System.Linq;
using System.Text.RegularExpressions;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Covers the public ANSI helpers <see cref="TigerConsole.MarkupToAnsi"/> and
/// <see cref="TigerConsole.RenderGridToAnsi"/>, including semantic-token resolution and the
/// invariant that stripping ANSI sequences recovers the plain rendered text.
/// </summary>
public sealed class TigerConsoleAnsiTests
{
    private const string Esc = "\u001b";

    private static readonly Regex AnsiPattern = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);

    private static string StripAnsi(string s) => AnsiPattern.Replace(s, string.Empty);

    // ---- MarkupToAnsi: raw colours ----

    [Fact]
    public void MarkupToAnsi_OceanBlue_Emits38_5_24_AndStripsTags()
    {
        Assert.Equal($"{Esc}[38;5;24mHello{Esc}[0m", TigerConsole.MarkupToAnsi("[OceanBlue]Hello[/]"));
    }

    [Fact]
    public void MarkupToAnsi_Gray85_Emits38_5_253()
    {
        Assert.Equal($"{Esc}[38;5;253mHello{Esc}[0m", TigerConsole.MarkupToAnsi("[Gray85]Hello[/]"));
    }

    [Fact]
    public void MarkupToAnsi_StandardColor_UsesClassicRemap()
    {
        // Red == 12 → classic 91 (not 38;5;12).
        Assert.Equal($"{Esc}[91mx{Esc}[0m", TigerConsole.MarkupToAnsi("[Red]x[/]"));
    }

    [Fact]
    public void MarkupToAnsi_PlainText_HasNoEscapes()
    {
        Assert.Equal("hello", TigerConsole.MarkupToAnsi("hello"));
    }

    [Fact]
    public void MarkupToAnsi_StripsToOriginalText()
    {
        Assert.Equal("Hello world", StripAnsi(TigerConsole.MarkupToAnsi("[OceanBlue]Hello[/] world")));
    }

    // ---- MarkupToAnsi: semantic tokens resolve through the theme ----

    [Fact]
    public void MarkupToAnsi_Accent_ResolvesSemanticTokenThroughTheme()
    {
        var theme = TigerConsole.CurrentTheme;
        var accentFg = theme.Resolve(ThemeStyle.Accent).CharStyle!.Value.Foreground;
        Assert.NotNull(accentFg);

        // [Accent] must render identically to the raw colour the theme resolves it to.
        var expected = TigerConsole.MarkupToAnsi($"[{accentFg}]Hello[/]", theme);
        var actual = TigerConsole.MarkupToAnsi("[Accent]Hello[/]", theme);

        Assert.Equal(expected, actual);
        Assert.Contains(Esc + "[", actual); // a colour escape was emitted
    }

    // ---- RenderGridToAnsi ----

    [Fact]
    public void RenderGridToAnsi_StyledCell_RendersExtendedForegroundFaithfully()
    {
        var grid = new CliGrid(1, 1);
        grid.Set(0, 0, "Hi", new CliCellStyle(new CliCharStyle(CliColor.OceanBlue)));

        var ansi = TigerConsole.RenderGridToAnsi(grid);

        // OceanBlue (24) is emitted as a 256-colour foreground; the grid's default Black background
        // means the cell renders as a combined "38;5;24;40" sequence, so assert the faithful token.
        Assert.Contains("38;5;24", ansi);
    }

    [Fact]
    public void RenderGridToAnsi_StripsToSameTextAsLineRenderer()
    {
        // The grid's default char style is Gray-on-Black, so even an unstyled grid emits escapes;
        // stripping them must recover exactly what the non-ANSI line renderer produces.
        var grid = new CliGrid(1, 1);
        grid.Set(0, 0, "Hi");
        var ansi = TigerConsole.RenderGridToAnsi(grid);

        var grid2 = new CliGrid(1, 1);
        grid2.Set(0, 0, "Hi");
        var plainLines = TigerConsole.RenderGridToLines(grid2);

        Assert.Contains(Esc, ansi); // default style produced at least one escape
        Assert.Equal(
            string.Join(System.Environment.NewLine, plainLines) + System.Environment.NewLine,
            StripAnsi(ansi));
    }
}
