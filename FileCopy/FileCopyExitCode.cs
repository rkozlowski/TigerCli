using ItTiger.Core;

namespace FileCopy;

/// <summary>The application-wide exit-code contract for File Copy.</summary>
[TigerText("file-copy exit codes")]
public enum FileCopyExitCode
{
    /// <summary>The file was copied successfully.</summary>
    [TigerText("OK", Description = "The file was copied successfully.")]
    Ok = 0,

    /// <summary>The file could not be copied.</summary>
    [TigerText("Copy failed", Description = "The source disappeared, the target was unsafe, or file I/O failed.")]
    CopyFailed = 1,

    /// <summary>The operation was cancelled.</summary>
    [TigerText("Cancelled", Description = "The file copy operation was cancelled.")]
    Cancelled = 2,

    /// <summary>Command-line or file-path validation failed.</summary>
    [TigerText("Validation error", Description = "A required path was missing or a file prompt rule rejected it.")]
    ValidationError = 3,

    /// <summary>A command menu was requested where interaction is unavailable.</summary>
    [TigerText("Interaction unavailable", Description = "The command menu cannot run non-interactively; name a command explicitly.")]
    InteractiveNotAllowed = 4,

    /// <summary>An unexpected internal failure occurred.</summary>
    [TigerText("Internal error", Description = "An unexpected internal failure occurred.")]
    InternalError = 70,
}
