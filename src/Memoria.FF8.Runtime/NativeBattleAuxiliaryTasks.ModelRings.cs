using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public (NativeBattleAuxiliaryState State, bool Allocated) AppendModelRing(NativeBattleAuxiliaryKind kind, BattleModelOwner model, NativeBattleModelAliases aliases, uint flags = 0, byte sequence = 0)
    {
        Validate();
        if (kind is not (NativeBattleAuxiliaryKind.ModelSequence505C70 or NativeBattleAuxiliaryKind.ModelStatus50A000 or NativeBattleAuxiliaryKind.ModelResourceFlags50B080) || !aliases.Roster.Contains(model))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        int slot = Enumerable.Range(0, Slots.Length).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            return (this, false);
        }
        byte[] payload = Slots[slot].Payload.ToArray();
        uint unknown = Slots[slot].UnknownByteMask;
        if (aliases.EncodeAddress(model) is { } encoded)
        {
            encoded.AsSpan().CopyTo(payload);
            unknown &= ~15u;
        }
        else
        {
            unknown |= 15u;
        }
        uint written = 0;
        if (kind == NativeBattleAuxiliaryKind.ModelStatus50A000)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), flags);
            written |= 0xF0;
        }
        else if (kind == NativeBattleAuxiliaryKind.ModelResourceFlags50B080)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), unchecked((ushort)flags));
            written |= 0x30;
        }
        else
        {
            payload[9] = sequence;
            written |= 0x200;
        }
        NativeBattleAuxiliaryTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = kind, Payload = payload.ToImmutableArray(), UnknownByteMask = unknown & ~written, ModelOwner = model };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return (this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot }, true);
    }

    private static void AdvanceModelRing(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        BattleModelOwner owner = task.ModelOwner ?? throw new InvalidDataException("Native ring callback has no live model owner.");
        uint argument = task.Kind switch { NativeBattleAuxiliaryKind.ModelStatus50A000 => BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(4)), NativeBattleAuxiliaryKind.ModelResourceFlags50B080 => BinaryPrimitives.ReadUInt16LittleEndian(task.Payload.AsSpan().Slice(4)), _ => task.Payload[9] };
        (inputs?.ApplyModelRing ?? throw new NotSupportedException("Native model ring callback requires its current model/sequence/status owners."))(task.Kind, owner, argument);
    }
}
