using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleActionMatrix? SequenceDeformationMatrix1D99BF8
    {
        get => effectState.SequenceDeformationMatrix1D99BF8;
        init => effectState = effectState with { SequenceDeformationMatrix1D99BF8 = value };
    }
}

public sealed partial record NativeBattleAuxiliaryState
{
    public NativeBattleAuxiliaryState AppendModelDeformation50F250(BattleModelOwner model, byte group, byte bone, NativeBattleVector velocity, short angle, uint color, NativeBattleModelAliases aliases)
    {
        Validate();
        if (!Slots.Any(task => (task.Flags & 1) == 0))
        {
            return this;
        }
        NativeBattleAuxiliaryState state = AppendModelGroupFade50F0B0(model, group, aliases);
        int slot = state.Tail!.Value;
        NativeBattleAuxiliaryTask task = state.Slots[slot];
        byte[] payload = task.Payload.ToArray();
        payload[5] = bone;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(8), velocity.X);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(10), velocity.Y);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(12), velocity.Z);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), color);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(20), angle);
        task = task with { Kind = NativeBattleAuxiliaryKind.ModelDeformation50F2E0, Payload = payload.ToImmutableArray(), UnknownByteMask = task.UnknownByteMask & ~0x3F3F70u | 0xC000u };
        return state with { Slots = state.Slots.SetItem(slot, task) };
    }
}
