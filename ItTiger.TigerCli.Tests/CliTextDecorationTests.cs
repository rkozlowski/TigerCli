using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Locks the decoration support in the low-level style model: the flags enum combines, and
/// <see cref="CliCharStyle"/> / <see cref="CliCellStyle"/> store, clone, and additively compose
/// decorations without disturbing the existing foreground/background behaviour.
/// </summary>
public sealed class CliTextDecorationTests
{
    [Fact]
    public void Flags_Combine_BoldItalicUnderline()
    {
        var all = CliTextDecoration.Bold | CliTextDecoration.Italic | CliTextDecoration.Underline;

        Assert.True(all.HasFlag(CliTextDecoration.Bold));
        Assert.True(all.HasFlag(CliTextDecoration.Italic));
        Assert.True(all.HasFlag(CliTextDecoration.Underline));
        Assert.Equal(CliTextDecoration.None, all & ~all);
    }

    [Fact]
    public void CharStyle_StoresDecorations()
    {
        var style = new CliCharStyle(CliColor.Yellow, CliColor.Black,
            CliTextDecoration.Bold | CliTextDecoration.Underline);

        Assert.Equal(CliColor.Yellow, style.Foreground);
        Assert.Equal(CliColor.Black, style.Background);
        Assert.Equal(CliTextDecoration.Bold | CliTextDecoration.Underline, style.Decorations);
    }

    [Fact]
    public void CharStyle_DefaultsToNone()
    {
        Assert.Equal(CliTextDecoration.None, new CliCharStyle(CliColor.Yellow).Decorations);
    }

    [Fact]
    public void CharStyle_Clone_PreservesDecorations()
    {
        var original = new CliCharStyle(CliColor.Red, decorations: CliTextDecoration.Italic);

        var clone = CliCharStyle.Clone(original);

        Assert.NotNull(clone);
        Assert.Equal(CliTextDecoration.Italic, clone!.Value.Decorations);
        Assert.Equal(CliColor.Red, clone.Value.Foreground);
    }

    [Fact]
    public void CellStyle_Clone_PreservesDecorations()
    {
        var cell = new CliCellStyle(new CliCharStyle(CliColor.Red, decorations: CliTextDecoration.Bold));

        var clone = CliCellStyle.Clone(cell);

        Assert.Equal(CliTextDecoration.Bold, clone!.CharStyle!.Value.Decorations);
    }

}
