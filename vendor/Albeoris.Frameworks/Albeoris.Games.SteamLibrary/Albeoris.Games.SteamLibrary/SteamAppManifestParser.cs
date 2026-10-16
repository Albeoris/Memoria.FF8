using Albeoris.Games.SteamLibrary.Abstractions.Models;
using Albeoris.Games.SteamLibrary.Internal;
using ValveKeyValue;

namespace Albeoris.Games.SteamLibrary;

/// <summary>
/// Parses Steam application manifest (<c>appmanifest_*.acf</c>) data.
/// </summary>
public static class SteamAppManifestParser
{
    /// <summary>
    /// Parses a Steam application manifest file without modifying it.
    /// </summary>
    /// <param name="path">The path of the manifest file.</param>
    /// <returns>The parsed manifest.</returns>
    public static SteamAppManifest Parse(String path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream input = ReadOnlyFile.Open(path);
        return Parse(input);
    }

    /// <summary>
    /// Parses Steam application manifest data from a readable stream.
    /// </summary>
    /// <param name="input">The stream containing Valve KeyValues text.</param>
    /// <returns>The parsed manifest.</returns>
    public static SteamAppManifest Parse(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
            throw new ArgumentException("The manifest stream must be readable.", nameof(input));

        KVDocument appState = CreateSerializer().Deserialize(input);

        return new SteamAppManifest(
            new SteamAppMainInfo(
                appState.GetUInt32("appid"),
                appState.GetUInt64("Universe"),
                (SteamAppState)(appState.FindUInt64("StateFlags") ?? 0),
                appState.GetString("name"),
                appState.FindString("LauncherPath"),
                appState.GetString("installdir")),
            new SteamAppUpdateInfo(
                appState.FindUInt64("LastOwner"),
                appState.FindInt64("LastPlayed"),
                appState.FindInt64("LastUpdated"),
                appState.FindInt64("UpdateResult"),
                appState.FindInt64("AutoUpdateBehavior"),
                appState.FindInt64("ScheduledAutoUpdate"),
                appState.FindInt64("AllowOtherDownloadsWhileRunning"),
                appState.FindUInt64("buildid"),
                appState.FindUInt64("TargetBuildID"),
                ReadInstalledDepots(appState),
                ReadStringSection(appState, "InstallScripts")),
            new SteamAppStorageInfo(
                appState.FindUInt64("SizeOnDisk"),
                appState.FindUInt64("StagingSize"),
                appState.FindUInt64("BytesToDownload"),
                appState.FindUInt64("BytesDownloaded"),
                appState.FindUInt64("BytesToStage"),
                appState.FindUInt64("BytesStaged")),
            new SteamAppConfigurationInfo(
                new SteamConfigurationValues(ReadStringSection(appState, "UserConfig")),
                new SteamConfigurationValues(ReadStringSection(appState, "MountedConfig"))));
    }

    private static KVSerializer CreateSerializer()
    {
        return KVSerializer.Create(KVSerializationFormat.KeyValues1Text)
            ?? throw new InvalidOperationException("Valve KeyValues text serialization is unavailable.");
    }

    private static Dictionary<String, SteamDepot> ReadInstalledDepots(KVDocument appState)
    {
        return appState.EnumerateChildren("InstalledDepots").ToDictionary(
            depot => depot.Name,
            depot => new SteamDepot(depot.GetUInt64("manifest"), depot.GetUInt64("size")));
    }

    private static Dictionary<String, String> ReadStringSection(KVDocument appState, String sectionName)
    {
        return appState.EnumerateChildren(sectionName).ToDictionary(
            item => item.Name,
            item => item.Value.ToString(ValveKeyValueExtensions.FormatProvider));
    }
}
