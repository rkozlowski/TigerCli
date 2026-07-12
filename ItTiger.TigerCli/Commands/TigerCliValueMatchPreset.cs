namespace ItTiger.TigerCli.Commands;

/// <summary>
/// Controls how a supplied command-line value is matched against a provider-backed
/// option's / argument's choices during non-interactive validation and multi-select
/// resolution. In every case the <em>bound</em> value remains the provider's canonical
/// key/value — matching only decides <em>whether</em> a supplied value corresponds to a
/// choice; it never replaces the bound value with the raw user input.
/// </summary>
public enum TigerCliValueMatchPreset
{
    /// <summary>
    /// The default. String provider keys and labels match case-insensitively; no
    /// filesystem/path normalization is applied. Non-string keys keep type-safe equality
    /// (the previous behavior).
    /// </summary>
    Default = 0,

    /// <summary>
    /// Case-sensitive (ordinal) string matching with no normalization.
    /// </summary>
    Exact,

    /// <summary>
    /// Explicit case-insensitive (ordinal, ignore-case) string matching with no
    /// filesystem/path normalization. Behaves like <see cref="Default"/> for strings but
    /// documents the intent at the call site.
    /// </summary>
    IgnoreCase,

    /// <summary>
    /// Path-like matching for filesystem paths. Values are normalized and compared
    /// according to platform filesystem rules before matching. On Windows this is
    /// case-insensitive and normalizes drive-root spelling (<c>K:</c> ⇒ <c>K:\</c>),
    /// slash direction, and trailing separators; drive-relative (<c>K:rel</c>) and
    /// rooted-without-drive (<c>\rel</c>) forms are never widened into absolute paths.
    /// On non-Windows platforms a conservative case-sensitive comparison is used with no
    /// Windows drive rules.
    /// </summary>
    FileSystemPath
}
