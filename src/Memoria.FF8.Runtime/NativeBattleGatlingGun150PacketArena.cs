namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGatlingGun150PacketWrite(NativeBattleGatlingGun150State State, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleGatlingGun150PacketArena
{
    public const string PacketBank = "2451E80";
    public const uint PacketAddress = 0x2451E80;
    public const int PacketLength = 0x10000;
    public const uint CursorAddress = 0x2461E80;

    public static byte[] Read(NativeBattleGatlingGun150State state, NativeBattlePrimitiveReference reference, int count)
    {
        if (reference.Bank != PacketBank || reference.Buffer != 0 || reference.ByteOffset < 0 || count < 0 || reference.ByteOffset > PacketLength - count)
        {
            throw new InvalidDataException("Gatling Gun packet reads require their actual contiguous native bank reference.");
        }
        return state.ReadPacket(checked(PacketAddress + (uint)reference.ByteOffset), count);
    }

    public static NativeBattleGatlingGun150PacketWrite Append(NativeBattleGatlingGun150State state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch)
    {
        state.Validate();
        uint physicalCursor = state.ReadDword(CursorAddress);
        if (physicalCursor < PacketAddress || physicalCursor > PacketAddress + PacketLength || (physicalCursor & 3) != 0)
        {
            throw new InvalidDataException("Gatling Gun packets require their retained physical cursor and contiguous native banks.");
        }
        int cursor = checked((int)(physicalCursor - PacketAddress));
        using NativeBattleEffectArenaWriter writer = new(state.Packets);
        using NativeBattleLogicalOrdering.Builder links = new(ordering);
        NativeBattleEffectPacketArena.AppendAttempts(writer, links, cursor, batch, bank: PacketBank, capacity: PacketLength);
        state = (state with { Packets = writer.Freeze() }).WriteDword(CursorAddress, checked(physicalCursor + (uint)batch.AllocationBytes));
        return new(state, links.ToImmutable());
    }
}
