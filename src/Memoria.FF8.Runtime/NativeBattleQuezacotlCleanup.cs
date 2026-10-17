using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuezacotlCleanup
{
    public static NativeBattleQuezacotlWorkspaceWrite RestoreAndUnlink355(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, Func<NativeBattleEffectMemory, NativeBattleEffectMemory> scheduleTextureAndRestoreStage)
    {
        ArgumentNullException.ThrowIfNull(scheduleTextureAndRestoreStage);
        NativeBattleEffectTaskPool child = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738);
        if (child.Head != 0 || child.Slots.Length != 1 || child.Slots[0].Callback != 0x6C3940 || child.Slots[0].ReadWord(12) != 355)
        {
            throw new InvalidDataException("Native6C5196 requires its live355 primary child.");
        }
        NativeBattleQuezacotlExecutableMemory executable = memory.QuezacotlExecutableMemory ?? throw new NotSupportedException("Native6C5196 requires its current mutable executable regions.");
        NativeBattleGuardianWorkBuffer backup = memory.GuardianWorkBuffer ?? throw new NotSupportedException("Native6C5196 requires its actual entry backup, not static resource seeds.");
        memory = memory with { QuezacotlExecutableMemory = executable.Restore6C5196(backup) };
        //The child returns2 only after508630 scheduling and6C6500 stage restoration have executed.
        memory = scheduleTextureAndRestoreStage(memory);
        NativeBattleEffectTaskStep unlinked = child.Advance((pool, _) => new(pool, 2));
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native355 child unlink requires its current physical task bytes.");
        memory = memory with { Arena = arena.Write(0, [0, 0]) };
        return new(memory, effect with { Pools = effect.Pools.Select(pool => pool.DescriptorAddress == child.DescriptorAddress ? unlinked.Pool : pool).ToImmutableArray() });
    }
}
