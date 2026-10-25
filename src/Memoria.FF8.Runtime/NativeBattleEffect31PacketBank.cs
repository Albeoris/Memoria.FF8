using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect31PacketBank
{
    public const int Length = 0xD000;

    public static int Require(NativeBattleEffect31Frame frame, NativeBattleGfGeometryBatch batch)
    {
        if (batch.AllocationBytes < 0 || batch.Attempts.IsDefault || batch.Attempts.Any(attempt => attempt.ByteOffsetRelative < 0))
        {
            throw new InvalidDataException("Effect31 packet batch has an invalid physical extent.");
        }
        int extent = Math.Max(batch.AllocationBytes, batch.Attempts.Select(attempt => checked(attempt.ByteOffsetRelative + attempt.PacketLength)).DefaultIfEmpty(0).Max());
        return Require(frame, extent);
    }

    public static int Require(NativeBattleEffect31Frame frame, int extent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(extent);
        NativeBattleEffectTaskPool root = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == RootDescriptor);
        int slot = root.Head ?? throw new InvalidDataException("Effect31 packet publication requires its current root task.");
        bool odd = (root.Slots[slot].ReadByte(0x5C) & 1) != 0;
        uint start = checked(frame.State.ReadDword(InitializerGlobal) + (odd ? 0u : (uint)Length));
        uint cursor = frame.State.ReadDword(CursorGlobal);
        if (cursor < start || (ulong)cursor + (uint)extent > (ulong)start + Length)
        {
            throw new InvalidDataException("Effect31 packet writes leave the current private bank.");
        }
        return NativeBattleWaterExecutionFields.ArenaOffset(cursor, extent);
    }
}
