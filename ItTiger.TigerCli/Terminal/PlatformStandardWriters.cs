using System;
using System.IO;
using System.Text;

namespace ItTiger.TigerCli.Terminal;

/// <summary>
/// Compatibility mechanism, Windows only: tells .NET's own standard writers apart from writers an app
/// or test host installed with <see cref="Console.SetOut"/> or <see cref="Console.SetError"/>.
/// </summary>
/// <remarks>
/// <para>
/// .NET has no documented way to ask whether <c>Console.Out</c> or <c>Console.Error</c> is still the
/// runtime's own writer, and TigerCli must never replace a writer someone installed. This class relies
/// on one runtime behavior that every .NET Core and .NET release has shown but that is not documented:
/// assigning <see cref="Console.OutputEncoding"/> discards the runtime's own writers (they are
/// recreated on next use) and leaves installed writers in place. Comparing the writer before and after
/// the assignment therefore identifies the runtime's writers.
/// </para>
/// <para>
/// The assignment is UTF-16, the one encoding .NET applies without calling <c>SetConsoleOutputCP</c>,
/// so probing never changes the console code page.
/// </para>
/// <para>
/// Guards: <c>ItTiger.TigerCli.Tests/ConsoleOutputEncodingTests.cs</c> pins that installed writers
/// survive the probe, and <c>CommandParserTest.Tests/RedirectedOutputEncodingTests.cs</c> pins that the
/// runtime's writers are recognized, by checking a real process's redirected output is UTF-8.
/// Fallback: if a future runtime stopped recreating its writers, every writer would be reported as
/// installed, TigerCli would keep the writers it found, and redirected output would stay in the
/// console code page (lossy outside it, never wrong bytes and never a replaced capture). The writers
/// are created before the assignment for exactly that reason: a writer the runtime fails to recreate
/// keeps the encoding it was created with instead of picking up UTF-16.
/// </para>
/// </remarks>
internal static class PlatformStandardWriters
{
    /// <summary>Which standard writers are the runtime's own (as opposed to installed by someone).</summary>
    internal readonly record struct Ownership(bool OutIsRuntimeWriter, bool ErrorIsRuntimeWriter);

    /// <summary>
    /// Switches <see cref="Console.OutputEncoding"/> to UTF-16 and reports which standard writers the
    /// runtime recreated, that is, which ones are its own.
    /// </summary>
    public static Ownership SwitchToUnicode()
    {
        var outBefore = Console.Out;
        var errorBefore = Console.Error;
        Console.OutputEncoding = Encoding.Unicode;
        return new Ownership(
            !ReferenceEquals(outBefore, Console.Out),
            !ReferenceEquals(errorBefore, Console.Error));
    }

    /// <summary>
    /// A writer for a redirected standard stream that encodes with <paramref name="encoding"/> and,
    /// like the runtime's own writers, writes no byte-order mark and flushes every write.
    /// </summary>
    public static TextWriter CreateWriter(Stream stream, Encoding encoding) =>
        new StreamWriter(stream, WithoutPreamble(encoding), bufferSize: 256, leaveOpen: true) { AutoFlush = true };

    private static Encoding WithoutPreamble(Encoding encoding) =>
        encoding.GetPreamble().Length == 0 ? encoding : encoding.CodePage switch
        {
            65001 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            1200 => new UnicodeEncoding(bigEndian: false, byteOrderMark: false),
            1201 => new UnicodeEncoding(bigEndian: true, byteOrderMark: false),
            _ => encoding
        };
}
