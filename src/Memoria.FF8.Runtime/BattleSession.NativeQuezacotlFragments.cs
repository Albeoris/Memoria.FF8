using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeQuezacotlFragments(NativeBattleGraphicsState graphics, NativeBattleGteDepthTrace depthTrace)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6C6660 requires its current effect memory.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native6C6660 lost its module state.");
        NativeBattleEffectTaskPool? before = effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleQuezacotlFragmentPool.Descriptor);
        if (before is null && memory.QuezacotlSecondaryHeads is { Length: 5 } heads && heads[4] == 0 && effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == 0x2521738) is { Head: null } child && child.Slots[0].ReadWord(12) == 355)
        {
            return graphics;
        }
        if (before is null)
        {
            throw new NotSupportedException("Native6C6660 requires its current EXE task pool.");
        }
        NativeBattleQuezacotlFragmentPool.ValidateHead(memory, before);
        if (before.Head is null)
        {
            return graphics;
        }
        NativeBattleQuezacotlFragmentPool.Validate(memory, before);
        before.BindStorage(memory.QuezacotlExecutableMemory!.Arena, NativeBattleQuezacotlExecutableMemory.Address);
        using NativeBattleEffectArenaTransaction transaction = new(memory.Arena ?? throw new NotSupportedException("Native fragments require their retained packet arena."));
        using NativeBattleTaskCallerOperation caller = new(transaction, effect.Pools);
        using NativeBattleLogicalOrderingTransaction ordering = new((graphics.PrimitiveFrame ?? throw new NotSupportedException("Native fragments require their retained ordering frame.")).Pending);
        memory = memory with { Arena = transaction.View };
        graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering.View } };
        NativeBattleEffectMemory committedMemory = memory;
        NativeBattleGraphicsState committedGraphics = graphics;
        ImmutableArray<NativeBattleEffectTaskPool> checkpointPools = effect.Pools;
        bool[] ownedPoolCheckpoints = new bool[checkpointPools.Length];
        bool checkpointActive = false;
        try
        {
            NativeBattleEffectTaskStep stepped = before.Advance((current, index) =>
            {
                ordering.BeginCheckpoint();
                NativeBattleTaskPoolCheckpoints.Begin(checkpointPools, ownedPoolCheckpoints);
                checkpointActive = true;
                if (current.Slots[index].Callback != NativeBattleQuezacotlFragmentPool.Callback)
                {
                    throw new NotSupportedException($"Native Quezacotl fragment callback{current.Slots[index].Callback:X} is not implemented.");
                }
                ImmutableArray<byte> raw = NativeBattleQuezacotlFragmentPool.ReadTask(memory, index);
                NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native6C6660 requires its prior projection registers.");
                NativeBattleGfGeometryBatch batch = NativeBattleQuezacotlFragmentPackets.Emit6C6660(memory, raw, projection, depthTrace.Push);
                NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C6660 requires its current ordering table.");
                NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlPacketArena.Append(memory, effect, frame.Pending, batch);
                memory = written.Memory;
                effect = written.Effect;
                bool paused = (memory.QuezacotlPause ?? throw new NotSupportedException("Native6C66F2 requires its retained pause DWORD.")) != 0;
                NativeBattleQuezacotlFragmentMotion moved = NativeBattleQuezacotlFragmentPhysics.Advance6C6707(raw, paused);
                if (!paused)
                {
                    memory = NativeBattleQuezacotlFragmentPool.WriteTask(memory, index, moved.Task44);
                }
                effect = effect with { Pools = effect.Pools.Select(pool => pool.DescriptorAddress == current.DescriptorAddress ? current : pool).ToImmutableArray() };
                graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = written.Ordering } };
                NativeBattleTaskPoolCheckpoints.Commit(checkpointPools, ownedPoolCheckpoints);
                caller.CommitOverlaySuffix();
                ordering.CommitCheckpoint();
                checkpointActive = false;
                committedMemory = memory;
                committedGraphics = graphics;
                caller.PreparePrefix();
                NativeBattleLogicalOrdering callbackAccepted = FreezeNativeQuezacotlOrderingPublication(ordering.Freeze(), provisionalQuezacotlOrdering, provisionalQuezacotlOrderingTransaction);
                caller.AcceptPrefix(callbackAccepted);
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = callbackAccepted } };
                NativeEffectMemory = memory;
                PublishNativeQuezacotlGraphics(graphics);
                return new(current, moved.Returned);
            });
            NativeBattleEffectTaskPool pool = stepped.Pool;
            memory = memory with { QuezacotlSecondaryHeads = memory.QuezacotlSecondaryHeads!.Value.SetItem(4, pool.Head is int head ? NativeBattleQuezacotlFragmentPool.Bank + (uint)head * pool.Stride : 0) };
            effect = effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == pool.DescriptorAddress ? pool : existing).ToImmutableArray() };
            caller.PreparePrefix();
            NativeBattleLogicalOrdering accepted = FreezeNativeQuezacotlOrderingPublication(ordering.Freeze(), provisionalQuezacotlOrdering, provisionalQuezacotlOrderingTransaction);
            caller.AcceptPrefix(accepted);
            NativeEffectMemory = memory;
            graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = accepted } };
            PublishNativeQuezacotlGraphics(graphics);
        }
        catch
        {
            if (checkpointActive)
            {
                NativeBattleTaskPoolCheckpoints.Rollback(checkpointPools, ownedPoolCheckpoints);
                caller.RollbackOverlaySuffix();
                ordering.RollbackCheckpoint();
            }
            memory = committedMemory;
            graphics = committedGraphics;
            throw;
        }
        finally
        {
            caller.PreparePrefix();
            NativeBattleLogicalOrdering pending = FreezeNativeQuezacotlOrderingPublication(ordering.Freeze(), provisionalQuezacotlOrdering, provisionalQuezacotlOrderingTransaction);
            caller.Complete(pending);
            NativeEffectMemory = memory with { Arena = caller.SourceArena };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = pending } };
            PublishNativeQuezacotlGraphics(graphics);
        }
        return graphics;
    }
}
