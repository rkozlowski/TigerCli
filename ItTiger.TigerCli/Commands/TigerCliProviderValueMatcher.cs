namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Central matching/normalization logic for provider-backed values. This is the single
/// source of truth shared by non-interactive single-select validation, prompt
/// preselection, and multi-select token resolution — the comparison and normalization
/// rules must not be duplicated across those paths.
/// </summary>
/// <remarks>
/// A match compares the supplied value against each choice's key (string form) and label
/// using the option/argument's <see cref="TigerCliValueMatchPreset"/>. On a match the
/// <em>provider key</em> is returned, so callers can bind the canonical value rather than
/// the raw user input.
/// </remarks>
internal static class TigerCliProviderValueMatcher
{
    /// <summary>
    /// Returns the provider key of the first choice that matches <paramref name="value"/>
    /// under <paramref name="preset"/>, or <c>null</c> when nothing matches.
    /// </summary>
    public static object? FindKey(
        IReadOnlyList<TigerCliPromptChoice> choices,
        object value,
        TigerCliValueMatchPreset preset)
    {
        var index = FindIndex(choices, value, preset);
        return index is int i ? choices[i].Key : null;
    }

    /// <summary>
    /// Returns the index of the first choice that matches <paramref name="value"/> under
    /// <paramref name="preset"/>, or <c>null</c> when nothing matches.
    /// </summary>
    public static int? FindIndex(
        IReadOnlyList<TigerCliPromptChoice> choices,
        object value,
        TigerCliValueMatchPreset preset = TigerCliValueMatchPreset.Default)
    {
        for (var i = 0; i < choices.Count; i++)
        {
            if (Matches(choices[i], value, preset))
                return i;
        }

        return null;
    }

    private static bool Matches(TigerCliPromptChoice choice, object value, TigerCliValueMatchPreset preset)
    {
        // String inputs (all multi-select tokens, and string-typed single-select values) are
        // matched against the key's string form and the label, honoring the preset.
        if (value is string text)
        {
            return StringMatches(choice.Key?.ToString(), text, preset)
                || StringMatches(choice.Label, text, preset);
        }

        // Non-string (typed) single-select values keep type-safe equality. Presets and label
        // matching do not apply here so typed behavior is unchanged.
        return Equals(choice.Key, value)
            || string.Equals(choice.Key?.ToString(), value.ToString(), StringComparison.Ordinal);
    }

    private static bool StringMatches(string? candidate, string input, TigerCliValueMatchPreset preset)
    {
        if (candidate == null)
            return false;

        return preset switch
        {
            TigerCliValueMatchPreset.Exact => string.Equals(candidate, input, StringComparison.Ordinal),
            TigerCliValueMatchPreset.FileSystemPath => FileSystemPathsMatch(candidate, input),
            // Default and IgnoreCase share case-insensitive ordinal comparison.
            _ => string.Equals(candidate, input, StringComparison.OrdinalIgnoreCase),
        };
    }

    internal static bool FileSystemPathsMatch(string candidate, string input) =>
        FileSystemPathsMatch(candidate, input, OperatingSystem.IsWindows());

    // Platform is a parameter so the two normalization regimes are directly testable on any host.
    internal static bool FileSystemPathsMatch(string candidate, string input, bool windows) =>
        windows
            ? string.Equals(NormalizeWindowsPath(candidate), NormalizeWindowsPath(input), StringComparison.Ordinal)
            : string.Equals(NormalizeUnixPath(candidate), NormalizeUnixPath(input), StringComparison.Ordinal);

    /// <summary>
    /// Produces a canonical, lower-cased comparison form of a Windows path. Distinct path
    /// categories (drive root, absolute drive path, drive-relative, rooted-without-drive)
    /// normalize to distinct forms so that, e.g., <c>K:xxx</c> can never equal <c>K:\xxx</c>
    /// and <c>\</c> can never equal <c>C:\</c>.
    /// </summary>
    private static string NormalizeWindowsPath(string path)
    {
        // Slash direction is insignificant on Windows.
        var p = path.Replace('/', '\\');

        // Drive-qualified paths: "<letter>:...".
        if (p.Length >= 2 && IsDriveLetter(p[0]) && p[1] == ':')
        {
            var drive = char.ToLowerInvariant(p[0]);
            var rest = p.Substring(2);

            // Bare drive spec "K:" is understood as the drive root "K:\".
            if (rest.Length == 0)
                return drive + ":\\";

            // Absolute drive path "K:\..." (or "K:/..."). Collapse the leading/trailing
            // separators to a single canonical root; a bare "K:\" collapses back to the root.
            if (rest[0] == '\\')
            {
                var body = rest.Trim('\\').ToLowerInvariant();
                return body.Length == 0 ? drive + ":\\" : drive + ":\\" + body;
            }

            // Drive-relative "K:xxx" — current-directory-of-drive relative. It must NOT be
            // widened into the absolute "K:\xxx", so it keeps a distinct separator-free form.
            return drive + ":" + rest.TrimEnd('\\').ToLowerInvariant();
        }

        // Non-drive paths (UNC, rooted-without-drive "\foo", relative "foo\bar"). A rooted path
        // without a drive is current-drive-relative; TigerCli must not guess the current drive,
        // so it is left in its own category and can never equal a drive-rooted path.
        var trimmed = p.TrimEnd('\\');
        if (trimmed.Length == 0)
            trimmed = "\\";
        return trimmed.ToLowerInvariant();
    }

    private static string NormalizeUnixPath(string path)
    {
        // Conservative: case-sensitive, no drive rules. Only collapse a trailing separator.
        var trimmed = path.Length > 1 ? path.TrimEnd('/') : path;
        return trimmed.Length == 0 ? "/" : trimmed;
    }

    private static bool IsDriveLetter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
}
