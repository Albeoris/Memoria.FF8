using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleRayBomb307Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb307Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleRayBomb307Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint relative = unchecked(address - pool.BankAddress);
            if (relative < pool.Stride * pool.Slots.Length && relative % pool.Stride == 0)
            {
                return pool.Slots[(int)(relative / pool.Stride)];
            }
        }
        throw new InvalidDataException("Ray-Bomb task pointer leaves its canonical pools.");
    }
    public static NativeBattleRayBomb307Frame Write(NativeBattleRayBomb307Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            uint relative = unchecked(address - owner.BankAddress);
            if (relative < owner.Stride * owner.Slots.Length && relative % owner.Stride == 0)
            {
                int slot = (int)(relative / owner.Stride);
                return WithPool(frame, pool, owner with { Slots = owner.Slots.SetItem(slot, task with { Next = owner.Slots[slot].Next }) });
            }
        }
        throw new InvalidDataException("Ray-Bomb task pointer leaves its canonical pools.");
    }
    public static (NativeBattleRayBomb307Frame Frame, uint Address) Append(NativeBattleRayBomb307Frame frame, int pool, uint callback)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        (owner, int slot) = owner.Append(callback);
        return (WithPool(frame, pool, owner), checked(owner.BankAddress + (uint)(slot * owner.Stride)));
    }
    private static NativeBattleRayBomb307Frame WithPool(NativeBattleRayBomb307Frame frame, int pool, NativeBattleEffectTaskPool owner) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(pool, owner) } };
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleRayBomb307Frame DrawRandom(NativeBattleRayBomb307Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static int FindParticle(NativeBattleRayBomb307Frame frame, int limit)
    {
        if (limit is < 0 or > 100)
        {
            throw new InvalidDataException("Ray-Bomb particle scan exceeds its original rows.");
        }
        for (int index = 0; index < limit; index++)
        {
            if (SlotDword(frame, index * 24) == 0)
            {
                return index;
            }
        }
        return -1;
    }
    private static short SlotWord(NativeBattleRayBomb307Frame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.State.Particles.Read(RequireSlot(offset, 2), 2));
    private static uint SlotDword(NativeBattleRayBomb307Frame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.State.Particles.Read(RequireSlot(offset, 4), 4));
    private static NativeBattleRayBomb307Frame SlotWord(NativeBattleRayBomb307Frame frame, int offset, short value) => WriteSlot(frame, offset, BitConverter.GetBytes(value));
    private static NativeBattleRayBomb307Frame SlotDword(NativeBattleRayBomb307Frame frame, int offset, uint value) => WriteSlot(frame, offset, BitConverter.GetBytes(value));
    private static NativeBattleRayBomb307Frame WriteSlot(NativeBattleRayBomb307Frame frame, int offset, ReadOnlySpan<byte> bytes) => frame with { State = frame.State with { Particles = frame.State.Particles.Write(RequireSlot(offset, bytes.Length), bytes) } };
    private static int RequireSlot(int offset, int length) => offset < 0 || length < 0 || offset > ParticlesLength - length ? throw new InvalidDataException("Ray-Bomb particle access leaves its original100rows.") : offset;
}
