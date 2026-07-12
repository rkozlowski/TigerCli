using ItTiger.TigerCli.Commands;

namespace FolderCopy;

/// <summary>
/// The Folder Copy command surface: a source folder and a destination folder.
/// </summary>
/// <remarks>
/// Both are <see cref="TigerCliOptionAttribute"/> options carrying
/// <see cref="TigerCliFolderSelectAttribute"/>, so a missing value is resolved with the inline folder
/// picker in semi-interactive mode and reported as a clean "missing required option" under
/// <c>--non-interactive</c>. They are options (not positional arguments) because the folder picker is
/// an option-level prompt; the trade-off is that the CLI uses the named forms
/// (<c>-s</c>/<c>--source</c>, <c>-d</c>/<c>--destination</c>) rather than bare positional paths.
/// </remarks>
public sealed class FolderCopySettings : TigerCliSettings
{
    [TigerCliOption("-s|--source", Required = true, Description = "Source folder to copy from.")]
    [TigerCliFolderSelect]
    public string? Source { get; set; }

    [TigerCliOption("-d|--destination", Required = true, Description = "Destination folder to copy into.")]
    [TigerCliFolderSelect]
    public string? Destination { get; set; }
}
