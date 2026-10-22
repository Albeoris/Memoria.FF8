namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNeedlesFamilyPacketWrite(NativeBattleNeedlesFamilyState State, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleNeedlesFamilyPacketArena
{
    private static readonly string ThousandBank = NativeBattleNeedlesFamilyProfile.ForEffect(177).PacketBase.ToString("X");
    private static readonly string TenThousandBank = NativeBattleNeedlesFamilyProfile.ForEffect(178).PacketBase.ToString("X");

    public static string Bank(int effect) => effect switch
    {
        177 => ThousandBank,
        178 => TenThousandBank,
        _ => throw new NotSupportedException("The needles packet bank belongs to an unsupported module.")
    };

    public static byte[] Read(NativeBattleNeedlesFamilyState state, NativeBattlePrimitiveReference reference, int count)
    {
        if (reference.Bank != Bank(state.Effect) || reference.Buffer != 0 || reference.ByteOffset < 0 || count < 0 || reference.ByteOffset > NativeBattleNeedlesFamilyProfile.PacketBytes - count)
        {
            throw new InvalidDataException("Needles packet reads require their actual contiguous native bank reference.");
        }
        return state.ReadPacket(checked(state.Profile.PacketBase + (uint)reference.ByteOffset), count);
    }

    public static NativeBattleNeedlesFamilyPacketWrite Append(NativeBattleNeedlesFamilyState state, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch)
    {
        state.Validate();
        NativeBattleNeedlesFamilyProfile profile = state.Profile;
        uint physicalCursor = state.ReadDword(profile.CursorGlobal);
        if (physicalCursor < profile.PacketBase || physicalCursor > profile.PacketBase + NativeBattleNeedlesFamilyProfile.PacketBytes || (physicalCursor & 3) != 0)
        {
            throw new InvalidDataException("Needles packets require their retained physical cursor and contiguous native banks.");
        }
        NativeBattleEffectArenaWriter writer = new(state.Packets);
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        NativeBattleEffectPacketArena.AppendAttempts(writer, links, checked((int)(physicalCursor - profile.PacketBase)), batch, bank: Bank(state.Effect), capacity: NativeBattleNeedlesFamilyProfile.PacketBytes);
        state = (state with { Packets = writer.Freeze() }).WriteDword(profile.CursorGlobal, checked(physicalCursor + (uint)batch.AllocationBytes));
        return new(state, links.ToImmutable());
    }
}
