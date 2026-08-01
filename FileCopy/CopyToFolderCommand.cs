using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

namespace FileCopy;

/// <summary>Copies one existing file into a selected folder under its original file name.</summary>
public sealed class CopyToFolderCommand : TigerCliAsyncCommandHandler<CopyToFolderSettings, FileCopyExitCode>
{
    /// <inheritdoc/>
    public override async Task<FileCopyExitCode> ExecuteAsync(CopyToFolderSettings settings)
    {
        var source = settings.Source;
        var destinationFolder = settings.DestinationFolder;
        if (!Directory.Exists(destinationFolder))
        {
            TigerConsole.MarkupErrorLine(settings.E(
                "[Error]Destination folder does not exist:[/] {0}", destinationFolder));
            return FileCopyExitCode.CopyFailed;
        }

        var destination = Path.Combine(destinationFolder, Path.GetFileName(source));
        return await FileCopyCommandRunner.RunAsync(
            settings,
            source,
            destination,
            overwrite: false).ConfigureAwait(false);
    }
}
