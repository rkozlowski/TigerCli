using ItTiger.TigerCli.Commands;
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
        // App identity (command name from <AssemblyName>folder-copy</AssemblyName>) and description
        // (from <Description>) live in FolderCopy.csproj and are imported here, the preferred pattern
        // for a normal executable app. See docs/guides/command-apps.md ("App Metadata"). This app has
        // no version, so version output stays off (enableVersion: false).
        var builder = TigerCliApp.CreateBuilder()
            .UseAssemblyMetadata(typeof(FolderCopyApp).Assembly, enableVersion: false)
            .SetDefaultCommand<FolderCopyCommand>();

        if (folderBrowser is not null)
            builder.UseFolderBrowser(folderBrowser);

        return builder.Build();
    }
}
