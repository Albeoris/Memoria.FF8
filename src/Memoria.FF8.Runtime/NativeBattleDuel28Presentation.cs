using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleDuel28Presentation
{
    private static readonly ConditionalWeakTable<object, Dictionary<int, long>> births = new();

    internal static void Allocated(NativeBattleModulePoolTask task, int row)
    {
        Dictionary<int, long> rows = births.GetValue(task.RequireOwner(), _ => []);
        rows[row] = rows.GetValueOrDefault(row) + 1;
    }

    internal static NativeBattlePilotVisualIdentity Identity(ushort effect, NativeBattleModulePoolTask task, uint resource, int primitive = 0)
    {
        return NativeBattlePilotVisualLifetimes.Identity(effect, task, primitive) with { ResourceGeneration = resource };
    }

    internal static long Row(NativeBattleModulePoolTask task, int row, uint resource)
    {
        return unchecked(((long)resource << 32) ^ births.GetValue(task.RequireOwner(), _ => []).GetValueOrDefault(row));
    }

    internal static ImmutableArray<NativeBattlePrimitiveReference> Record(ImmutableArray<NativeBattlePrimitiveReference> packets, string bank, byte buffer, int start, NativeBattleModulePacketBatch batch)
    {
        if (batch.CapturedWrites is { } captured)
        {
            return NativeBattleEffectPresentationPackets.Record(packets, bank, buffer, start, captured);
        }
        foreach (NativeBattleAllocatedPrimitive primitive in batch.Primitives)
        {
            packets = packets.Add(new(bank, buffer, checked(start + primitive.ByteOffset)));
        }
        return packets;
    }

    internal static NativeBattlePrimitiveReference Start(NativeBattlePrimitiveProcess process) => new(process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value);

    internal static ImmutableArray<NativeBattlePrimitiveReference> Common(ImmutableArray<NativeBattlePrimitiveReference> existing, NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveReference before, NativeBattlePrimitiveProcess after)
    {
        NativeBattlePrimitiveReference start = NativeBattlePrimitiveProcess.NormalizeCommonReference(before);
        NativeBattlePrimitiveReference end = NativeBattlePrimitiveProcess.NormalizeCommonReference(new(after.PacketBank!, after.PacketBuffer, after.ByteCursor!.Value));
        if (start.Bank != end.Bank || start.Buffer != end.Buffer || end.ByteOffset < start.ByteOffset)
        {
            throw new InvalidDataException("Duel body presentation requires its actual contiguous COMMON append span.");
        }
        return existing.AddRange(ordering.Packets.Select(item => NativeBattlePrimitiveProcess.NormalizeCommonReference(item.Reference)).Where(item => item.Bank == start.Bank && item.Buffer == start.Buffer && item.ByteOffset >= start.ByteOffset && item.ByteOffset < end.ByteOffset));
    }
}
