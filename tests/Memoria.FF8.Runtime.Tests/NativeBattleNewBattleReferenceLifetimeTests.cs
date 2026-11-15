using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNewBattleReferenceLifetimeTests
{
    private static readonly BattleExecutionReference.Model Model = new(new BattlePartyActorModel(new("prior-battle-party")));
    private static readonly BattleExecutionReference.Header Header = new(new(4));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewBattleDropsReferencesWhileKeepingPhysicalOwnersAndKnownness(bool useStorageGraph)
    {
        NativeBattleRetainedMemory cold = NativeBattleStorageSnapshots.Restore(NativeBattleRetainedMemory.FromColdProcess());
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        const int taskOffset = 0x100;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521950, 0x20DFAB8 + taskOffset, 48, 2).BindArena(arena);
        (_, int slot) = pool.Append(0x1000);
        pool.Append(0x2000);
        pool.Slots[slot].WriteExecutionReference(12, 1, Model, [7]);
        pool.Slots[slot].WriteExecutionReference(16, 4, Header);
        NativeBattleNodeSource source = NativeBattleNodeSource.Borrow(arena, taskOffset, NativeBattleRecover93Nodes.SourceLength);
        NativeBattleResourcePointer pointer = new(null, null) { CpuRow = 2 };
        arena.WriteResourcePointer(0x200, pointer);
        byte[] texture = [3, 1, 4, 1];
        NativeBattleTextureFileIdentity textureIdentity = new("fixture.tim", NativeBattleTextureDomain.Magic);
        arena.Write(0x210, texture).RememberTextureFile(0x210, texture, textureIdentity);

        NativeBattleFileStaging staging = cold.FileStaging!;
        const uint stagingAddress = NativeBattleFileStaging.Address;
        staging.WriteExecutionReference(stagingAddress, 1, Model, [6]);
        staging.WriteExecutionReference(stagingAddress + 4, 4, Header);
        staging.WriteResourcePointer(stagingAddress + 8, pointer);

        NativeBattleEffectMemory memory = cold.EffectMemory! with
        {
            Arena = arena,
            Recover93 = new(null, [], null, null, null, 0, 0, NativeBattleRecover93NodeState.FromColdProcess() with { Data = source }),
            ReturnedTaskPointer1D96AAC = pool.DescriptorAddress,
            DetachedReturnedTask = new(2, 1, new(2, 0x1000, 0), null, pool.DescriptorAddress) { Pools = [pool] }
        };
        NativeBattleRetainedMemory frozen = NativeBattleStorageSnapshots.Capture(cold with { EffectMemory = memory });
        Assert.NotNull(frozen.StorageGraph);
        if (!useStorageGraph)
        {
            frozen = frozen with { StorageGraph = null };
        }

        NativeBattleRetainedMemory fresh = NativeBattleStorageSnapshots.RestoreForNewBattle(frozen);
        NativeBattleEffectMemory restored = fresh.EffectMemory!;
        NativeBattleEffectArena restoredArena = restored.Arena!;
        NativeBattleEffectArena borrowed = restored.Recover93!.Nodes.Data!.Arena;
        Assert.Same(restoredArena.Working, borrowed.Working);
        Assert.NotSame(arena.Working, restoredArena.Working);
        Assert.Equal(0u, restored.ReturnedTaskPointer1D96AAC);
        Assert.Null(restored.DetachedReturnedTask);
        Assert.False(borrowed.TryReadExecutionReference<BattleExecutionReference.Model>(12, 1, out _));
        Assert.False(restoredArena.TryReadExecutionReference<BattleExecutionReference.Header>(taskOffset + 16, 4, out _));
        Assert.Equal(7, borrowed.ReadByte(12));
        Assert.Equal(arena.ReadSnapshot(taskOffset, 96).Known, restoredArena.ReadSnapshot(taskOffset, 96).Known);
        Assert.Equal(arena.ReadSnapshot(taskOffset, 96).Data, restoredArena.ReadSnapshot(taskOffset, 96).Data);
        Assert.Equal(pointer, restoredArena.ReadResourcePointer(0x200));
        Assert.Equal(textureIdentity, restoredArena.FindTextureFile(0x210, texture)!.Identity);
        Assert.False(fresh.FileStaging!.TryReadExecutionReference<BattleExecutionReference.Model>(stagingAddress, 1, out _));
        Assert.False(fresh.FileStaging.TryReadExecutionReference<BattleExecutionReference.Header>(stagingAddress + 4, 4, out _));
        Assert.Equal(6, fresh.FileStaging.ReadByte(stagingAddress));
        Assert.Equal(new[] { false, false, false, false }, fresh.FileStaging.ReadSnapshot(stagingAddress + 4, 4).Known);
        Assert.Equal(staging.ReadSnapshot(stagingAddress + 4, 4).Data, fresh.FileStaging.ReadSnapshot(stagingAddress + 4, 4).Data);
        Assert.Equal(pointer, fresh.FileStaging.ReadResourcePointer(stagingAddress + 8));

        // Restoring the same input again proves the new-battle filter did not mutate it.
        NativeBattleRetainedMemory continuation = NativeBattleStorageSnapshots.Restore(frozen);
        Assert.Equal(Model, continuation.EffectMemory!.Arena!.ReadExecutionReference<BattleExecutionReference.Model>(taskOffset + 12, 1));
        Assert.Equal(Header, continuation.EffectMemory.DetachedReturnedTask!.Pools[0].Slots[slot].ReadExecutionReference<BattleExecutionReference.Header>(16, 4));
        Assert.Equal(Model, continuation.FileStaging!.ReadExecutionReference<BattleExecutionReference.Model>(stagingAddress, 1));
        restoredArena.Write(taskOffset + 12, [9]);
        Assert.Equal(9, borrowed.ReadByte(12));
        Assert.Equal(7, arena.ReadByte(taskOffset + 12));
    }

    [Fact]
    public void NewBattleFiltersLegacyTaskPayloadFallbackWithoutRewritingPhysicalFields()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 24, 1);
        pool.Append(0x3000);
        pool.Slots[0].WriteExecutionReference(12, 1, Model, [8]);
        pool.Slots[0].WriteExecutionReference(16, 4, Header);
        NativeBattleTaskBankSnapshot legacy = new(pool.DescriptorAddress, pool.BankAddress, pool.Stride, [pool.Slots[0].ExportSnapshotData()], pool.Head, pool.Tail, true);
        NativeBattleStorageRestoreContext context = new(NativeBattleStorageRestorePurpose.NewBattle);
        NativeBattleEffectTaskPool restored = context.Map(NativeBattleEffectTaskPool.FromSnapshot(legacy));

        Assert.Empty(restored.Slots[0].EnumerateExecutionReferences());
        Assert.Equal(0x3000u, restored.Slots[0].Callback);
        Assert.Equal(8, restored.Slots[0].ReadByte(12));
        Assert.Equal(new[] { false, false, false, false }, restored.Slots[0].ReadSnapshot(16, 4).Known);
        Assert.Equal(Header, NativeBattleStorageSnapshots.Restore(NativeBattleEffectTaskPool.FromSnapshot(legacy)).Slots[0].ReadExecutionReference<BattleExecutionReference.Header>(16, 4));
    }

    [Fact]
    public void BattleEntryEndsPreviousInvocationWhilePrivateContinuationKeepsIt()
    {
        NativeBattleRetainedMemory cold = NativeBattleStorageSnapshots.Restore(NativeBattleRetainedMemory.FromColdProcess());
        BattleActorModels models = new([new(Model.Owner, new NativeBattleGraphicsModel(0))]);
        NativeBattleCinematicActorContext actors = NativeBattleCinematicActorContext.FromGraphics(models);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x10, [5]);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 24, 1);
        NativeBattleEffectParameters parameters = new(Model.Owner, 0, BattleModelTargets.From([Model.Owner]), new(4), 0, 0);
        NativeBattleEffectResourcePointer resource = new(0x3000, 0x10);
        NativeBattleOriginalEffectState effect = new(2, 1, new(2, 0x4000, 0), parameters, pool.DescriptorAddress)
        {
            Pools = [pool],
            EntryFirstRecipient = Model.Owner,
            FireModelCompletion = ImmutableDictionary<BattleModelOwner, int>.Empty.Add(Model.Owner, 42),
            ResourcePointers = [resource]
        };
        NativeBattleEffectMemory memory = cold.EffectMemory! with
        {
            Arena = arena,
            ScriptedEffects = new(arena) { PresentationActors = actors },
            DetachedReturnedTask = effect,
            ReturnedTaskPointer1D96AAC = pool.DescriptorAddress
        };
        NativeBattleRetainedMemory frozen = NativeBattleStorageSnapshots.Capture(cold with { EffectMemory = memory });
        NativeBattleEffectMemory fresh = NativeBattleStorageSnapshots.RestoreForNewBattle(frozen).EffectMemory!;
        Assert.Null(fresh.DetachedReturnedTask);
        Assert.Equal(0u, fresh.ReturnedTaskPointer1D96AAC);
        Assert.Null(fresh.ScriptedEffects!.PresentationActors);
        Assert.Same(fresh.Arena!.Working, fresh.ScriptedEffects.Core.Working);
        Assert.Equal(5, fresh.ScriptedEffects.Core.ReadByte(0x10));
        fresh.ValidateReturnedTaskOwner(null);

        NativeBattleEffectMemory continuation = NativeBattleStorageSnapshots.Restore(frozen).EffectMemory!;
        Assert.Equal(parameters, continuation.DetachedReturnedTask!.Parameters);
        Assert.Equal(Model.Owner, continuation.DetachedReturnedTask.EntryFirstRecipient);
        Assert.Equal(42, continuation.DetachedReturnedTask.FireModelCompletion![Model.Owner]);
        Assert.Equal(pool.DescriptorAddress, continuation.DetachedReturnedTask.ReturnedDescriptor);
        Assert.Equal(resource, Assert.Single(continuation.DetachedReturnedTask.ResourcePointers));
        Assert.Equal(Model.Owner, Assert.Single(continuation.ScriptedEffects!.PresentationActors!.Owners));
    }
}
