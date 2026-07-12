using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Themes;

namespace ItTiger.TigerCli.Tests;

/// <summary>
/// Regression coverage for the dogfooding crash where rendering threw "The handle is invalid." when
/// stdout was redirected/piped/captured, because the console sinks read <see cref="Console.WindowWidth"/>
/// directly during the measure pass. Structured output must now render safely with a deterministic
/// fallback width of <see cref="TerminalCapabilities.DefaultOutputWidth"/> (120) and without forcing
/// ANSI. Tests force <see cref="CliColorMode.Standard16"/> so the <c>ConsoleSink</c> path is exercised
/// deterministically regardless of how the test host's stdout is wired.
/// </summary>
public sealed class RedirectedOutputRenderingTests : TestBase
{
    private static readonly ITheme Blue = new TigerBlueTheme();

    private sealed record Device(string Id, string Name, string Url);

    private static readonly Device[] Devices =
    [
        new("d-1", "Front door", "https://example.com/devices/d-1"),
        new("d-2", "Garage", "https://example.com/devices/d-2"),
    ];

    // ---- CliList / CliDetails must not throw under redirected/captured output ----

    [Fact]
    public void CliList_RendersUnderCapturedConsole_WithoutThrowing()
    {
        var list = new CliList<Device>()
            .ApplyPreset(CliTableStylePreset.Lucca, Blue)
            .AddColumn("Id", d => d.Id)
            .AddColumn("Name", d => d.Name)
            .AddColumn("Url", d => d.Url);

        var stdout = WithForcedConsoleSink(() => TigerConsole.Render(list.Render(Devices)));

        Assert.Contains("Front door", stdout);
        Assert.Contains("Garage", stdout);
    }

    [Fact]
    public void CliDetails_RendersUnderCapturedConsole_WithoutThrowing()
    {
        var details = new CliDetails()
            .ApplyPreset(CliTableStylePreset.Details, Blue)
            .AddKey("Id:", "d-1")
            .Add("Name:", "Front door")
            .AddLink("Url:", "https://example.com/devices/d-1");

        var stdout = WithForcedConsoleSink(() => TigerConsole.Render(details));

        Assert.Contains("Front door", stdout);
    }

    [Fact]
    public void Markup_RendersUnderCapturedConsole_WithoutThrowing()
    {
        var stdout = WithForcedConsoleSink(() => TigerConsole.MarkupLine("[Red]hello[/] world"));
        Assert.Contains("hello world", stdout);
    }

    // ---- link text stays visible/copyable under redirected/captured output ----

    [Fact]
    public void LinkValue_RemainsVisibleAndCopyable_UnderCapturedConsole()
    {
        const string url = "https://example.com/devices/d-1";
        var details = new CliDetails()
            .ApplyPreset(CliTableStylePreset.Details, Blue)
            .AddLink("Url:", url);

        var stdout = WithForcedConsoleSink(() => TigerConsole.Render(details));

        // The link text is always emitted verbatim; clickability (OSC 8) is a separate enhancement.
        Assert.Contains(url, stdout);
    }

    // ---- grid wrapping uses the fallback width (120) when that is the available width ----

    [Fact]
    public void StarColumn_FillsToFallbackWidth_120()
    {
        var grid = new CliGrid(1, 1);
        grid.SetColumn(0, new CliGridColumnDefinition(null) { Sizing = CliColumnSizing.Star });
        grid.Set(0, 0, "X");

        // Simulate the redirected ConsoleSink reporting the deterministic fallback width.
        var sink = new FixedDimensionLinesSink { SoftMaxWidth = TerminalCapabilities.DefaultOutputWidth };
        TigerConsole.RenderGrid(grid, sink);

        Assert.NotEmpty(sink.Lines);
        Assert.All(sink.Lines, line => Assert.Equal(120, line.Length));
    }

    // ---- the width fallback is layout-only: it does not imply ANSI/colour support ----

    [Fact]
    public void RedirectedStream_StillDisablesAnsi_DespiteWidthFallback()
    {
        // Width falls back to 120 under redirection, but capability detection independently
        // keeps ANSI off for a redirected stream under Auto.
        Assert.Equal(120, TerminalCapabilities.ResolveWidth(isRedirected: true, () => 200));
        Assert.Equal(
            CliAnsiSupport.None,
            TerminalCapabilities.Detect(_ => null, isRedirected: true, isWindows: false, windowsVtSupported: false));
    }

    // Forces the ConsoleSink path (so SoftMaxWidth/Height come from the safe helper) and captures
    // whatever it writes, restoring the global console state afterwards.
    private static string WithForcedConsoleSink(Action render)
    {
        var originalMode = TigerConsole.ColorMode;
        var originalOut = Console.Out;
        using var stdout = new StringWriter();
        try
        {
            TigerConsole.ColorMode = CliColorMode.Standard16;
            Console.SetOut(stdout);
            render();
        }
        finally
        {
            Console.SetOut(originalOut);
            TigerConsole.ColorMode = originalMode;
        }
        return stdout.ToString();
    }

    // Public test sink: collects plain text per line and reports caller-configured soft bounds, so a
    // specific available width can be simulated without depending on the host console.
    private sealed class FixedDimensionLinesSink : ICliRenderSink
    {
        private readonly List<string> _lines = new();
        private readonly StringBuilder _current = new();

        public List<string> Lines => _lines;

        public int? SoftMaxWidth { get; init; }
        public int? SoftMaxHeight { get; init; }
        public int? MaxWidth { get; init; }
        public int? MaxHeight { get; init; }

        public void Write(CliTextSegment segment) => _current.Append(segment.Text);

        public void NewLine()
        {
            _lines.Add(_current.ToString());
            _current.Clear();
        }

        public void Flush()
        {
            if (_current.Length > 0)
            {
                _lines.Add(_current.ToString());
                _current.Clear();
            }
        }

        public void Reset()
        {
            _lines.Clear();
            _current.Clear();
        }
    }
}
