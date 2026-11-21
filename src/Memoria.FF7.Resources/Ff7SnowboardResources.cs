using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7SnowboardAssets(PcSnowboardModel Model, IReadOnlyDictionary<string, PcTexture> Textures);

public static class Ff7SnowboardResources
{
    public static Ff7SnowboardAssets LoadStory(Ff7Installation installation)
    {
        using LgpArchive archive = Open(installation);
        Dictionary<string, PcTexture> textures = new(StringComparer.OrdinalIgnoreCase);
        foreach (LgpEntry entry in archive.Entries.Where(entry => entry.Name.EndsWith(".tex", StringComparison.OrdinalIgnoreCase)))
        {
            if (!textures.TryAdd(entry.Name, PcTexture.Read(archive.ReadEntry(entry))))
            {
                throw new InvalidDataException("The FFVII snowboard archive contains an ambiguous texture resource.");
            }
        }
        return new(LoadStoryModel(installation), new System.Collections.ObjectModel.ReadOnlyDictionary<string, PcTexture>(textures));
    }

    public static PcSnowboardModel LoadStoryModel(Ff7Installation installation)
    {
        using LgpArchive archive = Open(installation);
        IReadOnlyList<LgpEntry> entries = archive.Find("for_ev.tmd");
        if (entries.Count != 1)
        {
            throw new InvalidDataException("The FFVII snowboard module requires one unambiguous story model resource.");
        }
        return PcSnowboardModel.Read(archive.ReadEntry(entries[0]));
    }

    public static LgpArchive Open(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        string name = installation.Language switch
        {
            "en" => "snowboard-us.lgp",
            "de" => "snowboard-ge.lgp",
            "fr" => "snowboard-fr.lgp",
            "es" => "snowboard-sp.lgp",
            _ => throw new NotSupportedException($"FFVII snowboard archive for language '{installation.Language}' is unsupported.")
        };
        return LgpArchive.Open(Path.Combine(installation.DataDirectory, "minigame", name));
    }
}
