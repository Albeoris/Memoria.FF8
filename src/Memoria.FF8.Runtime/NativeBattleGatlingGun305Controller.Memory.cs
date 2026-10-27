using static Memoria.FF8.Runtime.NativeBattleGatlingGun305Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGatlingGun305Controller
{
    private static NativeBattleGatlingGun305Frame DrawRandom(NativeBattleGatlingGun305Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleEffectParameters Parameters(NativeBattleGatlingGun305Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Gatling Gun requires its captured action parameters.");
    private static int FirstContact(NativeBattleGatlingGun305Frame frame, NativeBattleGatlingGun305Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length || inputs.Packets.Headers[index].ReadByte(16) == 0)
        {
            throw new InvalidDataException("Gatling Gun requires its first captured target contact.");
        }
        return inputs.Packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Gatling Gun requires its actual captured contact pointer.");
    }
    private static void RequireBinding(NativeBattleGatlingGun305Frame frame)
    {
        if (frame.Effect.RequestedEffect != 305 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Gatling Gun requires its original selected entry.");
        }
    }
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Gatling Gun actor pointer leaves the native seven-model bank.");
        }
        return (int)(offset / 156);
    }
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Gatling Gun actor leaves the native model bank.");
    private static NativeBattleModulePoolTask Task(NativeBattleGatlingGun305Frame frame, int pool, int slot) => frame.Effect.Pools[pool].Slots[slot];
    private static NativeBattleGatlingGun305Frame WithPool(NativeBattleGatlingGun305Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleGatlingGun305Frame Put(NativeBattleGatlingGun305Frame frame, int pool, int slot, NativeBattleModulePoolTask task) => WithPool(frame, pool, frame.Effect.Pools[pool].WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, Task(frame, pool, slot).Next, task.Callback)));
    public static NativeBattleModulePoolTask Read(NativeBattleGatlingGun305Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Gatling Gun task pointer leaves its canonical pools.");
    }
    public static NativeBattleGatlingGun305Frame Write(NativeBattleGatlingGun305Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            if (address >= owner.BankAddress && address - owner.BankAddress < (uint)(owner.Stride * owner.Slots.Length) && (address - owner.BankAddress) % owner.Stride == 0)
            {
                return Put(frame, pool, (int)((address - owner.BankAddress) / owner.Stride), task);
            }
        }
        throw new InvalidDataException("Gatling Gun task pointer leaves its canonical pools.");
    }
    private static (NativeBattleGatlingGun305Frame Frame, uint Address) AppendPool(NativeBattleGatlingGun305Frame frame, int pool, uint callback)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        if (!owner.Slots.Any(task => (task.Flags & 1) == 0))
        {
            return (frame, 0);
        }
        (owner, int slot) = owner.Append(callback);
        return (WithPool(frame, pool, owner), checked(owner.BankAddress + (uint)(slot * owner.Stride)));
    }
    public static (NativeBattleGatlingGun305Frame Frame, uint Address) Append(NativeBattleGatlingGun305Frame frame, uint callback)
    {
        (frame, uint address) = AppendPool(frame, 1, callback);
        if (address == 0)
        {
            throw new NotSupportedException("Gatling Gun reached a full original child pool.");
        }
        return (frame, address);
    }
}
