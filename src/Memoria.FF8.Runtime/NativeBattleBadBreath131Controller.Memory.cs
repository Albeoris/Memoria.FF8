using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBadBreath131Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBadBreath131Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleBadBreath131Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Bad Breath task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleBadBreath131Frame Write(NativeBattleBadBreath131Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                int slot = (int)(offset / pool.Stride);
                return WithPool(frame, index, pool.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, pool.Slots[slot].Next, task.Callback)));
            }
        }
        throw new InvalidDataException("Bad Breath task write leaves its canonical physical pools.");
    }

    public static (NativeBattleBadBreath131Frame Frame, uint Address) Append(NativeBattleBadBreath131Frame frame, int poolIndex, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, poolIndex, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleEffectArena Arena(NativeBattleBadBreath131Frame frame) => frame.Memory.Arena ?? throw new NotSupportedException("Bad Breath requires its actual working allocation.");
    public static NativeBattleBadBreath131Frame WriteArena(NativeBattleBadBreath131Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Arena = Arena(frame).Write(Offset(address, bytes.Length), bytes) } };
    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
    private static NativeBattleBadBreath131Frame WithPool(NativeBattleBadBreath131Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleBadBreath131Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Bad Breath requires its actual captured parameters.");
    private static NativeBattleBadBreath131Frame DrawRandom(NativeBattleBadBreath131Frame frame, out ushort value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out value);
        return frame with { Random = random };
    }
    private static uint Working(NativeBattleBadBreath131Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
    private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        return offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Bad Breath model pointer leaves its seven-model bank.");
    }
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Bad Breath actor leaves its seven-model bank.");
}
