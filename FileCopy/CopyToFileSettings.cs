using ItTiger.TigerCli.Commands;

namespace FileCopy;

/// <summary>Options for copying an existing file to an explicit output file path.</summary>
public sealed class CopyToFileSettings : TigerCliSettings
{
    /// <summary>The existing source file.</summary>
    [TigerCliOption("-s|--source", Description = "Existing source file to copy.")]
    [TigerCliFileOpen(Filter = "*.*")]
    public string Source { get; set; } = string.Empty;

    /// <summary>The selected or newly entered destination file.</summary>
    [TigerCliOption("-d|--destination", Description = "Output file path.")]
    [TigerCliFileSave(
        Filter = "*.*",
        Overwrite = TigerCliFileOverwrite.Prompt,
        OverwriteWhenOption = nameof(Force))]
    public string DestinationFile { get; set; } = string.Empty;

    /// <summary>Allows an existing destination file to be overwritten without confirmation.</summary>
    [TigerCliOption("-f|--force", Description = "Overwrite an existing destination without prompting.")]
    public bool Force { get; set; }
}
