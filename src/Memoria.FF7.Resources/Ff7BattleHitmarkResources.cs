using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public static class Ff7BattleHitmarkResources
{
    public static IReadOnlyList<IReadOnlyList<BattleBillboardQuad>> ReadSprite(LgpArchive magic, int index)
    {
        ArgumentNullException.ThrowIfNull(magic);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, 8u);
        string name = $"jo_c{index + 1:D2}.s";
        IReadOnlyList<LgpEntry> entries = magic.Find("ff7/data/battle/effect/hitmark/" + name);
        if (entries.Count == 0)
        {
            entries = magic.Find(name);
        }
        if (entries.Count != 1)
        {
            throw new InvalidDataException($"FFVII hitmark resource '{name}' resolves to {entries.Count} members.");
        }
        return BattleBillboard.Read(magic.ReadEntry(entries[0]));
    }
}
