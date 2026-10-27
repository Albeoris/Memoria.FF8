using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSaliva138Controller
{
    public static NativeBattleSaliva138Frame ReleaseReturnedBanks(NativeBattleSaliva138Frame frame)
    {
        if (frame.Effect.ReturnedDescriptor != 0)
        {
            throw new InvalidDataException("Saliva bank release requires its actual completed returned root.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Saliva bank release requires its retained arena owner.");
        foreach (uint descriptor in new[] { CurveDescriptor, MovingSpriteDescriptor })
        {
            if (frame.Effect.Pools.Count(pool => pool.DescriptorAddress == descriptor) != 1)
            {
                throw new InvalidDataException("Saliva bank release requires each undrained physical bank exactly once.");
            }
        }
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            pool.Validate();
            bool undrained = pool.DescriptorAddress is CurveDescriptor or MovingSpriteDescriptor;
            if (!undrained && (pool.Head is not null || pool.Slots.Any(task => (task.Flags & 1) != 0)))
            {
                throw new InvalidDataException("Saliva bank release requires its remaining task banks to be inactive.");
            }
            bool arenaBank = NativeBattleEffectTaskMemory.ArenaOffset(pool) is not null;
            if (undrained && !arenaBank)
            {
                throw new NotSupportedException("Saliva bank release requires each arena bank to fit its physical owner.");
            }
            if (arenaBank && !pool.IsBoundToStorage(arena, NativeBattleEffectNodeMemory.ArenaAddress))
            {
                throw new InvalidOperationException("Saliva bank release requires its bound canonical physical owner.");
            }
            if (arenaBank)
            {
                pool.RequireOwner().ValidateRetirementBoundary();
            }
        }
        foreach (uint descriptor in new[] { CurveDescriptor, MovingSpriteDescriptor })
        {
            NativeBattleEffectTaskTransfer detached = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
            frame = frame with { Memory = detached.Memory, Effect = detached.Effect };
        }
        NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
        return frame with { Memory = transferred.Memory, Effect = transferred.Effect };
    }
}
