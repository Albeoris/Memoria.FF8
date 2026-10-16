using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public NativeBattleAuxiliaryState AppendTextureAnimation(int model, byte sequence, NativeBattleAuxiliaryProgram program)
    {
        Validate();
        program.Validate();
        _ = SkipSoundParticleProgram(program.Source, program.Offset);
        if ((uint)model >= 7)
        {
            throw new ArgumentOutOfRangeException(nameof(model));
        }
        int slot = Enumerable.Range(0, 16).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            throw new InvalidOperationException("Native9F dereferences506C10's allocation; all sixteen physical auxiliary slots are occupied.");
        }
        byte[] payload = Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 0x1D972C0u + 156u * (uint)model);
        payload[8] = 1;
        payload[9] = sequence;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10), 0);
        NativeBattleAuxiliaryTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = NativeBattleAuxiliaryKind.TextureAnimation5057D0, Payload = payload.ToImmutableArray(), Program = program, UnknownByteMask = Slots[slot].UnknownByteMask & ~0xF0Fu | 0xF0u };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot };
    }

    private static (NativeBattleAuxiliaryTask Task, bool Release) AdvanceTextureAnimation(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        NativeBattleAuxiliaryProgram program = task.Program ?? throw new InvalidDataException("Native5057D0 lacks its original deadline list.");
        program.Validate();
        uint address = BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan());
        uint relative = unchecked(address - 0x1D972C0u);
        if (relative % 156 != 0 || relative / 156 >= 7)
        {
            throw new InvalidDataException("Native5057D0's retained graphics model pointer is invalid.");
        }
        int model = (int)(relative / 156);
        byte deadline = program.Source[program.Offset];
        if (deadline != 255)
        {
            ushort elapsed = unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(task.Payload.AsSpan().Slice(10)) + 1));
            byte[] payload = task.Payload.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10), elapsed);
            if (elapsed >= deadline)
            {
                (inputs?.ApplyTexture ?? throw new NotSupportedException("Native5057D0 requires its50C780 copy callback."))(model, 0, payload[8]);
                payload[8] = payload[8] == 1 ? (byte)0 : (byte)1;
                program = program with { Offset = checked(program.Offset + 1) };
                program.Validate();
            }
            task = task with { Payload = payload.ToImmutableArray(), Program = program };
        }
        NativeBattleGraphicsModel current = (inputs?.ReadModel ?? throw new NotSupportedException("Native5057D0 requires its current graphics model and active resource flags."))(model);
        if ((current.Flags0 & 2) != 0 && ((current.ResourceFlags44 ?? throw new InvalidDataException("Native5057D0 lacks active resource WORD44.")) & 0x40) == 0 && current.Sequence == task.Payload[9])
        {
            return (task, false);
        }
        (inputs?.ApplyTexture ?? throw new NotSupportedException("Native5057D0 release requires its50C780 copy callback."))(model, 0, 0);
        (inputs?.RestoreTextureBlink ?? throw new NotSupportedException("Native5057D0 release requires model flag0x400 restoration."))(model);
        return (task, true);
    }
}
