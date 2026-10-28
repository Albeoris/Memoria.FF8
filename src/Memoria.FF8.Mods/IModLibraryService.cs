namespace Memoria.FF8.Mods;

/// <summary>Application-lifetime catalog and job commands. Reading View performs no I/O.</summary>
public interface IModLibraryService
{
    ModLibrarySnapshot View { get; }
    void RefreshCatalog();
    void Queue(RemoteModEntry entry);
    void Pause(Guid id);
    void Resume(Guid id);
    void Cancel(Guid id);
    void Install(Guid id);
    void Retry(Guid id);
    void OpenDownloadPage(Guid id)
    {
    }
}

public sealed record ModLibrarySnapshot(long Revision, RemoteCatalogSnapshot Catalog, IReadOnlyList<ModLibraryJob> Jobs, string TargetProfile, bool TargetSupported, string? Error = null)
{
    public IReadOnlyList<ModLibraryInstallation> Installations { get; init; } = Array.Empty<ModLibraryInstallation>();
    public string TargetLabel { get; init; } = TargetProfile;
}

public sealed record ModLibraryInstallation(string SourceId, Guid Id, string Version, string PackageId, bool Pending);

public sealed record ModLibraryJob(Guid Id, RemoteModEntry Entry, string Phase, long ReceivedBytes, long? TotalBytes, double BytesPerSecond, string? Error, string? Details, bool CanPause, bool CanResume, bool CanCancel, bool CanInstall, bool CanRetry)
{
    public bool CanOpenDownloadPage { get; init; }
}
