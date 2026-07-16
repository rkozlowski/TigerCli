using ItTiger.Core;

namespace FolderCopy;

/// <summary>
/// The application-wide exit-code contract for Folder Copy. Command outcomes and framework failures
/// are mapped to these documented values by <see cref="FolderCopyApp.Create"/>.
/// </summary>
[TigerText("folder-copy exit codes")]
public enum FolderCopyExitCode
{
    /// <summary>Copy completed successfully.</summary>
    [TigerText("OK", Description = "Copy completed successfully.")]
    Ok = 0,

    /// <summary>The copy could not be completed.</summary>
    [TigerText("Copy failed", Description = "The source could not be scanned or its files could not be copied.")]
    CopyFailed = 1,

    /// <summary>The copy operation was cancelled.</summary>
    [TigerText("Cancelled", Description = "The copy operation was cancelled.")]
    Cancelled = 2,

    /// <summary>Command-line input failed validation.</summary>
    [TigerText("Validation error", Description = "A required folder option was missing or a supplied value failed validation.")]
    ValidationError = 3,

    /// <summary>An unexpected internal failure occurred.</summary>
    [TigerText("Internal error", Description = "An unexpected internal failure occurred.")]
    InternalError = 70,
}
