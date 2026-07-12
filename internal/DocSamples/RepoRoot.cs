namespace DocSamples;

/// <summary>
/// Locates the repository root (the directory containing <c>TigerCli.sln</c>) by walking up from
/// the executing assembly's base directory. Used by the generator to find <c>docs/examples/</c> and
/// by the drift test to find the committed artifacts.
/// </summary>
public static class RepoRoot
{
    public static string Locate()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TigerCli.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the repository root (no TigerCli.sln found above " + AppContext.BaseDirectory + ").");
    }
}
