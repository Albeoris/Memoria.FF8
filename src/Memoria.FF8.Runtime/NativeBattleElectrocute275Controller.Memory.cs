using static Memoria.FF8.Runtime.NativeBattleElectrocute275Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectrocute275Controller
{
    private static NativeBattleElectrocute275Frame DrawRandom(NativeBattleElectrocute275Frame frame, out ushort value) => frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out value) };
    private static NativeBattleEffectParameters Parameters(NativeBattleElectrocute275Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Electrocute requires its captured action parameters.");
    private static int FirstContact(NativeBattleElectrocute275Frame frame, NativeBattleElectrocute275Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length || inputs.Packets.Headers[index].ReadByte(16) == 0)
        {
            throw new InvalidDataException("Electrocute requires its first captured target contact.");
        }
        return inputs.Packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Electrocute requires its actual captured contact pointer.");
    }
    private static void RequireBinding(NativeBattleElectrocute275Frame frame)
    {
        if (frame.Effect.RequestedEffect != 275 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Electrocute requires its original selected entry.");
        }
    }
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Electrocute actor pointer leaves the native seven-model bank.");
        }
        return (int)(offset / 156);
    }
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Electrocute actor leaves the native model bank.");
    private static NativeBattleModulePoolTask Task(NativeBattleElectrocute275Frame frame, int pool, int slot) => frame.Effect.Pools[pool].Slots[slot];
    private static NativeBattleElectrocute275Frame WithPool(NativeBattleElectrocute275Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleElectrocute275Frame Put(NativeBattleElectrocute275Frame frame, int pool, int slot, NativeBattleModulePoolTask task) => WithPool(frame, pool, frame.Effect.Pools[pool] with { Slots = frame.Effect.Pools[pool].Slots.SetItem(slot, task with { Next = Task(frame, pool, slot).Next }) });
    public static NativeBattleModulePoolTask Read(NativeBattleElectrocute275Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Electrocute task pointer leaves its canonical pools.");
    }
    public static NativeBattleElectrocute275Frame Write(NativeBattleElectrocute275Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            if (address >= owner.BankAddress && address - owner.BankAddress < (uint)(owner.Stride * owner.Slots.Length) && (address - owner.BankAddress) % owner.Stride == 0)
            {
                return Put(frame, pool, (int)((address - owner.BankAddress) / owner.Stride), task);
            }
        }
        throw new InvalidDataException("Electrocute task pointer leaves its canonical pools.");
    }
    private static (NativeBattleElectrocute275Frame Frame, uint Address) AppendPool(NativeBattleElectrocute275Frame frame, int pool, uint callback)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        if (!owner.Slots.Any(task => (task.Flags & 1) == 0))
        {
            return (frame, 0);
        }
        (owner, int slot) = owner.Append(callback);
        return (WithPool(frame, pool, owner), checked(owner.BankAddress + (uint)(slot * owner.Stride)));
    }
    public static (NativeBattleElectrocute275Frame Frame, uint Address) Append(NativeBattleElectrocute275Frame frame, int pool, uint callback) => AppendPool(frame, pool, callback);
    private static NativeBattleEffectArena Arena(NativeBattleElectrocute275Frame frame) => frame.Memory.Arena ?? throw new NotSupportedException("Electrocute requires its current working arena.");
    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
    private static NativeBattleElectrocute275Frame WriteArena(NativeBattleElectrocute275Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Arena = Arena(frame).Write(Offset(address, bytes.Length), bytes) } };
    private static uint Working(NativeBattleElectrocute275Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
