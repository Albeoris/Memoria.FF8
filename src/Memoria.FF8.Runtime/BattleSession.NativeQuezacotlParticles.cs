using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, uint? PathOutlineDirectionDword) AdvanceNativeQuezacotlParticles(NativeBattleGraphicsState graphics, NativeBattleGteDepthTrace depthTrace)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6C7EA0 requires its retained effect memory.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native6C7EA0 lost its current module.");
        NativeBattleEffectTaskPool before = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521718);
        if (before.Head is null)
        {
            return (graphics, null);
        }
        NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, effect.Pools);
        using NativeBattleEffectArenaTransaction transaction = new(memory.Arena ?? throw new NotSupportedException("Native secondary pool requires its retained arena."));
        using NativeBattleLogicalOrderingTransaction ordering = new((graphics.PrimitiveFrame ?? throw new NotSupportedException("Native secondary pool requires its retained ordering frame.")).Pending);
        memory = memory with { Arena = transaction.View };
        graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering.View } };
        NativeBattleEffectMemory committedMemory = memory;
        NativeBattleGraphicsState committedGraphics = graphics;
        ImmutableArray<NativeBattleEffectTaskPool> checkpointPools = effect.Pools;
        bool[] ownedPoolCheckpoints = new bool[checkpointPools.Length];
        bool checkpointActive = false;
        uint? pathOutlineDirection = null;
        try
        {
            (NativeBattleEffectTaskStep stepped, pathOutlineDirection) = NativeBattleQuezacotlParticleCallbacks.AdvancePool(before, (current, index) =>
            {
                transaction.BeginCheckpoint();
                ordering.BeginCheckpoint();
                NativeBattleTaskPoolCheckpoints.Begin(checkpointPools, ownedPoolCheckpoints);
                checkpointActive = true;
                if (current.Slots[index].Callback != 0x6C7EA0)
                {
                    throw new NotSupportedException($"Native Quezacotl particle callback{current.Slots[index].Callback:X} is not implemented.");
                }
                NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C7EA0 requires its current ordering table.");
                NativeBattleQuezacotlParticleStep particle = NativeBattleQuezacotlParticleCallbacks.Advance6C7EA0(memory, effect, current, index, frame.Pending, NativeCamera?.Projection ?? throw new NotSupportedException("Native6C7EA0 requires its prior projection."), depthTrace);
                memory = particle.Memory;
                effect = particle.Effect;
                graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = particle.Ordering } };
                NativeBattleTaskPoolCheckpoints.Commit(checkpointPools, ownedPoolCheckpoints);
                transaction.CommitCheckpoint();
                ordering.CommitCheckpoint();
                checkpointActive = false;
                committedMemory = memory;
                committedGraphics = graphics;
                return new(particle.Pool, particle.Returned);
            });
            transaction.BeginCheckpoint();
            ordering.BeginCheckpoint();
            NativeBattleTaskPoolCheckpoints.Begin(checkpointPools, ownedPoolCheckpoints);
            checkpointActive = true;
            NativeBattleEffectTaskPool pool = stepped.Pool;
            memory = memory with { QuezacotlSecondaryHeads = memory.QuezacotlSecondaryHeads!.Value.SetItem(1, pool.Head is int head ? checked(pool.BankAddress + (uint)head * pool.Stride) : 0) };
            effect = effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == pool.DescriptorAddress ? pool : existing).ToImmutableArray() };
            graphics = graphics with { OriginalEffect = effect };
            NativeBattleTaskPoolCheckpoints.Commit(checkpointPools, ownedPoolCheckpoints);
            transaction.CommitCheckpoint();
            ordering.CommitCheckpoint();
            checkpointActive = false;
        }
        catch
        {
            if (checkpointActive)
            {
                NativeBattleTaskPoolCheckpoints.Rollback(checkpointPools, ownedPoolCheckpoints);
                transaction.RollbackCheckpoint();
                ordering.RollbackCheckpoint();
            }
            memory = committedMemory;
            graphics = committedGraphics;
            throw;
        }
        finally
        {
            NativeEffectMemory = memory with { Arena = transaction.Freeze() };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering.Freeze() } };
            PublishNativeQuezacotlGraphics(graphics);
        }
        return (graphics, pathOutlineDirection);
    }
}
