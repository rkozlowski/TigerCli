using System.Text;
using System.Text.RegularExpressions;

namespace DocSamples;

/// <summary>
/// Generates <c>docs/reference/api-map.md</c> — a compact, committed index of the public TigerCli
/// type surface — from the DocFX ManagedReference metadata under <c>docs/api-docfx/api/</c>.
///
/// The map is generated output, never hand-maintained. Behavioral contracts live in XML docs, the
/// generated API reference, and the guides; this file is only a navigational index (namespaces →
/// types → kind → one-line summary → source path). Type names link to the published API page.
///
/// DocFX YAML shape used (per-type file <c>api/&lt;uid&gt;.yml</c>, first <c>items</c> entry is the
/// type itself; later entries are its members and are ignored):
///   <list type="bullet">
///     <item><c>name</c>      — short type name, with generic parameters (e.g. <c>Foo&lt;T&gt;</c>).</item>
///     <item><c>type</c>      — Class / Struct / Interface / Enum / Delegate (Namespace is skipped).</item>
///     <item><c>namespace</c> — grouping key.</item>
///     <item><c>summary</c>   — folded scalar; xref tags are reduced to their target's short name
///                              and only the first sentence is kept.</item>
///     <item><c>source.remote.path</c> — repo-relative source file path.</item>
///   </list>
/// The published API page URL is derived from the YAML file name (DocFX names the HTML page
/// identically), and is only emitted when the generated page actually exists under <c>_site/</c>.
/// </summary>
public static class DocApiMap
{
    /// <summary>Repo-relative path of the generated map.</summary>
    public const string RelativePath = "docs/reference/api-map.md";

    private const string PublishedApiBaseUrl = "https://rkozlowski.github.io/TigerCli/api/";
    private const string RepositoryBlobBaseUrl = "https://github.com/rkozlowski/TigerCli/blob/main/";

    private static readonly string[] TypeKinds =
        { "Class", "Struct", "Interface", "Enum", "Delegate" };

    /// <summary>
    /// Builds the full Markdown content of the API map from the DocFX metadata under
    /// <paramref name="repoRoot"/>. Deterministic and LF-only.
    /// </summary>
    public static string Generate(string repoRoot)
    {
        var apiDir = Path.Combine(repoRoot, "docs", "api-docfx", "api");
        if (!Directory.Exists(apiDir))
            throw new DirectoryNotFoundException(
                $"DocFX metadata not found at {apiDir}. Run: dotnet docfx docs/api-docfx/docfx.json");

        var siteApiDir = Path.Combine(repoRoot, "docs", "api-docfx", "_site", "api");

        var types = new List<TypeEntry>();
        foreach (var file in Directory.EnumerateFiles(apiDir, "*.yml"))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            if (string.Equals(stem, "toc", StringComparison.OrdinalIgnoreCase))
                continue;

            var entry = TryParse(File.ReadAllText(file), stem, siteApiDir);
            if (entry is not null)
                types.Add(entry);
        }

        var builder = new StringBuilder();
        builder.Append("# TigerCli API Map\n\n");
        builder.Append("<!-- Generated from DocFX metadata by internal/DocSamples (`api-map` mode). Do not edit by hand. -->\n\n");
        builder.Append("Generated from DocFX metadata. Do not edit by hand.\n\n");
        builder.Append(
            "This is a structured index of public TigerCli types for humans and AI tools. Type names " +
            "link to the published DocFX reference; source paths link to the repository. Behavioral " +
            "contracts live in XML documentation comments, the generated API reference, and the guides.\n\n");
        builder.Append("**Coverage:** ").Append(types.Count).Append(" public types across ")
            .Append(types.Select(t => t.Namespace).Distinct(StringComparer.Ordinal).Count())
            .Append(" namespaces.\n\n");
        builder.Append("Regenerate with:\n\n");
        builder.Append("```\n");
        builder.Append("dotnet docfx docs/api-docfx/docfx.json\n");
        builder.Append("dotnet run --project internal/DocSamples -- api-map\n");
        builder.Append("```\n");

        foreach (var group in types
                     .GroupBy(t => t.Namespace, StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            builder.Append("\n## ").Append(group.Key).Append('\n').Append('\n');
            builder.Append("| Public type | Kind | Summary | Source |\n");
            builder.Append("|---|---|---|---|\n");

            foreach (var type in group.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                if (type.ApiPage is not null)
                    builder.Append("| [").Append(EscapeMarkdownCell(type.Name)).Append("](")
                        .Append(type.ApiPage).Append(") ");
                else
                    builder.Append("| `").Append(EscapeMarkdownCell(type.Name)).Append("` ");

                builder.Append("| `").Append(type.Kind).Append("` | ")
                    .Append(EscapeMarkdownCell(type.Summary)).Append(" | ");

                if (type.SourcePath == "(unknown)")
                    builder.Append("`(unknown)`");
                else
                    builder.Append("[`").Append(EscapeMarkdownCell(type.SourcePath)).Append("`](")
                        .Append(RepositoryBlobBaseUrl).Append(EscapeUrlPath(type.SourcePath)).Append(')');

                builder.Append(" |\n");
            }
        }

        return builder.ToString();
    }

    /// <summary>Writes the map to <c>docs/reference/api-map.md</c> (UTF-8 without BOM, LF).</summary>
    public static void Write(string repoRoot)
    {
        var path = Path.Combine(repoRoot, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(path, Generate(repoRoot), utf8NoBom);
        Console.WriteLine($"wrote {RelativePath}");
    }

    /// <summary>
    /// Returns <c>true</c> if the committed map matches freshly generated output (newline-normalized),
    /// otherwise reports drift and returns <c>false</c>.
    /// </summary>
    public static bool Check(string repoRoot)
    {
        var path = Path.Combine(repoRoot, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var generated = Generate(repoRoot).Replace("\r\n", "\n");
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"MISSING {RelativePath}");
            return false;
        }

        var committed = File.ReadAllText(path).Replace("\r\n", "\n");
        if (!string.Equals(committed, generated, StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                $"DRIFT   {RelativePath} (regenerate with: dotnet run --project internal/DocSamples -- api-map)");
            return false;
        }

        Console.WriteLine($"ok      {RelativePath}");
        return true;
    }

    private sealed record TypeEntry(
        string Name, string Kind, string Namespace, string Summary, string SourcePath, string? ApiPage);

    // The first `- uid:` entry in a ManagedReference file is the type; the rest are members.
    private static readonly Regex ItemSplit = new(@"(?m)^- uid:", RegexOptions.Compiled);
    private static readonly Regex NameField = new(@"(?m)^  name:\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex TypeField = new(@"(?m)^  type:\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex NamespaceField = new(@"(?m)^  namespace:\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex RemotePath = new(@"(?m)^    remote:\s*\n(?:^      \w+:.*\n)*?^      path:\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex SourcePathFallback = new(@"(?m)^    path:\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex XrefTag = new("<xref href=\"([^\"]+)\"[^>]*>\\s*</xref>", RegexOptions.Compiled);
    private static readonly Regex AnyTag = new("<[^>]+>", RegexOptions.Compiled);

    private static TypeEntry? TryParse(string yaml, string stem, string siteApiDir)
    {
        var parts = ItemSplit.Split(yaml);
        if (parts.Length < 2)
            return null;

        // parts[0] is the file header; parts[1] is the type item (up to the first member, which
        // begins the next split). Only the fields before the first member matter, so slicing to the
        // start of parts[2..] is unnecessary — the field regexes take their first match, which is
        // always the type's.
        var item = parts[1];

        var kind = TypeField.Match(item) is { Success: true } tm ? tm.Groups[1].Value : null;
        if (kind is null || Array.IndexOf(TypeKinds, kind) < 0)
            return null; // Namespace files and anything unexpected are skipped.

        var name = NameField.Match(item) is { Success: true } nm ? nm.Groups[1].Value : stem;
        var ns = NamespaceField.Match(item) is { Success: true } nsm ? nsm.Groups[1].Value : "(global)";

        var sourcePath = RemotePath.Match(item) is { Success: true } rp
            ? rp.Groups[1].Value
            : NormalizeSourcePath(SourcePathFallback.Match(item));

        var summary = ExtractSummary(item);

        string? apiPage = null;
        if (File.Exists(Path.Combine(siteApiDir, stem + ".html")))
            apiPage = PublishedApiBaseUrl + stem + ".html";

        return new TypeEntry(name, kind.ToLowerInvariant(), ns, summary, sourcePath, apiPage);
    }

    private static string NormalizeSourcePath(Match fallback)
    {
        if (!fallback.Success)
            return "(unknown)";
        // DocFX emits `../../<repo-relative>` for source.path; strip leading up-dir hops.
        var value = fallback.Groups[1].Value;
        while (value.StartsWith("../", StringComparison.Ordinal))
            value = value[3..];
        return value;
    }

    private static string ExtractSummary(string item)
    {
        var lines = item.Split('\n');
        int start = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("  summary:", StringComparison.Ordinal))
            {
                start = i;
                break;
            }
        }

        if (start < 0)
            return "No summary yet.";

        var body = new StringBuilder();

        // `summary:` carries its value inline for short summaries (`summary: One sentence.`) and as a
        // folded/literal block (`summary: >-`) for multi-line ones. Anything after the colon that is
        // not a block indicator is the inline value.
        var inline = lines[start]["  summary:".Length..].Trim();
        if (inline.Length > 0 && inline[0] is not ('>' or '|'))
        {
            body.Append(inline).Append(' ');
        }
        else
        {
            // Folded scalar body: subsequent lines indented deeper than the `  summary:` key
            // (>= 4 spaces) or blank; stops at the next 2-space key such as `  example:`.
            for (int i = start + 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length == 0 || string.IsNullOrWhiteSpace(line))
                {
                    body.Append(' ');
                    continue;
                }

                if (!line.StartsWith("    ", StringComparison.Ordinal))
                    break; // next key at the item level

                body.Append(line.Trim()).Append(' ');
            }
        }

        var text = body.ToString();
        text = XrefTag.Replace(text, m => ShortName(m.Groups[1].Value));
        text = AnyTag.Replace(text, string.Empty);
        text = DecodeEntities(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();

        if (text.Length == 0)
            return "No summary yet.";

        return FirstSentence(text);
    }

    // xref uids point at a member/type; the last dotted segment reads well inline (backtick-arity and
    // method parameter lists trimmed).
    private static string ShortName(string uid)
    {
        uid = Uri.UnescapeDataString(uid);
        var paren = uid.IndexOf('(');
        if (paren >= 0)
            uid = uid[..paren];
        var dot = uid.LastIndexOf('.');
        var name = dot >= 0 ? uid[(dot + 1)..] : uid;
        var tick = name.IndexOf('`');
        return tick >= 0 ? name[..tick] : name;
    }

    private static string FirstSentence(string text)
    {
        for (int i = 0; i < text.Length - 1; i++)
        {
            if (text[i] == '.' && text[i + 1] == ' ')
            {
                var throughPeriod = text.AsSpan(0, i + 1);
                if (throughPeriod.EndsWith("e.g.", StringComparison.OrdinalIgnoreCase) ||
                    throughPeriod.EndsWith("i.e.", StringComparison.OrdinalIgnoreCase))
                    continue;

                return text[..(i + 1)];
            }
        }

        return text;
    }

    private static string EscapeMarkdownCell(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

    private static string EscapeUrlPath(string path) =>
        string.Join('/', path.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));

    private static string DecodeEntities(string text) => text
        .Replace("&lt;", "<")
        .Replace("&gt;", ">")
        .Replace("&quot;", "\"")
        .Replace("&#39;", "'")
        .Replace("&amp;", "&");
}
