using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public NativeBattleAuxiliaryState AppendModelGroupFade50F0B0(int model, byte group)
    {
        Validate();
        if ((uint)model >= 7)
        {
            throw new ArgumentOutOfRangeException(nameof(model));
        }
        int slot = Enumerable.Range(0, 16).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            throw new InvalidOperationException("Native50F0B0 exhausted its shared sixteen-slot auxiliary pool.");
        }
        byte[] payload = Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 0x1D972C0u + 156u * (uint)model);
        payload[4] = group;
        payload[6] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), 255);
        NativeBattleAuxiliaryTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = NativeBattleAuxiliaryKind.ModelGroupFade50F0E0, Payload = payload.ToImmutableArray(), UnknownByteMask = Slots[slot].UnknownByteMask & ~0xF005Fu };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot };
    }

    private static (NativeBattleAuxiliaryTask Task, bool Release) AdvanceModelGroupFade50F0E0(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        if ((task.UnknownByteMask & 0xF005F) != 0)
        {
            throw new InvalidDataException("Native50F0E0 lacks its actual model/group/phase/color operands.");
        }
        byte[] payload = task.Payload.ToArray();
        int model = MotionModel(payload, 0);
        uint unknown = task.UnknownByteMask;
        if (payload[6] == 0)
        {
            NativeBattleGraphicsModel current = (inputs?.ReadModel ?? throw new NotSupportedException("Native50F0E0 requires its model bank."))(model);
            NativeBattleSequenceState state = current.SequenceState ?? throw new NotSupportedException("Native50F0E0 requires its actual mesh mask owner.");
            (inputs?.WriteModel ?? throw new NotSupportedException("Native50F0E0 requires its model publication owner."))(model, current with { SequenceState = state with { MeshMask124 = state.MeshMask124 & ~(1u << payload[4]) } });
            payload[7] = 0;
            payload[6] = 1;
            unknown &= ~0x80u;
        }
        if ((unknown & 0x80) != 0)
        {
            throw new InvalidDataException("Native50F0E0 lacks its initialized frame BYTE.");
        }
        if (payload[6] == 1)
        {
            (inputs?.DrawModelGroup50F190 ?? throw new NotSupportedException("Native50F0E0 requires its original group renderer."))(model, payload[4], payload[7], BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(16)));
        }
        payload[7] = unchecked((byte)(payload[7] + 1));
        return (task with { Payload = payload.ToImmutableArray(), UnknownByteMask = unknown }, payload[7] >= 15);
    }
}
