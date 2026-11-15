using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public static class Ff7ChocoboResources
{
    public static PcChocoboCourse LoadCourse(Ff7Installation installation, bool shortCourse)
    {
        using LgpArchive archive = Open(installation);
        IReadOnlyList<LgpEntry> entries = archive.Find("chocobo.wat");
        if (entries.Count != 1)
        {
            throw new InvalidDataException("The FFVII Chocobo course requires one unambiguous chocobo.wat member.");
        }
        return PcChocoboCourse.Read(archive.ReadEntry(entries[0]), shortCourse);
    }

    public static LgpArchive Open(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        string name = installation.Language switch
        {
            "en" => "chocobo.lgp",
            "de" => "gchocobo.lgp",
            "fr" => "fchocobo.lgp",
            "es" => "schocobo.lgp",
            _ => throw new NotSupportedException($"FFVII Chocobo archive for language '{installation.Language}' is unsupported.")
        };
        return LgpArchive.Open(Path.Combine(installation.DataDirectory, "minigame", name));
    }
}
