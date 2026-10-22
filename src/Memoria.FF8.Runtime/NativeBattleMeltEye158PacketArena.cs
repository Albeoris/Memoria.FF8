namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeltEye158PacketWrite(NativeBattleMeltEye158State State, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleMeltEye158PacketArena
{
    public const string PacketBank = "23DB5BC";
    public const uint PacketAddress = NativeBattleMeltEye158Profile.PacketBase;
    public const int PacketLength = NativeBattleMeltEye158Profile.PacketBytes;
    public const uint CursorAddress = NativeBattleMeltEye158Profile.CursorGlobal;

    public static byte[] Read(NativeBattleMeltEye158State state, NativeBattlePrimitiveReference reference, int count)
    {
        if (reference.Bank != PacketBank || reference.Buffer != 0 || reference.ByteOffset < 0 || count < 0 || reference.ByteOffset > PacketLength - count)
        {
            throw new InvalidDataException("Melt-Eye packet reads require their actual contiguous native bank reference.");
        }
        return state.ReadPacket(checked(PacketAddress + (uint)reference.ByteOffset), count);
    }

    public static NativeBattleMeltEye158PacketWrite Append(NativeBattleMeltEye158State state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch)
    {
        state.Validate();
        uint physicalCursor = state.ReadDword(CursorAddress);
        if (physicalCursor < PacketAddress || physicalCursor > PacketAddress + PacketLength || (physicalCursor & 3) != 0)
        {
            throw new InvalidDataException("Melt-Eye packets require their retained physical cursor and contiguous native banks.");
        }
        int cursor = checked((int)(physicalCursor - PacketAddress));
        NativeBattleEffectArenaWriter writer = new(state.Packets);
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        NativeBattleEffectPacketArena.AppendAttempts(writer, links, cursor, batch, bank: PacketBank, capacity: PacketLength);
        state = (state with { Packets = writer.Freeze() }).WriteDword(CursorAddress, checked(physicalCursor + (uint)batch.AllocationBytes));
        return new(state, links.ToImmutable());
    }
}
