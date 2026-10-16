using System.Collections.ObjectModel;

namespace Albeoris.Games.SteamLibrary.Abstractions.Models;

/// <summary>
/// Contains update metadata from a Steam application manifest.
/// </summary>
public sealed class SteamAppUpdateInfo
{
    /// <summary>
    /// Initializes application update information.
    /// </summary>
    /// <param name="lastOwnerId">The last owning Steam account identifier.</param>
    /// <param name="lastPlayedUnixTime">The last-played Unix timestamp.</param>
    /// <param name="lastUpdatedUnixTime">The last-updated Unix timestamp.</param>
    /// <param name="updateResult">The last update result code.</param>
    /// <param name="autoUpdateBehavior">The automatic update behavior code.</param>
    /// <param name="scheduledAutoUpdateUnixTime">The scheduled automatic update Unix timestamp.</param>
    /// <param name="allowOtherDownloadsWhileRunning">The concurrent-download setting.</param>
    /// <param name="buildId">The installed build identifier.</param>
    /// <param name="targetBuildId">The target build identifier.</param>
    /// <param name="installedDepots">The installed depots.</param>
    /// <param name="installScripts">The registered install scripts.</param>
    public SteamAppUpdateInfo(
        UInt64? lastOwnerId,
        Int64? lastPlayedUnixTime,
        Int64? lastUpdatedUnixTime,
        Int64? updateResult,
        Int64? autoUpdateBehavior,
        Int64? scheduledAutoUpdateUnixTime,
        Int64? allowOtherDownloadsWhileRunning,
        UInt64? buildId,
        UInt64? targetBuildId,
        IEnumerable<KeyValuePair<String, SteamDepot>> installedDepots,
        IEnumerable<KeyValuePair<String, String>> installScripts)
    {
        ArgumentNullException.ThrowIfNull(installedDepots);
        ArgumentNullException.ThrowIfNull(installScripts);

        LastOwnerId = lastOwnerId;
        LastPlayedUnixTime = lastPlayedUnixTime;
        LastUpdatedUnixTime = lastUpdatedUnixTime;
        UpdateResult = updateResult;
        AutoUpdateBehavior = autoUpdateBehavior;
        ScheduledAutoUpdateUnixTime = scheduledAutoUpdateUnixTime;
        AllowOtherDownloadsWhileRunning = allowOtherDownloadsWhileRunning;
        BuildId = buildId;
        TargetBuildId = targetBuildId;
        InstalledDepots = Copy(installedDepots);
        InstallScripts = Copy(installScripts);
    }

    /// <summary>Gets the last owning Steam account identifier, if recorded.</summary>
    public UInt64? LastOwnerId { get; }

    /// <summary>Gets the last-played Unix timestamp, if recorded.</summary>
    public Int64? LastPlayedUnixTime { get; }

    /// <summary>Gets the last-updated Unix timestamp, if recorded.</summary>
    public Int64? LastUpdatedUnixTime { get; }

    /// <summary>Gets the last update result code, if recorded.</summary>
    public Int64? UpdateResult { get; }

    /// <summary>Gets the automatic update behavior code, if recorded.</summary>
    public Int64? AutoUpdateBehavior { get; }

    /// <summary>Gets the scheduled automatic update Unix timestamp, if recorded.</summary>
    public Int64? ScheduledAutoUpdateUnixTime { get; }

    /// <summary>Gets the concurrent-download setting, if recorded.</summary>
    public Int64? AllowOtherDownloadsWhileRunning { get; }

    /// <summary>Gets the installed build identifier, if recorded.</summary>
    public UInt64? BuildId { get; }

    /// <summary>Gets the target build identifier, if recorded.</summary>
    public UInt64? TargetBuildId { get; }

    /// <summary>Gets the installed depots keyed by depot identifier.</summary>
    public IReadOnlyDictionary<String, SteamDepot> InstalledDepots { get; }

    /// <summary>Gets the install scripts keyed by script identifier.</summary>
    public IReadOnlyDictionary<String, String> InstallScripts { get; }

    private static IReadOnlyDictionary<TKey, TValue> Copy<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> values) where TKey : notnull
    {
        return new ReadOnlyDictionary<TKey, TValue>(values.ToDictionary());
    }
}
