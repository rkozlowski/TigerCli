using ItTiger.TigerCli.Commands;

namespace FileCopy;

/// <summary>Copies one existing file to an explicit output file selected through the save prompt.</summary>
public sealed class CopyToFileCommand : TigerCliAsyncCommandHandler<CopyToFileSettings, FileCopyExitCode>
{
    /// <inheritdoc/>
    public override Task<FileCopyExitCode> ExecuteAsync(CopyToFileSettings settings) =>
        FileCopyCommandRunner.RunAsync(
            settings,
            settings.Source,
            settings.DestinationFile,
            overwrite: true);
}
