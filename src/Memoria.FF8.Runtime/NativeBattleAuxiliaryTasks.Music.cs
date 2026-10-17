using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public NativeBattleAuxiliaryState AppendMusicLoad501A20(uint completionAddress, Action<uint, byte> writeCompletion)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(writeCompletion);
        int slot = Enumerable.Range(0, Slots.Length).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            throw new InvalidOperationException("Native501A20 dereferences506C10's allocation after exhausting the shared auxiliary pool.");
        }
        NativeBattleAuxiliaryTask task = Slots[slot];
        byte[] payload = task.Payload.ToArray();
        payload[0] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), completionAddress);
        task = task with { Flags = (ushort)(task.Flags | 1), State = 0, Next = null, Kind = NativeBattleAuxiliaryKind.MusicLoad501A70, Payload = payload.ToImmutableArray(), UnknownByteMask = task.UnknownByteMask & ~0xF1u };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        writeCompletion(completionAddress, 0);
        return this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot };
    }

    private static (NativeBattleAuxiliaryTask Task, bool Release) AdvanceMusicLoad(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        var advance = inputs?.AdvanceMusicLoad501A70 ?? throw new NotSupportedException("Native501A70 requires its retained music globals and actual caller completion owner.");
        uint address = BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan()[4..]);
        (byte phase, bool completed) = advance(task.Payload[0], address);
        return (task with { Payload = task.Payload.SetItem(0, phase) }, completed);
    }
}
