using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public (NativeBattleAuxiliaryState State, bool Allocated) AppendCommonTextureLoad508630(uint destination, uint completionAddress, Action<uint, byte> writeCompletion)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(writeCompletion);
        int slot = Enumerable.Range(0, Slots.Length).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            return (this, false);
        }
        NativeBattleAuxiliaryTask task = Slots[slot];
        byte[] payload = task.Payload.ToArray();
        payload[0] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), destination);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), completionAddress);
        task = task with { Flags = (ushort)(task.Flags | 1), State = 0, Next = null, Kind = NativeBattleAuxiliaryKind.CommonTextureLoad508660, Payload = payload.ToImmutableArray(), UnknownByteMask = task.UnknownByteMask & ~0xFF1u };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        writeCompletion(completionAddress, 0);
        return (this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot }, true);
    }

    private static (NativeBattleAuxiliaryTask Task, bool Release) AdvanceCommonTextureLoad508660(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        switch (task.Payload[0])
        {
            case 0:
                uint destination = BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(4));
                (inputs?.RequestFile5084B0 ?? throw new NotSupportedException("Native508660 phase0 requires its actual5084B0 A3 file request."))(0xA3, destination);
                return (task with { Payload = task.Payload.SetItem(0, 1) }, false);
            case 1:
                int result = (inputs?.ReadEffectIoResult508470 ?? throw new NotSupportedException("Native508660 phase1 requires shared signed1D999C8."))();
                if (result < 0)
                {
                    return (task, false);
                }
                uint source = BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(4));
                (inputs?.EnqueueTexture505E30 ?? throw new NotSupportedException("Native508660 phase1 requires its current mutable TIM pointer."))(source);
                return (task with { Payload = task.Payload.SetItem(0, 2) }, false);
            case 2:
                uint completion = BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(8));
                (inputs?.WriteCompletionByte ?? throw new NotSupportedException("Native508660 phase2 requires its actual caller completion BYTE owner."))(completion, 255);
                return (task, true);
            default:
                return (task, false);
        }
    }
}
