using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Five disjoint process-retained sword-trail banks, at offsets relative to21FF2E8.</summary>
public sealed record NativeBattleGilgameshSwordTrailState([property: JsonRequired] NativeBattleEffectArena Memory)
{
    public const int GatherA = 0;
    public const int GatherB = 0x58;
    public const int CurveA = 0x790;
    public const int CurveB = 0x890;
    public const int History = 0x1B10;

    public static NativeBattleGilgameshSwordTrailState FromColdProcess() => new(NativeBattleEffectArena.Clear());

    public void Validate()
    {
        if (Memory is null)
        {
            throw new InvalidDataException("Native sword trail requires its retained process banks.");
        }
        Memory.Validate();
        if (Memory.Regions.Any(region => !Contains(region.Offset, region.Data.Length)) || Memory.UnknownRegions.Any(region => !Contains(region.Offset, region.Length)))
        {
            throw new InvalidDataException("Native sword trail retained bytes leave its five bounded physical banks.");
        }
    }

    internal static NativeBattleEffectArena CopyPoint(NativeBattleEffectArena memory, int source, int destination)
    {
        byte[] bytes = new byte[8];
        foreach (NativeBattleEffectMemoryRegion region in memory.Regions)
        {
            int first = Math.Max(source, region.Offset);
            int end = Math.Min(source + 8, region.Offset + region.Data.Length);
            if (first < end)
            {
                region.Data.AsSpan().Slice(first - region.Offset, end - first).CopyTo(bytes.AsSpan(first - source));
            }
        }
        ImmutableArray<NativeBattleEffectUnknownRegion> unknown = memory.UnknownRegions;
        memory = memory.Write(destination, bytes);
        foreach (NativeBattleEffectUnknownRegion region in unknown)
        {
            int first = Math.Max(source, region.Offset);
            int end = Math.Min(source + 8, region.Offset + region.Length);
            if (first < end)
            {
                memory = memory.MarkUnknown(destination + first - source, end - first);
            }
        }
        return memory;
    }

    private static bool Contains(int offset, int length) => WithinRange(offset, length, GatherA, 80) || WithinRange(offset, length, GatherB, 80) || WithinRange(offset, length, CurveA, 512) || WithinRange(offset, length, History, 200);

    private static bool WithinRange(int offset, int length, int first, int count) => offset >= first && offset <= first + count - length;
}
