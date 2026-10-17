using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public NativeBattleAuxiliaryState AppendStageFade501F50(byte duration)
    {
        Validate();
        int slot = Enumerable.Range(0, 16).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            throw new InvalidOperationException("Native501F50 exhausted its shared sixteen-slot auxiliary pool.");
        }
        byte[] payload = Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, 0);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), duration);
        NativeBattleAuxiliaryTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = NativeBattleAuxiliaryKind.StageFade501F90, Payload = payload.ToImmutableArray(), UnknownByteMask = Slots[slot].UnknownByteMask & ~0xFu };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot };
    }

    private static (NativeBattleAuxiliaryTask Task, bool Release) AdvanceStageFade501F90(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        if ((task.UnknownByteMask & 15) != 0)
        {
            throw new InvalidDataException("Native501F90 lacks its actual elapsed/duration WORDs.");
        }
        short elapsed = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan());
        short duration = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(2));
        short depth = unchecked((short)NativeBattleCameraMath.Sine((4096 - (elapsed << 12) / duration) / 4));
        (inputs?.WriteStageColorDepth ?? throw new NotSupportedException("Native501F90 requires its four stage color-depth WORD owners."))(depth);
        byte[] payload = task.Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, unchecked((short)(elapsed + 1)));
        return (task with { Payload = payload.ToImmutableArray() }, elapsed >= duration);
    }
}
