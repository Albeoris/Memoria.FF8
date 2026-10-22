namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGatlingGun305PacketWrite(NativeBattleGatlingGun305State State, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleGatlingGun305PacketArena
{
    public const string PacketBank = "2247790";
    public const uint PacketAddress = NativeBattleGatlingGun305Profile.PacketBase;
    public const int PacketLength = NativeBattleGatlingGun305Profile.PacketBytes;
    public const uint CursorAddress = NativeBattleGatlingGun305Profile.CursorGlobal;

    public static byte[] Read(NativeBattleGatlingGun305State state, NativeBattlePrimitiveReference reference, int count)
    {
        if (reference.Bank != PacketBank || reference.Buffer != 0 || reference.ByteOffset < 0 || count < 0 || reference.ByteOffset > PacketLength - count)
        {
            throw new InvalidDataException("Gatling Gun packet reads require their actual contiguous native bank reference.");
        }
        return state.ReadPacket(checked(PacketAddress + (uint)reference.ByteOffset), count);
    }

    public static NativeBattleGatlingGun305PacketWrite Append(NativeBattleGatlingGun305State state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch)
    {
        state.Validate();
        uint physicalCursor = state.ReadDword(CursorAddress);
        if (physicalCursor < PacketAddress || physicalCursor > PacketAddress + PacketLength || (physicalCursor & 3) != 0)
        {
            throw new InvalidDataException("Gatling Gun packets require their retained physical cursor and contiguous native banks.");
        }
        int cursor = checked((int)(physicalCursor - PacketAddress));
        NativeBattleEffectArenaWriter writer = new(state.Packets);
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        NativeBattleEffectPacketArena.AppendAttempts(writer, links, cursor, batch, bank: PacketBank, capacity: PacketLength);
        state = (state with { Packets = writer.Freeze() }).WriteDword(CursorAddress, checked(physicalCursor + (uint)batch.AllocationBytes));
        return new(state, links.ToImmutable());
    }
}
