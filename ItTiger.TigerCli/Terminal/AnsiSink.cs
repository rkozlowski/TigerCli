using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using System;
using System.Collections.Generic;
using System.IO;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// An <see cref="ICliRenderSink"/> that writes styled output as ANSI SGR escape sequences to a
/// <see cref="TextWriter"/>. Unlike <see cref="ConsoleSink"/> — which degrades ANSI 16–255 colours
/// to the nearest <see cref="System.ConsoleColor"/> — <see cref="AnsiSink"/> renders the full
/// 0–255 palette faithfully (see <see cref="AnsiSgr"/> for the mapping rules).
/// <para>Style handling is deterministic and diffed: an escape sequence is emitted only when the
/// foreground, background, or text decorations change; all changed attributes coalesce into a single
/// sequence; and a <c>null</c> colour channel resolves to the ANSI default (<c>39</c>/<c>49</c>)
/// rather than "leave as-is". Decoration flags emit attribute on/off codes (bold 1/22, italic 3/23,
/// underline 4/24) diffed against the previous style. A reset (<see cref="AnsiSgr.Reset"/>) — which
/// also clears decorations — is emitted before each newline and on flush whenever a style is active,
/// so a styled background never bleeds past the line.</para>
/// </summary>
public sealed class AnsiSink : ICliRenderSink
{
    private readonly TextWriter _writer;
    private readonly bool _emitHyperlinks;
    private readonly bool _emitTerminalControls;

    // Tracks the style currently in effect in the output stream. Null colours and None decorations
    // mean the stream is at the terminal default (no active style), so no trailing reset is needed.
    private CliColor? _fg;
    private CliColor? _bg;
    private CliTextDecoration _decorations;
    private bool _styleActive;

    // The OSC 8 hyperlink target currently open in the stream (sanitized), or null when none is open.
    private string? _hyperlink;

    /// <summary>
    /// Creates an ANSI sink. When <paramref name="emitHyperlinks"/> is <c>true</c>, text runs carrying
    /// a <see cref="CliCharStyle.HyperlinkTarget"/> are wrapped in OSC 8 hyperlink sequences (the
    /// visible text is always written regardless). Defaults to <c>false</c> so existing direct callers
    /// (and tests) emit no hyperlink sequences unless they opt in; <see cref="ConsoleSinkFactory"/>
    /// sets it from <see cref="TigerConsole.HyperlinkMode"/>.
    /// </summary>
    public AnsiSink(TextWriter writer, bool emitHyperlinks = false, bool emitTerminalControls = true)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _emitHyperlinks = emitHyperlinks;
        _emitTerminalControls = emitTerminalControls;
    }

    /// <inheritdoc/>
    public int? SoftMaxWidth => null;
    /// <inheritdoc/>
    public int? SoftMaxHeight => null;
    /// <inheritdoc/>
    public int? MaxWidth => null;
    /// <inheritdoc/>
    public int? MaxHeight => null;

    /// <summary>Writes a styled text segment using ANSI sequences where required.</summary>
    /// <param name="segment">The styled text segment to write.</param>
    public void Write(CliTextSegment segment)
    {
        var fg = segment.Style.Foreground;
        var bg = segment.Style.Background;
        var decorations = segment.Style.Decorations;

        bool fgChanged = fg != _fg;
        bool bgChanged = bg != _bg;
        bool decorationsChanged = decorations != _decorations;

        if (fgChanged || bgChanged || decorationsChanged)
        {
            var parts = new List<string>(4);
            // Decoration on/off codes first, then colour codes — all coalesced into one sequence.
            if (decorationsChanged) AnsiSgr.AppendDecorationParams(parts, _decorations, decorations);
            if (fgChanged) parts.Add(AnsiSgr.ForegroundParamsOrDefault(fg));
            if (bgChanged) parts.Add(AnsiSgr.BackgroundParamsOrDefault(bg));

            _writer.Write(AnsiSgr.BuildSgr(parts));

            _fg = fg;
            _bg = bg;
            _decorations = decorations;
            _styleActive = fg.HasValue || bg.HasValue || decorations != CliTextDecoration.None;
        }

        UpdateHyperlink(segment.Style.HyperlinkTarget);

        _writer.Write(segment.Text);
    }

    // Diffs the hyperlink target like the SGR attributes: a change closes the open link (if any) and
    // opens the new one (if any), so contiguous same-target segments stay one continuous link. No-op
    // when hyperlink emission is disabled. The target is sanitized before it enters the OSC 8 sequence.
    private void UpdateHyperlink(string? rawTarget)
    {
        if (!_emitHyperlinks)
            return;

        var sanitized = AnsiSgr.SanitizeHyperlinkTarget(rawTarget);
        var desired = string.IsNullOrEmpty(sanitized) ? null : sanitized;

        if (desired == _hyperlink)
            return;

        if (_hyperlink is not null)
            _writer.Write(AnsiSgr.Osc8Close);
        if (desired is not null)
            _writer.Write(AnsiSgr.Osc8Open(desired));

        _hyperlink = desired;
    }

    /// <summary>Resets active ANSI state and writes a line terminator.</summary>
    public void NewLine()
    {
        ResetIfActive();
        _writer.WriteLine();
    }

    /// <summary>Resets active ANSI state and flushes the underlying writer.</summary>
    public void Flush()
    {
        ResetIfActive();
        _writer.Flush();
    }

    /// <summary>Closes any active hyperlink and resets active ANSI styling.</summary>
    public void Reset()
    {
        ResetIfActive();
    }

    /// <summary>
    /// Writes and flushes an ANSI window-title control sequence when terminal controls are enabled.
    /// </summary>
    /// <param name="title">The window title to set.</param>
    public void SetWindowTitle(string title)
    {
        if (!_emitTerminalControls)
            return;

        ResetIfActive();
        _writer.Write(AnsiSgr.SetWindowTitle(title));
        _writer.Flush();
    }

    // Closes any open hyperlink and emits a reset when a style is active, then returns the tracked
    // state to the default. Closing the hyperlink here ensures a link never bleeds past a newline,
    // flush, or explicit reset; the next styled segment re-opens it from the carried style if needed.
    private void ResetIfActive()
    {
        if (_hyperlink is not null)
        {
            _writer.Write(AnsiSgr.Osc8Close);
            _hyperlink = null;
        }

        if (_styleActive)
            _writer.Write(AnsiSgr.Reset);

        _fg = null;
        _bg = null;
        _decorations = CliTextDecoration.None;
        _styleActive = false;
    }
}
