using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed class Ff7WorldResources : IDisposable
{
    private readonly LgpArchive archive;

    private Ff7WorldResources(int world, PcWorldMap map, LgpArchive archive)
    {
        World = world;
        Map = map;
        this.archive = archive;
    }

    public int World { get; }
    public PcWorldMap Map { get; }
    public IReadOnlyList<LgpEntry> Entries => archive.Entries;
    internal LgpArchive Archive => archive;

    public PcWorldEvents ReadEvents()
    {
        return PcWorldEvents.Read(ReadRequired($"wm{World}.ev"));
    }

    public PcWorldFieldTable ReadFieldTable()
    {
        return PcWorldFieldTable.Read(ReadRequired("field.tbl"));
    }

    public static Ff7WorldResources Load(Ff7Installation installation, int world = 0)
    {
        ArgumentNullException.ThrowIfNull(installation);
        if (world is not (0 or 2 or 3))
        {
            throw new NotSupportedException("FF7 world resources require an original supported world number.");
        }
        string mapPath = Path.Combine(installation.DataDirectory, "wm", $"wm{world}.map");
        PcWorldMap map = PcWorldMap.Read(File.ReadAllBytes(mapPath));
        return new(world, map, LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.WorldArchive).Path));
    }

    public byte[] ReadRequired(string name)
    {
        IReadOnlyList<LgpEntry> entries = archive.Find(name);
        if (entries.Count != 1)
        {
            throw new InvalidDataException($"FF7 world resource '{name}' requires exactly one archive member.");
        }
        return archive.ReadEntry(entries[0]);
    }

    public void Dispose()
    {
        archive.Dispose();
    }
}
