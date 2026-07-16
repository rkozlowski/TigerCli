using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Tui.Abstractions;

namespace FolderCopy;

/// <summary>
/// The app factory. Program.cs and the app-boundary tests build the app through this one method, so
/// command registration, prompting policy, and metadata never drift between production and test runs
/// (see docs/guides/app-testing.md).
/// </summary>
/// <remarks>
/// Folder Copy is a single-default-command app: there is no command menu. The source and destination
/// folders are options (<c>-s|--source</c>, <c>-d|--destination</c>) so a missing value can be
/// resolved with the inline folder picker in semi-interactive mode and fails cleanly under
/// <c>--non-interactive</c>.
/// </remarks>
public static class FolderCopyApp
{
    /// <summary>
    /// Builds the app. Pass an explicit <paramref name="folderBrowser"/> to make the folder picker
    /// deterministic in tests; production passes <c>null</c> to use the real filesystem browser.
    /// </summary>
    public static TigerCliApp Create(IFolderBrowser? folderBrowser = null)
    {
        // App identity, display name, description, version, and project/repository links come from
        // FolderCopy.csproj and Version.props, and are imported here — the normal executable-app
        // pattern. See docs/guides/command-apps.md ("App Metadata").
        var builder = TigerCliApp.CreateBuilder()
            .UseAssemblyMetadata(typeof(FolderCopyApp).Assembly)
            .UseExitCodes(FolderCopyExitCode.Ok, FolderCopyExitCode.InternalError)
                .ExitKind(TigerCliExitKind.ValidationError, FolderCopyExitCode.ValidationError)
                .ExitKind(TigerCliExitKind.Cancelled, FolderCopyExitCode.Cancelled)
            .SetDefaultCommand<FolderCopyCommand>();

        if (folderBrowser is not null)
            builder.UseFolderBrowser(folderBrowser);

        return builder.Build();
    }
}
