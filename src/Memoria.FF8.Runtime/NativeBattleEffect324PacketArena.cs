namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect324PacketWrite(NativeBattleEffect324State State, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleEffect324PacketArena
{
    public const string FirstBank = "220BC60";
    public const string SecondBank = "2213C60";
    public const uint FirstAddress = 0x220BC60;
    public const uint SecondAddress = 0x2213C60;
    public const int BankLength = 0x8000;
    public const uint CursorAddress = 0x221BC60;

    public static byte[] Read(NativeBattleEffect324State state, NativeBattlePrimitiveReference reference, int count)
    {
        uint address = reference.Bank switch
        {
            FirstBank => FirstAddress,
            SecondBank => SecondAddress,
            _ => throw new InvalidDataException("Effect324 requires an actual private packet bank reference.")
        };
        if (reference.Buffer != 0 || reference.ByteOffset < 0 || count < 0 || reference.ByteOffset > BankLength - count)
        {
            throw new InvalidDataException("Effect324 packet reads leave their retained physical bank.");
        }
        return state.Read(checked(address + (uint)reference.ByteOffset), count);
    }

    public static NativeBattleEffect324PacketWrite Append(NativeBattleEffect324State state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, ushort flags = 0)
    {
        uint physicalCursor = state.ReadDword(CursorAddress);
        uint address = physicalCursor >= FirstAddress && physicalCursor < SecondAddress ? FirstAddress : physicalCursor >= SecondAddress && physicalCursor <= SecondAddress + BankLength ? SecondAddress : throw new InvalidDataException("Effect324 requires its actual retained physical packet cursor.");
        int cursor = checked((int)(physicalCursor - address));
        if ((cursor & 3) != 0)
        {
            throw new InvalidDataException("Effect324 packet cursor must be DWORD aligned.");
        }
        NativeBattleEffectArena arena = state.ReadView(address, BankLength);
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, cursor, BankLength, offset =>
        {
            (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
            return (bytes[0], known[0]);
        });
        NativeBattleEffectArenaWriter writer = new(arena);
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        NativeBattleEffectPacketArena.AppendAttempts(writer, links, cursor, resolved, flags, bank: address == FirstAddress ? FirstBank : SecondBank, capacity: BankLength);
        state = state.WriteView(address, writer.Freeze(), BankLength).WriteDword(CursorAddress, checked(physicalCursor + (uint)batch.AllocationBytes));
        return new(state, links.ToImmutable());
    }
}
