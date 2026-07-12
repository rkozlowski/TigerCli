using System.Reflection;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Tui.Controls;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Frame-set ownership for <see cref="SpinnerTicker"/>: it owns the default frame set, the predefined
/// frame sets, custom frames, and timing. Controls must not re-declare frame lists. Frames are raw
/// content; presentation (brackets, styling, title prefix) is decided by whatever renders them.
/// </summary>
public sealed class SpinnerTickerTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    // Drives a freshly-constructed ticker through exactly one full cycle and returns the frames in order.
    private static IReadOnlyList<string> Cycle(SpinnerTicker ticker, int frameCount)
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ticker.Advance(start); // first call only captures the interval baseline (no advance)

        var frames = new List<string> { ticker.CurrentContent };
        for (int i = 1; i < frameCount; i++)
        {
            ticker.Advance(start + ticker.Interval * i);
            frames.Add(ticker.CurrentContent);
        }

        return frames;
    }

    [Fact]
    public void Default_FrameSet_IsTheFourBrailleFrames()
    {
        string[] expected = ["⠖", "⠲", "⠴", "⠦"];

        Assert.Equal(expected, SpinnerTicker.Frames(SpinnerFrameSet.Default));
        // The parameterless/default ctor cycles exactly those frames.
        Assert.Equal(expected, Cycle(new SpinnerTicker(), expected.Length));
    }

    [Theory]
    [InlineData(SpinnerFrameSet.Default, new[] { "⠖", "⠲", "⠴", "⠦" })]
    [InlineData(SpinnerFrameSet.Dots6, new[] { "⠇", "⠋", "⠙", "⠸", "⠴", "⠦" })]
    [InlineData(SpinnerFrameSet.Dots8, new[] { "⡇", "⠏", "⠛", "⠹", "⢸", "⣰", "⣤", "⣆" })]
    [InlineData(SpinnerFrameSet.Slide, new[] { "⠉", "⠒", "⠤", "⣀" })]
    [InlineData(SpinnerFrameSet.SlideBounce, new[] { "⠉", "⠒", "⠤", "⣀", "⠤", "⠒" })]
    [InlineData(SpinnerFrameSet.Line, new[] { "|", "/", "—", "\\" })]
    public void PredefinedFrameSet_ReturnsAndCyclesExpectedFrames(SpinnerFrameSet set, string[] expected)
    {
        Assert.Equal(expected, SpinnerTicker.Frames(set));
        // Cycling wraps after the last frame, so a full lap reproduces the sequence from the first frame.
        Assert.Equal(expected, Cycle(new SpinnerTicker(set), expected.Length));
    }

    [Fact]
    public void Line_FrameSet_UsesStrings_AndIncludesBackslash()
    {
        var frames = SpinnerTicker.Frames(SpinnerFrameSet.Line);

        Assert.IsAssignableFrom<IReadOnlyList<string>>(frames);
        Assert.Equal("\\", frames[3]);
        Assert.Single(frames, f => f == "\\");
    }

    [Fact]
    public void Snake_FrameSet_PreservesLeadingAndTrailingSpaces()
    {
        var frames = SpinnerTicker.Frames(SpinnerFrameSet.Snake);

        Assert.Equal(8, frames.Count);
        Assert.Equal("⢎ ", frames[0]);   // trailing space on the first frame
        Assert.Equal(" ⡱", frames[4]);    // leading space on the fifth frame
        // Every Snake frame is two columns wide (a glyph/space pair).
        Assert.All(frames, f => Assert.Equal(2, f.Length));
    }

    [Fact]
    public void CustomFrames_Cycle_InOrder()
    {
        var ticker = new SpinnerTicker(TimeSpan.FromMilliseconds(10), ["A", "B", "C"]);

        Assert.Equal(new[] { "A", "B", "C" }, Cycle(ticker, 3));
        // One more interval (the cycle stepped to start + 2*10ms) wraps back to the first frame.
        ticker.Advance(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) + TimeSpan.FromMilliseconds(30));
        Assert.Equal("A", ticker.CurrentContent);
    }

    [Fact]
    public void CustomFrames_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new SpinnerTicker(Interval, null!));

    [Fact]
    public void CustomFrames_Empty_Throws() =>
        Assert.Throws<ArgumentException>(() => new SpinnerTicker(Interval, Array.Empty<string>()));

    [Fact]
    public void CustomFrames_NullFrame_Throws() =>
        Assert.Throws<ArgumentException>(() => new SpinnerTicker(Interval, ["A", null!]));

    [Fact]
    public void CustomFrames_EmptyFrame_Throws() =>
        Assert.Throws<ArgumentException>(() => new SpinnerTicker(Interval, ["A", ""]));

    [Fact]
    public void Interval_NonPositive_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpinnerTicker(TimeSpan.Zero, ["A"]));

    [Fact]
    public void Active_ArgumentControls_InitialIsActive()
    {
        Assert.True(new SpinnerTicker(active: true).IsActive);
        Assert.False(new SpinnerTicker(active: false).IsActive);

        var scoped = new SpinnerTicker(active: false);
        scoped.Start();
        Assert.True(scoped.IsActive);
        scoped.Stop();
        Assert.False(scoped.IsActive);
    }

    [Fact]
    public void InlineActivityControl_DoesNotDeclareItsOwnSpinnerFrames()
    {
        // Frame ownership lives in SpinnerTicker; the control must not re-declare a frame list.
        var frameFields = typeof(InlineActivityControl<object>)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(string[]) || f.FieldType == typeof(IReadOnlyList<string>))
            .ToArray();

        Assert.Empty(frameFields);
    }
}
