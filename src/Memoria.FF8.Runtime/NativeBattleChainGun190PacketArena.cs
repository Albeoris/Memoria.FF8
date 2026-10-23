namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChainGun190PacketWrite(NativeBattleChainGun190State State, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleChainGun190PacketArena
{
    public const string PacketBank = "2283F40";
    public const uint PacketAddress = 0x2283F40;
    public const int PacketLength = 0x10000;
    public const uint CursorAddress = 0x2293F40;

    public static byte[] Read(NativeBattleChainGun190State state, NativeBattlePrimitiveReference reference, int count)
    {
        if (reference.Bank != PacketBank || reference.Buffer != 0 || reference.ByteOffset < 0 || count < 0 || reference.ByteOffset > PacketLength - count)
        {
            throw new InvalidDataException("Chain Gun packet reads require their actual contiguous native bank reference.");
        }
        return state.ReadPacket(checked(PacketAddress + (uint)reference.ByteOffset), count);
    }

    public static NativeBattleChainGun190PacketWrite Append(NativeBattleChainGun190State state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch)
    {
        state.Validate();
        uint physicalCursor = state.ReadDword(CursorAddress);
        if (physicalCursor < PacketAddress || physicalCursor > PacketAddress + PacketLength || (physicalCursor & 3) != 0)
        {
            throw new InvalidDataException("Chain Gun packets require their retained physical cursor and contiguous native banks.");
        }
        int cursor = checked((int)(physicalCursor - PacketAddress));
        NativeBattleEffectArenaWriter writer = new(state.Packets);
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        NativeBattleEffectPacketArena.AppendAttempts(writer, links, cursor, batch, bank: PacketBank, capacity: PacketLength);
        state = (state with { Packets = writer.Freeze() }).WriteDword(CursorAddress, checked(physicalCursor + (uint)batch.AllocationBytes));
        return new(state, links.ToImmutable());
    }
}
