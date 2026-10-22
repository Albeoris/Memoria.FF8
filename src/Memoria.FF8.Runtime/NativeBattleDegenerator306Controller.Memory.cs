using static Memoria.FF8.Runtime.NativeBattleDegenerator306Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDegenerator306Controller
{
    private static NativeBattleDegenerator306Frame DrawRandom(NativeBattleDegenerator306Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleEffectParameters Parameters(NativeBattleDegenerator306Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Degenerator requires its captured action parameters.");
    private static int FirstContact(NativeBattleDegenerator306Frame frame, NativeBattleDegenerator306Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length || inputs.Packets.Headers[index].ReadByte(16) == 0)
        {
            throw new InvalidDataException("Degenerator requires its first captured target contact.");
        }
        return inputs.Packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Degenerator requires its actual captured contact pointer.");
    }
    private static void RequireBinding(NativeBattleDegenerator306Frame frame)
    {
        if (frame.Effect.RequestedEffect != 306 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Degenerator requires its original selected entry.");
        }
    }
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Degenerator actor pointer leaves the native seven-model bank.");
        }
        return (int)(offset / 156);
    }
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Degenerator actor leaves the native model bank.");
    private static NativeBattleModulePoolTask Task(NativeBattleDegenerator306Frame frame, int pool, int slot) => frame.Effect.Pools[pool].Slots[slot];
    private static NativeBattleDegenerator306Frame WithPool(NativeBattleDegenerator306Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleDegenerator306Frame Put(NativeBattleDegenerator306Frame frame, int pool, int slot, NativeBattleModulePoolTask task) => WithPool(frame, pool, frame.Effect.Pools[pool] with { Slots = frame.Effect.Pools[pool].Slots.SetItem(slot, task with { Next = Task(frame, pool, slot).Next }) });
    public static NativeBattleModulePoolTask Read(NativeBattleDegenerator306Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Degenerator task pointer leaves its canonical pools.");
    }
    public static NativeBattleDegenerator306Frame Write(NativeBattleDegenerator306Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            if (address >= owner.BankAddress && address - owner.BankAddress < (uint)(owner.Stride * owner.Slots.Length) && (address - owner.BankAddress) % owner.Stride == 0)
            {
                return Put(frame, pool, (int)((address - owner.BankAddress) / owner.Stride), task);
            }
        }
        throw new InvalidDataException("Degenerator task pointer leaves its canonical pools.");
    }
    private static (NativeBattleDegenerator306Frame Frame, uint Address) AppendPool(NativeBattleDegenerator306Frame frame, int pool, uint callback)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        if (!owner.Slots.Any(task => (task.Flags & 1) == 0))
        {
            return (frame, 0);
        }
        (owner, int slot) = owner.Append(callback);
        return (WithPool(frame, pool, owner), checked(owner.BankAddress + (uint)(slot * owner.Stride)));
    }
    public static (NativeBattleDegenerator306Frame Frame, uint Address) Append(NativeBattleDegenerator306Frame frame, uint callback)
    {
        (frame, uint address) = AppendPool(frame, 1, callback);
        if (address == 0)
        {
            throw new NotSupportedException("Degenerator reached a full original child pool.");
        }
        return (frame, address);
    }
    public static NativeBattleDegenerator306Frame WriteCompletionByte(NativeBattleDegenerator306Frame frame, uint address, byte value)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            uint offset = unchecked(address - owner.BankAddress);
            if (offset < (uint)(owner.Stride * owner.Slots.Length))
            {
                int slot = (int)(offset / owner.Stride);
                int field = (int)(offset % owner.Stride);
                return Put(frame, pool, slot, owner.Slots[slot].WriteByte(field, value));
            }
        }
        throw new InvalidDataException("Degenerator completion byte leaves its canonical task pools.");
    }
}
