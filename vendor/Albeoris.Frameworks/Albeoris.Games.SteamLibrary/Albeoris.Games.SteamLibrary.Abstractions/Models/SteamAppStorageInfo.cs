namespace Albeoris.Games.SteamLibrary.Abstractions.Models;

/// <summary>
/// Contains storage and transfer counters from a Steam application manifest.
/// </summary>
public sealed class SteamAppStorageInfo
{
    /// <summary>
    /// Initializes application storage information.
    /// </summary>
    /// <param name="sizeOnDisk">The installed size in bytes.</param>
    /// <param name="stagingSize">The staging size in bytes.</param>
    /// <param name="bytesToDownload">The number of bytes to download.</param>
    /// <param name="bytesDownloaded">The number of downloaded bytes.</param>
    /// <param name="bytesToStage">The number of bytes to stage.</param>
    /// <param name="bytesStaged">The number of staged bytes.</param>
    public SteamAppStorageInfo(
        UInt64? sizeOnDisk,
        UInt64? stagingSize,
        UInt64? bytesToDownload,
        UInt64? bytesDownloaded,
        UInt64? bytesToStage,
        UInt64? bytesStaged)
    {
        SizeOnDisk = sizeOnDisk;
        StagingSize = stagingSize;
        BytesToDownload = bytesToDownload;
        BytesDownloaded = bytesDownloaded;
        BytesToStage = bytesToStage;
        BytesStaged = bytesStaged;
    }

    /// <summary>Gets the installed size in bytes, if recorded.</summary>
    public UInt64? SizeOnDisk { get; }

    /// <summary>Gets the staging size in bytes, if recorded.</summary>
    public UInt64? StagingSize { get; }

    /// <summary>Gets the number of bytes to download, if recorded.</summary>
    public UInt64? BytesToDownload { get; }

    /// <summary>Gets the number of downloaded bytes, if recorded.</summary>
    public UInt64? BytesDownloaded { get; }

    /// <summary>Gets the number of bytes to stage, if recorded.</summary>
    public UInt64? BytesToStage { get; }

    /// <summary>Gets the number of staged bytes, if recorded.</summary>
    public UInt64? BytesStaged { get; }
}
