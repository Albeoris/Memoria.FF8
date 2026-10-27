using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRetainedEffectStorageTests
{
    [Fact]
    public void Failed_effect_operation_restores_independent_retained_globals_and_particle_bytes()
    {
        NativeBattleEffect315State state = NativeBattleEffect315State.FromColdProcess();
        state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 17);
        state.WriteDword(NativeBattleEffect315Profile.ParticleAddress, 23);
        NativeBattleEffectMemory memory = Memory(state);
        NativeBattleEffectTaskPool pool = Pool();
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using NativeBattleTaskOperation operation = NativeBattleTaskOperation.ForEffect(memory, [pool]);
            state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 99);
            state.WriteDword(NativeBattleEffect315Profile.ParticleAddress, 101);
            pool.Append(0x1234);
            throw new InvalidOperationException("Unpublished effect callback failed.");
        }));
        Assert.Equal(17u, state.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
        Assert.Equal(23u, state.ReadDword(NativeBattleEffect315Profile.ParticleAddress));
        Assert.Null(pool.Head);
        Assert.False(state.Globals.Working.HasCheckpoint);
        Assert.False(state.ParticleStorage.Working.HasCheckpoint);
    }

    [Fact]
    public void Publication_accepts_only_touched_banks_of_its_authority_and_keeps_private_pool_rollback()
    {
        NativeBattleEffectMemory published = Memory(NativeBattleEffect315State.FromColdProcess());
        NativeBattleEffectMemory other = NativeBattleRetainedEffectStorage.Admit(Memory(NativeBattleEffect315State.FromColdProcess()));
        NativeBattleEffectTaskPool pool = Pool();
        NativeBattleEffect315State state = published.Effect315!;
        using (NativeBattleTaskOperation outer = NativeBattleTaskOperation.ForEffect(published, [pool]))
        {
            state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 1);
            using (NativeBattleTaskOperation inner = new([pool]))
            {
                state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 2);
                state.WriteDword(NativeBattleEffect315Profile.ParticleAddress, 3);
                other.Effect315!.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 4);
                pool.Append(0x1234);
                NativeBattleTaskOperation.AcceptPublishedMemory(published with { Cursor = 4 });
                state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 5);
                state.WriteDword(NativeBattleEffect315Profile.ParticleAddress, 6);
            }
            Assert.Equal(2u, state.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
            Assert.Equal(3u, state.ReadDword(NativeBattleEffect315Profile.ParticleAddress));
        }
        Assert.Equal(2u, state.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
        Assert.Equal(3u, state.ReadDword(NativeBattleEffect315Profile.ParticleAddress));
        Assert.Equal(0u, other.Effect315!.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
        Assert.Null(pool.Head);
    }

    [Fact]
    public void Cold_admission_keeps_lazy_arenas_unmaterialized_and_ignores_private_task_workspace_aliases()
    {
        NativeBattleEffectArena lazy = new(ImmutableArray<NativeBattleEffectMemoryRegion>.Empty);
        NativeBattleEffectTaskPool pool = Pool();
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(pool.Slots[0], 12, 4);
        NativeBattleEffectMemory memory = Memory(new(lazy, NativeBattleEffectArena.Clear(), null, null));
        CountingTraffic traffic = new();
        using (NativeBattleTelemetry.BeginScope(traffic))
        {
            NativeBattleRetainedEffectStorage.Admit(memory);
            NativeBattleRetainedEffectStorage.AdmitStorage(workspace, memory.RetainedAuthority!);
        }
        Assert.Equal(0, traffic.Calls);
        Assert.Null(pool.CheckpointStorage.RetainedAuthority);
        using (NativeBattleTaskOperation operation = NativeBattleTaskOperation.ForEffect(memory, []))
        {
            workspace.WriteDword(0, 123);
            Assert.False(pool.CheckpointStorage.HasCheckpoint);
        }
        Assert.Equal(123u, pool.Slots[0].ReadDword(12));
    }

    [Fact]
    public void Capture_removes_runtime_authority_and_independent_restores_admit_separate_owners()
    {
        NativeBattleEffectMemory original = NativeBattleRetainedEffectStorage.Admit(Memory(NativeBattleEffect315State.FromColdProcess()));
        original.Effect315!.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 7);
        NativeBattleEffectMemory snapshot = NativeBattleStorageSnapshots.Capture(original);
        Assert.Null(snapshot.RetainedAuthority);
        NativeBattleEffectMemory first = NativeBattleStorageSnapshots.Restore(snapshot);
        NativeBattleEffectMemory second = NativeBattleStorageSnapshots.Restore(snapshot);
        Assert.NotNull(first.RetainedAuthority);
        Assert.NotSame(original.RetainedAuthority, first.RetainedAuthority);
        Assert.NotSame(first.RetainedAuthority, second.RetainedAuthority);
        using (NativeBattleTaskOperation operation = NativeBattleTaskOperation.ForEffect(first, []))
        {
            first.Effect315!.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 9);
        }
        Assert.Equal(7u, first.Effect315!.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
        Assert.Equal(7u, second.Effect315!.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
        Assert.Equal(7u, original.Effect315!.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
        first.Effect315!.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 11);
        Assert.Equal(7u, second.Effect315!.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
    }

    [Fact]
    public void Explicit_producer_journal_is_borrowed_and_later_direct_writes_can_enlist_after_it_closes()
    {
        NativeBattleEffect315State state = NativeBattleEffect315State.FromColdProcess();
        NativeBattleEffectMemory memory = Memory(state);
        using (NativeBattleTaskOperation operation = NativeBattleTaskOperation.ForEffect(memory, []))
        {
            state.Globals.Working.BeginCheckpoint();
            state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 2);
            Assert.Equal(1, state.Globals.Working.CheckpointDepth);
            state.Globals.Working.CommitCheckpoint();
            state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 3);
        }
        Assert.Equal(2u, state.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
    }

    [Fact]
    public void Publication_preflights_all_retained_kernels_before_accepting_any_prefix()
    {
        NativeBattleEffect315State state = NativeBattleEffect315State.FromColdProcess();
        NativeBattleEffectArena main = NativeBattleEffectArena.Clear();
        NativeBattleEffectMemory memory = Memory(state) with { Arena = main };
        using (NativeBattleTaskOperation operation = NativeBattleTaskOperation.ForEffect(memory, []))
        {
            main.WriteUInt32(0, 7);
            state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 8);
            state.Globals.Working.BeginNestedCheckpoint();
            Assert.Throws<InvalidOperationException>(() => NativeBattleTaskOperation.AcceptPublishedMemory(memory));
            state.Globals.Working.RollbackNestedCheckpoint();
        }
        Assert.Equal(0u, main.ReadUInt32(0));
        Assert.Equal(0u, state.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
    }

    [Fact]
    public void Failed_admission_validates_all_authorities_before_tagging_new_banks()
    {
        NativeBattleEffectMemory existing = NativeBattleRetainedEffectStorage.Admit(Memory(NativeBattleEffect315State.FromColdProcess()));
        NativeBattleEffectArena fresh = NativeBattleEffectArena.Clear();
        NativeBattleEffectMemory mixed = Memory(new(fresh, existing.Effect315!.ParticleStorage, null, null));
        Assert.Throws<InvalidOperationException>(() => NativeBattleRetainedEffectStorage.Admit(mixed));
        Assert.Null(mixed.RetainedAuthority);
        Assert.Null(fresh.Working.RetainedAuthority);
        Assert.Same(existing.RetainedAuthority, existing.Effect315.ParticleStorage.Working.RetainedAuthority);
    }

    [Fact]
    public void Dirty_main_view_defers_physical_acceptance_but_clean_frozen_owner_can_accept_with_route_registered()
    {
        NativeBattleEffect315State state = NativeBattleEffect315State.FromColdProcess();
        NativeBattleEffectArena main = NativeBattleEffectArena.Clear();
        NativeBattleEffectMemory memory = Memory(state) with { Arena = main };
        using (NativeBattleTaskOperation operation = NativeBattleTaskOperation.ForEffect(memory, []))
        {
            main.WriteUInt32(0, 1);
            using NativeBattleEffectArenaTransaction transaction = new(main);
            transaction.View.WriteUInt32(0, 7);
            state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 11);
            NativeBattleTaskOperation.AcceptPublishedMemory(memory with { Arena = transaction.View });
            Assert.Equal(1u, main.ReadUInt32(0));
            transaction.Freeze();
            Assert.True(NativeBattleEffectArenaTransaction.HasActive(main.Working));
            NativeBattleTaskOperation.AcceptPublishedMemory(memory);
            main.WriteUInt32(0, 8);
            state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 12);
        }
        Assert.Equal(7u, main.ReadUInt32(0));
        Assert.Equal(11u, state.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
    }

    [Fact]
    public void Explicit_effect_pool_publication_accepts_private_pool_prefix_without_accepting_unpublished_globals()
    {
        NativeBattleEffect315State state = NativeBattleEffect315State.FromColdProcess();
        NativeBattleEffectMemory memory = Memory(state);
        NativeBattleEffectTaskPool pool = Pool();
        using (NativeBattleTaskOperation operation = NativeBattleTaskOperation.ForEffect(memory, [pool]))
        {
            pool.Append(0x1234);
            state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 19);
            NativeBattleTaskOperation.AcceptPublishedPools([pool], memory);
            pool.Slots[0].WriteDword(12, 20);
        }
        Assert.Equal(0, pool.Head);
        Assert.Equal(0x1234u, pool.Slots[0].Callback);
        Assert.Equal(0u, pool.Slots[0].ReadDword(12));
        Assert.Equal(0u, state.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
    }

    [Fact]
    public void Exact_node_publication_accepts_one_effect_bank_and_memory_publication_keeps_other_effect_banks_private()
    {
        NativeBattleEffect315State state = NativeBattleEffect315State.FromColdProcess();
        NativeBattleEffectMemory memory = Memory(state);
        NativeBattleOriginalEffectState effect = Effect();
        NativeBattleNodeSource first = new([0, 0, 0, 0]);
        NativeBattleNodeSource second = new([0, 0, 0, 0]);
        using (NativeBattleTaskOperation operation = new(memory, effect))
        {
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(first);
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(second);
            Assert.NotSame(memory.RetainedAuthority, first.Arena.Working.RetainedAuthority);
            Assert.Same(effect.RetainedAuthority, first.Arena.Working.RetainedAuthority);
            first.Arena.WriteUInt32(0, 1);
            second.Arena.WriteUInt32(0, 2);
            state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 3);
            NativeBattleTaskOperation.AcceptPublishedMemory(memory);
            NativeBattleTaskOperation.AcceptPublishedEffectStorage(first);
            first.Arena.WriteUInt32(0, 9);
            second.Arena.WriteUInt32(0, 8);
            state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 4);
        }
        Assert.Equal(1u, first.ReadUInt32(0));
        Assert.Equal(0u, second.ReadUInt32(0));
        Assert.Equal(3u, state.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
    }

    [Fact]
    public void Effect_publication_accepts_its_nodes_and_pool_prefix_without_accepting_memory_globals()
    {
        NativeBattleEffect315State state = NativeBattleEffect315State.FromColdProcess();
        NativeBattleEffectMemory memory = Memory(state);
        NativeBattleEffectTaskPool pool = Pool();
        NativeBattleOriginalEffectState effect = Effect() with { Pools = [pool] };
        NativeBattleNodeSource source = new([0, 0, 0, 0]);
        using (NativeBattleTaskOperation operation = new(memory, effect))
        {
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(source);
            source.Arena.WriteUInt32(0, 5);
            pool.Append(0x1234);
            state.WriteDword(NativeBattleEffect315Profile.TargetGlobal, 6);
            NativeBattleTaskOperation.AcceptPublishedEffect(effect, memory);
            source.Arena.WriteUInt32(0, 7);
        }
        Assert.Equal(5u, source.ReadUInt32(0));
        Assert.Equal(0, pool.Head);
        Assert.Equal(0u, state.ReadDword(NativeBattleEffect315Profile.TargetGlobal));
    }

    [Fact]
    public void Restore_assigns_fresh_separate_domains_and_a_borrowed_main_node_keeps_physical_memory_authority()
    {
        NativeBattleEffectArena main = NativeBattleEffectArena.CreateUnknown(16).Write(0, new byte[16]);
        NativeBattleEffectMemory memory = Memory(NativeBattleEffect315State.FromColdProcess()) with { Arena = main };
        NativeBattleOriginalEffectState effect = Effect();
        using (NativeBattleTaskOperation operation = new(memory, effect))
        {
            operation.Commit();
        }
        EffectHistory snapshot = NativeBattleStorageSnapshots.Capture(new EffectHistory(effect, memory, NativeBattleNodeSource.Borrow(main, 0, 4)));
        Assert.Null(snapshot.Effect.RetainedAuthority);
        EffectHistory first = NativeBattleStorageSnapshots.Restore(snapshot);
        EffectHistory second = NativeBattleStorageSnapshots.Restore(snapshot);
        Assert.NotSame(first.Effect.RetainedAuthority, first.Memory.RetainedAuthority);
        Assert.NotSame(first.Effect.RetainedAuthority, second.Effect.RetainedAuthority);
        Assert.NotSame(first.Memory.RetainedAuthority, second.Memory.RetainedAuthority);
        using (NativeBattleTaskOperation operation = new(first.Memory, first.Effect))
        {
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(first.Source);
            Assert.Same(first.Memory.RetainedAuthority, first.Source.Arena.Working.RetainedAuthority);
            first.Source.Arena.WriteUInt32(0, 123);
        }
        Assert.Equal(0u, first.Source.ReadUInt32(0));
        Assert.Equal(0u, second.Source.ReadUInt32(0));
    }

    [Fact]
    public void Snapshot_receipts_preserve_with_copy_authority_without_merging_distinct_effect_executions()
    {
        NativeBattleOriginalEffectState first = NativeBattleRetainedEffectStorage.AdmitEffect(Effect());
        NativeBattleOriginalEffectState copy = first with { DrawArenaOffset = 4 };
        NativeBattleOriginalEffectState other = NativeBattleRetainedEffectStorage.AdmitEffect(Effect());
        EffectExecutions snapshot = NativeBattleStorageSnapshots.Capture(new EffectExecutions(first, copy, other));
        Assert.Null(snapshot.First.RetainedAuthority);
        Assert.Same(snapshot.First.RetainedReceipt, snapshot.Copy.RetainedReceipt);
        Assert.NotSame(snapshot.First.RetainedReceipt, snapshot.Other.RetainedReceipt);
        EffectExecutions restored = NativeBattleStorageSnapshots.Restore(snapshot);
        EffectExecutions independent = NativeBattleStorageSnapshots.Restore(snapshot);
        Assert.Same(restored.First.RetainedAuthority, restored.Copy.RetainedAuthority);
        Assert.NotSame(restored.First.RetainedAuthority, restored.Other.RetainedAuthority);
        Assert.NotSame(restored.First.RetainedAuthority, independent.First.RetainedAuthority);
        string json = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("Retained", json);
        EffectExecutions oldShape = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<EffectExecutions>(json)!);
        Assert.NotSame(oldShape.First.RetainedAuthority, oldShape.Copy.RetainedAuthority);
        Assert.NotSame(oldShape.First.RetainedAuthority, oldShape.Other.RetainedAuthority);
    }

    private sealed record EffectExecutions(NativeBattleOriginalEffectState First, NativeBattleOriginalEffectState Copy, NativeBattleOriginalEffectState Other);

    private static NativeBattleOriginalEffectState Effect() => new(1, 0, new(1, 1, 0), null, null);
    private sealed record EffectHistory(NativeBattleOriginalEffectState Effect, NativeBattleEffectMemory Memory, NativeBattleNodeSource Source);

    private static NativeBattleEffectMemory Memory(NativeBattleEffect315State state) => new(null, 0, 0, [], 0, 0, 0, 0) { Effect315 = state };
    private static NativeBattleEffectTaskPool Pool() => NativeBattleEffectTaskPool.Initialize(0x3000000, 0x3100000, 24, 1);

    private sealed class CountingTraffic : INativeBattleTelemetrySink
    {
        internal int Calls;
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes) => Calls++;
    }
}
