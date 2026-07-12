using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Markup;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Tui.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// Static entry point for TigerCli console output, markup rendering, structured rendering, themes,
/// colour mode, and test/documentation capture helpers.
/// </summary>
public static partial class TigerConsole
{
    /// <summary>Optional logger used for render diagnostics.</summary>
    public static ILogger? Logger { get; set; }

    private static Encoding _outputEncoding = Encoding.UTF8;

    static TigerConsole()
    {
        Console.OutputEncoding = _outputEncoding;
    }

    /// <summary>
    /// Output encoding assigned to <see cref="Console.OutputEncoding"/>. Defaults to UTF-8.
    /// </summary>
    public static Encoding OutputEncoding
    {
        get
        {
            return _outputEncoding;
        }
        set
        {
            _outputEncoding = value;
            Console.OutputEncoding = _outputEncoding;
        }
    }

    private static bool _treatDbNullAsNull = true;

    /// <summary>
    /// Gets or sets whether <see cref="DBNull.Value"/> is treated as <c>null</c>
    /// when setting cell content. Default is <c>true</c>.
    /// </summary>
    public static bool TreatDbNullAsNull
    {
        get => _treatDbNullAsNull;
        set => _treatDbNullAsNull = value;
    }

    /// <summary>
    /// Writes TigerCli bracket markup to stdout through the active output sink.
    /// </summary>
    public static void Markup(string markup)
    {
        // Sink chosen by an ambient scope when pushed, else by ColorMode (Auto detects ConsoleSink
        // vs AnsiSink for stdout). A capture scope also pins a plain base style — console colours
        // are meaningless (and machine-dependent) in captured output.
        var (sink, plainBaseStyle) = GetOutputSinkWithPolicy();
        // Read current console colors defensively: a redirected/captured console (common on Linux)
        // can report an invalid sentinel that must degrade to "no color", not an invalid CliColor.
        CliCharStyle? baseStyle = plainBaseStyle
            ? null
            : new CliCharStyle(
                CliColorMapper.FromConsoleColorOrNull(Console.ForegroundColor),
                CliColorMapper.FromConsoleColorOrNull(Console.BackgroundColor));
        var styles = CreateMarkupStyleResolver();

        foreach (var segment in CliMarkupParser.Parse(markup, baseStyle, styles, ColorAliases))
        {
            sink.Write(segment);
        }

        sink.Flush();
    }

    /// <summary>
    /// Formats text with <see cref="string.Format(IFormatProvider, string, object?[])"/> and writes it as markup.
    /// </summary>
    public static void Markup(IFormatProvider provider, string format, params object[] args)
    {
        var formatted = string.Format(provider, format, args);
        Markup(formatted); 
    }

    /// <summary>
    /// Writes markup followed by a newline to stdout.
    /// </summary>
    public static void MarkupLine(string markup)
    {
        Markup(markup);
        Markup(Environment.NewLine);        
    }

    /// <summary>
    /// Writes a blank line to stdout through the active output sink.
    /// </summary>
    public static void MarkupLine()
    {
        Markup(Environment.NewLine);
    }

    /// <summary>
    /// Formats text and writes it as markup followed by a newline to stdout.
    /// </summary>
    public static void MarkupLine(IFormatProvider provider, string format, params object[] args)
    {
        var formatted = string.Format(provider, format, args);
        MarkupLine(formatted);
    }

    /// <summary>
    /// Writes TigerCli bracket markup to stderr through the active error sink.
    /// </summary>
    public static void MarkupError(string markup)
    {
        // Sink chosen by an ambient error scope when pushed (styled app-run capture), else by
        // ColorMode (Auto detects ConsoleErrorSink vs AnsiSink for stderr).
        var (sink, plainBaseStyle) = GetErrorSinkWithPolicy();
        // Read current console colors defensively: a redirected/captured console (common on Linux)
        // can report an invalid sentinel that must degrade to "no color", not an invalid CliColor.
        CliCharStyle? baseStyle = plainBaseStyle
            ? null
            : new CliCharStyle(
                CliColorMapper.FromConsoleColorOrNull(Console.ForegroundColor),
                CliColorMapper.FromConsoleColorOrNull(Console.BackgroundColor));
        var styles = CreateMarkupStyleResolver();

        foreach (var segment in CliMarkupParser.Parse(markup, baseStyle, styles, ColorAliases))
        {
            sink.Write(segment);
        }

        sink.Flush();
    }

    /// <summary>
    /// Formats text and writes it as markup to stderr.
    /// </summary>
    public static void MarkupError(IFormatProvider provider, string format, params object[] args)
    {
        var formatted = string.Format(provider, format, args);
        MarkupError(formatted);
    }

    /// <summary>
    /// Writes markup followed by a newline to stderr.
    /// </summary>
    public static void MarkupErrorLine(string markup)
    {
        MarkupError(markup);
        MarkupError(Environment.NewLine);
    }

    /// <summary>
    /// Formats text and writes it as markup followed by a newline to stderr.
    /// </summary>
    public static void MarkupErrorLine(IFormatProvider provider, string format, params object[] args)
    {
        var formatted = string.Format(provider, format, args);
        MarkupErrorLine(formatted);
    }

    /// <summary>
    /// Parses TigerCli bracket markup and returns it rendered as an ANSI SGR escape-sequence string
    /// (via <see cref="AnsiSink"/>), rather than writing to the console. Semantic tokens (e.g.
    /// <c>[Accent]</c>) are resolved through <paramref name="theme"/> when supplied, otherwise
    /// through <see cref="CurrentTheme"/>. The base style is plain (no foreground/background), so the
    /// result contains escape sequences only for colours introduced by the markup. Primarily useful
    /// for tests, docs, and generated examples.
    /// </summary>
    public static string MarkupToAnsi(string markup, ITheme? theme = null)
    {
        var writer = new StringWriter();
        var sink = new AnsiSink(writer);
        var styles = CreateMarkupStyleResolver(theme ?? CurrentTheme);

        foreach (var segment in CliMarkupParser.Parse(markup, baseStyle: null, styles, ColorAliases))
            sink.Write(segment);

        sink.Flush();
        return writer.ToString();
    }

    /// <summary>
    /// Parses TigerCli bracket markup and returns it rendered as a deterministic HTML string (via
    /// <see cref="HtmlSink"/>), rather than writing to the console. Semantic tokens (e.g. <c>[Heading]</c>)
    /// are resolved through <paramref name="theme"/> when supplied, otherwise through
    /// <see cref="CurrentTheme"/>; the resolved <see cref="CliCharStyle"/> is rendered (the original
    /// token name is not reconstructed). The base style is plain, so only colours/decorations introduced
    /// by the markup appear. Primarily useful for tests, docs, and generated examples.
    /// </summary>
    public static string MarkupToHtml(string markup, HtmlSinkOptions? options = null, ITheme? theme = null)
    {
        var writer = new StringWriter();
        var sink = new HtmlSink(writer, options);
        var styles = CreateMarkupStyleResolver(theme ?? CurrentTheme);

        foreach (var segment in CliMarkupParser.Parse(markup, baseStyle: null, styles, ColorAliases))
            sink.Write(segment);

        sink.Flush();
        return writer.ToString();
    }

    /// <summary>
    /// Converts a renderable component to a grid and renders it to stdout.
    /// </summary>
    public static void Render(CliRenderableComponent component)
    {
        var grid = component.ToGrid();
        RenderGrid(grid);
    }

    /// <summary>
    /// Converts a renderable component to a grid and returns its rendered plain-text lines.
    /// </summary>
    public static List<string> RenderToLines(CliRenderableComponent component)
    {
        var grid = component.ToGrid();
        return RenderGridToLines(grid);
    }

    /// <summary>
    /// Renders a component to a deterministic HTML string via <see cref="HtmlSink"/> — for snapshot
    /// tests and documentation examples. Opt-in; does not affect any console/ANSI/text output path.
    /// </summary>
    public static string RenderToHtml(CliRenderableComponent component, HtmlSinkOptions? options = null)
    {
        var grid = component.ToGrid();
        return RenderGridToHtml(grid, options);
    }
}
