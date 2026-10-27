using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffectTaskMemory
{
    internal static NativeBattleGfAnimationWorkspace BorrowAnimationWorkspace(NativeBattleEffectMemory memory, ImmutableArray<NativeBattleEffectTaskPool> pools, uint address, int length)
    {
        return NativeBattleGfAnimationWorkspace.Borrow(AnimationStorage(memory, pools, address, length), 0, length);
    }

    internal static void PublishAnimationWorkspace(NativeBattleEffectMemory memory, ImmutableArray<NativeBattleEffectTaskPool> pools, uint address, NativeBattleGfAnimationWorkspace workspace)
    {
        workspace.RequireStorage(AnimationStorage(memory, pools, address, workspace.Length), 0);
    }

    internal static NativeBattleModulePoolTask PublishAnimationWorkspace(NativeBattleModulePoolTask task, int offset, NativeBattleGfAnimationWorkspace workspace)
    {
        workspace.RequireStorage(task.BorrowPayloadArena(offset, workspace.Length), 0);
        return task;
    }

    private static NativeBattleEffectArena AnimationStorage(NativeBattleEffectMemory memory, ImmutableArray<NativeBattleEffectTaskPool> pools, uint address, int length)
    {
        if (length <= 0 || (ulong)address + (uint)length > uint.MaxValue + 1UL)
        {
            throw new InvalidDataException("Native animation workspace requires its bounded physical extent.");
        }
        foreach (NativeBattleEffectTaskPool pool in pools)
        {
            ulong end = (ulong)pool.BankAddress + (ulong)pool.Stride * (uint)pool.Slots.Length;
            if (address >= pool.BankAddress && address < end)
            {
                int relative = checked((int)(address - pool.BankAddress));
                NativeBattleModulePoolTask task = pool.Slots[relative / pool.Stride];
                return task.BorrowPayloadArena(relative % pool.Stride, length);
            }
            if (address < pool.BankAddress && (ulong)address + (uint)length > pool.BankAddress)
            {
                throw new InvalidDataException("Native animation workspace crosses a physical task bank boundary.");
            }
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native animation workspace requires its actual physical owner.");
        if (address < ArenaAddress || (ulong)address + (uint)length > (ulong)ArenaAddress + (uint)arena.LogicalLength)
        {
            throw new NotSupportedException("Native animation workspace is outside its active task and COMMON owners.");
        }
        return NativeBattleEffectArena.BorrowWorking(arena.Working, checked(arena.WorkingOffset + (int)(address - ArenaAddress)), length);
    }
}
