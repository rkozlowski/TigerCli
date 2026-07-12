using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using System.Collections.Generic;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// Pure ANSI SGR (Select Graphic Rendition) helpers used by <see cref="AnsiSink"/>. Turns a
/// <see cref="CliColor"/> into the escape-sequence parameters that render it faithfully.
/// <para><b>0–15:</b> <see cref="CliColor"/> values 0–15 follow <see cref="System.ConsoleColor"/>
/// order, which is <i>not</i> the ANSI palette order (ANSI index 1 is red, but
/// <see cref="CliColor.DarkBlue"/> is 1). Emitting <c>38;5;&lt;index&gt;</c> for these would render
/// the wrong colour, so they are remapped explicitly to classic 16-colour SGR codes.</para>
/// <para><b>16–255:</b> a <see cref="CliColor"/> value equals its ANSI palette index, so these emit
/// <c>38;5;&lt;n&gt;</c> (foreground) / <c>48;5;&lt;n&gt;</c> (background) directly.</para>
/// </summary>
internal static class AnsiSgr
{
    /// <summary>The ASCII escape character (<c>0x1B</c>) that introduces CSI and OSC sequences.</summary>
    public const string Esc = "\u001b";

    /// <summary>The ASCII bell character (<c>0x07</c>) used here to terminate OSC title sequences.</summary>
    public const string Bel = "\u0007";

    /// <summary>Resets all attributes to the terminal default.</summary>
    public const string Reset = Esc + "[0m";

    /// <summary>SGR parameter selecting the default foreground colour.</summary>
    public const int DefaultForeground = 39;

    /// <summary>SGR parameter selecting the default background colour.</summary>
    public const int DefaultBackground = 49;

    // Text-decoration SGR parameters. On-codes turn an attribute on; off-codes turn just that one
    // attribute off (without disturbing colours or the other attributes).
    public const int BoldOn = 1, BoldOff = 22;
    public const int ItalicOn = 3, ItalicOff = 23;
    public const int UnderlineOn = 4, UnderlineOff = 24;

    // Classic 16-colour foreground SGR codes indexed by (int)CliColor for 0–15 (ConsoleColor
    // order). The dark/standard set maps into 30–37 and the bright set into 90–97, both remapped
    // from ConsoleColor order to ANSI hue order. Background codes are these values + 10.
    private static readonly int[] StandardForeground =
    {
        30, // 0  Black
        34, // 1  DarkBlue
        32, // 2  DarkGreen
        36, // 3  DarkCyan
        31, // 4  DarkRed
        35, // 5  DarkMagenta
        33, // 6  DarkYellow
        37, // 7  Gray
        90, // 8  DarkGray
        94, // 9  Blue
        92, // 10 Green
        96, // 11 Cyan
        91, // 12 Red
        95, // 13 Magenta
        93, // 14 Yellow
        97, // 15 White
    };

    /// <summary>Foreground SGR parameters for a concrete colour.</summary>
    public static string ForegroundParams(CliColor color)
    {
        int i = (int)color;
        return i < CliColorPalette.StandardColorCount
            ? StandardForeground[i].ToString()
            : $"38;5;{i}";
    }

    /// <summary>Background SGR parameters for a concrete colour.</summary>
    public static string BackgroundParams(CliColor color)
    {
        int i = (int)color;
        return i < CliColorPalette.StandardColorCount
            ? (StandardForeground[i] + 10).ToString()
            : $"48;5;{i}";
    }

    /// <summary>
    /// Foreground SGR parameters for a nullable colour; <c>null</c> selects the ANSI default
    /// foreground (<see cref="DefaultForeground"/>).
    /// </summary>
    public static string ForegroundParamsOrDefault(CliColor? color)
        => color.HasValue ? ForegroundParams(color.Value) : DefaultForeground.ToString();

    /// <summary>
    /// Background SGR parameters for a nullable colour; <c>null</c> selects the ANSI default
    /// background (<see cref="DefaultBackground"/>).
    /// </summary>
    public static string BackgroundParamsOrDefault(CliColor? color)
        => color.HasValue ? BackgroundParams(color.Value) : DefaultBackground.ToString();

    /// <summary>
    /// Appends the SGR parameters needed to transition the decoration attributes from
    /// <paramref name="previous"/> to <paramref name="next"/>: each newly added flag emits its
    /// on-code, each removed flag its off-code. Flags unchanged between the two emit nothing.
    /// </summary>
    public static void AppendDecorationParams(
        List<string> parts, CliTextDecoration previous, CliTextDecoration next)
    {
        var added = next & ~previous;
        var removed = previous & ~next;

        if ((added & CliTextDecoration.Bold) != 0) parts.Add(BoldOn.ToString());
        if ((removed & CliTextDecoration.Bold) != 0) parts.Add(BoldOff.ToString());
        if ((added & CliTextDecoration.Italic) != 0) parts.Add(ItalicOn.ToString());
        if ((removed & CliTextDecoration.Italic) != 0) parts.Add(ItalicOff.ToString());
        if ((added & CliTextDecoration.Underline) != 0) parts.Add(UnderlineOn.ToString());
        if ((removed & CliTextDecoration.Underline) != 0) parts.Add(UnderlineOff.ToString());
    }

    /// <summary>Builds a single SGR escape sequence from the supplied parameter parts.</summary>
    public static string BuildSgr(IReadOnlyList<string> parts)
        => $"{Esc}[{string.Join(';', parts)}m";

    /// <summary>
    /// Builds an OSC 0 window-title sequence: <c>ESC ] 0 ; title BEL</c>. The title is sanitized before
    /// emission so app-provided text cannot terminate or break out of the control sequence.
    /// </summary>
    public static string SetWindowTitle(string title)
        => $"{Esc}]0;{SanitizeControlString(title)}{Bel}";

    // ---- OSC 8 hyperlinks ----

    /// <summary>String Terminator (ST) that ends an OSC sequence: <c>ESC \</c>.</summary>
    public const string St = Esc + "\\";

    /// <summary>Closes an open OSC 8 hyperlink: <c>ESC ] 8 ; ; ST</c>.</summary>
    public const string Osc8Close = Esc + "]8;;" + St;

    /// <summary>
    /// Opens an OSC 8 hyperlink to <paramref name="uri"/> (no params): <c>ESC ] 8 ; ; URI ST</c>.
    /// Callers must pass a sanitized URI (see <see cref="SanitizeHyperlinkTarget"/>).
    /// </summary>
    public static string Osc8Open(string uri) => $"{Esc}]8;;{uri}{St}";

    /// <summary>
    /// Sanitizes a hyperlink target for safe inclusion in an OSC 8 sequence: strips control
    /// characters (C0 including <c>ESC</c>, DEL, and C1) so a malformed value cannot terminate or
    /// break out of the escape sequence. Returns the empty string for a <c>null</c>/empty input. Only
    /// the emitted target is affected — the visible text is never changed.
    /// </summary>
    public static string SanitizeHyperlinkTarget(string? target)
        => SanitizeControlString(target);

    /// <summary>
    /// Strips C0 controls (including ESC and BEL), DEL, and C1 controls from an OSC/control payload.
    /// Returns the empty string for a <c>null</c>/empty input.
    /// </summary>
    public static string SanitizeControlString(string? target)
    {
        if (string.IsNullOrEmpty(target))
            return string.Empty;

        var sb = new System.Text.StringBuilder(target.Length);
        foreach (var ch in target)
        {
            if (char.IsControl(ch) || ch == '\u007f' || (ch >= '\u0080' && ch <= '\u009f'))
                continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
