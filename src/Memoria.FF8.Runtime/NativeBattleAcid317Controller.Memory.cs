using static Memoria.FF8.Runtime.NativeBattleAcid317Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid317Controller
{
    private static NativeBattleAcid317Frame DrawRandom(NativeBattleAcid317Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleEffectParameters Parameters(NativeBattleAcid317Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Acid requires its captured action parameters.");
    private static int FirstContact(NativeBattleAcid317Frame frame, NativeBattleAcid317Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length || inputs.Packets.Headers[index].ReadByte(16) == 0)
        {
            throw new InvalidDataException("Acid requires its first captured target contact.");
        }
        return inputs.Packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Acid requires its actual captured contact pointer.");
    }
    private static void RequireBinding(NativeBattleAcid317Frame frame)
    {
        if (frame.Effect.RequestedEffect != 317 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Acid requires its original selected entry.");
        }
    }
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Acid actor pointer leaves the native seven-model bank.");
        }
        return (int)(offset / 156);
    }
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Acid actor leaves the native model bank.");
    private static NativeBattleModulePoolTask Task(NativeBattleAcid317Frame frame, int pool, int slot) => frame.Effect.Pools[pool].Slots[slot];
    private static NativeBattleAcid317Frame WithPool(NativeBattleAcid317Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleAcid317Frame Put(NativeBattleAcid317Frame frame, int pool, int slot, NativeBattleModulePoolTask task) => WithPool(frame, pool, frame.Effect.Pools[pool].WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, Task(frame, pool, slot).Next, task.Callback)));
    public static NativeBattleModulePoolTask Read(NativeBattleAcid317Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Acid task pointer leaves its canonical pools.");
    }
    public static NativeBattleAcid317Frame Write(NativeBattleAcid317Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            if (address >= owner.BankAddress && address - owner.BankAddress < (uint)(owner.Stride * owner.Slots.Length) && (address - owner.BankAddress) % owner.Stride == 0)
            {
                return Put(frame, pool, (int)((address - owner.BankAddress) / owner.Stride), task);
            }
        }
        throw new InvalidDataException("Acid task pointer leaves its canonical pools.");
    }
    private static (NativeBattleAcid317Frame Frame, uint Address) AppendPool(NativeBattleAcid317Frame frame, int pool, uint callback)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        if (!owner.Slots.Any(task => (task.Flags & 1) == 0))
        {
            return (frame, 0);
        }
        (owner, int slot) = owner.Append(callback);
        return (WithPool(frame, pool, owner), checked(owner.BankAddress + (uint)(slot * owner.Stride)));
    }
    public static (NativeBattleAcid317Frame Frame, uint Address) Append(NativeBattleAcid317Frame frame, int pool, uint callback) => AppendPool(frame, pool, callback);
    private static NativeBattleEffectArena Arena(NativeBattleAcid317Frame frame) => frame.Memory.Arena ?? throw new NotSupportedException("Acid requires its current working arena.");
    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
    private static NativeBattleAcid317Frame WriteArena(NativeBattleAcid317Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Arena = Arena(frame).Write(Offset(address, bytes.Length), bytes) } };
    private static uint Working(NativeBattleAcid317Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
