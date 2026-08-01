using ItTiger.TigerCli.Commands;

namespace FileCopy;

/// <summary>Options for copying an existing file into a destination folder.</summary>
public sealed class CopyToFolderSettings : TigerCliSettings
{
    /// <summary>The existing source file.</summary>
    [TigerCliOption("-s|--source", Description = "Existing source file to copy.")]
    [TigerCliFileOpen(Filter = "*.*")]
    public string Source { get; set; } = string.Empty;

    /// <summary>The existing folder that receives the source file under its original name.</summary>
    [TigerCliOption("-d|--destination", Required = true, Description = "Destination folder that receives the source file.")]
    [TigerCliFolderSelect]
    public string DestinationFolder { get; set; } = string.Empty;
}
