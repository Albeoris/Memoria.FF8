using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public NativeBattleAuxiliaryState BeginScreenBlend501E40(short duration)
    {
        Validate();
        if (Slots.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ScreenBlend501E80))
        {
            return this;
        }
        if (Slots.All(task => (task.Flags & 1) != 0))
        {
            throw new InvalidOperationException("Native501E40 cannot initialize its required callback after auxiliary allocation fails.");
        }
        return AppendScreenFade501D10(duration, 0, 255, NativeBattleAuxiliaryKind.ScreenBlend501E80, 1);
    }

    public NativeBattleAuxiliaryState ReverseScreenBlend501F30(short duration)
    {
        Validate();
        for (int index = 0; index < Slots.Length; index++)
        {
            NativeBattleAuxiliaryTask task = Slots[index];
            if ((task.Flags & 1) == 0 || task.Kind != NativeBattleAuxiliaryKind.ScreenBlend501E80)
            {
                continue;
            }
            byte[] payload = task.Payload.ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(payload, 0);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), duration);
            payload[4] = 255;
            payload[5] = 0;
            return this with { Slots = Slots.SetItem(index, task with { Payload = payload.ToImmutableArray(), UnknownByteMask = task.UnknownByteMask & ~0x3Fu }) };
        }
        return this;
    }

    private static (NativeBattleAuxiliaryTask Task, bool Completed) AdvanceScreenBlend501E80(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        short elapsed = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan());
        short duration = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(2));
        int start = task.Payload[4];
        int target = task.Payload[5];
        int weight = (elapsed << 12) / duration;
        uint value = unchecked((uint)(start + (unchecked((target - start) * weight) >> 12)));
        uint color = value | 0x6200;
        color = unchecked(color << 8) | value;
        color = unchecked(color << 8) | value;
        (inputs?.ApplyScreenBlend501E80 ?? throw new NotSupportedException("Native501E80 requires its current COMMON screen rectangle publisher."))(color);
        bool complete = false;
        if (elapsed < duration)
        {
            elapsed = unchecked((short)(elapsed + 1));
            byte[] payload = task.Payload.ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(payload, elapsed);
            task = task with { Payload = payload.ToImmutableArray() };
            complete = elapsed >= duration && target == 0;
        }
        return (task, complete);
    }
}
