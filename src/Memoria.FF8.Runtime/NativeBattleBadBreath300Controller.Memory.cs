using static Memoria.FF8.Runtime.NativeBattleBadBreath300Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBadBreath300Controller
{
    private static NativeBattleBadBreath300Frame DrawRandom(NativeBattleBadBreath300Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleEffectParameters Parameters(NativeBattleBadBreath300Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Bad Breath requires its captured action parameters.");
    private static int FirstContact(NativeBattleBadBreath300Frame frame, NativeBattleBadBreath300Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length || inputs.Packets.Headers[index].ReadByte(16) == 0)
        {
            throw new InvalidDataException("Bad Breath requires its first captured target contact.");
        }
        return inputs.Packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Bad Breath requires its actual captured contact pointer.");
    }
    private static void RequireBinding(NativeBattleBadBreath300Frame frame)
    {
        if (frame.Effect.RequestedEffect != 300 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Bad Breath requires its original selected entry.");
        }
    }
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Bad Breath actor pointer leaves the native seven-model bank.");
        }
        return (int)(offset / 156);
    }
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Bad Breath actor leaves the native model bank.");
    private static NativeBattleModulePoolTask Task(NativeBattleBadBreath300Frame frame, int pool, int slot) => frame.Effect.Pools[pool].Slots[slot];
    private static NativeBattleBadBreath300Frame WithPool(NativeBattleBadBreath300Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleBadBreath300Frame Put(NativeBattleBadBreath300Frame frame, int pool, int slot, NativeBattleModulePoolTask task) => WithPool(frame, pool, frame.Effect.Pools[pool].WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, Task(frame, pool, slot).Next, task.Callback)));
    public static NativeBattleModulePoolTask Read(NativeBattleBadBreath300Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Bad Breath task pointer leaves its canonical pools.");
    }
    public static NativeBattleBadBreath300Frame Write(NativeBattleBadBreath300Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            if (address >= owner.BankAddress && address - owner.BankAddress < (uint)(owner.Stride * owner.Slots.Length) && (address - owner.BankAddress) % owner.Stride == 0)
            {
                return Put(frame, pool, (int)((address - owner.BankAddress) / owner.Stride), task);
            }
        }
        throw new InvalidDataException("Bad Breath task pointer leaves its canonical pools.");
    }
    private static (NativeBattleBadBreath300Frame Frame, uint Address) Append(NativeBattleBadBreath300Frame frame, int pool, uint callback)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        if (!owner.Slots.Any(task => (task.Flags & 1) == 0))
        {
            return (frame, 0);
        }
        (owner, int slot) = owner.Append(callback);
        return (WithPool(frame, pool, owner), checked(owner.BankAddress + (uint)(slot * owner.Stride)));
    }
}
