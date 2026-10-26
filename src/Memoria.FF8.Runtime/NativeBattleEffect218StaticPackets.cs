namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218StaticPacketStep(NativeBattleEffect218State State, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleEffect218StaticPackets
{
    public const string GlobalsBank = "24F2340";
    public const string QuadBank = "E25AD8";

    public static byte[] Read(NativeBattleEffect218State state, NativeBattlePrimitiveReference reference, int length)
    {
        uint address = Address(reference);
        Require(address, length);
        return state.ReadView(address, length).Read(0, length);
    }

    public static NativeBattleEffect218StaticPacketStep Store(NativeBattleEffect218State state, NativeBattleLogicalOrdering ordering, uint address, NativeBattleGfGeometryBatch batch, ushort flags)
    {
        Require(address, batch.AllocationBytes);
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            Require(checked(address + (uint)attempt.ByteOffsetRelative), attempt.PacketLength);
        }
        bool quad = address == NativeBattleEffect218State.CaptureQuadAddress;
        NativeBattleEffectArena source = quad ? state.CaptureQuad ?? throw new NotSupportedException("Effect218 static packet requires its current original quad.") : state.Globals;
        uint start = quad ? NativeBattleEffect218State.CaptureQuadAddress : NativeBattleEffect218State.GlobalsAddress;
        NativeBattleEffectArenaWriter writer = new(source);
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        NativeBattleEffectPacketArena.AppendAttempts(writer, links, checked((int)(address - start)), batch, flags, bank: quad ? QuadBank : GlobalsBank);
        state = quad ? state with { CaptureQuad = writer.Freeze() } : state with { Globals = writer.Freeze() };
        return new(state, links.ToImmutable());
    }

    private static uint Address(NativeBattlePrimitiveReference reference)
    {
        if (reference.Buffer != 0 || reference.ByteOffset < 0)
        {
            throw new InvalidDataException("Effect218 static packet requires its actual physical reference.");
        }
        uint start = reference.Bank switch
        {
            GlobalsBank => NativeBattleEffect218State.GlobalsAddress,
            QuadBank => NativeBattleEffect218State.CaptureQuadAddress,
            _ => throw new InvalidDataException("Effect218 static packet has an unsupported physical bank.")
        };
        return checked(start + (uint)reference.ByteOffset);
    }

    private static void Require(uint address, int length)
    {
        (uint Start, int Length)[] ranges = [(0x24F24D8, 64), (0x24F3538, 8), (0x24F23C8, 12), (0x24F24C8, 8), (NativeBattleEffect218State.CaptureQuadAddress, 24)];
        if (length < 0 || !ranges.Any(range => address >= range.Start && (ulong)address + (uint)length <= (ulong)range.Start + (uint)range.Length))
        {
            throw new InvalidDataException("Effect218 static packet leaves its reached physical packet slots.");
        }
    }
}
