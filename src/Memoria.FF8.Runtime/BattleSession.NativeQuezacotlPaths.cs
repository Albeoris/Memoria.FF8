using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeQuezacotlPaths(NativeBattleGraphicsState graphics, NativeBattleGteDepthTrace depthTrace, uint? incomingOutlineDirectionDword)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6C7800 requires its retained effect memory.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native6C7800 lost its current module.");
        NativeBattleEffectTaskPool before = effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleQuezacotlPathProducer.Descriptor);
        if (before.Head is null)
        {
            return graphics;
        }
        NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, effect.Pools);
        if (incomingOutlineDirectionDword is null && memory.ExecutionStack is { } stack)
        {
            incomingOutlineDirectionDword = NativeBattleQuezacotlPathCallbacks.ResolveInitialOutlineDirection(stack, graphics.EntryLifecycle);
        }
        using NativeBattleEffectArenaTransaction transaction = new(memory.Arena ?? throw new NotSupportedException("Native secondary pool requires its retained arena."));
        using NativeBattleLogicalOrderingTransaction ordering = new((graphics.PrimitiveFrame ?? throw new NotSupportedException("Native secondary pool requires its retained ordering frame.")).Pending);
        memory = memory with { Arena = transaction.View };
        graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering.View } };
        NativeBattleEffectMemory committedMemory = memory;
        NativeBattleGraphicsState committedGraphics = graphics;
        ImmutableArray<NativeBattleEffectTaskPool> checkpointPools = effect.Pools;
        bool[] ownedPoolCheckpoints = new bool[checkpointPools.Length];
        bool checkpointActive = false;
        try
        {
            //508420 reuses one native stack depth for all path tasks after the preceding particle pass.
            uint? outlineDirection = incomingOutlineDirectionDword;
            NativeBattleEffectTaskStep stepped = before.Advance((current, index) =>
            {
                transaction.BeginCheckpoint();
                ordering.BeginCheckpoint();
                NativeBattleTaskPoolCheckpoints.Begin(checkpointPools, ownedPoolCheckpoints);
                checkpointActive = true;
                if (current.Slots[index].Callback != 0x6C7800)
                {
                    throw new NotSupportedException($"Native Quezacotl path callback{current.Slots[index].Callback:X} is not implemented.");
                }
                NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C7800 requires its current ordering table.");
                NativeBattleQuezacotlPathStep path = NativeBattleQuezacotlPathCallbacks.Advance6C7800(memory, effect, current, index, frame.Pending, NativeCamera?.Projection ?? throw new NotSupportedException("Native6C7800 requires its prior projection registers."), depthTrace.DepthFifo4, outlineDirection);
                memory = path.Memory;
                effect = path.Effect;
                depthTrace.ObserveFinal(path.DepthFifo4);
                outlineDirection = path.FinalOutlineDirectionDword;
                graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = path.Ordering } };
                NativeBattleTaskPoolCheckpoints.Commit(checkpointPools, ownedPoolCheckpoints);
                transaction.CommitCheckpoint();
                ordering.CommitCheckpoint();
                checkpointActive = false;
                committedMemory = memory;
                committedGraphics = graphics;
                return new(path.Pool, path.Returned);
            });
            transaction.BeginCheckpoint();
            ordering.BeginCheckpoint();
            NativeBattleTaskPoolCheckpoints.Begin(checkpointPools, ownedPoolCheckpoints);
            checkpointActive = true;
            NativeBattleEffectTaskPool pool = stepped.Pool;
            memory = memory with { QuezacotlSecondaryHeads = memory.QuezacotlSecondaryHeads!.Value.SetItem(2, pool.Head is int head ? checked(pool.BankAddress + (uint)head * pool.Stride) : 0) };
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
        return graphics;
    }
}
