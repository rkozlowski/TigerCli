using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Tui.Abstractions;

namespace FileCopy;

/// <summary>Builds the File Copy sample app used by both the executable and app-boundary tests.</summary>
public static class FileCopyApp
{
    /// <summary>
    /// Creates the menu-driven app. Tests may supply a folder browser to control folder navigation;
    /// production uses TigerCli's local filesystem browser.
    /// </summary>
    public static TigerCliApp Create(IFolderBrowser? folderBrowser = null)
    {
        var builder = TigerCliApp.CreateBuilder()
            .UseAssemblyMetadata(typeof(FileCopyApp).Assembly)
            .UseExitCodes(FileCopyExitCode.Ok, FileCopyExitCode.InternalError)
                .ExitKind(TigerCliExitKind.ValidationError, FileCopyExitCode.ValidationError)
                .ExitKind(TigerCliExitKind.InteractiveNotAllowed, FileCopyExitCode.InteractiveNotAllowed)
                .ExitKind(TigerCliExitKind.Cancelled, FileCopyExitCode.Cancelled)
            .UseCommandMenu(CommandMenuMode.Enabled)
            .AddCommand<CopyToFolderCommand>(
                "copy-to-folder",
                "Copies one existing file into a selected destination folder.")
            .AddCommand<CopyToFileCommand>(
                "copy-to-file",
                "Copies one existing file to a selected or newly entered output file path.");

        if (folderBrowser is not null)
            builder.UseFolderBrowser(folderBrowser);

        return builder.Build();
    }
}
