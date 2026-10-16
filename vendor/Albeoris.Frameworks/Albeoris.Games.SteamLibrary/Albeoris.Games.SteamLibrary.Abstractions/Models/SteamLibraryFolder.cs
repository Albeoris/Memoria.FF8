using System.Collections.ObjectModel;

namespace Albeoris.Games.SteamLibrary.Abstractions.Models;

/// <summary>
/// Describes a library folder registered with Steam.
/// </summary>
public sealed class SteamLibraryFolder
{
    /// <summary>
    /// Initializes a Steam library folder description.
    /// </summary>
    /// <param name="path">The absolute library root path.</param>
    /// <param name="label">The optional user-defined label.</param>
    /// <param name="contentId">The library content identifier.</param>
    /// <param name="totalSize">The reported library capacity in bytes.</param>
    /// <param name="updateCleanBytesTally">The update cleanup byte tally.</param>
    /// <param name="lastUpdateVerifiedUnixTime">The last verification Unix timestamp.</param>
    /// <param name="lastUpdateCorruptionUnixTime">The last corruption Unix timestamp.</param>
    /// <param name="applications">Application sizes keyed by Steam application identifier.</param>
    public SteamLibraryFolder(
        String path,
        String? label,
        UInt64? contentId,
        UInt64? totalSize,
        UInt64? updateCleanBytesTally,
        Int64? lastUpdateVerifiedUnixTime,
        Int64? lastUpdateCorruptionUnixTime,
        IEnumerable<KeyValuePair<UInt32, UInt64>> applications)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(applications);

        Path = path;
        Label = label;
        ContentId = contentId;
        TotalSize = totalSize;
        UpdateCleanBytesTally = updateCleanBytesTally;
        LastUpdateVerifiedUnixTime = lastUpdateVerifiedUnixTime;
        LastUpdateCorruptionUnixTime = lastUpdateCorruptionUnixTime;
        Applications = new ReadOnlyDictionary<UInt32, UInt64>(applications.ToDictionary());
    }

    /// <summary>Gets the absolute library root path.</summary>
    public String Path { get; }

    /// <summary>Gets the optional user-defined label.</summary>
    public String? Label { get; }

    /// <summary>Gets the library content identifier, if recorded.</summary>
    public UInt64? ContentId { get; }

    /// <summary>Gets the reported library capacity in bytes, if recorded.</summary>
    public UInt64? TotalSize { get; }

    /// <summary>Gets the update cleanup byte tally, if recorded.</summary>
    public UInt64? UpdateCleanBytesTally { get; }

    /// <summary>Gets the last verification Unix timestamp, if recorded.</summary>
    public Int64? LastUpdateVerifiedUnixTime { get; }

    /// <summary>Gets the last corruption Unix timestamp, if recorded.</summary>
    public Int64? LastUpdateCorruptionUnixTime { get; }

    /// <summary>Gets reported application sizes keyed by Steam application identifier.</summary>
    public IReadOnlyDictionary<UInt32, UInt64> Applications { get; }
}
