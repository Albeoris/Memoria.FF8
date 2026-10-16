using System.Globalization;
using Albeoris.Games.SteamLibrary.Abstractions.Models;
using Albeoris.Games.SteamLibrary.Internal;
using ValveKeyValue;

namespace Albeoris.Games.SteamLibrary;

/// <summary>
/// Parses Steam <c>libraryfolders.vdf</c> data.
/// </summary>
public static class SteamLibraryFoldersParser
{
    /// <summary>
    /// Parses a Steam library descriptor file without modifying it.
    /// </summary>
    /// <param name="path">The path of the <c>libraryfolders.vdf</c> file.</param>
    /// <returns>The parsed library folders in descriptor order.</returns>
    public static IReadOnlyList<SteamLibraryFolder> Parse(String path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream input = ReadOnlyFile.Open(path);
        return Parse(input);
    }

    /// <summary>
    /// Parses Steam library descriptor data from a readable stream.
    /// </summary>
    /// <param name="input">The stream containing Valve KeyValues text.</param>
    /// <returns>The parsed library folders in descriptor order.</returns>
    public static IReadOnlyList<SteamLibraryFolder> Parse(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
            throw new ArgumentException("The library descriptor stream must be readable.", nameof(input));

        KVSerializer serializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text)
            ?? throw new InvalidOperationException("Valve KeyValues text serialization is unavailable.");
        KVDocument data = serializer.Deserialize(input);

        return data.Children.Select(ParseFolder).ToArray();
    }

    private static SteamLibraryFolder ParseFolder(KVObject folder)
    {
        Dictionary<UInt32, UInt64> applications = folder.EnumerateChildren("apps").ToDictionary(
            app => UInt32.Parse(app.Name, NumberStyles.None, CultureInfo.InvariantCulture),
            app => app.Value.ToUInt64(CultureInfo.InvariantCulture));

        return new SteamLibraryFolder(
            folder.GetString("path"),
            folder.FindString("label"),
            folder.FindUInt64("contentid"),
            folder.FindUInt64("totalsize"),
            folder.FindUInt64("update_clean_bytes_tally"),
            folder.FindInt64("time_last_update_verified"),
            folder.FindInt64("time_last_update_corruption"),
            applications);
    }
}
