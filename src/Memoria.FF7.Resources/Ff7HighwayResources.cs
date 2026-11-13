using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public static class Ff7HighwayResources
{
    public static PcHighwayCourse Load(Ff7Installation installation, bool arcade)
    {
        using LgpArchive archive = Open(installation);
        return PcHighwayCourse.Read(ReadMember(archive, "aaaa.adr"), ReadMember(archive, "baaa.bin"), arcade);
    }

    public static LgpArchive Open(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        string archiveName = installation.Language switch
        {
            "en" => "high-us.lgp",
            "de" => "high-ge.lgp",
            "fr" => "high-fr.lgp",
            "es" => "high-sp.lgp",
            _ => throw new NotSupportedException($"FFVII highway archive for language '{installation.Language}' is unsupported.")
        };
        return LgpArchive.Open(Path.Combine(installation.DataDirectory, "minigame", archiveName));
    }

    private static byte[] ReadMember(LgpArchive archive, string name)
    {
        IReadOnlyList<LgpEntry> members = archive.Find(name);
        if (members.Count != 1)
        {
            throw new InvalidDataException($"The FFVII highway member '{name}' resolves to {members.Count} entries.");
        }
        return archive.ReadEntry(members[0]);
    }
}
