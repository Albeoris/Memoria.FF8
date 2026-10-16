namespace Albeoris.Games.SteamLibrary.Abstractions.Models;

/// <summary>
/// Describes the state flags stored in a Steam application manifest.
/// </summary>
[Flags]
public enum SteamAppState : UInt64
{
    /// <summary>The state is invalid or unavailable.</summary>
    Invalid = 0,
    /// <summary>The application is not installed.</summary>
    Uninstalled = 1,
    /// <summary>The application requires an update.</summary>
    UpdateRequired = 2,
    /// <summary>The application is fully installed.</summary>
    FullyInstalled = 4,
    /// <summary>The application is encrypted.</summary>
    Encrypted = 8,
    /// <summary>The application is locked.</summary>
    Locked = 16,
    /// <summary>Some application files are missing.</summary>
    FilesMissing = 32,
    /// <summary>The application is running.</summary>
    AppRunning = 64,
    /// <summary>Some application files are corrupt.</summary>
    FilesCorrupt = 128,
    /// <summary>An update is running.</summary>
    UpdateRunning = 256,
    /// <summary>An update is paused.</summary>
    UpdatePaused = 512,
    /// <summary>An update has started.</summary>
    UpdateStarted = 1024,
    /// <summary>The application is being uninstalled.</summary>
    Uninstalling = 2048,
    /// <summary>A backup is running.</summary>
    BackupRunning = 4096,
    /// <summary>The application is being reconfigured.</summary>
    Reconfiguring = 65536,
    /// <summary>The application files are being validated.</summary>
    Validating = 131072,
    /// <summary>Files are being added.</summary>
    AddingFiles = 262144,
    /// <summary>Disk space is being preallocated.</summary>
    Preallocating = 524288,
    /// <summary>Application data is being downloaded.</summary>
    Downloading = 1048576,
    /// <summary>Application data is being staged.</summary>
    Staging = 2097152,
    /// <summary>Changes are being committed.</summary>
    Committing = 4194304,
    /// <summary>The update process is stopping.</summary>
    UpdateStopping = 8388608,
}
