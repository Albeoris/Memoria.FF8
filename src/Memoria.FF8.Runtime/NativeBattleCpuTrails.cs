namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCpuTrailFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect)
{
    public NativeBattleFileStaging? Staging { get; init; }
    public uint? BoneDirectionWord { get; init; }
}

public sealed record NativeBattleCpuTrailLayout(uint WorldBankAddress, uint WorldFreePointerAddress, uint BoneBankAddress, uint BoneFreePointerAddress, int PointCount, int WorldPoolIndex, uint WorldCallback, int BonePoolIndex, uint BoneCallback);
public sealed record NativeBattleCpuTrailAddressStep(NativeBattleCpuTrailFrame Frame, uint Address);
public sealed record NativeBattleCpuTrailTaskStep(NativeBattleCpuTrailFrame Frame, int Result);

public static partial class NativeBattleCpuTrails
{
    private static NativeBattleCpuTrailFrame DrawRandom(NativeBattleCpuTrailFrame frame, out ushort value) => frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out value) };
    private static NativeBattleCpuTrailFrame WithPool(NativeBattleCpuTrailFrame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static void ValidateLayout(NativeBattleCpuTrailLayout layout)
    {
        if (layout.PointCount != 512 || layout.WorldPoolIndex < 0 || layout.BonePoolIndex < 0)
        {
            throw new InvalidDataException("Native CPU trail requires its original point-bank layout.");
        }
        _ = Offset(layout.WorldBankAddress, checked(layout.PointCount * 40));
        _ = Offset(layout.BoneBankAddress, checked(layout.PointCount * 40));
        _ = Offset(layout.WorldFreePointerAddress, 4);
        _ = Offset(layout.BoneFreePointerAddress, 4);
    }
    public static NativeBattleModulePoolTask Read(NativeBattleCpuTrailFrame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Native CPU trail task pointer leaves its canonical pools.");
    }
    public static NativeBattleCpuTrailFrame Write(NativeBattleCpuTrailFrame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            if (address >= owner.BankAddress && address - owner.BankAddress < (uint)(owner.Stride * owner.Slots.Length) && (address - owner.BankAddress) % owner.Stride == 0)
            {
                int slot = (int)((address - owner.BankAddress) / owner.Stride);
                task = task with { Next = owner.Slots[slot].Next };
                return WithPool(frame, pool, owner with { Slots = owner.Slots.SetItem(slot, task) });
            }
        }
        throw new InvalidDataException("Native CPU trail task pointer leaves its canonical pools.");
    }
    private static (NativeBattleCpuTrailFrame Frame, uint Address) AppendPool(NativeBattleCpuTrailFrame frame, int pool, uint callback)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        if (!owner.Slots.Any(task => (task.Flags & 1) == 0))
        {
            return (frame, 0);
        }
        (owner, int slot) = owner.Append(callback);
        return (WithPool(frame, pool, owner), checked(owner.BankAddress + (uint)(slot * owner.Stride)));
    }
    public static (NativeBattleCpuTrailFrame Frame, uint Address) Append(NativeBattleCpuTrailFrame frame, int pool, uint callback) => AppendPool(frame, pool, callback);
    private static NativeBattleEffectArena Arena(NativeBattleCpuTrailFrame frame) => frame.Memory.Arena ?? throw new NotSupportedException("Native CPU trail requires its current working arena.");
    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
    private static NativeBattleCpuTrailFrame WriteArena(NativeBattleCpuTrailFrame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Arena = Arena(frame).Write(Offset(address, bytes.Length), bytes) } };
}
