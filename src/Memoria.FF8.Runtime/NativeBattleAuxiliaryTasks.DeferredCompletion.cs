using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public NativeBattleAuxiliaryState AppendDeferredCompletion5086C0(uint address, Action<uint, byte> writeCompletion)
    {
        Validate();
        int slot = Enumerable.Range(0, Slots.Length).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            throw new InvalidOperationException("Native5086C0 cannot write its required payload after auxiliary allocation fails.");
        }
        byte[] payload = Slots[slot].Payload.ToArray();
        payload[0] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), address);
        NativeBattleAuxiliaryTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = NativeBattleAuxiliaryKind.DeferredCompletion5086E0, Payload = payload.ToImmutableArray(), UnknownByteMask = Slots[slot].UnknownByteMask & ~0xF01u };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        writeCompletion(address, 0);
        return this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot };
    }
}
